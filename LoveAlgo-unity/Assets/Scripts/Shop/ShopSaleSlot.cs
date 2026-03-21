using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 판매 슬롯 — 카드형 그리드 아이템
    ///
    /// 프리팹 구조:
    ///   - img_bg: 카드 배경 (SpriteSwap: normal/hover/selected)
    ///   - img_icon: 아이템 아이콘 (큰 이미지)
    ///   - txt_name: 아이템 이름
    ///   - txt_price: ₩가격
    ///
    /// 동작:
    ///   - 클릭 → 장바구니에 추가 (수량 증가)
    ///   - 호버 → 배경 스프라이트 교체 + 설명 팝업 요청
    ///   - 장바구니에 담긴 상태면 selectedSprite 표시
    /// </summary>
    public class ShopSaleSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI 요소")]
        [SerializeField] Image bgImage;
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text priceText;
        [SerializeField] Button slotButton;

        [Header("배경 스프라이트")]
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite hoverSprite;
        [SerializeField] Sprite selectedSprite;

        ItemData itemData;
        bool isInCart;
        bool isHovered;

        /// <summary>클릭 콜백: (슬롯)</summary>
        Action<ShopSaleSlot> onClicked;

        /// <summary>호버 콜백: (슬롯, 호버진입여부)</summary>
        Action<ShopSaleSlot, bool> onHovered;

        /// <summary>현재 아이템 데이터</summary>
        public ItemData Item => itemData;

        /// <summary>
        /// 슬롯 설정
        /// </summary>
        public void Setup(ItemData item,
            Action<ShopSaleSlot> onClick,
            Action<ShopSaleSlot, bool> onHover = null)
        {
            itemData = item;
            onClicked = onClick;
            onHovered = onHover;
            isInCart = false;
            isHovered = false;

            if (nameText != null) nameText.text = item.Name;
            if (priceText != null) priceText.text = $"{item.Price:N0}원";

            if (iconImage != null)
                iconImage.sprite = item.GetSaleIcon();

            // 배경 스프라이트 초기화
            ApplyBgSprite();

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(OnClick);
            }
        }

        /// <summary>외부에서 선택 상태 설정 (장바구니 담김/해제)</summary>
        public void SetSelected(bool selected)
        {
            isInCart = selected;
            ApplyBgSprite();
        }

        void OnClick()
        {
            onClicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            ApplyBgSprite();
            onHovered?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            ApplyBgSprite();
            onHovered?.Invoke(this, false);
        }

        /// <summary>현재 상태에 맞는 배경 스프라이트 적용 (우선순위: hover > selected > normal)</summary>
        void ApplyBgSprite()
        {
            if (bgImage == null) return;

            if (isHovered && hoverSprite != null)
                bgImage.sprite = hoverSprite;
            else if (isInCart && selectedSprite != null)
                bgImage.sprite = selectedSprite;
            else if (normalSprite != null)
                bgImage.sprite = normalSprite;
        }
    }
}
