using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // EyeMaskCommand, EyeMaskAction, CompletionHandle, NarrativeFinishedEvent
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 아이마스크 뷰(*View, M3 슬라이스2: 눈감김/뜨기). <see cref="EyeMaskCommand"/>를 구독해 상/하 검은 바를 눈꺼풀처럼
    /// 보간(SmoothStep)하고 완료 핸들을 푼다(ADR-007: UI는 표시만). 슬라이스1처럼 DOTween 미사용. 닫히면 두 바가
    /// 화면 중앙에서 만나 전체 암전(POV 눈감김). 일반 VN 표준의 단순 대칭 슬라이드 — 닫힘 정도 t(0=뜸,1=감김) 단일 보간.
    ///
    /// 바 지오메트리는 부모(캔버스) 높이에서 자동 설정: 상단 바=상단 스트레치(아래로 늘어짐), 하단 바=하단 스트레치(위로).
    /// 내러티브 종료 시 즉시 뜨기(잔여 암전 방지).
    /// </summary>
    public class EyeMaskView : MonoBehaviour
    {
        [Tooltip("상단 검은 바(전체 너비). 미바인딩 시 효과 생략·핸들만 완료.")]
        [SerializeField] RectTransform topBar;
        [Tooltip("하단 검은 바(전체 너비).")]
        [SerializeField] RectTransform bottomBar;

        public RectTransform TopBar { get => topBar; set => topBar = value; }
        public RectTransform BottomBar { get => bottomBar; set => bottomBar = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        CompletionHandle _pending;
        float _halfHeight;
        bool _geometryReady;

        void OnEnable()
        {
            _sub = EventBus.Subscribe<EyeMaskCommand>(OnCommand);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => OpenImmediate());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => OpenImmediate()); // 도구 화면 정리
            EnsureGeometry();
            ApplyCloseAmount(0f); // 시작은 뜬 상태.
            SetBarsActive(false);
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
        }

        void EnsureGeometry()
        {
            if (_geometryReady || topBar == null || bottomBar == null) return;
            var parent = topBar.parent as RectTransform;
            float h = parent != null ? parent.rect.height : 0f;
            if (h <= 0f) h = Screen.height; // 캔버스 레이아웃 전(OnEnable)이면 폴백, 캐시하지 않고 명령 시점에 재계산.
            _halfHeight = h * 0.5f;

            ConfigureBar(topBar, isTop: true);
            ConfigureBar(bottomBar, isTop: false);

            // 부모(캔버스) 높이가 확정됐을 때만 캐시 — OnEnable 폴백값이 굳지 않도록.
            if (parent != null && parent.rect.height > 0f) _geometryReady = true;
        }

        // 상단 바: 상단 가로 스트레치, pivot 위(0.5,1) — 아래로 halfHeight만큼 늘어져 윗절반을 덮는다.
        // 하단 바: 하단 가로 스트레치, pivot 아래(0.5,0) — 위로 늘어져 아랫절반을 덮는다. 여유분(+여백) 포함.
        void ConfigureBar(RectTransform bar, bool isTop)
        {
            float y = isTop ? 1f : 0f;
            bar.anchorMin = new Vector2(0f, y);
            bar.anchorMax = new Vector2(1f, y);
            bar.pivot = new Vector2(0.5f, y);
            bar.sizeDelta = new Vector2(0f, _halfHeight + 2f); // +2 = 중앙 이음새 방지
        }

        void OnCommand(EyeMaskCommand e)
        {
            if (topBar == null || bottomBar == null) { e.Handle?.Complete(); return; }
            EnsureGeometry();

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _pending?.Complete(); // 끊긴 이전 핸들이 엔진을 막지 않도록.
            }
            _pending = e.Handle;

            if (e.Action == EyeMaskAction.CloseImmediate)
            {
                SetBarsActive(true);
                ApplyCloseAmount(1f);
                _routine = null;
                Finish();
                return;
            }
            _routine = StartCoroutine(Run(e));
        }

        IEnumerator Run(EyeMaskCommand e)
        {
            SetBarsActive(true);
            switch (e.Action)
            {
                case EyeMaskAction.Close:
                    yield return Tween(0f, 1f, e.CloseDuration);
                    break;

                case EyeMaskAction.Open:
                    yield return Tween(1f, 0f, e.OpenDuration);
                    SetBarsActive(false);
                    break;

                case EyeMaskAction.Blink:
                    yield return Tween(0f, 1f, e.CloseDuration);
                    if (e.HoldDuration > 0f) yield return new WaitForSeconds(e.HoldDuration);
                    yield return Tween(1f, 0f, e.OpenDuration);
                    SetBarsActive(false);
                    break;
            }
            Finish();
        }

        IEnumerator Tween(float from, float to, float duration)
        {
            if (duration <= 0f) { ApplyCloseAmount(to); yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                ApplyCloseAmount(Mathf.SmoothStep(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            ApplyCloseAmount(to);
        }

        // closeAmount: 0=완전히 뜸(바가 화면 밖), 1=감김(중앙 합류).
        void ApplyCloseAmount(float t)
        {
            if (topBar == null || bottomBar == null) return;
            topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(_halfHeight, 0f, t));
            bottomBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(-_halfHeight, 0f, t));
        }

        void SetBarsActive(bool active)
        {
            if (topBar != null) topBar.gameObject.SetActive(active);
            if (bottomBar != null) bottomBar.gameObject.SetActive(active);
        }

        void Finish()
        {
            var h = _pending;
            _pending = null;
            _routine = null;
            h?.Complete();
        }

        /// <summary>내러티브 종료 시 즉시 뜨기(잔여 암전이 시뮬레이션으로 새지 않도록).</summary>
        void OpenImmediate()
        {
            if (_routine != null) { StopCoroutine(_routine); _pending?.Complete(); _pending = null; _routine = null; }
            ApplyCloseAmount(0f);
            SetBarsActive(false);
        }
    }
}
