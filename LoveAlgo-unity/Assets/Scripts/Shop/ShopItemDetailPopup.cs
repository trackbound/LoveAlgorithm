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

        [Tooltip("Viewport 가장자리와 설명카드 사이 최소 여백 (px)")]
        [SerializeField] float edgePadding = 16f;

        [Tooltip("세로 위치를 중앙으로 살짝 보정하는 강도 (0=슬롯 중심 고정, 1=Viewport 중앙)")]
        [SerializeField, Range(0f, 0.35f)] float verticalCenterBias = 0.08f;

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
        /// 슬롯 옆으로 설명카드를 배치한다.
        ///
        /// 기본 원칙:
        /// - 좌측 카드면 우측에, 우측 카드면 좌측에 붙인다.
        /// - 세로축은 슬롯 중심을 기준으로 두되, 화면 중앙 쪽으로만 약하게 보정한다.
        /// - 화면 밖으로 나가지 않도록 Viewport 안에서 clamp 한다.
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
            float vpCenterY = (vpBottom + vpTop) * 0.5f;

            // 슬롯 크기 및 월드 위치
            Vector3 slotWorld = slotRect.position;
            float slotW = slotRect.rect.width * slotRect.lossyScale.x;
            float slotH = slotRect.rect.height * slotRect.lossyScale.y;
            float popupW = rectTransform.rect.width * rectTransform.lossyScale.x;
            float popupH = rectTransform.rect.height * rectTransform.lossyScale.y;

            float nx = Mathf.InverseLerp(vpLeft, vpRight, slotWorld.x);

            // X축은 항상 좌우 사이드 배치
            float baseOffsetX = slotW * 0.5f + popupW * 0.5f + gap;
            float dirX = nx < 0.5f ? 1f : -1f;
            float posX = slotWorld.x + dirX * baseOffsetX;

            // Y축은 슬롯 중심 기준, 화면 중앙으로만 약하게 보정
            float posY = Mathf.Lerp(slotWorld.y, vpCenterY, verticalCenterBias);

            // ── 화면 밖 보정 (Viewport 범위 내로 clamp)
            float halfW = popupW * 0.5f + edgePadding;
            float halfH = popupH * 0.5f + edgePadding;
            posX = Mathf.Clamp(posX, vpLeft + halfW, vpRight - halfW);
            posY = Mathf.Clamp(posY, vpBottom + halfH, vpTop - halfH);

            rectTransform.position = new Vector3(posX, posY, slotWorld.z);
        }
    }
}
