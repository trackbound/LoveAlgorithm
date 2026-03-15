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
    ///   - img_bg: 카드 배경 (SpriteSwap: normal/hover)
    ///   - img_icon: 아이템 아이콘 (큰 이미지)
    ///   - txt_name: 아이템 이름
    ///   - txt_price: ₩가격
    ///   - obj_check: 체크마크 오브젝트 (선택 시 활성)
    ///
    /// 동작:
    ///   - 클릭 → 토글 선택/해제 (체크마크 + 장바구니 반영)
    ///   - 호버 → 배경 스프라이트 교체 + 설명 팝업 요청
    /// </summary>
    public class ShopSaleSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI 요소")]
        [SerializeField] Image bgImage;
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text priceText;
        [SerializeField] GameObject checkMark;
        [SerializeField] Button slotButton;

        [Header("호버 스프라이트")]
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite hoverSprite;

        ItemData itemData;
        bool isSelected;

        /// <summary>토글 콜백: (슬롯, 선택여부)</summary>
        Action<ShopSaleSlot, bool> onToggled;

        /// <summary>호버 콜백: (슬롯, 호버진입여부)</summary>
        Action<ShopSaleSlot, bool> onHovered;

        /// <summary>현재 아이템 데이터</summary>
        public ItemData Item => itemData;

        /// <summary>선택 상태</summary>
        public bool IsSelected => isSelected;

        /// <summary>
        /// 슬롯 설정
        /// </summary>
        public void Setup(ItemData item,
            Action<ShopSaleSlot, bool> onToggle,
            Action<ShopSaleSlot, bool> onHover = null)
        {
            itemData = item;
            onToggled = onToggle;
            onHovered = onHover;
            isSelected = false;

            if (nameText != null) nameText.text = item.Name;
            if (priceText != null) priceText.text = $"{item.Price:N0}원";
            if (checkMark != null) checkMark.SetActive(false);

            // 아이콘 로드
            if (iconImage != null && !string.IsNullOrEmpty(item.IconPath))
            {
                var sprite = Resources.Load<Sprite>(item.IconPath);
                if (sprite != null) iconImage.sprite = sprite;
            }

            // 배경 스프라이트 초기화
            if (bgImage != null && normalSprite != null)
                bgImage.sprite = normalSprite;

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(OnClick);
            }
        }

        /// <summary>외부에서 선택 상태 강제 설정 (장바구니 수량 0 시 해제 등)</summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (checkMark != null) checkMark.SetActive(isSelected);
        }

        void OnClick()
        {
            isSelected = !isSelected;
            if (checkMark != null) checkMark.SetActive(isSelected);
            onToggled?.Invoke(this, isSelected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (bgImage != null && hoverSprite != null)
                bgImage.sprite = hoverSprite;
            onHovered?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (bgImage != null && normalSprite != null)
                bgImage.sprite = normalSprite;
            onHovered?.Invoke(this, false);
        }
    }
}
