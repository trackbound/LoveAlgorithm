using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 호버 설명 팝업
    /// 
    /// 아이템 카드 위에 마우스를 올리면 표시되는 확대 설명창.
    /// 기획서: 그리드 내 위치에 따라 설명창 표시 위치가 다름.
    /// </summary>
    public class ShopItemDetailPopup : MonoBehaviour
    {
        [SerializeField] Image bgImage;
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text priceText;
        [SerializeField] TMP_Text descriptionText;

        RectTransform rectTransform;
        Canvas rootCanvas;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 설명 팝업 표시
        /// </summary>
        /// <param name="item">아이템 데이터</param>
        /// <param name="slotRect">호버 중인 슬롯의 RectTransform</param>
        /// <param name="gridContainer">그리드 컨테이너의 RectTransform (위치 계산 기준)</param>
        public void Show(ItemData item, RectTransform slotRect, RectTransform gridContainer)
        {
            if (item == null) return;

            // 텍스트 설정
            if (nameText != null) nameText.text = item.Name;
            if (priceText != null) priceText.text = $"{item.Price:N0}원";
            if (descriptionText != null) descriptionText.text = item.Description;

            // 아이콘 로드
            if (iconImage != null && !string.IsNullOrEmpty(item.IconPath))
            {
                var sprite = Resources.Load<Sprite>(item.IconPath);
                if (sprite != null) iconImage.sprite = sprite;
            }

            // 위치 계산: 슬롯이 그리드 중앙 기준 왼쪽이면 오른쪽에, 오른쪽이면 왼쪽에 표시
            PositionRelativeToSlot(slotRect, gridContainer);

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 설명 팝업 숨김
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 슬롯 위치 기준으로 팝업 위치 결정
        /// 기획서: 왼쪽 칸 호버 → 오른쪽에 표시, 오른쪽 칸 호버 → 왼쪽에 표시
        /// </summary>
        void PositionRelativeToSlot(RectTransform slotRect, RectTransform gridContainer)
        {
            if (rectTransform == null || slotRect == null) return;

            // 슬롯의 로컬 좌표를 팝업 부모 기준으로 변환
            Vector3 slotWorldPos = slotRect.position;
            Vector3 gridWorldCenter = gridContainer != null
                ? gridContainer.position
                : slotRect.parent.position;

            // 슬롯이 그리드 중앙보다 왼쪽이면 → 팝업을 슬롯 오른쪽에
            // 슬롯이 그리드 중앙보다 오른쪽이면 → 팝업을 슬롯 왼쪽에
            float popupWidth = rectTransform.rect.width;
            float slotWidth = slotRect.rect.width;
            float offset = (slotWidth * 0.5f + popupWidth * 0.5f + 10f); // 10px 간격

            Vector3 newPos = slotWorldPos;
            if (slotWorldPos.x < gridWorldCenter.x)
                newPos.x += offset;
            else
                newPos.x -= offset;

            rectTransform.position = newPos;
        }
    }
}
