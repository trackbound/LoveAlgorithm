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

        [Header("핸들 호버 스프라이트")]
        [SerializeField] Sprite handleHoverSprite;

        [Header("최소 핸들 크기 (0~1, 클릭 영역 보장)")]
        [SerializeField, Range(0.02f, 0.5f)] float minHandleSize = 0.1f;

        Scrollbar scrollbar;
        Sprite handleNormalSprite;
        bool isHovered;

        void Awake()
        {
            scrollbar = GetComponent<Scrollbar>();
            if (handleVisual != null)
                handleNormalSprite = handleVisual.sprite;
        }

        void OnEnable()
        {
            isHovered = false;
            if (handleVisual != null && handleNormalSprite != null)
                handleVisual.sprite = handleNormalSprite;
        }

        void LateUpdate()
        {
            if (scrollbar != null && scrollbar.size < minHandleSize)
                scrollbar.size = minHandleSize;
        }

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
    }
}
