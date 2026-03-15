using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LoveAlgo.UI
{
    /// <summary>
    /// Unity Scrollbar 보조 컴포넌트
    /// - 핸들 최소 크기 보정 (콘텐츠가 많아도 핸들이 너무 작아지지 않음)
    /// - 핸들 호버 스프라이트 전환
    /// - Scrollbar.Transition 강제 None
    /// </summary>
    [RequireComponent(typeof(Scrollbar))]
    public class ScrollbarEX : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Header("최소 핸들 크기 (0~1)")]
        [SerializeField, Range(0.02f, 0.5f)] float minHandleSize = 0.1f;

        [Header("핸들 호버 스프라이트")]
        [SerializeField] Sprite handleHoverSprite;

        Scrollbar scrollbar;
        Image handleImage;
        Sprite handleNormalSprite;
        bool isHovered;

        void Awake()
        {
            scrollbar = GetComponent<Scrollbar>();
            scrollbar.transition = Selectable.Transition.None;

            if (scrollbar.handleRect != null)
            {
                handleImage = scrollbar.handleRect.GetComponent<Image>();
                if (handleImage != null)
                    handleNormalSprite = handleImage.sprite;
            }
        }

        void OnEnable()
        {
            isHovered = false;
            if (handleImage != null && handleNormalSprite != null)
                handleImage.sprite = handleNormalSprite;
        }

        void LateUpdate()
        {
            if (scrollbar != null && scrollbar.size < minHandleSize)
                scrollbar.size = minHandleSize;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (handleImage == null || handleHoverSprite == null) return;
            isHovered = true;
            handleImage.sprite = handleHoverSprite;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (handleImage == null) return;
            isHovered = false;
            handleImage.sprite = handleNormalSprite;
        }

        /// <summary>핸들 스프라이트 런타임 교체</summary>
        public void SetHandleSprites(Sprite normal, Sprite hover)
        {
            handleNormalSprite = normal;
            handleHoverSprite = hover;
            if (handleImage != null)
                handleImage.sprite = isHovered && hover != null ? hover : normal;
        }
    }
}
