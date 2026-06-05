using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowLoadingCommand, CompletionHandle, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로딩 화면 뷰(*View: LoadingScene). <see cref="ShowLoadingCommand"/>를 구독해 풀스크린 오버레이를
    /// <see cref="ShowLoadingCommand.Seconds"/> 동안 표시 후 숨기고 핸들을 푼다(ADR-007: UI는 표시만).
    /// 내러티브 종료/도구 화면정리 시 즉시 숨김. 스타일(스피너/문구)은 오버레이 자식으로 감독 튜닝.
    /// </summary>
    public class LoadingScreenView : MonoBehaviour
    {
        [Tooltip("풀스크린 로딩 오버레이. 미바인딩 시 효과 생략·핸들만 완료.")]
        [SerializeField] GameObject overlay;

        public GameObject Overlay { get => overlay; set => overlay = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        CompletionHandle _pending;

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ShowLoadingCommand>(OnShow);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetView());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetView()); // 도구 화면 정리
            if (overlay != null) overlay.SetActive(false);
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
        }

        void OnShow(ShowLoadingCommand e)
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

        IEnumerator Run(ShowLoadingCommand e)
        {
            overlay.SetActive(true);
            if (e.Seconds > 0f) yield return new WaitForSeconds(e.Seconds);
            overlay.SetActive(false);

            var h = _pending; _pending = null; _routine = null; h?.Complete();
        }

        void ResetView()
        {
            if (_routine != null) { StopCoroutine(_routine); _pending?.Complete(); _pending = null; _routine = null; }
            if (overlay != null) overlay.SetActive(false);
        }
    }
}
