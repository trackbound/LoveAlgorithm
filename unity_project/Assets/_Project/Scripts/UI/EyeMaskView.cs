using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // EyeMaskCommand, EyeMaskAction, CompletionHandle, NarrativeFinishedEvent
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 아이마스크 뷰(*View, M3 슬라이스2: 눈감김/뜨기). <see cref="EyeMaskCommand"/>를 구독해 상/하 곡선 눈꺼풀
    /// 스프라이트(eyelid_top/eyelid_bottom)를 보간하고 완료 핸들을 푼다(ADR-007: UI는 표시만). DOTween 미사용.
    ///
    /// 직선 셔터가 아니라 진짜 눈처럼 보이도록:
    ///  1) 모양: 각 바는 화면 높이만큼 풀스크린 눈꺼풀이며, 맞닿는 가장자리가 중앙이 깊게 내려온 부드러운 아치(스프라이트).
    ///     반쯤 닫히면 가운데가 아몬드형(렌즈)으로 열려 눈처럼 보이고, 좌우 눈꼬리가 가장 늦게 닫힌다.
    ///  2) 시간: 감김은 가속(ease-in, '탁' 닫힘), 뜨기는 감속(ease-out, 스르륵 풀림)으로 방향마다 다른 이징.
    /// 닫힘 정도 t(0=뜸,1=감김) 단일 보간. 두 눈꺼풀이 화면 높이만큼 풀스크린이라 닫히면 좌우 끝까지 겹쳐 전체 암전.
    /// 바 지오메트리는 부모(캔버스) 높이에서 자동 설정. 내러티브 종료 시 즉시 뜨기(잔여 암전 방지).
    /// </summary>
    public class EyeMaskView : MonoBehaviour
    {
        [Tooltip("상단 검은 바(전체 너비). 미바인딩 시 효과 생략·핸들만 완료.")]
        [SerializeField] RectTransform topBar;
        [Tooltip("하단 검은 바(전체 너비).")]
        [SerializeField] RectTransform bottomBar;

        [Header("눈꺼풀 곡선/이징")]
        [Tooltip("닫힘 시 좌우 눈꼬리(곡선이 얕은 양끝)까지 확실히 겹치도록 바 높이에 더하는 여유 비율(화면 높이 대비).")]
        [SerializeField, Range(0f, 0.2f)] float lidOverlap = 0.06f;
        [Tooltip("감김 가속도(ease-in 지수). 1=선형, 클수록 천천히 시작해 끝에서 '탁' 닫힘.")]
        [SerializeField, Range(1f, 3f)] float closeEase = 1.4f;
        [Tooltip("뜨기 감속도(ease-out 지수). 1=선형, 클수록 빠르게 열리다 끝에서 스르륵 멈춤.")]
        [SerializeField, Range(1f, 3f)] float openEase = 1.7f;
        [Tooltip("상안검 주도 정도. 1=상하 대칭(조리개식 축소), 클수록 윗눈꺼풀이 먼저·더 많이 내려오고 아랫눈꺼풀이 늦게 따라와 '눈꺼풀이 닫히는' 느낌. 두 바의 최종 위치는 불변이라 완전 암전은 유지.")]
        [SerializeField, Range(1f, 3f)] float upperLidLead = 1.9f;

        public RectTransform TopBar { get => topBar; set => topBar = value; }
        public RectTransform BottomBar { get => bottomBar; set => bottomBar = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        CompletionHandle _pending;
        float _barHeight;
        bool _geometryReady;
        bool _shroudActive; // 눈꺼풀 바가 화면을 덮는 중인가(차폐 상태 이벤트 중복 발행 방지).

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

            // 두 눈꺼풀 모두 화면 높이(+여유)만큼 풀스크린. 닫히면 화면 전체를 덮어 좌우 끝까지 겹쳐 암전한다.
            _barHeight = h * (1f + lidOverlap);

            ConfigureBar(topBar, isTop: true, height: _barHeight);
            ConfigureBar(bottomBar, isTop: false, height: _barHeight);

            // 부모(캔버스) 높이가 확정됐을 때만 캐시 — OnEnable 폴백값이 굳지 않도록.
            if (parent != null && parent.rect.height > 0f) _geometryReady = true;
        }

        // 상단 바: 상단 가로 스트레치, pivot 위(0.5,1) — 아래로 height만큼 늘어진다(곡선 아래 가장자리가 화면을 덮음).
        // 하단 바: 하단 가로 스트레치, pivot 아래(0.5,0) — 위로 늘어진다. 두 바 모두 화면 높이만큼이라 닫히면 겹쳐 암전.
        void ConfigureBar(RectTransform bar, bool isTop, float height)
        {
            float y = isTop ? 1f : 0f;
            bar.anchorMin = new Vector2(0f, y);
            bar.anchorMax = new Vector2(1f, y);
            bar.pivot = new Vector2(0.5f, y);
            bar.sizeDelta = new Vector2(0f, height);
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

        // closeAmount: 0=완전히 뜸(두 눈꺼풀이 화면 밖), 1=감김(둘 다 화면을 덮어 곡선 가장자리가 중앙에서 합류).
        // 상하 비대칭: 같은 t에서 윗눈꺼풀(tTop)이 더 닫히고 아랫눈꺼풀(tBot)이 덜 닫혀 윗눈꺼풀이 동작을 주도한다.
        // 양끝(t=0,1)에선 tTop=tBot=t라 열림/완전 암전 위치는 대칭과 동일 — 닫힘 보장 불변.
        void ApplyCloseAmount(float t)
        {
            if (topBar == null || bottomBar == null) return;
            t = Mathf.Clamp01(t);
            float p = Mathf.Max(1f, upperLidLead);
            float tTop = Mathf.Pow(t, 1f / p); // 상안검 선행
            float tBot = Mathf.Pow(t, p);      // 하안검 후행
            topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(_barHeight, 0f, tTop));
            bottomBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(-_barHeight, 0f, tBot));
        }

        void SetBarsActive(bool active)
        {
            if (topBar != null) topBar.gameObject.SetActive(active);
            if (bottomBar != null) bottomBar.gameObject.SetActive(active);
            // 차폐 상태 변화만 1회 발행 — 대사창이 이 동안만 정렬을 눈꺼풀 위로 올린다(평상시 모달/팝업 아래 유지).
            if (active != _shroudActive)
            {
                _shroudActive = active;
                EventBus.Publish(new EyeMaskShroudChanged(active));
            }
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
