using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common;
using LoveAlgo.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// PC잠금 메인 패널. 기획서 §구성 / §비밀번호 입력 커스텀 시스템 통합.
    ///
    /// 시퀀스 (FirstSetup / Reset 첫 진입):
    ///   0. CanvasGroup.alpha=0, SetActive(true)
    ///   1. (선택) 5초 페이드인 — 게임 첫 시작 시 useFirstStartFadeIn=true
    ///   2. ToDo/Clock/위젯 표시
    ///   3. 5초 대기 → 메시지 4개 순차 출력 (1개당 효과음)
    ///   4. 마지막 메시지 +3초 후 클릭 가능 (InputCatcher 활성화)
    ///   5. 클릭: 좌측 위젯 슬라이드아웃 + 메시지 슬라이드다운 + dim 페이드인 → 입력창 활성화
    ///   6. Confirm: SetPassword → "설정 완료!" → Outro
    ///
    /// 시퀀스 (Normal):
    ///   동일하나 페이드인 0초 + mask=true + hintNormal
    ///   실패 시 PlayShake. FailCount>=3 → hintForgot + 열쇠.
    ///   열쇠 클릭 → ConfirmPopup → 예 → Reset (=FirstSetup 동일 흐름)
    ///
    /// Outro: 패널 1→0 (3초) → 검은 오버레이 0→1 (3초) → caller에 제어 반환
    /// </summary>
    public class LockScreenPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] CanvasGroup rootCanvasGroup;

        [Header("Widgets")]
        [SerializeField] ClockWidget clock;
        [SerializeField] ToDoWidget toDo;
        [SerializeField] RoaMessageWidget roaMessage;
        [SerializeField] PasswordInputWidget passwordInput;

        [Header("Header (안내 문구)")]
        [SerializeField] TMP_Text headerText;

        [Header("Left Widgets (슬라이드아웃 그룹)")]
        [Tooltip("WARNING/음악/ToDo 등 — 클릭 시 왼쪽으로 슬라이드아웃")]
        [SerializeField] List<RectTransform> leftWidgets = new List<RectTransform>();
        [SerializeField] float leftSlideOutDistance = 600f;
        [SerializeField] float leftSlideOutDuration = 0.5f;

        [Header("Login Stage")]
        [Tooltip("로그인 단계 진입 시 활성화 (입력칸+LOGIN 부모)")]
        [SerializeField] GameObject loginStage;
        [SerializeField] CanvasGroup loginDim;
        [SerializeField] float loginDimAlpha = 0.6f;
        [SerializeField] float loginDimFadeDuration = 0.4f;

        [Header("Input Catcher (메시지 후 클릭 대기)")]
        [SerializeField] Button inputCatcher;

        [Header("Outro Black Overlay")]
        [Tooltip("로그인 후 페이드아웃 끝에서 fade in되는 검은 오버레이")]
        [SerializeField] CanvasGroup blackOverlay;

        [Header("Sound (임시 — D8)")]
        [Tooltip("정식 SFX 도착 시 교체. 임시: dialoguenext.mp3 등")]
        [SerializeField] AudioClip messageSfx;
        [SerializeField] AudioSource sfxSource;

        [Header("Timing")]
        [Tooltip("게임 첫 시작 시 5초 페이드인. EntryRouter가 첫 시작 분기에서 외부 set 권장.")]
        [SerializeField] bool useFirstStartFadeIn = false;
        [SerializeField] float firstStartFadeInSec = 5f;
        [Tooltip("화면 표출 후 메시지 시작까지 (기획서: 5초)")]
        [SerializeField] float beforeMessagesDelaySec = 5f;
        [Tooltip("마지막 메시지 후 클릭 가능까지 (기획서: 3초)")]
        [SerializeField] float afterLastMessageDelaySec = 3f;
        [Tooltip("Outro 패널 페이드아웃 (기획서: 3초)")]
        [SerializeField] float outroPanelFadeSec = 3f;
        [Tooltip("Outro 검은 오버레이 페이드인 (기획서: 3초)")]
        [SerializeField] float outroBlackFadeSec = 3f;

        ILockScreen lockScreen;
        Coroutine seqCo;
        Vector2[] leftWidgetOriginalPos;

        /// <summary>잠금 흐름 정상 완료. 외부(EntryRouter/CSV)가 다음 화면 전환에 사용.</summary>
        public event Action OnFlowComplete;

        void Awake()
        {
            lockScreen = Services.Get<ILockScreen>();
            if (lockScreen == null)
                Debug.LogError("[LockScreenPanel] ILockScreen 미등록 — 씬에 LockScreenModule 확인");

            if (passwordInput != null)
            {
                passwordInput.OnPasswordEntered += HandlePasswordEntered;
                passwordInput.OnKeyClicked += HandleKeyClicked;
            }
            if (lockScreen != null) lockScreen.OnPasswordFailed += HandlePasswordFailed;
            if (inputCatcher != null) inputCatcher.onClick.AddListener(OnInputCatcherClicked);

            leftWidgetOriginalPos = new Vector2[leftWidgets.Count];
            for (int i = 0; i < leftWidgets.Count; i++)
            {
                if (leftWidgets[i] != null)
                    leftWidgetOriginalPos[i] = leftWidgets[i].anchoredPosition;
            }
        }

        void OnDestroy()
        {
            if (passwordInput != null)
            {
                passwordInput.OnPasswordEntered -= HandlePasswordEntered;
                passwordInput.OnKeyClicked -= HandleKeyClicked;
            }
            if (lockScreen != null) lockScreen.OnPasswordFailed -= HandlePasswordFailed;
            if (inputCatcher != null) inputCatcher.onClick.RemoveListener(OnInputCatcherClicked);
        }

        // ── 외부 진입 API ──
        public void OpenFirstSetup() => Begin(LockScreenMode.FirstSetup, fadeIn: useFirstStartFadeIn);
        public void OpenNormal()     => Begin(LockScreenMode.Normal,     fadeIn: false);
        public void OpenReset()      => Begin(LockScreenMode.Reset,      fadeIn: false);

        public void Close()
        {
            if (seqCo != null) StopCoroutine(seqCo);
            gameObject.SetActive(false);
        }

        void Begin(LockScreenMode mode, bool fadeIn)
        {
            if (lockScreen == null) return;
            switch (mode)
            {
                case LockScreenMode.FirstSetup: lockScreen.OpenForFirstSetup(); break;
                case LockScreenMode.Reset:      lockScreen.OpenForReset();      break;
                default:                        lockScreen.OpenForNormal();     break;
            }

            gameObject.SetActive(true);
            if (toDo != null) toDo.Populate(lockScreen);
            if (clock != null) clock.Refresh();
            if (roaMessage != null) roaMessage.HideAllImmediate();

            ResetLeftWidgetPositions();
            if (loginStage != null) loginStage.SetActive(false);
            if (loginDim != null) loginDim.alpha = 0f;
            if (inputCatcher != null) inputCatcher.gameObject.SetActive(false);
            if (blackOverlay != null) blackOverlay.alpha = 0f;

            if (seqCo != null) StopCoroutine(seqCo);
            seqCo = StartCoroutine(IntroSequence(fadeIn));
        }

        IEnumerator IntroSequence(bool fadeIn)
        {
            // 1. 페이드인
            if (rootCanvasGroup != null)
            {
                if (fadeIn) yield return FadeCanvas(rootCanvasGroup, 0f, 1f, firstStartFadeInSec);
                else rootCanvasGroup.alpha = 1f;
            }

            // 2. 메시지 시작 전 대기 (5초)
            yield return new WaitForSecondsRealtime(beforeMessagesDelaySec);

            // 3. 4메시지 순차 + 효과음
            if (roaMessage != null)
            {
                var messages = CollectRoaMessages();
                roaMessage.OnMessageShown += PlayMessageSfx;
                yield return roaMessage.PlaySequence(messages);
                roaMessage.OnMessageShown -= PlayMessageSfx;
            }

            // 4. +3초 후 클릭 가능
            yield return new WaitForSecondsRealtime(afterLastMessageDelaySec);
            if (inputCatcher != null) inputCatcher.gameObject.SetActive(true);
        }

        void OnInputCatcherClicked()
        {
            if (seqCo != null) StopCoroutine(seqCo);
            seqCo = StartCoroutine(EnterLoginStage());
        }

        IEnumerator EnterLoginStage()
        {
            if (inputCatcher != null) inputCatcher.gameObject.SetActive(false);

            // 좌측 위젯/메시지/dim 동시 진행
            Coroutine left = StartCoroutine(SlideOutLeftWidgets());
            Coroutine msg  = roaMessage != null ? StartCoroutine(roaMessage.HideRoutine()) : null;
            Coroutine dim  = loginDim != null ? StartCoroutine(FadeCanvas(loginDim, 0f, loginDimAlpha, loginDimFadeDuration)) : null;

            yield return left;
            if (msg != null) yield return msg;
            if (dim != null) yield return dim;

            if (loginStage != null) loginStage.SetActive(true);
            ApplyHintForCurrentMode();

            if (passwordInput != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(passwordInput.gameObject);
        }

        void ApplyHintForCurrentMode()
        {
            if (lockScreen == null || passwordInput == null) return;
            switch (lockScreen.CurrentMode)
            {
                case LockScreenMode.FirstSetup:
                case LockScreenMode.Reset:
                    SetHeader(LockScreenHint.FirstSetup);
                    passwordInput.SetMaskMode(false); // 첫 설정 — 평문
                    passwordInput.SetKeyIcon(false);
                    break;
                case LockScreenMode.Normal:
                default:
                    SetHeader(LockScreenHint.Normal);
                    passwordInput.SetMaskMode(true);  // 평상시 — 마스킹
                    passwordInput.SetKeyIcon(false);
                    break;
            }
        }

        void SetHeader(LockScreenHint kind)
        {
            if (headerText == null || lockScreen == null) return;
            headerText.text = lockScreen.GetHint(kind);
        }

        // ── 비번 처리 ──
        void HandlePasswordEntered(string pwd)
        {
            if (lockScreen == null) return;
            switch (lockScreen.CurrentMode)
            {
                case LockScreenMode.FirstSetup:
                case LockScreenMode.Reset:
                    if (lockScreen.SetPassword(pwd))
                    {
                        SetHeader(LockScreenHint.Complete);
                        StartCoroutine(DelayedOutro(1.0f));
                    }
                    else passwordInput.PlayShake();
                    break;

                case LockScreenMode.Normal:
                    if (lockScreen.VerifyPassword(pwd))
                        StartCoroutine(OutroSequence());
                    // 실패는 HandlePasswordFailed에서
                    break;
            }
        }

        void HandlePasswordFailed(int failCount)
        {
            if (passwordInput == null || lockScreen == null) return;
            passwordInput.PlayShake();
            passwordInput.SetKeyIcon(lockScreen.ShowKeyIcon);
            if (lockScreen.ShowKeyIcon) SetHeader(LockScreenHint.Forgot);
        }

        // ── 열쇠 → 재설정 확인 팝업 ──
        async void HandleKeyClicked()
        {
            if (lockScreen == null) return;
            var content = lockScreen.Content;
            string title = content != null ? content.resetConfirmTitle : "새로운 비밀번호 설정을\n진행하시겠습니까?";
            string yes   = content != null ? content.resetConfirmYes : "예";
            string no    = content != null ? content.resetConfirmNo : "아니오";

            var pm = PopupManager.Instance;
            if (pm == null)
            {
                Debug.LogWarning("[LockScreenPanel] PopupManager 미존재 — 재설정 직접 진행");
                Begin(LockScreenMode.Reset, fadeIn: false);
                return;
            }
            var popup = pm.Get<ConfirmPopup>();
            if (popup == null)
            {
                Debug.LogWarning("[LockScreenPanel] ConfirmPopup 미등록 — 재설정 직접 진행");
                Begin(LockScreenMode.Reset, fadeIn: false);
                return;
            }

            bool ok = await popup.ShowAsync(new ConfirmPopupData
            {
                mainText = title,
                confirmText = yes,
                cancelText = no
            });

            if (ok) Begin(LockScreenMode.Reset, fadeIn: false);
        }

        // ── Outro ──
        IEnumerator DelayedOutro(float waitSec)
        {
            yield return new WaitForSecondsRealtime(waitSec);
            yield return OutroSequence();
        }

        IEnumerator OutroSequence()
        {
            if (rootCanvasGroup != null)
                yield return FadeCanvas(rootCanvasGroup, rootCanvasGroup.alpha, 0f, outroPanelFadeSec);

            if (blackOverlay != null)
                yield return FadeCanvas(blackOverlay, 0f, 1f, outroBlackFadeSec);

            OnFlowComplete?.Invoke();
            gameObject.SetActive(false);
        }

        // ── 좌측 위젯 슬라이드아웃 ──
        IEnumerator SlideOutLeftWidgets()
        {
            float t = 0f;
            while (t < leftSlideOutDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / leftSlideOutDuration);
                float ease = p * p;
                for (int i = 0; i < leftWidgets.Count; i++)
                {
                    if (leftWidgets[i] == null) continue;
                    Vector2 dest = leftWidgetOriginalPos[i] + Vector2.left * leftSlideOutDistance;
                    leftWidgets[i].anchoredPosition = Vector2.Lerp(leftWidgetOriginalPos[i], dest, ease);
                }
                yield return null;
            }
            for (int i = 0; i < leftWidgets.Count; i++)
            {
                if (leftWidgets[i] == null) continue;
                leftWidgets[i].gameObject.SetActive(false);
            }
        }

        void ResetLeftWidgetPositions()
        {
            for (int i = 0; i < leftWidgets.Count; i++)
            {
                if (leftWidgets[i] == null) continue;
                leftWidgets[i].gameObject.SetActive(true);
                leftWidgets[i].anchoredPosition = leftWidgetOriginalPos[i];
            }
        }

        // ── 헬퍼 ──
        IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;
            if (duration <= 0f) { cg.alpha = to; yield break; }
            float t = 0f;
            cg.alpha = from;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            cg.alpha = to;
        }

        IList<string> CollectRoaMessages()
        {
            var list = new List<string>();
            if (lockScreen == null) return list;
            for (int i = 0; i < 4; i++)
            {
                string m = lockScreen.GetRoaMessage(i);
                if (!string.IsNullOrEmpty(m)) list.Add(m);
            }
            if (list.Count == 0)
                Debug.LogWarning("[LockScreenPanel] 로아 메시지 4개 비어있음 — LockScreenContentSO 확인");
            return list;
        }

        void PlayMessageSfx()
        {
            // D8 임시. 정식 SFX 도착 시 교체.
            if (sfxSource != null && messageSfx != null)
                sfxSource.PlayOneShot(messageSfx);
        }
    }
}
