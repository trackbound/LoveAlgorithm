using System;
using LoveAlgo.Common;
using TMPro;
using UnityEngine;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// PC잠금 메인 패널. 모드별 흐름 제어 + 위젯 통합.
    ///
    /// 모드 흐름:
    ///   FirstSetup: 신규 비번 입력 → SetPassword → 완료(OnFlowComplete)
    ///   Normal:     비번 검증 입력 → VerifyPassword → 성공 시 완료
    ///   Reset:      기존 비번 검증 → 성공 시 신규 비번 입력 → SetPassword → 완료
    /// </summary>
    public class LockScreenPanel : MonoBehaviour
    {
        enum Step { EnterCurrent, EnterNew }

        [Header("Widgets")]
        [SerializeField] ClockWidget clock;
        [SerializeField] ToDoWidget toDo;
        [SerializeField] RoaMessageWidget roaMessage;
        [SerializeField] PasswordInputWidget passwordInput;

        [Header("Mode Headers (선택)")]
        [Tooltip("모드별 안내 문구 표시")]
        [SerializeField] TMP_Text headerText;
        [SerializeField] string headerFirstSetup = "비밀번호를 설정하세요";
        [SerializeField] string headerNormal = "비밀번호를 입력하세요";
        [SerializeField] string headerResetCurrent = "현재 비밀번호 확인";
        [SerializeField] string headerResetNew = "새 비밀번호 입력";

        [Header("Roa Message Indexes (모드별)")]
        [SerializeField] int roaIdxFirstSetup = 0;
        [SerializeField] int roaIdxNormal = 1;
        [SerializeField] int roaIdxResetCurrent = 2;
        [SerializeField] int roaIdxResetNew = 3;

        ILockScreen lockScreen;
        Step currentStep;

        /// <summary>잠금 흐름이 정상 완료된 시점. 외부(TitlePanel 등)가 다음 화면 전환에 사용.</summary>
        public event Action OnFlowComplete;

        void Awake()
        {
            lockScreen = Services.Get<ILockScreen>();
            if (lockScreen == null)
            {
                Debug.LogError("[LockScreenPanel] ILockScreen 서비스 미등록");
                return;
            }

            if (passwordInput != null)
                passwordInput.OnPinEntered += HandlePinEntered;

            lockScreen.OnPasswordFailed += HandlePasswordFailed;
        }

        void OnDestroy()
        {
            if (passwordInput != null) passwordInput.OnPinEntered -= HandlePinEntered;
            if (lockScreen != null) lockScreen.OnPasswordFailed -= HandlePasswordFailed;
        }

        // ── 외부 진입 API ───────────────────────────────────────
        public void OpenFirstSetup()
        {
            lockScreen?.OpenForFirstSetup();
            currentStep = Step.EnterNew;
            ApplyUIForStep();
            gameObject.SetActive(true);
            if (toDo != null) toDo.Populate(lockScreen);
        }

        public void OpenNormal()
        {
            lockScreen?.OpenForNormal();
            currentStep = Step.EnterCurrent;
            ApplyUIForStep();
            gameObject.SetActive(true);
            if (toDo != null) toDo.Populate(lockScreen);
        }

        public void OpenReset()
        {
            lockScreen?.OpenForReset();
            currentStep = Step.EnterCurrent;
            ApplyUIForStep();
            gameObject.SetActive(true);
            if (toDo != null) toDo.Populate(lockScreen);
        }

        public void Close() => gameObject.SetActive(false);

        // ── 내부 처리 ───────────────────────────────────────────
        void HandlePinEntered(string pin)
        {
            if (lockScreen == null) return;

            switch (lockScreen.CurrentMode)
            {
                case LockScreenMode.FirstSetup:
                    if (lockScreen.SetPassword(pin)) CompleteFlow();
                    else passwordInput.Clear();
                    break;

                case LockScreenMode.Normal:
                    if (lockScreen.VerifyPassword(pin)) CompleteFlow();
                    // 실패 시 HandlePasswordFailed에서 처리
                    break;

                case LockScreenMode.Reset:
                    if (currentStep == Step.EnterCurrent)
                    {
                        if (lockScreen.VerifyPassword(pin))
                        {
                            currentStep = Step.EnterNew;
                            ApplyUIForStep();
                            passwordInput.Clear();
                        }
                        // 실패 시 HandlePasswordFailed
                    }
                    else // EnterNew
                    {
                        if (lockScreen.SetPassword(pin)) CompleteFlow();
                        else passwordInput.Clear();
                    }
                    break;
            }
        }

        void HandlePasswordFailed(int failCount)
        {
            if (passwordInput != null)
            {
                passwordInput.Clear();
                passwordInput.SetKeyIcon(lockScreen.ShowKeyIcon);
            }
        }

        void CompleteFlow()
        {
            OnFlowComplete?.Invoke();
            Close();
        }

        void ApplyUIForStep()
        {
            if (lockScreen == null) return;

            string header = "";
            int roaIdx = 0;

            switch (lockScreen.CurrentMode)
            {
                case LockScreenMode.FirstSetup:
                    header = headerFirstSetup;
                    roaIdx = roaIdxFirstSetup;
                    break;
                case LockScreenMode.Normal:
                    header = headerNormal;
                    roaIdx = roaIdxNormal;
                    break;
                case LockScreenMode.Reset:
                    if (currentStep == Step.EnterCurrent)
                    {
                        header = headerResetCurrent;
                        roaIdx = roaIdxResetCurrent;
                    }
                    else
                    {
                        header = headerResetNew;
                        roaIdx = roaIdxResetNew;
                    }
                    break;
            }

            if (headerText != null) headerText.text = header;
            if (roaMessage != null) roaMessage.ShowIndex(lockScreen, roaIdx);
            if (passwordInput != null) passwordInput.Clear();
        }
    }
}
