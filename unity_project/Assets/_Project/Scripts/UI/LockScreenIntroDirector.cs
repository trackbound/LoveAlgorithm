using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 잠금화면 진입 연출 오케스트레이터(*View: LockScreen). 영상(03_login) 플로우대로
    /// ① 위젯 hold → ② 위젯별 가까운 화면 밖으로 슬라이드아웃(ease-in) → ③ Dim 0→target 페이드
    /// → ④ 입력/버튼 그룹 0→1 reveal → ⑤ onInputReady 콜백. 코루틴 lerp(DOTween 미사용).
    /// LockScreenView가 위임하며, 미바인딩 시 view가 즉시 경로로 폴백한다. 수치는 인스펙터 노출(ADR-012).
    /// </summary>
    public class LockScreenIntroDirector : MonoBehaviour
    {
        [Serializable]
        public struct SlideWidget
        {
            public RectTransform target;
            [Tooltip("기준 위치에서 화면 밖으로의 이동량(px). 가까운 가장자리 방향.")]
            public Vector2 exitOffset;
        }

        [SerializeField] List<SlideWidget> widgets = new();
        [Tooltip("딤 오버레이 Image(검은 반투명). alpha 0→target.")]
        [SerializeField] Image dim;
        [Tooltip("입력+버튼+가이드 묶음 CanvasGroup. alpha 0→1.")]
        [SerializeField] CanvasGroup inputGroup;

        [Header("Timing")]
        [SerializeField] float introHold = 0.6f;
        [SerializeField] float slideDuration = 0.35f;
        [SerializeField] float dimFade = 0.3f;
        [SerializeField] float inputReveal = 0.25f;
        [SerializeField] float dimTargetAlpha = 0.58f;
        [Tooltip("슬라이드아웃 이징(가속해서 빠져나감).")]
        [SerializeField] AnimationCurve slideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public Image Dim { get => dim; set => dim = value; }
        public CanvasGroup InputGroup { get => inputGroup; set => inputGroup = value; }
        public float DimTargetAlpha { get => dimTargetAlpha; set => dimTargetAlpha = value; }
        public bool IsPlaying { get; private set; }

        readonly List<Vector2> _basePos = new();
        Coroutine _co;

        /// <summary>테스트/부팅용 타이밍 일괄 주입.</summary>
        public void SetTimings(float hold, float slide, float dimF, float reveal)
        {
            introHold = hold; slideDuration = slide; dimFade = dimF; inputReveal = reveal;
        }

        /// <summary>테스트/부팅용 위젯 리스트 주입.</summary>
        public void SetWidgets(IEnumerable<(RectTransform rt, Vector2 exit)> items)
        {
            widgets.Clear();
            foreach (var (rt, exit) in items)
                widgets.Add(new SlideWidget { target = rt, exitOffset = exit });
        }

        void CacheBase()
        {
            _basePos.Clear();
            for (int i = 0; i < widgets.Count; i++)
                _basePos.Add(widgets[i].target != null ? widgets[i].target.anchoredPosition : Vector2.zero);
        }

        /// <summary>시작 상태로 복원 — 위젯 기준 위치, dim 0, 입력 그룹 0. Play 전 호출.</summary>
        public void ResetToStart()
        {
            if (_basePos.Count != widgets.Count) CacheBase();
            for (int i = 0; i < widgets.Count; i++)
                if (widgets[i].target != null) widgets[i].target.anchoredPosition = _basePos[i];
            if (dim != null) { var c = dim.color; c.a = 0f; dim.color = c; }
            if (inputGroup != null) inputGroup.alpha = 0f;
        }

        public void Play(Action onInputReady)
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Run(onInputReady));
        }

        IEnumerator Run(Action onInputReady)
        {
            IsPlaying = true;
            CacheBase();

            if (introHold > 0f) yield return new WaitForSeconds(introHold);

            // ② 위젯 슬라이드아웃(동시)
            float t = 0f;
            while (t < slideDuration)
            {
                t += Time.deltaTime;
                float k = slideDuration > 0f ? slideEase.Evaluate(Mathf.Clamp01(t / slideDuration)) : 1f;
                for (int i = 0; i < widgets.Count; i++)
                    if (widgets[i].target != null)
                        widgets[i].target.anchoredPosition = _basePos[i] + widgets[i].exitOffset * k;
                yield return null;
            }
            for (int i = 0; i < widgets.Count; i++)
                if (widgets[i].target != null)
                    widgets[i].target.anchoredPosition = _basePos[i] + widgets[i].exitOffset;

            // ③ Dim 페이드
            yield return Fade(a => { if (dim != null) { var c = dim.color; c.a = a; dim.color = c; } },
                              0f, dimTargetAlpha, dimFade);

            // ④ 입력 그룹 reveal
            yield return Fade(a => { if (inputGroup != null) inputGroup.alpha = a; },
                              0f, 1f, inputReveal);

            IsPlaying = false;
            _co = null;
            onInputReady?.Invoke();
        }

        IEnumerator Fade(Action<float> set, float from, float to, float dur)
        {
            if (dur <= 0f) { set(to); yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                set(Mathf.Lerp(from, to, Mathf.Clamp01(t / dur)));
                yield return null;
            }
            set(to);
        }
    }
}
