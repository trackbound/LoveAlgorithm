#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowDevToastCommand, DevToastSeverity
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 개발 디버그 토스트 오버레이(화면 우측 상단). 게임용 <see cref="ToastView"/>와 분리된 개발 전용 채널 —
    /// 파일 전체가 <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c>라 릴리즈(프로덕션) 빌드엔 컴파일조차 되지 않는다.
    ///
    /// 씬/프리팹 배선 없이 <see cref="Bootstrap"/>(<c>RuntimeInitializeOnLoadMethod</c>)가 자체 오버레이 캔버스를
    /// 코드로 1회 생성(DontDestroyOnLoad, 최상위 sortingOrder)해 어떤 화면·팝업·눈꺼풀 위에도 항상 보인다.
    /// <see cref="ShowDevToastCommand"/>를 구독해 심각도별 색 박스(Info=초록/Warn=노랑/Error=빨강)+흰 글씨를 띄우고
    /// 페이드 인 → 유지 → 페이드 아웃 후 제거한다. GraphicRaycaster가 없어 입력을 전혀 막지 않는다(비차단).
    /// 타임스케일 무관(언스케일드 시간) — 설정 팝업이 일시정지를 걸어도 정상 표시.
    /// </summary>
    [DisallowMultipleComponent]
    public class DevToastView : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("[DevToastOverlay]");
            DontDestroyOnLoad(go);
            go.AddComponent<DevToastView>();
        }

        const int MaxEntries = 6;          // 동시 표시 상한(초과 시 가장 오래된 것부터 제거)
        const float FadeIn = 0.15f;
        const float FadeOut = 0.5f;
        const float DefaultHold = 2.5f;
        const float MaxTextWidth = 560f;   // 본문 줄바꿈 기준 폭(px, 기준해상도)

        RectTransform _stack;
        IDisposable _sub;
        readonly List<Entry> _entries = new();

        class Entry
        {
            public GameObject go;
            public CanvasGroup group;
            public Coroutine routine;
        }

        void Awake()
        {
            BuildOverlay();
            _sub = EventBus.Subscribe<ShowDevToastCommand>(OnShow);
        }

        void OnDestroy()
        {
            _sub?.Dispose();
            _sub = null;
        }

        // ── 오버레이 캔버스 + 우측 상단 세로 스택 컨테이너 생성 ──
        void BuildOverlay()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760; // 게임 내 모든 캔버스(최대 100)·눈꺼풀(95)보다 확실히 위

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); // 게임 캔버스와 동일
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // 너비 기준(게임 캔버스와 동일)
            // GraphicRaycaster 없음 → 입력 차단 안 함(비차단 오버레이)

            var stackGo = new GameObject("Stack", typeof(RectTransform));
            _stack = (RectTransform)stackGo.transform;
            _stack.SetParent(transform, false);
            _stack.anchorMin = _stack.anchorMax = new Vector2(1f, 1f); // 우측 상단 고정
            _stack.pivot = new Vector2(1f, 1f);
            _stack.anchoredPosition = new Vector2(-16f, -16f);

            var layout = stackGo.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = stackGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void OnShow(ShowDevToastCommand e)
        {
            if (_stack == null) return;
            var entry = BuildEntry(e.Message ?? "", e.Severity);
            _entries.Add(entry);

            // 상한 초과 시 가장 오래된 항목 즉시 제거.
            while (_entries.Count > MaxEntries)
                RemoveEntry(_entries[0]);

            float hold = e.Duration > 0f ? e.Duration : DefaultHold;
            entry.routine = StartCoroutine(PlayAndDispose(entry, hold));
        }

        Entry BuildEntry(string message, DevToastSeverity severity)
        {
            // 박스
            var boxGo = new GameObject($"DevToast_{severity}", typeof(RectTransform));
            boxGo.transform.SetParent(_stack, false);
            boxGo.transform.SetAsFirstSibling(); // 최신이 위로

            var box = boxGo.AddComponent<Image>();
            box.color = ColorFor(severity);
            box.raycastTarget = false;

            var group = boxGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var pad = boxGo.AddComponent<HorizontalLayoutGroup>();
            pad.padding = new RectOffset(14, 14, 8, 8);
            pad.childAlignment = TextAnchor.MiddleLeft;
            pad.childControlWidth = true;
            pad.childControlHeight = true;
            pad.childForceExpandWidth = false;
            pad.childForceExpandHeight = false;

            var boxFitter = boxGo.AddComponent<ContentSizeFitter>();
            boxFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            boxFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 본문(흰 글씨) — 폰트 미지정 시 TMP 기본 폰트(Aggro-Medium SDF, 한글 지원) 사용.
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(boxGo.transform, false);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = message;
            text.color = Color.white;
            text.fontSize = 26f;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;

            var le = textGo.AddComponent<LayoutElement>();
            le.preferredWidth = MaxTextWidth; // 과도하게 긴 메시지는 이 폭에서 줄바꿈

            return new Entry { go = boxGo, group = group };
        }

        IEnumerator PlayAndDispose(Entry entry, float hold)
        {
            yield return Fade(entry.group, 0f, 1f, FadeIn);
            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(entry.group, 1f, 0f, FadeOut);
            RemoveEntry(entry);
        }

        static IEnumerator Fade(CanvasGroup g, float from, float to, float dur)
        {
            if (g == null) yield break;
            g.alpha = from;
            if (dur <= 0f) { g.alpha = to; yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            g.alpha = to;
        }

        void RemoveEntry(Entry entry)
        {
            if (entry == null) return;
            _entries.Remove(entry);
            if (entry.routine != null) StopCoroutine(entry.routine);
            if (entry.go != null) Destroy(entry.go);
        }

        static Color ColorFor(DevToastSeverity severity)
        {
            switch (severity)
            {
                case DevToastSeverity.Warn:  return new Color(0.85f, 0.62f, 0.10f, 0.94f); // 노랑(앰버)
                case DevToastSeverity.Error: return new Color(0.80f, 0.18f, 0.18f, 0.94f); // 빨강
                default:                     return new Color(0.16f, 0.60f, 0.30f, 0.94f); // 초록(Info)
            }
        }
    }
}
#endif
