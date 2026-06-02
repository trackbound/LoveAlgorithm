using System;
using System.Collections;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Events; // ShowScreenFxCommand, ScreenFxKind, FxRequest, NarrativeFinishedEvent
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 스크린 오버레이 FX 뷰(*View, M3 슬라이스2: FadeOut/FadeIn/Flash). <see cref="ShowScreenFxCommand"/>를
    /// 구독해 전체화면 오버레이 Image의 알파를 코루틴 lerp하고, 완료 시 핸들(<see cref="FxRequest"/>)을 푼다
    /// (ADR-007: UI는 표시만). 엔진(NarrativeController)은 이 뷰를 직접 알지 못한다 — 명령 + 핸들로만 연결.
    /// 대사 UI까지 덮어야 하므로 최상위 캔버스(_ScreenFx, 높은 sortingOrder)에 부착. 슬라이스1처럼 DOTween 미사용.
    ///
    /// FadeOut=검정 0→1(유지), FadeIn=검정 1→0(해제), Flash=흰색 0→1→0. 내러티브 종료 시 잔여 암전 리셋.
    /// </summary>
    public class ScreenFxView : MonoBehaviour
    {
        [Tooltip("전체화면 오버레이 Image(검정/흰색을 알파로 연출). 미바인딩 시 효과 생략·핸들만 완료.")]
        [SerializeField] Image overlay;
        [SerializeField] Color fadeColor = Color.black;
        [SerializeField] Color flashColor = Color.white;

        public Image Overlay { get => overlay; set => overlay = value; }

        IDisposable _sub, _finishSub;
        Coroutine _routine;
        FxRequest _pending;

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ShowScreenFxCommand>(OnShow);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetOverlay());
            if (overlay != null)
            {
                overlay.raycastTarget = false; // 투명 오버레이가 클릭을 먹지 않도록.
                SetOverlay(fadeColor, 0f);
                overlay.enabled = false;
            }
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose();
            _sub = _finishSub = null;
        }

        void OnShow(ShowScreenFxCommand e)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _pending?.Complete(); // 끊긴 이전 핸들이 엔진을 막지 않도록.
            }
            _pending = e.Handle;
            _routine = StartCoroutine(Run(e));
        }

        IEnumerator Run(ShowScreenFxCommand e)
        {
            if (overlay == null)
            {
                Finish();
                yield break;
            }

            switch (e.Kind)
            {
                case ScreenFxKind.FadeOut:
                    SetOverlay(fadeColor, overlay.color.a);
                    overlay.enabled = true;
                    yield return FadeAlpha(overlay.color.a, 1f, e.Duration);
                    break;

                case ScreenFxKind.FadeIn:
                    SetOverlay(fadeColor, overlay.color.a);
                    overlay.enabled = true;
                    yield return FadeAlpha(overlay.color.a, 0f, e.Duration);
                    overlay.enabled = false;
                    break;

                case ScreenFxKind.Flash:
                    SetOverlay(flashColor, 0f);
                    overlay.enabled = true;
                    float half = e.Duration * 0.5f;
                    yield return FadeAlpha(0f, 1f, half);
                    yield return FadeAlpha(1f, 0f, half);
                    overlay.enabled = false;
                    break;
            }

            Finish();
        }

        void Finish()
        {
            var h = _pending;
            _pending = null;
            _routine = null;
            h?.Complete();
        }

        /// <summary>내러티브 종료 시 잔여 암전(FadeOut 검정)이 시뮬레이션으로 새지 않도록 즉시 해제.</summary>
        void ResetOverlay()
        {
            if (_routine != null) { StopCoroutine(_routine); _pending?.Complete(); _pending = null; _routine = null; }
            if (overlay != null) { SetOverlay(fadeColor, 0f); overlay.enabled = false; }
        }

        IEnumerator FadeAlpha(float from, float to, float duration)
        {
            if (overlay == null) yield break;
            if (duration <= 0f) { SetAlpha(to); yield break; }

            float t = 0f;
            SetAlpha(from);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAlpha(to);
        }

        void SetOverlay(Color rgb, float a)
        {
            if (overlay != null) overlay.color = new Color(rgb.r, rgb.g, rgb.b, a);
        }

        void SetAlpha(float a)
        {
            if (overlay == null) return;
            var c = overlay.color;
            c.a = a;
            overlay.color = c;
        }
    }
}
