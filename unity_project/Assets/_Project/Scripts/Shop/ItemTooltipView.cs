using LoveAlgo.Core; // MoneyFormat
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 호버 설명창(기획서: 아이템 칸 위에 마우스 호버 시 설명창 출력). 표시 전용 — 호버 진입/이탈은
    /// <see cref="SaleSlotView"/>가 콜백으로 알리고 ShopView가 중계한다. 절대 입력을 막지 않도록
    /// CanvasGroup blocksRaycasts=false 고정(설명창이 호버 이탈을 유발하는 깜빡임 방지).
    /// 위치 = 슬롯 기준 오프셋(기획서의 "칸 위치별 설명창 위치"는 감독 튜닝 영역 — offset 직렬화).
    /// </summary>
    public class ItemTooltipView : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text priceText;
        [SerializeField] TMP_Text descText;
        [Tooltip("호버한 슬롯 중심에서의 표시 오프셋(부모 캔버스 로컬 px).")]
        [SerializeField] Vector2 offset = new Vector2(0f, 160f);

        public CanvasGroup Group { get => canvasGroup; set => canvasGroup = value; }
        public TMP_Text NameText { get => nameText; set => nameText = value; }
        public TMP_Text DescText { get => descText; set => descText = value; }
        public bool IsShown => canvasGroup != null && canvasGroup.alpha > 0f;

        void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false; // 설명창이 포인터를 가로채면 호버 이탈 깜빡임 발생
                canvasGroup.interactable = false;
            }
            Hide();
        }

        /// <summary>아이템 내용을 채우고 슬롯 근처에 표시.</summary>
        public void Show(ItemData item, Transform nearSlot)
        {
            if (item == null) return;
            if (icon != null) icon.sprite = item.GetSaleIcon();
            if (nameText != null) nameText.text = item.Name;
            if (priceText != null) priceText.text = MoneyFormat.Currency(item.Price);
            if (descText != null) descText.text = item.Description ?? "";
            if (nearSlot != null) transform.position = nearSlot.position + (Vector3)offset;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        public void Hide()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }
    }
}
