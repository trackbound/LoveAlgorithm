using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common;
using LoveAlgo.Core;
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

        [Header("동작 모드 (timing은 FXDefaultsConfig SO에서 단일 관리)")]
        [Tooltip("게임 첫 시작 시 페이드인. EntryRouter가 첫 시작 분기에서 외부 set 권장.")]
        [SerializeField] bool useFirstStartFadeIn = false;
        [Tooltip("기본 outro에 fade-out까지 포함할지. true면 흐름 종료 시 화면이 완전히 노출됨.\n외부에서 SetFadeOutAfter(bool)로 1회 override 가능 (CSV :FadeOut 옵션).")]
        [SerializeField] bool defaultWithFadeOut = false;

        // ── 타이밍 (FXDefaultsConfig SO 단일 정전 — SerializedField/const 폴백 X) ──
        // SO 없으면 안전 폴백. 사용자가 시간 조정은 Resources/Data/FXDefaultsConfig.asset에서만.
        static float FirstStartFadeInSec   => Cfg(c => c.lockScreenFirstStartFadeIn, 0.8f);
        static float BeforeMessagesDelaySec => Cfg(c => c.lockScreenBeforeMessages, 5f);
        static float AfterLastMessageDelaySec => Cfg(c => c.lockScreenAfterLastMessage, 3f);
        static float OutroFadeToBlackSec   => Cfg(c => c.lockScreenOutroToBlack, 0.5f);
        static float OutroFadeFromBlackSec => Cfg(c => c.lockScreenOutroFromBlack, 0.5f);

        static float Cfg(Func<FXDefaultsConfig, float> sel, float fallback)
        {
            var cfg = FXDefaultsConfig.Instance;
            return cfg != null ? sel(cfg) : fallback;
        }

        ILockScreen lockScreen;
        Coroutine seqCo;
        Vector2[] leftWidgetOriginalPos;
        bool? withFadeOutOverride;     // null = defaultWithFadeOut 사용

        /// <summary>잠금 흐름 정상 완료 (페이드아웃까지 끝남 — withFadeOut 적용 시).
        /// CSV 다음 라인 또는 caller가 다음 화면 전환에 사용.</summary>
        public event Action OnFlowComplete;

        /// <summary>화면이 완전히 검정에 도달한 순간 (Outro Phase 1 끝).
        /// EntryRouter가 Title 활성화 시점으로 사용 — 그 후 fade-out reveal.</summary>
        public event Action OnBlackoutReached;

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

        // ══════════════════════════════════════════════
        //  외부 진입 API
        //  ─ 화면 상태 2개:
        //    Standby = 시계/ToDo/로아 메시지 4개 (대기화면, 클릭 액션 없음)
        //    Login   = 비번 입력창 + 눈 토글 + LOGIN 버튼 (잠금화면)
        //  ─ Full = Standby → 클릭 대기 → Login (기획서 기본 흐름)
        // ══════════════════════════════════════════════

        /// <summary>
        /// 기획서 §구성 ① — 대기화면만 표시. 로그인 단계로 자동 진입하지 않음.
        /// 클릭/대화 진행 등 외부 트리거 전까지 대기 상태 유지.
        /// 외부에서 GoToLoginStage() 호출하면 잠금화면으로 전환 가능.
        /// </summary>
        public void OpenStandbyOnly(LockScreenMode mode, bool fadeIn = false)
            => Begin(mode, fadeIn: fadeIn, gotoLogin: false);

        /// <summary>
        /// 기획서 §구성 ② — 잠금화면 단계만. 대기화면 인트로(시계/메시지) 스킵.
        /// 재진입·게임 내 로그인 요청 등에 사용.
        /// </summary>
        public void OpenLoginOnly(LockScreenMode mode, bool fadeIn = false)
            => Begin(mode, fadeIn: fadeIn, skipIntro: true, gotoLogin: true);

        /// <summary>
        /// 기획서 기본 흐름 — 대기화면 → 클릭 대기 → 잠금화면 → 비번 입력 → outro.
        /// </summary>
        public void OpenFullSequence(LockScreenMode mode, bool fadeIn = false)
            => Begin(mode, fadeIn: fadeIn, gotoLogin: true);

        // ── 기존 sugar (하위 호환) ──
        public void OpenFirstSetup() => OpenFullSequence(LockScreenMode.FirstSetup, fadeIn: useFirstStartFadeIn);
        public void OpenNormal()     => OpenFullSequence(LockScreenMode.Normal);
        public void OpenReset()      => OpenFullSequence(LockScreenMode.Reset);

        /// <summary>
        /// 게임 첫 시작 sugar — 5초 페이드인 강제 + GameStart 모드(LOGIN 버튼만).
        /// EntryRouter / CSV GameStart 모드에서 호출.
        /// </summary>
        public void OpenForGameStart()
            => OpenFullSequence(LockScreenMode.GameStart, fadeIn: true);

        /// <summary>
        /// CSV Auto sugar — 비번 자동 판별, 페이드인 없음.
        /// </summary>
        public void OpenAuto()
        {
            var mode = (lockScreen != null && lockScreen.IsPasswordSet)
                ? LockScreenMode.Normal : LockScreenMode.FirstSetup;
            OpenFullSequence(mode);
        }

        /// <summary>
        /// 대기화면만 띄운 상태에서 외부 트리거(예: 스크립트 라인)로 잠금화면 전환.
        /// OpenStandbyOnly로 진입한 경우에만 의미 있음.
        /// </summary>
        public void GoToLoginStage()
        {
            if (seqCo != null) StopCoroutine(seqCo);
            seqCo = StartCoroutine(EnterLoginStage());
        }

        /// <summary>이번 1회 outro에 fade-out(black→0)까지 포함할지 override. null=defaultWithFadeOut.</summary>
        public void SetFadeOutAfter(bool? value)
        {
            withFadeOutOverride = value;
        }

        public void Close()
        {
            if (seqCo != null) StopCoroutine(seqCo);
            gameObject.SetActive(false);
        }

        /// <param name="gotoLogin">true면 대기 → 클릭 대기 → 로그인. false면 대기 상태에서 멈춤.</param>
        /// <param name="skipIntro">true면 대기화면(시계/메시지) 스킵하고 곧장 로그인 단계로.</param>
        void Begin(LockScreenMode mode, bool fadeIn, bool gotoLogin = true, bool skipIntro = false)
        {
            if (lockScreen == null) return;
            switch (mode)
            {
                case LockScreenMode.FirstSetup: lockScreen.OpenForFirstSetup(); break;
                case LockScreenMode.Reset:      lockScreen.OpenForReset();      break;
                case LockScreenMode.GameStart:  lockScreen.OpenForGameStart();  break;
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

            if (skipIntro)
            {
                // 잠금화면 단독 — 대기 인트로 스킵, 곧장 로그인
                if (rootCanvasGroup != null) rootCanvasGroup.alpha = 1f;
                // 좌측 위젯은 처음부터 숨김(슬라이드 대신 즉시 제거)
                HideLeftWidgetsImmediate();
                seqCo = StartCoroutine(EnterLoginStage(skipSlideAnim: true));
            }
            else
            {
                seqCo = StartCoroutine(IntroSequence(fadeIn, gotoLogin));
            }
        }

        IEnumerator IntroSequence(bool fadeIn, bool gotoLogin)
        {
            // 1. 페이드인
            if (rootCanvasGroup != null)
            {
                if (fadeIn) yield return FadeCanvas(rootCanvasGroup, 0f, 1f, FirstStartFadeInSec);
                else rootCanvasGroup.alpha = 1f;
            }

            // 2. 메시지 시작 전 대기 (5초)
            yield return new WaitForSecondsRealtime(BeforeMessagesDelaySec);

            // 3. 4메시지 순차 + 효과음
            if (roaMessage != null)
            {
                var messages = CollectRoaMessages();
                roaMessage.OnMessageShown += PlayMessageSfx;
                yield return roaMessage.PlaySequence(messages);
                roaMessage.OnMessageShown -= PlayMessageSfx;
            }

            // 4. +3초 후 클릭 가능 (gotoLogin=false면 여기서 멈춤 — 외부 트리거 대기)
            yield return new WaitForSecondsRealtime(AfterLastMessageDelaySec);
            if (gotoLogin && inputCatcher != null) inputCatcher.gameObject.SetActive(true);
        }

        void HideLeftWidgetsImmediate()
        {
            for (int i = 0; i < leftWidgets.Count; i++)
                if (leftWidgets[i] != null) leftWidgets[i].gameObject.SetActive(false);
        }

        void OnInputCatcherClicked()
        {
            if (seqCo != null) StopCoroutine(seqCo);
            seqCo = StartCoroutine(EnterLoginStage());
        }

        IEnumerator EnterLoginStage(bool skipSlideAnim = false)
        {
            if (inputCatcher != null) inputCatcher.gameObject.SetActive(false);

            if (skipSlideAnim)
            {
                // OpenLoginOnly 진입 — 슬라이드/메시지 페이드 없이 즉시 dim 표시
                if (roaMessage != null) roaMessage.HideAllImmediate();
                if (loginDim != null) loginDim.alpha = loginDimAlpha;
            }
            else
            {
                // 좌측 위젯/메시지/dim 동시 진행
                Coroutine left = StartCoroutine(SlideOutLeftWidgets());
                Coroutine msg  = roaMessage != null ? StartCoroutine(roaMessage.HideRoutine()) : null;
                Coroutine dim  = loginDim != null ? StartCoroutine(FadeCanvas(loginDim, 0f, loginDimAlpha, loginDimFadeDuration)) : null;

                yield return left;
                if (msg != null) yield return msg;
                if (dim != null) yield return dim;
            }

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
                case LockScreenMode.GameStart:
                    // 첫 진입: 안내 텍스트 비움, InputField/Toggle 숨김, LOGIN 버튼만 노출
                    if (headerText != null) headerText.text = "";
                    passwordInput.SetLoginOnly(true);
                    passwordInput.SetKeyIcon(false);
                    break;
                case LockScreenMode.FirstSetup:
                case LockScreenMode.Reset:
                    SetHeader(LockScreenHint.FirstSetup);
                    passwordInput.SetLoginOnly(false);
                    passwordInput.SetMaskMode(false); // 첫 설정 — 평문
                    passwordInput.SetKeyIcon(false);
                    break;
                case LockScreenMode.Normal:
                default:
                    SetHeader(LockScreenHint.Normal);
                    passwordInput.SetLoginOnly(false);
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
                case LockScreenMode.GameStart:
                    // 비번 검증/저장 없이 그대로 outro (시각 연출 통과)
                    StartCoroutine(OutroSequence());
                    break;

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

            // 실패 횟수별 안내문 차별화 (3회 이상 = Forgot + 열쇠)
            if (lockScreen.ShowKeyIcon)
                SetHeader(LockScreenHint.Forgot);
            else if (failCount == 1)
                SetHeader(LockScreenHint.WrongOnce);
            else if (failCount == 2)
                SetHeader(LockScreenHint.WrongTwice);
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
            // ── Phase 1: 완전 페이드 — 검정 오버레이가 panel을 덮으며 등장 ──
            // (이전 크로스페이드: panel↓ + black↑ 동시 → 도중에 알파 합산으로 회색 톤 발생)
            // black이 panel을 확실히 가리도록 sibling order 강제.
            if (blackOverlay != null)
            {
                blackOverlay.transform.SetAsLastSibling();   // panel 위로 보장
                yield return FadeCanvas(blackOverlay, blackOverlay.alpha, 1f, OutroFadeToBlackSec);
                if (rootCanvasGroup != null) rootCanvasGroup.alpha = 0f;
            }
            else if (rootCanvasGroup != null)
            {
                // 검정 오버레이가 없을 때만 panel을 fade out
                yield return FadeCanvas(rootCanvasGroup, rootCanvasGroup.alpha, 0f, OutroFadeToBlackSec);
            }

            // 검은 화면 도달 — EntryRouter/외부가 다음 화면을 검은 뒤에서 셋업할 수 있는 순간
            OnBlackoutReached?.Invoke();

            // ── Phase 2 (옵션): 페이드아웃 — 검정 사라지며 다음 화면 reveal ──
            bool withFadeOut = withFadeOutOverride ?? defaultWithFadeOut;
            withFadeOutOverride = null; // 1회용 override 리셋

            if (withFadeOut && blackOverlay != null)
                yield return FadeCanvas(blackOverlay, 1f, 0f, OutroFadeFromBlackSec);

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
