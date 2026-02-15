using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 장바구니 슬롯 (ShopCartSlot 프리팹에 연결)
    /// 
    /// 프리팹 구조 (예상):
    ///   - txt_name: 아이템 이름
    ///   - txt_qty: 수량
    ///   - txt_subtotal: 소계
    ///   - btn_plus: 수량 증가
    ///   - btn_minus: 수량 감소
    ///   - btn_remove: 제거
    /// </summary>
    public class ShopCartSlot : MonoBehaviour
    {
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text quantityText;
        [SerializeField] TMP_Text subtotalText;
        [SerializeField] Button plusButton;
        [SerializeField] Button minusButton;
        [SerializeField] Button removeButton;

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

            if (removeButton != null)
            {
                removeButton.onClick.RemoveAllListeners();
                removeButton.onClick.AddListener(() => onQuantityChanged?.Invoke(itemData.Id, 0));
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
            if (quantityText != null) quantityText.text = $"x{quantity}";
            if (subtotalText != null) subtotalText.text = $"{itemData.Price * quantity:N0}원";
        }
    }
}
