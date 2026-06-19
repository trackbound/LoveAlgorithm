using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowLoadingCommand, CompletionHandle, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using UnityEngine;
using UnityEngine.UI; // Image

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로딩 화면 뷰(*View: LoadingScene). <see cref="ShowLoadingCommand"/>를 구독해 풀스크린 오버레이를
    /// <see cref="ShowLoadingCommand.Seconds"/> 동안 표시 후 숨기고 핸들을 푼다(ADR-007: UI는 표시만).
    /// 내러티브 종료/도구 화면정리 시 즉시 숨김. 표시할 때마다 <see cref="splashFolder"/>(Resources)의
    /// 풀스크린 스플래시(1920×1080, 캔버스와 동일 비율)를 직전과 다른 것으로 무작위 교체한다 — 로딩 비트마다
    /// 캐릭터 일러스트가 바뀌어 보이도록(가챠/비주얼노벨 관용). 에셋 없으면 효과만 생략하고 동작 동일.
    /// </summary>
    public class LoadingScreenView : MonoBehaviour
    {
        [Tooltip("풀스크린 로딩 오버레이. 미바인딩 시 효과 생략·핸들만 완료.")]
        [SerializeField] GameObject overlay;
        [Tooltip("스플래시를 표시할 Image. 미바인딩 시 overlay의 Image를 자동 사용.")]
        [SerializeField] Image splashImage;
        [Tooltip("Resources 하위 스플래시 폴더(LoadAll<Sprite>). 비우면 일러스트 교체 생략.")]
        [SerializeField] string splashFolder = "UI/Loading";

        public GameObject Overlay { get => overlay; set => overlay = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        CompletionHandle _pending;

        Sprite[] _splashes;   // 지연 로드 + 캐시(LoadAll은 1회만).
        int _lastSplash = -1; // 직전 인덱스(연속 중복 회피용).

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
            ApplySplash(e.Key);
            overlay.SetActive(true);
            if (e.Seconds > 0f) yield return new WaitForSeconds(e.Seconds);
            overlay.SetActive(false);

            var h = _pending; _pending = null; _routine = null; h?.Complete();
        }

        /// <summary>
        /// 표시 직전 스플래시 일러스트를 교체. <paramref name="key"/>(캐릭터 폴더 id)가 있으면 이름에 그 키를
        /// 포함한 스플래시 중 무작위로, 없거나 매칭 0이면 전체에서 직전과 다른 무작위로 고른다. 에셋/이미지 없으면 무동작.
        /// </summary>
        void ApplySplash(string key)
        {
            if (string.IsNullOrEmpty(splashFolder)) return;
            var img = splashImage != null ? splashImage : overlay.GetComponent<Image>();
            if (img == null) return;

            if (_splashes == null) _splashes = Resources.LoadAll<Sprite>(splashFolder);
            if (_splashes == null || _splashes.Length == 0) return;

            // 컨텍스트 키: 이름 부분일치(대소문자 무시). 매칭이 있으면 그 안에서 무작위(연속회피는 전체 풀에만 적용).
            if (!string.IsNullOrEmpty(key))
            {
                var matches = Array.FindAll(_splashes, s => s != null && s.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
                if (matches.Length > 0)
                {
                    img.sprite = matches[UnityEngine.Random.Range(0, matches.Length)];
                    return;
                }
                // 매칭 0 → 전체 무작위 폴백(아래로).
            }

            int idx = UnityEngine.Random.Range(0, _splashes.Length);
            if (idx == _lastSplash && _splashes.Length > 1) idx = (idx + 1) % _splashes.Length;
            _lastSplash = idx;
            img.sprite = _splashes[idx];
        }

        void ResetView()
        {
            if (_routine != null) { StopCoroutine(_routine); _pending?.Complete(); _pending = null; _routine = null; }
            if (overlay != null) overlay.SetActive(false);
        }
    }
}
