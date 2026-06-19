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

        public GameObject Overlay { get => overlay; set => overlay = value; }
        public CanvasGroup FadeGroup { get => fadeGroup; set => fadeGroup = value; }
        public TMP_InputField Input { get => input; set => input = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _fadeRoutine;
        bool _fadeOut;

        void OnEnable()
        {
            _sub       = EventBus.Subscribe<ShowLockScreenCommand>(OnShow);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => HideImmediate());
            _resetSub  = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => HideImmediate());
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
            _sub = _finishSub = _resetSub = null;
            if (input != null) input.onSubmit.RemoveListener(OnInputSubmit);
        }

        /// <summary>잠금화면 표시 — 오버레이 켜고 입력 초기화·포커스. 직접 호출도 가능(테스트).</summary>
        public void OnShow(ShowLockScreenCommand e)
        {
            _fadeOut = e.FadeOut;
            if (overlay == null) return; // 효과 생략 — Controller가 Submit으로 핸들 완료(여기선 막지 않음).
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            overlay.SetActive(true);
            if (input != null)
            {
                input.text = "";
                input.ActivateInputField(); // 포커스
            }
            // 시작 크로스페이드: fadeGroup 알파 0→1로 짧게 페이드인(스토리 위로 부드럽게 진입).
            // 미바인딩/비활성/0초면 즉시 표시(폴백).
            if (fadeGroup != null && isActiveAndEnabled && fadeInDuration > 0f)
                _fadeRoutine = StartCoroutine(FadeInAndShow());
            else if (fadeGroup != null)
                fadeGroup.alpha = 1f;
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
            EventBus.Publish(new SubmitPasswordCommand(pwd)); // 저장은 Controller(ADR-007).
            Hide();
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
