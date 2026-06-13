using System;
using LoveAlgo.Core; // MoneyFormat
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 장바구니 1칸(얇은 표시 — ADR-007). 아이콘·이름·1개당 가격·수량을 표시하고 수량 ± 버튼 클릭을
    /// 콜백으로 통지(수량 계산·제거 판단은 ShopView+<see cref="CartMath"/> 몫 — 기획서: 수량 조절, 99 제한,
    /// 가격은 1개당). 모든 참조는 null 허용(부분 바인딩 프리팹/테스트 템플릿 안전).
    /// </summary>
    public class CartSlotView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameText;
        [Tooltip("1개당 가격(기획서: 선택한 아이템 1개당 가격 출력).")]
        [SerializeField] TMP_Text priceText;
        [SerializeField] TMP_Text qtyText;
        [SerializeField] Button upButton;
        [SerializeField] Button downButton;

        public Image Icon { get => icon; set => icon = value; }
        public TMP_Text NameText { get => nameText; set => nameText = value; }
        public TMP_Text PriceText { get => priceText; set => priceText = value; }
        public TMP_Text QtyText { get => qtyText; set => qtyText = value; }
        public Button UpButton { get => upButton; set => upButton = value; }
        public Button DownButton { get => downButton; set => downButton = value; }
        public string ItemId { get; private set; }

        Action<string> _onUp, _onDown;

        void Awake()
        {
            if (upButton != null) upButton.onClick.AddListener(HandleUp);
            if (downButton != null) downButton.onClick.AddListener(HandleDown);
        }

        /// <summary>칸 바인딩 — 표시 갱신 + 수량 콜백 연결(재바인딩 안전: 콜백만 교체).</summary>
        public void Bind(ItemData item, int qty, Action<string> onUp, Action<string> onDown)
        {
            ItemId = item.Id;
            _onUp = onUp;
            _onDown = onDown;
            if (icon != null) icon.sprite = item.GetSmallIcon();
            if (nameText != null) nameText.text = item.Name;
            if (priceText != null) priceText.text = MoneyFormat.Currency(item.Price);
            if (qtyText != null) qtyText.text = qty.ToString();
        }

        void HandleUp() => _onUp?.Invoke(ItemId);
        void HandleDown() => _onDown?.Invoke(ItemId);

        void OnDestroy()
        {
            if (upButton != null) upButton.onClick.RemoveListener(HandleUp);
            if (downButton != null) downButton.onClick.RemoveListener(HandleDown);
        }
    }
}
