using System;
using LoveAlgo.Core; // MoneyFormat
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 판매 그리드 1칸(얇은 표시 — ADR-007). 클릭=선택 토글 콜백(장바구니 담기/빼기는 ShopView 몫),
    /// 호버=설명창 콜백(기획서: 아이템 칸 위에 마우스 호버 시 설명창 출력). 선택 시 시각 = 체크 마크
    /// (<see cref="selectedMark"/>, 기획서 "우상단 체크 표시") — 아트 미배선이면 루트 틴트 폴백.
    /// 모든 참조는 null 허용(부분 바인딩 프리팹/테스트 템플릿 안전).
    /// </summary>
    public class SaleSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text priceText;
        [Tooltip("선택(장바구니 담김) 체크 마크. 미배선 시 루트 Image 틴트로 폴백.")]
        [SerializeField] Image selectedMark;
        [Tooltip("체크 마크 미배선 시 선택 상태 루트 틴트 색.")]
        [SerializeField] Color selectedTint = new Color(1f, 0.85f, 0.92f, 1f);

        public Button Button { get => button; set => button = value; }
        public Image Icon { get => icon; set => icon = value; }
        public TMP_Text NameText { get => nameText; set => nameText = value; }
        public TMP_Text PriceText { get => priceText; set => priceText = value; }
        public ItemData Item { get; private set; }
        public bool IsSelected { get; private set; }

        Action<ItemData> _onClick;
        Action<SaleSlotView, bool> _onHover; // (슬롯, 진입 여부)
        Image _rootImage;
        Color _rootBaseColor = Color.white;

        void Awake()
        {
            _rootImage = GetComponent<Image>();
            if (_rootImage != null) _rootBaseColor = _rootImage.color;
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        /// <summary>슬롯 바인딩 — 아이템 표시 + 클릭/호버 콜백 연결.</summary>
        public void Bind(ItemData item, Action<ItemData> onClick, Action<SaleSlotView, bool> onHover)
        {
            Item = item;
            _onClick = onClick;
            _onHover = onHover;
            if (icon != null) icon.sprite = item.GetSaleIcon();
            if (nameText != null) nameText.text = item.Name;
            if (priceText != null) priceText.text = MoneyFormat.Currency(item.Price);
            SetSelected(false);
        }

        /// <summary>선택(장바구니 담김) 시각 갱신 — 체크 마크 또는 루트 틴트 폴백.</summary>
        public void SetSelected(bool on)
        {
            IsSelected = on;
            if (selectedMark != null) selectedMark.gameObject.SetActive(on);
            else if (_rootImage != null) _rootImage.color = on ? selectedTint : _rootBaseColor;
        }

        void HandleClick() => _onClick?.Invoke(Item);
        public void OnPointerEnter(PointerEventData _) => _onHover?.Invoke(this, true);
        public void OnPointerExit(PointerEventData _) => _onHover?.Invoke(this, false);

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }
    }
}
