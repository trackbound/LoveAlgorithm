using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO, ScreenPhase
using LoveAlgo.Events; // Messenger 통지, ScreenPhaseChangedEvent, SetCgModeCommand

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 폰 버튼(*View) — 메신저 진입점 1(기획서): 평소 우측 가장자리에 살짝 보이다 호버 시 왼쪽으로
    /// 슬라이드, 새 메시지 도착 시 진동 2초 + 배지, 클릭 시 메신저 열기 명령 발행(표시만, ADR-007).
    ///
    /// 노출 규칙(기획서 + Q&amp;A 확정): Story 페이즈에서만 표시 — 타이틀(별도 씬)/스탯·행동창(Schedule,
    /// 빠른 메뉴가 진입 담당)/엔딩에서 숨김. CG 모드·메신저 열림 중에도 숨김(연출 보호).
    /// GO 비활성 대신 CanvasGroup으로 숨겨 구독·코루틴을 유지한다.
    /// </summary>
    public class PhoneButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] GameStateSO state;
        [SerializeField] MessengerTuningSO tuning;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Button button;
        [SerializeField] GameObject badge;

        public GameStateSO State { get => state; set => state = value; }
        public MessengerTuningSO Tuning { get => tuning; set => tuning = value; }
        public CanvasGroup Group { get => canvasGroup; set => canvasGroup = value; }
        public Button Button { get => button; set => button = value; }
        public GameObject Badge { get => badge; set => badge = value; }

        readonly List<IDisposable> _subs = new();
        RectTransform _rt;
        Vector2 _restPos;
        bool _cgMode;
        bool _messengerOpen;
        Coroutine _slide;
        Coroutine _vibrate;

        public bool IsShown => canvasGroup == null || canvasGroup.alpha > 0.5f;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _restPos = _rt.anchoredPosition;
            if (button != null) button.onClick.AddListener(() => EventBus.Publish(new OpenMessengerCommand()));
        }

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<ScreenPhaseChangedEvent>(_ => ApplyVisibility()));
            _subs.Add(EventBus.Subscribe<SetCgModeCommand>(e => { _cgMode = e.Active; ApplyVisibility(); }));
            _subs.Add(EventBus.Subscribe<OpenMessengerCommand>(_ => { _messengerOpen = true; ApplyVisibility(); }));
            _subs.Add(EventBus.Subscribe<CloseMessengerCommand>(_ => { _messengerOpen = false; ApplyVisibility(); }));
            _subs.Add(EventBus.Subscribe<MessengerMessageArrivedEvent>(OnArrived));
            _subs.Add(EventBus.Subscribe<MessengerSequenceReadEvent>(_ => RefreshBadge()));
            ApplyVisibility();
            RefreshBadge();
        }

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
            StopAllCoroutines();
            _slide = null; _vibrate = null;
            if (_rt != null) { _rt.anchoredPosition = _restPos; _rt.localRotation = Quaternion.identity; }
        }

        void OnArrived(MessengerMessageArrivedEvent _)
        {
            RefreshBadge();
            if (!IsShown) return; // 숨김 중엔 배지만(연출 보호)
            if (_vibrate != null) StopCoroutine(_vibrate);
            _vibrate = StartCoroutine(Vibrate());
        }

        void RefreshBadge()
        {
            if (badge != null) badge.SetActive(state != null && MessengerService.UnreadCount(state) > 0);
        }

        void ApplyVisibility()
        {
            bool visible = state != null
                && state.Phase == ScreenPhase.Story
                && !_cgMode
                && !_messengerOpen;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            if (!visible && _rt != null) _rt.anchoredPosition = _restPos; // 숨김 시 슬라이드 원복
        }

        // ── 호버 슬라이드(기획서: 마우스 커서 가져다 대면 왼쪽으로 슬라이드되며 열림) ──

        public void OnPointerEnter(PointerEventData _) => SlideTo(-SlideDistance);
        public void OnPointerExit(PointerEventData _) => SlideTo(0f);

        float SlideDistance => tuning != null ? tuning.slideDistance : 150f;
        float SlideDuration => tuning != null ? tuning.slideDuration : 0.15f;

        void SlideTo(float offsetX)
        {
            if (!IsShown) return;
            if (_slide != null) StopCoroutine(_slide);
            _slide = StartCoroutine(SlideRoutine(_restPos + new Vector2(offsetX, 0f)));
        }

        IEnumerator SlideRoutine(Vector2 target)
        {
            Vector2 from = _rt.anchoredPosition;
            float dur = Mathf.Max(0.0001f, SlideDuration);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                _rt.anchoredPosition = Vector2.LerpUnclamped(from, target, k);
                yield return null;
            }
            _rt.anchoredPosition = target;
            _slide = null;
        }

        // ── 새 메시지 진동(기획서 동결: 2초) — 감쇠 사인 회전(ShakeView 임팩트 모델 미니멀판) ──

        IEnumerator Vibrate()
        {
            float dur = tuning != null ? tuning.vibrateDuration : 2f;
            float angle = tuning != null ? tuning.vibrateAngle : 8f;
            float freq = tuning != null ? tuning.vibrateFrequency : 18f;

            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float decay = 1f - (t / dur);
                float z = Mathf.Sin(t * freq * Mathf.PI * 2f) * angle * decay;
                _rt.localRotation = Quaternion.Euler(0f, 0f, z);
                yield return null;
            }
            _rt.localRotation = Quaternion.identity;
            _vibrate = null;
        }
    }
}
