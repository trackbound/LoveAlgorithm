using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowLockScreenCommand, SubmitPasswordCommand, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 잠금화면 입력 뷰(*View: LockScreen) — ADR-007: UI는 표시 + 명령 발행만(State 안 씀).
    /// <see cref="ShowLockScreenCommand"/>를 구독해 풀스크린 잠금 오버레이를 띄우고 비번 입력을 받는다.
    /// 확정(엔터/버튼, 빈 입력은 무시) 시 <see cref="SubmitPasswordCommand"/>를 발행하고(저장은 LockScreenController)
    /// 오버레이를 닫는다. <see cref="ShowLockScreenCommand.FadeOut"/>=true면 닫을 때 <see cref="fadeGroup"/> 알파를
    /// 1→0으로 페이드(검은 배경→게임화면 노출, A안 자체 페이드). 내러티브 종료/도구 화면정리 시 즉시 숨김.
    /// 이번 슬라이스는 FirstSetup(평문, 마스킹 없음)만. 스타일(시계/배경/확정버튼)은 오버레이 자식으로 감독 튜닝.
    /// </summary>
    public class LockScreenView : MonoBehaviour
    {
        [Tooltip("풀스크린 잠금 오버레이 루트. 미바인딩 시 효과 생략(핸들은 Controller가 Submit으로 완료).")]
        [SerializeField] GameObject overlay;

        [Tooltip("FadeOut 페이드 대상(검은 배경 포함 오버레이 그룹). 미바인딩 시 즉시 닫기.")]
        [SerializeField] CanvasGroup fadeGroup;

        [Tooltip("비밀번호 입력 필드(FirstSetup=평문).")]
        [SerializeField] TMP_InputField input;

        [Tooltip("FadeOut 지속(초).")]
        [SerializeField] float fadeOutDuration = 0.5f;

        [Tooltip("시작 크로스페이드(페이드인) 지속(초). 0이면 즉시 표시. fadeGroup 알파 0→1로 스토리 위에 부드럽게 진입.")]
        [SerializeField] float fadeInDuration = 0.3f;

        [Header("Custom System (선택 — 미바인딩 시 기존 즉시 경로 폴백)")]
        [Tooltip("진입 연출 오케스트레이터. 바인딩 시 위젯 슬라이드아웃→딤→입력 reveal 후 입력 활성.")]
        [SerializeField] LockScreenIntroDirector intro;
        [Tooltip("입력칸 래퍼(마스킹/눈토글/7자/진동).")]
        [SerializeField] PasswordInputField passwordField;
        [Tooltip("입력칸 위 안내 텍스트(상태별).")]
        [SerializeField] LockScreenGuideText guide;
        [Tooltip("확정 버튼(모드별 라벨).")]
        [SerializeField] LoginButton loginButton;
        [Tooltip("분실 시 우하단 열쇠 버튼(3회+ 오류 노출).")]
        [SerializeField] KeyResetButton keyButton;
        [Tooltip("분실 안내·열쇠 노출 임계 오류 횟수.")]
        [SerializeField] int lostThreshold = 3;
        [SerializeField] string setupButtonLabel = "입력 완료";
        [SerializeField] string normalButtonLabel = "LOGIN";

        public GameObject Overlay { get => overlay; set => overlay = value; }
        public CanvasGroup FadeGroup { get => fadeGroup; set => fadeGroup = value; }
        public TMP_InputField Input { get => input; set => input = value; }
        public LockScreenIntroDirector Intro { get => intro; set => intro = value; }
        public PasswordInputField PasswordField { get => passwordField; set => passwordField = value; }
        public LockScreenGuideText Guide { get => guide; set => guide = value; }
        public LoginButton LoginButton { get => loginButton; set => loginButton = value; }
        public KeyResetButton KeyButton { get => keyButton; set => keyButton = value; }
        public int LostThreshold { get => lostThreshold; set => lostThreshold = value; }

        IDisposable _sub, _finishSub, _resetSub, _failSub, _acceptSub, _resetReqSub;
        Coroutine _fadeRoutine;
        bool _fadeOut;
        LockMode _mode;

        void OnEnable()
        {
            _sub       = EventBus.Subscribe<ShowLockScreenCommand>(OnShow);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => HideImmediate());
            _resetSub  = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => HideImmediate());
            _failSub   = EventBus.Subscribe<PasswordVerifyFailedEvent>(OnVerifyFailed);
            _acceptSub = EventBus.Subscribe<PasswordAcceptedEvent>(_ => Hide());
            _resetReqSub = EventBus.Subscribe<RequestPasswordResetCommand>(_ => OnResetRequested());
            if (input != null)
            {
                // 수동 구성 InputField 안전망: textComponent 미연결 시 자식 TMP_Text를 런타임 연결(없으면 입력 표시 불가).
                // 표준 TMP InputField는 이미 연결돼 있어 무해(null일 때만 동작).
                if (input.textComponent == null)
                {
                    var tc = input.GetComponentInChildren<TMP_Text>(true);
                    if (tc != null) input.textComponent = tc;
                }
                input.onSubmit.AddListener(OnInputSubmit);
            }
            HideImmediate();
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _failSub?.Dispose(); _acceptSub?.Dispose(); _resetReqSub?.Dispose();
            _sub = _finishSub = _resetSub = _failSub = _acceptSub = _resetReqSub = null;
            if (input != null) input.onSubmit.RemoveListener(OnInputSubmit);
        }

        /// <summary>잠금화면 표시 — 오버레이 켜고 모드별 구성. intro 바인딩 시 연출 후 입력 활성, 아니면 즉시.</summary>
        public void OnShow(ShowLockScreenCommand e)
        {
            _fadeOut = e.FadeOut;
            _mode = e.Mode;
            if (overlay == null) return; // 효과 생략 — Controller가 Submit으로 핸들 완료(여기선 막지 않음).
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            overlay.SetActive(true);
            if (input != null) input.text = "";
            if (passwordField != null) passwordField.ResetField();

            if (keyButton != null) keyButton.SetVisible(false); // 새 세션 — 열쇠 숨김
            ConfigureForMode(_mode);

            if (intro != null)
            {
                // 연출 경로: 즉시 전체 표시(스토리 위 위젯 present) 후 staged 연출.
                if (fadeGroup != null) fadeGroup.alpha = 1f;
                intro.ResetToStart();
                if (isActiveAndEnabled) intro.Play(ActivateInput);
                else { ActivateInput(); }
            }
            else
            {
                // 폴백(기존 동작): 입력 즉시 활성 + 시작 크로스페이드.
                ActivateInput();
                if (fadeGroup != null && isActiveAndEnabled && fadeInDuration > 0f)
                    _fadeRoutine = StartCoroutine(FadeInAndShow());
                else if (fadeGroup != null)
                    fadeGroup.alpha = 1f;
            }
        }

        /// <summary>모드별 위젯 구성 — 마스킹 기본값/버튼 라벨/가이드 상태. 미바인딩 필드는 건너뜀.</summary>
        void ConfigureForMode(LockMode mode)
        {
            bool normal = mode == LockMode.Normal;
            if (passwordField != null) passwordField.SetMasked(normal);
            if (loginButton != null) { loginButton.SetLabel(normal ? normalButtonLabel : setupButtonLabel); loginButton.Refresh(); }
            if (guide != null) guide.SetState(normal ? LockScreenGuideText.LockGuideState.Normal
                                                     : LockScreenGuideText.LockGuideState.Setup);
        }

        /// <summary>입력 활성·포커스. 연출 종료 콜백 또는 폴백 즉시 호출.</summary>
        void ActivateInput()
        {
            if (input != null) input.ActivateInputField();
        }

        IEnumerator FadeInAndShow()
        {
            float t = 0f;
            fadeGroup.alpha = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                fadeGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
            fadeGroup.alpha = 1f;
            _fadeRoutine = null;
        }

        void OnInputSubmit(string _) => Confirm();

        /// <summary>입력 확정 — 비면 무시(재입력 유도), 아니면 Submit 발행 후 닫기. 확정 버튼에서도 호출.</summary>
        public void Confirm()
        {
            if (overlay == null || !overlay.activeSelf) return;
            string pwd = input != null ? input.text : "";
            if (string.IsNullOrEmpty(pwd))
            {
                if (input != null) input.ActivateInputField();
                return;
            }
            // FirstSetup/Reset 제출 시 '설정 완료!' 안내로 전환(닫힘 페이드 동안 노출).
            if (guide != null && _mode != LockMode.Normal)
                guide.SetState(LockScreenGuideText.LockGuideState.SetupComplete);
            EventBus.Publish(new SubmitPasswordCommand(pwd)); // 저장/검증은 Controller(ADR-007).
            // Normal은 검증 결과를 기다린다(불일치 재입력). 닫기는 PasswordAcceptedEvent 수신 시.
            if (_mode != LockMode.Normal) Hide();
        }

        /// <summary>검증 실패 — 입력칸 진동 + 입력 초기화·재포커스(가이드는 S3에서 ≥3 분실 처리).</summary>
        void OnVerifyFailed(PasswordVerifyFailedEvent e)
        {
            if (overlay == null || !overlay.activeSelf) return;
            if (passwordField != null) passwordField.Shake();
            if (input != null) { input.text = ""; input.ActivateInputField(); }
            else if (passwordField != null) passwordField.ResetField();

            if (e.ErrorCount >= lostThreshold)
            {
                if (guide != null) guide.SetState(LockScreenGuideText.LockGuideState.Lost);
                if (keyButton != null) keyButton.SetVisible(true);
            }
        }

        /// <summary>재설정 요청 — Reset 모드로 UI 재구성(평문·설정 가이드·"입력 완료"), 열쇠 숨김, 입력 초기화.</summary>
        void OnResetRequested()
        {
            if (overlay == null || !overlay.activeSelf) return;
            _mode = LockMode.Reset;
            ConfigureForMode(_mode);
            if (keyButton != null) keyButton.SetVisible(false);
            if (input != null) { input.text = ""; input.ActivateInputField(); }
            else if (passwordField != null) passwordField.ResetField();
        }

        void Hide()
        {
            if (overlay == null) return;
            if (_fadeOut && fadeGroup != null && isActiveAndEnabled)
                _fadeRoutine = StartCoroutine(FadeOutAndHide());
            else
                HideImmediate();
        }

        IEnumerator FadeOutAndHide()
        {
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                if (fadeGroup != null) fadeGroup.alpha = Mathf.Clamp01(1f - t / fadeOutDuration);
                yield return null;
            }
            _fadeRoutine = null;
            HideImmediate();
        }

        void HideImmediate()
        {
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            if (overlay != null) overlay.SetActive(false);
        }
    }
}
