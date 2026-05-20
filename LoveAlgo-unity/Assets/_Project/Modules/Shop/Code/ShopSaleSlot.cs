using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LoveAlgo.Core;
using LoveAlgo.UI;

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
    public class ShopSaleSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
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
            if (priceText != null) priceText.text = MoneyFormat.Currency(item.Price);

            if (iconImage != null)
                iconImage.sprite = item.GetSaleIcon();

            // 마키는 호버 시에만 — 그리드 정적 상태 유지 (시선 집중도 ↑)
            // 평상시 긴 이름은 RectMask2D가 자동 잘림 처리
            nameText?.GetComponent<TextMarquee>()?.Stop();

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

            // 긴 이름이면 호버 진입 시에만 마키 시작 (부드럽게 가속 후 정속)
            if (itemData != null && itemData.UseMarquee && nameText != null)
                TextMarquee.GetOrAdd(nameText).Play();

            onHovered?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            ApplyBgSprite();

            // 호버 이탈 시 부드럽게 감속 후 원위치 복원
            nameText?.GetComponent<TextMarquee>()?.Stop();

            onHovered?.Invoke(this, false);
        }

        /// <summary>현재 상태에 맞는 배경 스프라이트 적용 (우선순위: selected > hover > normal)</summary>
        void ApplyBgSprite()
        {
            if (bgImage == null) return;

            if (isInCart && selectedSprite != null)
                bgImage.sprite = selectedSprite;
            else if (isHovered && hoverSprite != null)
                bgImage.sprite = hoverSprite;
            else if (normalSprite != null)
                bgImage.sprite = normalSprite;
        }

        /// <summary>스크롤 이벤트를 부모 ScrollRect로 전달 (호버 중에도 스크롤 가능)</summary>
        ScrollRect parentScrollRect;

        public void OnScroll(PointerEventData eventData)
        {
            if (parentScrollRect == null)
                parentScrollRect = GetComponentInParent<ScrollRect>();
            if (parentScrollRect != null)
                parentScrollRect.OnScroll(eventData);
        }
    }
}
