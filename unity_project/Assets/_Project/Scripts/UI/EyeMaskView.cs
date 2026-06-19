using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // EyeMaskCommand, EyeMaskAction, CompletionHandle, NarrativeFinishedEvent
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 아이마스크 뷰(*View, M3 슬라이스2: 눈감김/뜨기). <see cref="EyeMaskCommand"/>를 구독해 상/하 검은 바를 눈꺼풀처럼
    /// 보간하고 완료 핸들을 푼다(ADR-007: UI는 표시만). DOTween 미사용. 닫히면 두 바가 만나 전체 암전(POV 눈감김).
    ///
    /// 셔터(대칭 레터박스)가 아니라 눈꺼풀처럼 보이도록 두 가지를 비대칭으로 둔다:
    ///  1) 공간: 윗꺼풀이 더 내려와 화면 하단(<see cref="lidSplit"/> 비율) 지점에서 만난다. 같은 시간에 더 먼 거리를
    ///     이동하므로 윗바가 자연히 더 빨리 움직인다(실제 윗눈꺼풀 우세).
    ///  2) 시간: 감김은 가속(ease-in, '탁' 닫힘), 뜨기는 감속(ease-out, 스르륵 풀림)으로 방향마다 다른 이징.
    /// 닫힘 정도 t(0=뜸,1=감김) 단일 보간. 바 지오메트리는 부모(캔버스) 높이에서 자동 설정.
    /// 내러티브 종료 시 즉시 뜨기(잔여 암전 방지).
    /// </summary>
    public class EyeMaskView : MonoBehaviour
    {
        [Tooltip("상단 검은 바(전체 너비). 미바인딩 시 효과 생략·핸들만 완료.")]
        [SerializeField] RectTransform topBar;
        [Tooltip("하단 검은 바(전체 너비).")]
        [SerializeField] RectTransform bottomBar;

        [Header("눈꺼풀 비대칭 (셔터 → 눈꺼풀)")]
        [Tooltip("화면 위에서부터 두 바가 만나는 높이 비율. 0.5=정중앙(대칭 셔터), 높일수록 윗꺼풀이 더 내려와 눈꺼풀다움.")]
        [SerializeField, Range(0.5f, 0.85f)] float lidSplit = 0.66f;
        [Tooltip("감김 가속도(ease-in 지수). 1=선형, 클수록 천천히 시작해 끝에서 '탁' 닫힘.")]
        [SerializeField, Range(1f, 3f)] float closeEase = 1.4f;
        [Tooltip("뜨기 감속도(ease-out 지수). 1=선형, 클수록 빠르게 열리다 끝에서 스르륵 멈춤.")]
        [SerializeField, Range(1f, 3f)] float openEase = 1.7f;

        public RectTransform TopBar { get => topBar; set => topBar = value; }
        public RectTransform BottomBar { get => bottomBar; set => bottomBar = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        CompletionHandle _pending;
        float _topHeight, _botHeight;
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

            // 윗바는 lidSplit만큼, 아랫바는 나머지를 덮는다(둘이 만나 전체 암전). 윗바가 더 길어 더 멀리·빨리 내려온다.
            _topHeight = h * lidSplit;
            _botHeight = h * (1f - lidSplit);

            ConfigureBar(topBar, isTop: true, height: _topHeight);
            ConfigureBar(bottomBar, isTop: false, height: _botHeight);

            // 부모(캔버스) 높이가 확정됐을 때만 캐시 — OnEnable 폴백값이 굳지 않도록.
            if (parent != null && parent.rect.height > 0f) _geometryReady = true;
        }

        // 상단 바: 상단 가로 스트레치, pivot 위(0.5,1) — 아래로 height만큼 늘어져 윗부분을 덮는다.
        // 하단 바: 하단 가로 스트레치, pivot 아래(0.5,0) — 위로 늘어져 아랫부분을 덮는다. 이음새 여백(+2) 포함.
        void ConfigureBar(RectTransform bar, bool isTop, float height)
        {
            float y = isTop ? 1f : 0f;
            bar.anchorMin = new Vector2(0f, y);
            bar.anchorMax = new Vector2(1f, y);
            bar.pivot = new Vector2(0.5f, y);
            bar.sizeDelta = new Vector2(0f, height + 2f); // +2 = 중앙 이음새 방지
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
                    yield return Drive(e.CloseDuration, CloseAmount);
                    break;

                case EyeMaskAction.Open:
                    yield return Drive(e.OpenDuration, OpenAmount);
                    SetBarsActive(false);
                    break;

                case EyeMaskAction.Blink:
                    yield return Drive(e.CloseDuration, CloseAmount);
                    if (e.HoldDuration > 0f) yield return new WaitForSeconds(e.HoldDuration);
                    yield return Drive(e.OpenDuration, OpenAmount);
                    SetBarsActive(false);
                    break;
            }
            Finish();
        }

        // 감김: 진행도 x를 가속 곡선으로 매핑 → 0(뜸)에서 1(감김)로, 끝에서 '탁'.
        float CloseAmount(float x) => Mathf.Pow(Mathf.Clamp01(x), closeEase);
        // 뜨기: 1(감김)에서 0(뜸)으로, 처음 빠르게 풀리다 끝에서 스르륵 감속.
        float OpenAmount(float x) => Mathf.Pow(1f - Mathf.Clamp01(x), openEase);

        IEnumerator Drive(float duration, Func<float, float> amountOf)
        {
            if (duration <= 0f) { ApplyCloseAmount(amountOf(1f)); yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                ApplyCloseAmount(amountOf(t / duration));
                yield return null;
            }
            ApplyCloseAmount(amountOf(1f));
        }

        // closeAmount: 0=완전히 뜸(바가 화면 밖), 1=감김(합류). 윗바/아랫바 이동량이 달라 비대칭으로 만난다.
        void ApplyCloseAmount(float t)
        {
            if (topBar == null || bottomBar == null) return;
            topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(_topHeight, 0f, t));
            bottomBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(-_botHeight, 0f, t));
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
