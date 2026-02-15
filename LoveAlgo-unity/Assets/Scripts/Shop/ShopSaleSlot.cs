using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 판매 슬롯 (ShopSaleSlot 프리팹에 연결)
    /// 
    /// 프리팹 구조 (예상):
    ///   - img_icon: 아이템 아이콘
    ///   - txt_name: 아이템 이름
    ///   - txt_price: 가격
    ///   - btn_add: 장바구니 추가 버튼
    /// </summary>
    public class ShopSaleSlot : MonoBehaviour
    {
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text priceText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] Button addButton;

        ItemData itemData;
        Action<ItemData> onAddClicked;

        /// <summary>
        /// 슬롯 설정
        /// </summary>
        public void Setup(ItemData item, Action<ItemData> onAdd)
        {
            itemData = item;
            onAddClicked = onAdd;

            if (nameText != null) nameText.text = item.Name;
            if (priceText != null) priceText.text = $"{item.Price:N0}원";
            if (descriptionText != null) descriptionText.text = item.Description;

            // 아이콘 로드 (없으면 기본)
            if (iconImage != null && !string.IsNullOrEmpty(item.IconPath))
            {
                var sprite = Resources.Load<Sprite>(item.IconPath);
                if (sprite != null) iconImage.sprite = sprite;
            }

            // 카테고리 표시 (이름 옆에 태그)
            if (nameText != null)
            {
                string tag = item.Category == ItemCategory.Gift ? "<color=#FF6B9D>[선물]</color>" : "<color=#6BC8FF>[소모품]</color>";
                nameText.text = $"{tag} {item.Name}";
            }

            if (addButton != null)
            {
                addButton.onClick.RemoveAllListeners();
                addButton.onClick.AddListener(OnAddClick);
            }
        }

        void OnAddClick()
        {
            onAddClicked?.Invoke(itemData);
        }
    }
}
