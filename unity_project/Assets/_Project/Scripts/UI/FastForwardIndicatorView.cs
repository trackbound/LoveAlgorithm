using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // FastForwardChanged
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 시프트 홀드 빠른 진행 중임을 화면 우측 상단에 띄우는 인디케이터("FAST »" 알약).
    /// <see cref="DevToastView"/>(개발 전용, 릴리즈에서 제거됨)와 달리 이 파일은 무조건 컴파일된다 —
    /// 빌드본(릴리즈 포함)에서도 빠른 진행 중이면 보이게 하라는 요구사항.
    ///
    /// 씬/프리팹 배선 없이 <see cref="Bootstrap"/>(<c>RuntimeInitializeOnLoadMethod</c>)가 자체 오버레이 캔버스를
    /// 코드로 1회 생성(DontDestroyOnLoad, 최상위 정렬)해 어떤 화면·팝업 위에도 항상 보인다. GraphicRaycaster가
    /// 없어 입력을 막지 않는다(비차단). <see cref="FastForwardChanged"/>를 구독해 on=페이드 인 / off=페이드 아웃.
    /// 타임스케일 무관(언스케일드 시간).
    /// </summary>
    [DisallowMultipleComponent]
    public class FastForwardIndicatorView : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("[FastForwardIndicator]");
            DontDestroyOnLoad(go);
            go.AddComponent<FastForwardIndicatorView>();
        }

        const string Label = "FAST »"; // 알약 문구(이모티콘 기호 포함). 다른 글자/이모지로 바꾸려면 여기만 수정.
        const float Fade = 0.12f;

        CanvasGroup _group;
        IDisposable _sub;
        Coroutine _fadeRoutine;

        void Awake()
        {
            BuildOverlay();
            _sub = EventBus.Subscribe<FastForwardChanged>(OnChanged);
        }

        void OnDestroy()
        {
            _sub?.Dispose();
            _sub = null;
        }

        void OnChanged(FastForwardChanged e)
        {
            if (_group == null) return;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeTo(e.Active ? 1f : 0f));
        }

        // ── 오버레이 캔버스 + 우측 상단 알약 1개 생성(초기 숨김) ──
        void BuildOverlay()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32750; // 게임 내 모든 캔버스(최대 100)·눈꺼풀(95)보다 위, DevToast(32760)보다 아래

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            // GraphicRaycaster 없음 → 비차단 오버레이.

            // 알약 박스(우측 상단 고정).
            var boxGo = new GameObject("Pill", typeof(RectTransform));
            boxGo.transform.SetParent(transform, false);
            var boxRt = (RectTransform)boxGo.transform;
            boxRt.anchorMin = boxRt.anchorMax = new Vector2(1f, 1f);
            boxRt.pivot = new Vector2(1f, 1f);
            boxRt.anchoredPosition = new Vector2(-24f, -24f);

            var box = boxGo.AddComponent<Image>();
            box.color = new Color(0.16f, 0.60f, 0.30f, 0.94f); // 초록(진행 중)
            box.raycastTarget = false;

            _group = boxGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f; // 시작은 숨김
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var pad = boxGo.AddComponent<HorizontalLayoutGroup>();
            pad.padding = new RectOffset(20, 20, 8, 8);
            pad.childAlignment = TextAnchor.MiddleCenter;
            pad.childControlWidth = true;
            pad.childControlHeight = true;
            pad.childForceExpandWidth = false;
            pad.childForceExpandHeight = false;

            var fitter = boxGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 문구(흰 글씨) — 폰트 미지정 시 TMP 기본 폰트 사용.
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(boxGo.transform, false);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = Label;
            text.color = Color.white;
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }

        IEnumerator FadeTo(float target)
        {
            float from = _group.alpha;
            if (Mathf.Approximately(from, target)) { _group.alpha = target; yield break; }
            float t = 0f;
            while (t < Fade)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / Fade));
                yield return null;
            }
            _group.alpha = target;
        }
    }
}
