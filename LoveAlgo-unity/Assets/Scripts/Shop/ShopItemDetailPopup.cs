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

        [Header("위치 설정")]
        [Tooltip("슬롯과 팝업 사이 간격 (px)")]
        [SerializeField] float gap = 12f;

        [Tooltip("Viewport 중앙 방향으로 밀어내는 강도 (0=슬롯 옆, 1=Viewport 중앙)")]
        [SerializeField, Range(0f, 0.5f)] float centerBias = 0.15f;

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
        /// <param name="viewportRect">Viewport의 RectTransform (위치 계산 기준)</param>
        public void Show(ItemData item, RectTransform slotRect, RectTransform viewportRect)
        {
            if (item == null) return;

            // 텍스트 설정
            if (nameText != null) nameText.text = item.Name;
            if (priceText != null) priceText.text = $"{item.Price:N0}원";
            if (descriptionText != null) descriptionText.text = item.Description;

            if (iconImage != null)
                iconImage.sprite = item.GetDetailImage();

            PositionRelativeToSlot(slotRect, viewportRect);

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
        /// 슬롯의 Viewport 내 정규화 좌표(0~1)에 따라 팝업을 중앙 방향으로 배치
        ///
        /// X: 좌측 슬롯 → 팝업 우측, 우측 슬롯 → 팝업 좌측
        /// Y: 상단 슬롯 → 팝업 하단, 하단 슬롯 → 팝업 상단
        /// 중앙 가까울수록 팝업도 슬롯에 가까이 붙음
        /// </summary>
        void PositionRelativeToSlot(RectTransform slotRect, RectTransform viewportRect)
        {
            if (rectTransform == null || slotRect == null) return;

            // Viewport 영역의 월드 좌표 Bounds 계산
            Vector3[] vpCorners = new Vector3[4];
            (viewportRect != null ? viewportRect : (RectTransform)slotRect.parent)
                .GetWorldCorners(vpCorners);
            // vpCorners: [0]=BL, [1]=TL, [2]=TR, [3]=BR
            float vpLeft   = vpCorners[0].x;
            float vpRight  = vpCorners[2].x;
            float vpBottom = vpCorners[0].y;
            float vpTop    = vpCorners[2].y;
            float vpCenterX = (vpLeft + vpRight) * 0.5f;
            float vpCenterY = (vpBottom + vpTop) * 0.5f;

            // 슬롯 크기 및 월드 위치
            Vector3 slotWorld = slotRect.position;
            float slotW = slotRect.rect.width * slotRect.lossyScale.x;
            float slotH = slotRect.rect.height * slotRect.lossyScale.y;
            float popupW = rectTransform.rect.width * rectTransform.lossyScale.x;
            float popupH = rectTransform.rect.height * rectTransform.lossyScale.y;

            // 슬롯이 Viewport 내 어디에 있는지 정규화 (0~1)
            float nx = Mathf.InverseLerp(vpLeft, vpRight, slotWorld.x);
            float ny = Mathf.InverseLerp(vpBottom, vpTop, slotWorld.y);

            // ── X축: 슬롯의 반대쪽으로 배치 + 중앙 방향 bias
            float baseOffsetX = slotW * 0.5f + popupW * 0.5f + gap;
            float dirX = (nx < 0.5f) ? 1f : -1f;  // 좌측이면 우로, 우측이면 좌로
            float posX = slotWorld.x + dirX * baseOffsetX;
            // 중앙 방향으로 살짝 끌어당김
            posX = Mathf.Lerp(posX, vpCenterX, centerBias);

            // ── Y축: 슬롯의 반대쪽으로 배치 + 중앙 방향 bias
            float baseOffsetY = slotH * 0.5f + popupH * 0.5f + gap;
            float dirY = (ny < 0.5f) ? 1f : -1f;  // 하단이면 위로, 상단이면 아래로
            float posY = slotWorld.y + dirY * baseOffsetY;
            posY = Mathf.Lerp(posY, vpCenterY, centerBias);

            // ── 화면 밖 보정 (Viewport 범위 내로 clamp)
            float halfW = popupW * 0.5f;
            float halfH = popupH * 0.5f;
            posX = Mathf.Clamp(posX, vpLeft + halfW, vpRight - halfW);
            posY = Mathf.Clamp(posY, vpBottom + halfH, vpTop - halfH);

            rectTransform.position = new Vector3(posX, posY, slotWorld.z);
        }
    }
}
