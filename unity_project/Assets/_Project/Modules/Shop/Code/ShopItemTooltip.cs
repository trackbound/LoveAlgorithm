using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 호버 설명 팝업
    /// 
    /// 아이템 카드 위에 마우스를 올리면 표시되는 확대 설명창.
    /// 기획서: 그리드 내 위치에 따라 설명창 표시 위치가 다름.
    /// </summary>
    public class ShopItemTooltip : MonoBehaviour
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



        RectTransform rectTransform;
        Canvas rootCanvas;
        CanvasGroup canvasGroup;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();

            // 설명창이 마우스 이벤트를 가로채지 않도록 (슬롯 호버 깜박임 방지)
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

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
            if (priceText != null) priceText.text = MoneyFormat.Currency(item.Price);
            if (descriptionText != null) descriptionText.text = item.Description;

            if (iconImage != null)
                iconImage.sprite = item.GetDetailImage();

            // 마키 효과 (긴 이름 스크롤)
            if (item.UseMarquee && nameText != null)
                TextMarquee.GetOrAdd(nameText).Play();
            else
                nameText?.GetComponent<TextMarquee>()?.Stop();

            PositionRelativeToSlot(slotRect, viewportRect);

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 설명 팝업 숨김
        /// </summary>
        public void Hide()
        {
            nameText?.GetComponent<TextMarquee>()?.Stop();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 슬롯 위치에 따라 설명카드를 배치한다.
        ///
        /// 기본 원칙 (기획서 참조):
        /// - X축: 슬롯이 Viewport 좌반부이면 오른쪽에, 우반부이면 왼쪽에 표시
        /// - Y축: 슬롯 중심에 정렬하되, Viewport 밖으로 나가지 않도록 clamp
        /// - 좌/우에 공간이 없으면 상/하 fallback
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

            // 슬롯/팝업 크기 (월드 스케일 반영)
            Vector3 slotWorld = slotRect.position;
            float slotW = slotRect.rect.width  * slotRect.lossyScale.x;
            float slotH = slotRect.rect.height * slotRect.lossyScale.y;
            float popupW = rectTransform.rect.width  * rectTransform.lossyScale.x;
            float popupH = rectTransform.rect.height * rectTransform.lossyScale.y;

            float halfPopupW = popupW * 0.5f;
            float halfPopupH = popupH * 0.5f;

            float posX, posY;

            // ── X축: 좌/우 배치 판단 ──────────────────
            float nx = Mathf.InverseLerp(vpLeft, vpRight, slotWorld.x);
            float offsetX = slotW * 0.5f + halfPopupW + gap;

            // 좌측 → 오른쪽에 배치, 우측 → 왼쪽에 배치
            float rightX = slotWorld.x + offsetX;
            float leftX  = slotWorld.x - offsetX;

            bool fitsRight = (rightX + halfPopupW + edgePadding) <= vpRight;
            bool fitsLeft  = (leftX  - halfPopupW - edgePadding) >= vpLeft;

            if (nx <= 0.5f && fitsRight)
                posX = rightX;
            else if (nx > 0.5f && fitsLeft)
                posX = leftX;
            else if (fitsRight)
                posX = rightX;
            else if (fitsLeft)
                posX = leftX;
            else
            {
                // 좌/우 모두 불가 → 슬롯 중심 정렬 + 상/하 fallback
                posX = slotWorld.x;
                posX = Mathf.Clamp(posX, vpLeft + halfPopupW + edgePadding, vpRight - halfPopupW - edgePadding);

                float ny = Mathf.InverseLerp(vpBottom, vpTop, slotWorld.y);
                float offsetY = slotH * 0.5f + halfPopupH + gap;
                posY = slotWorld.y + (ny > 0.5f ? -offsetY : offsetY);
                posY = Mathf.Clamp(posY, vpBottom + halfPopupH + edgePadding, vpTop - halfPopupH - edgePadding);

                rectTransform.position = new Vector3(posX, posY, slotWorld.z);
                return;
            }

            // ── Y축: 슬롯 중심 정렬 + clamp ─────────
            posY = slotWorld.y;
            posY = Mathf.Clamp(posY, vpBottom + halfPopupH + edgePadding, vpTop - halfPopupH - edgePadding);

            rectTransform.position = new Vector3(posX, posY, slotWorld.z);
        }
    }
}
