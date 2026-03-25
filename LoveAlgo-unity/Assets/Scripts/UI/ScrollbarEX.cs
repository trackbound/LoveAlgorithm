using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LoveAlgo.UI
{
    /// <summary>
    /// Unity Scrollbar 보조 컴포넌트 (비주얼 자식 패턴)
    ///
    /// Scrollbar/Handle의 Image → 투명 (Unity가 RectTransform 자유 제어)
    /// 비주얼 → 자식 Image로 표시 (고정 크기, 깨짐 없음)
    ///
    /// 구조:
    ///   Scrollbar (Image: 투명)
    ///     ├─ TrackVisual  (Image: 트랙 스프라이트, 스트레치)
    ///     └─ Handle Slide Area
    ///          └─ Handle (Image: 투명)
    ///               └─ HandleVisual (Image: 핸들 스프라이트, 고정 크기)
    /// </summary>
    [RequireComponent(typeof(Scrollbar))]
    public class ScrollbarEX : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Header("비주얼")]
        [SerializeField] Image handleVisual;
        [SerializeField] RectTransform trackVisual;

        [Header("핸들 호버 스프라이트")]
        [SerializeField] Sprite handleHoverSprite;

        [Header("핸들 크기")]
        [Tooltip("체크 시 핸들 크기를 고정 (ScrollRect 자동 계산 무시)")]
        [SerializeField] bool fixHandleSize;
        [SerializeField, Range(0.02f, 0.5f)] float handleSize = 0.1f;

        Scrollbar scrollbar;
        Sprite handleNormalSprite;
        bool isHovered;

        void Awake()
        {
            scrollbar = GetComponent<Scrollbar>();
            CacheReferences();
            ApplyLayoutCorrection();

            if (handleVisual != null)
                handleNormalSprite = handleVisual.sprite;
        }

        void OnEnable()
        {
            isHovered = false;
            ApplyLayoutCorrection();

            if (handleVisual != null && handleNormalSprite != null)
                handleVisual.sprite = handleNormalSprite;
        }

        void LateUpdate()
        {
            if (scrollbar == null)
                return;

            float clamped = Mathf.Clamp01(handleSize);
            if (fixHandleSize)
                scrollbar.size = clamped;
            else if (scrollbar.size < clamped)
                scrollbar.size = clamped;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            scrollbar = GetComponent<Scrollbar>();
            CacheReferences();
            ApplyLayoutCorrection();
        }
#endif

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (handleVisual == null || handleHoverSprite == null) return;
            isHovered = true;
            handleVisual.sprite = handleHoverSprite;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (handleVisual == null) return;
            isHovered = false;
            handleVisual.sprite = handleNormalSprite;
        }

        /// <summary>핸들 스프라이트 런타임 교체</summary>
        public void SetHandleSprites(Sprite normal, Sprite hover)
        {
            handleNormalSprite = normal;
            handleHoverSprite = hover;
            if (handleVisual != null)
                handleVisual.sprite = isHovered && hover != null ? hover : normal;
        }

        void CacheReferences()
        {
            if (scrollbar == null)
                scrollbar = GetComponent<Scrollbar>();

            if (handleVisual == null && scrollbar != null && scrollbar.handleRect != null)
                handleVisual = scrollbar.handleRect.GetComponentInChildren<Image>();

            if (trackVisual == null)
            {
                Transform track = transform.Find("TrackVisual");
                if (track != null)
                    trackVisual = track as RectTransform;
            }
        }

        void ApplyLayoutCorrection()
        {
            if (scrollbar == null)
                return;

            StretchTrackVisual();
        }

        void StretchTrackVisual()
        {
            if (trackVisual == null)
                return;

            if (IsVertical())
            {
                trackVisual.anchorMin = new Vector2(0.5f, 0f);
                trackVisual.anchorMax = new Vector2(0.5f, 1f);
                trackVisual.anchoredPosition = Vector2.zero;
                trackVisual.sizeDelta = new Vector2(trackVisual.sizeDelta.x, 0f);
            }
            else
            {
                trackVisual.anchorMin = new Vector2(0f, 0.5f);
                trackVisual.anchorMax = new Vector2(1f, 0.5f);
                trackVisual.anchoredPosition = Vector2.zero;
                trackVisual.sizeDelta = new Vector2(0f, trackVisual.sizeDelta.y);
            }
        }

        bool IsVertical()
        {
            return scrollbar.direction == Scrollbar.Direction.BottomToTop
                || scrollbar.direction == Scrollbar.Direction.TopToBottom;
        }
    }
}
