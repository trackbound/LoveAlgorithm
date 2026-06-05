using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowPlaceCommand, CompletionHandle, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 위치 배너 뷰(*View: Place). <see cref="ShowPlaceCommand"/>를 구독해 제목/장소를 채우고 CanvasGroup 알파를
    /// 코루틴으로 등장(0→1)→유지→퇴장(1→0) 페이드한다(ADR-007: UI는 표시만, DOTween 미사용). 등장 완료 시 핸들을
    /// 풀어(await 대비) 스크립트가 비블로킹으로 진행하게 한다. 내러티브 종료/도구 화면정리 시 즉시 숨김.
    /// </summary>
    public class PlaceCardView : MonoBehaviour
    {
        [Tooltip("배너 루트 CanvasGroup(페이드). 미바인딩 시 효과 생략·핸들만 완료.")]
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text placeText;

        public CanvasGroup Group { get => group; set => group = value; }
        public TMP_Text TitleText { get => titleText; set => titleText = value; }
        public TMP_Text PlaceText { get => placeText; set => placeText = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        CompletionHandle _pending;

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ShowPlaceCommand>(OnShow);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetBanner());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetBanner()); // 도구 화면 정리
            if (group != null) group.alpha = 0f;
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
        }

        void OnShow(ShowPlaceCommand e)
        {
            if (group == null) { e.Handle?.Complete(); return; }

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _pending?.Complete(); // 끊긴 이전 핸들이 엔진을 막지 않도록.
            }
            if (titleText != null) titleText.text = e.Title ?? "";
            if (placeText != null) placeText.text = e.Place ?? "";
            _pending = e.Handle;
            _routine = StartCoroutine(Run(e));
        }

        IEnumerator Run(ShowPlaceCommand e)
        {
            yield return Fade(0f, 1f, e.EnterDuration);

            // 등장 완료 = 핸들 완료(스크립트는 여기까지만 await; 유지·퇴장은 비블로킹).
            var h = _pending; _pending = null; h?.Complete();

            if (e.HoldDuration > 0f) yield return new WaitForSeconds(e.HoldDuration);
            yield return Fade(1f, 0f, e.ExitDuration);
            _routine = null;
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            if (dur <= 0f) { group.alpha = to; yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            group.alpha = to;
        }

        void ResetBanner()
        {
            if (_routine != null) { StopCoroutine(_routine); _pending?.Complete(); _pending = null; _routine = null; }
            if (group != null) group.alpha = 0f;
        }
    }
}
