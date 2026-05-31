using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 장바구니 슬롯 (ShopCartSlot 프리팹에 연결)
    ///
    /// 프리팹 구조:
    ///   - img_icon: 아이템 아이콘 (소형)
    ///   - txt_name: 아이템 이름
    ///   - txt_price: 개별 가격
    ///   - txt_qty: 수량 뱃지
    ///   - btn_plus: 수량 증가
    ///   - btn_minus: 수량 감소
    /// </summary>
    public class ShopCartSlot : MonoBehaviour
    {
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text priceText;
        [SerializeField] TMP_Text quantityText;
        [SerializeField] Button plusButton;
        [SerializeField] Button minusButton;

        ItemData itemData;
        int quantity;
        Action<string, int> onQuantityChanged;

        /// <summary>
        /// 슬롯 설정
        /// </summary>
        public void Setup(ItemData item, int qty, Action<string, int> onChanged)
        {
            itemData = item;
            quantity = qty;
            onQuantityChanged = onChanged;

            if (iconImage != null)
                iconImage.sprite = item.GetSmallIcon();

            Refresh();

            if (plusButton != null)
            {
                plusButton.onClick.RemoveAllListeners();
                plusButton.onClick.AddListener(() => ChangeQuantity(1));
            }

            if (minusButton != null)
            {
                minusButton.onClick.RemoveAllListeners();
                minusButton.onClick.AddListener(() => ChangeQuantity(-1));
            }
        }

        void ChangeQuantity(int delta)
        {
            int newQty = Mathf.Max(0, quantity + delta);
            onQuantityChanged?.Invoke(itemData.Id, newQty);
        }

        void Refresh()
        {
            if (nameText != null) nameText.text = itemData.Name;
            if (priceText != null) priceText.text = MoneyFormat.Currency(itemData.Price);
            if (quantityText != null) quantityText.text = $"{quantity}";
        }
    }
}
