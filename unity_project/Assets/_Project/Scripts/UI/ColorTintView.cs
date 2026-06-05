using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ColorTintCommand, CompletionHandle, NarrativeFinishedEvent
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 색 틴트 뷰(*View, M3 슬라이스2: ColorTint). <see cref="ColorTintCommand"/>를 구독해 전체화면 오버레이 Image의
    /// 색을 코루틴 lerp하고, 완료 시 핸들을 푼다(ADR-007: UI는 표시만). 슬라이스1처럼 DOTween 미사용.
    /// 무드 틴트는 지속 상태(다음 틴트/Clear까지 유지). 구 ScreenFX처럼 화면 최상위에 얹되 입력은 막지 않는다.
    /// Clear면 현재 색을 유지하며 알파만 0으로. 내러티브 종료 시 잔여 틴트 해제.
    /// </summary>
    public class ColorTintView : MonoBehaviour
    {
        [Tooltip("전체화면 틴트 오버레이 Image. 미바인딩 시 효과 생략·핸들만 완료.")]
        [SerializeField] Image overlay;

        public Image Overlay { get => overlay; set => overlay = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        CompletionHandle _pending;

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ColorTintCommand>(OnTint);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetTint());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetTint()); // 도구 화면 정리
            if (overlay != null)
            {
                overlay.raycastTarget = false; // 틴트는 입력을 막지 않음.
                SetAlpha(0f);
                overlay.enabled = false;
            }
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
        }

        void OnTint(ColorTintCommand e)
        {
            if (overlay == null) { e.Handle?.Complete(); return; }

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _pending?.Complete(); // 끊긴 이전 핸들이 엔진을 막지 않도록.
            }
            _pending = e.Handle;
            _routine = StartCoroutine(Run(e));
        }

        IEnumerator Run(ColorTintCommand e)
        {
            overlay.enabled = true;
            Color from = overlay.color;
            // Clear면 현재 색 유지하며 알파만 0. 아니면 (R,G,B,Alpha)로 전이.
            Color to = e.Clear ? new Color(from.r, from.g, from.b, 0f) : new Color(e.R, e.G, e.B, e.Alpha);

            float duration = e.Duration;
            if (duration <= 0f)
            {
                overlay.color = to;
            }
            else
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    overlay.color = Color.LerpUnclamped(from, to, Mathf.Clamp01(t / duration));
                    yield return null;
                }
                overlay.color = to;
            }

            if (to.a <= 0f) overlay.enabled = false; // 완전 해제 시 비활성.
            Finish();
        }

        void Finish()
        {
            var h = _pending;
            _pending = null;
            _routine = null;
            h?.Complete();
        }

        /// <summary>내러티브 종료 시 잔여 틴트가 시뮬레이션으로 새지 않도록 즉시 해제.</summary>
        void ResetTint()
        {
            if (_routine != null) { StopCoroutine(_routine); _pending?.Complete(); _pending = null; _routine = null; }
            if (overlay != null) { SetAlpha(0f); overlay.enabled = false; }
        }

        void SetAlpha(float a)
        {
            var c = overlay.color;
            c.a = a;
            overlay.color = c;
        }
    }
}
