using System.Collections.Generic;
using DG.Tweening;
using LoveAlgo.Common;
using LoveAlgo.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Modules.Stats
{
    /// <summary>
    /// 스탯 변화 시 +N/−N floating text를 띄우는 알리미 (Phase D6).
    /// StatChangedEvent 구독 → 풀에서 GameObject 빌려와 위로 떠오르며 페이드아웃.
    /// 풀 재사용으로 GC 알로케이션 최소화.
    ///
    /// AfterSceneLoad 자동 부트스트랩. Headless 환경에선 부트스트랩 스킵.
    /// TMP_Settings.defaultFontAsset가 null이면 visual 없이 no-op (이벤트 무시).
    /// 스타일/색 = Resources/Data/StatFloatingTextStyle.asset (없으면 코드 폴백).
    /// </summary>
    public class StatFloatingTextNotifier : MonoBehaviour
    {
        const string StyleResourcePath = "Data/StatFloatingTextStyle";
        const int InitialPoolSize = 4;
        const int ReferenceWidth = 1920;
        const int ReferenceHeight = 1080;

        StatFloatingTextStyleSO _style;
        TMP_FontAsset _font;
        Canvas _canvas;
        RectTransform _canvasRect;
        readonly List<FloatingTextItem> _pool = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Headless.IsEnabled) return;

            var go = new GameObject("[StatFloatingTextNotifier]");
            DontDestroyOnLoad(go);
            go.AddComponent<StatFloatingTextNotifier>();
        }

        void Awake()
        {
            _style = Resources.Load<StatFloatingTextStyleSO>(StyleResourcePath);
            if (_style == null)
            {
                // 폴백: 코드 기본값으로 SO 인스턴스 임시 생성 (디스크 저장 안 함)
                _style = ScriptableObject.CreateInstance<StatFloatingTextStyleSO>();
            }

            _font = TMP_Settings.defaultFontAsset;
            if (_font == null)
            {
                Debug.LogWarning("[StatFloatingTextNotifier] TMP_Settings.defaultFontAsset 없음 — visual 비활성 (StatChangedEvent는 정상 publish, 이 알리미만 no-op)");
                // 폰트 없으면 visual 빌드 안 함 — 구독도 하지 않음
                return;
            }

            BuildCanvas();
            PreWarmPool(InitialPoolSize);
            this.SubscribeOnDestroy<StatChangedEvent>(OnStatChanged);
        }

        void OnDestroy()
        {
            // 떠다니는 트윈들 정리 (풀 객체별 DOKill)
            for (int i = 0; i < _pool.Count; i++)
                _pool[i]?.Kill();
        }

        void OnStatChanged(StatChangedEvent evt)
        {
            if (evt.Delta == 0) return;
            if (_canvas == null) return;

            var item = AcquireFromPool();
            string txt = (evt.Delta > 0 ? "+" : "") + evt.Delta + "  " + evt.StatId;
            Color c = _style.ResolveColor(evt.StatId, evt.Delta);
            item.Play(txt, c, _style.anchor, _style.floatDistance, _style.lifetime);
        }

        void BuildCanvas()
        {
            var go = new GameObject("FloatingTextCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000; // 다른 UI보다 위에

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            // Raycaster는 클릭 차단 방지 위해 비활성
            go.GetComponent<GraphicRaycaster>().enabled = false;

            _canvasRect = go.GetComponent<RectTransform>();
        }

        void PreWarmPool(int count)
        {
            for (int i = 0; i < count; i++)
                _pool.Add(CreateItem());
        }

        FloatingTextItem AcquireFromPool()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null && !_pool[i].IsActive)
                    return _pool[i];
            }
            var fresh = CreateItem();
            _pool.Add(fresh);
            return fresh;
        }

        FloatingTextItem CreateItem()
        {
            var go = new GameObject("FloatingText", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_canvasRect, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300, 80);

            var cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0f;

            var tmpGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            tmpGo.transform.SetParent(rt, false);
            var tmpRt = (RectTransform)tmpGo.transform;
            tmpRt.anchorMin = Vector2.zero;
            tmpRt.anchorMax = Vector2.one;
            tmpRt.offsetMin = Vector2.zero;
            tmpRt.offsetMax = Vector2.zero;

            var tmp = tmpGo.GetComponent<TextMeshProUGUI>();
            tmp.font = _font;
            tmp.fontSize = _style.fontSize > 0 ? _style.fontSize : 60f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            go.SetActive(false);
            return new FloatingTextItem(go, rt, cg, tmp, _canvasRect);
        }

        /// <summary>풀에 보관되는 한 개의 floating text 인스턴스 + 트윈 상태.</summary>
        sealed class FloatingTextItem
        {
            readonly GameObject _go;
            readonly RectTransform _rt;
            readonly CanvasGroup _cg;
            readonly TextMeshProUGUI _tmp;
            readonly RectTransform _canvasRect;
            Sequence _seq;

            public bool IsActive { get; private set; }

            public FloatingTextItem(GameObject go, RectTransform rt, CanvasGroup cg, TextMeshProUGUI tmp, RectTransform canvasRect)
            {
                _go = go; _rt = rt; _cg = cg; _tmp = tmp; _canvasRect = canvasRect;
            }

            public void Play(string text, Color color, Vector2 anchorNorm, float floatY, float lifetime)
            {
                IsActive = true;
                _go.SetActive(true);

                _tmp.text = text;
                _tmp.color = color;

                // 캔버스 크기 기준 anchor 위치를 anchoredPosition으로 변환
                var canvasSize = _canvasRect.rect.size;
                _rt.anchoredPosition = new Vector2(anchorNorm.x * canvasSize.x, anchorNorm.y * canvasSize.y);

                _cg.alpha = 1f;
                KillSeq();
                _seq = DOTween.Sequence();
                _ = _seq.Append(_rt.DOAnchorPosY(_rt.anchoredPosition.y + floatY, lifetime).SetEase(Ease.OutCubic));
                _ = _seq.Join(_cg.DOFade(0f, lifetime).SetEase(Ease.InQuad));
                _ = _seq.OnComplete(Release);
                _ = _seq.OnKill(Release);
            }

            public void Kill() => KillSeq();

            void KillSeq()
            {
                if (_seq != null && _seq.IsActive())
                {
                    var s = _seq;
                    _seq = null;
                    s.Kill();
                }
            }

            void Release()
            {
                IsActive = false;
                _seq = null;
                if (_go != null) _go.SetActive(false);
            }
        }
    }
}
