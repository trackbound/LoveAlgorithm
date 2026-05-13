using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 히로인/엑스트라/주인공 통합 엔트리.
    /// - 헤더 컬럼은 항상 너비를 점유 (대화 컬럼 위치 일관성)
    /// - 초상화 없음 → portraitContainer 비활성 (네임박스만 단락 중앙)
    /// - isUser → 주인공용 네임박스/말풍선 스프라이트로 교체
    /// </summary>
    public class LogDialogueEntry : LogEntryBase
    {
        [Header("헤더 — 초상화")]
        [SerializeField] GameObject portraitContainer;  // Portrait GO만 토글
        [SerializeField] Image portraitImage;

        [Header("헤더 — 네임박스")]
        [SerializeField] RectTransform nameBoxRect;
        [SerializeField] LayoutElement nameBoxLayout;
        [SerializeField] Image nameBoxImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] Sprite characterNameBoxSprite;
        [SerializeField] Sprite userNameBoxSprite;
        [SerializeField] Vector2 characterNameBoxSize = new(208f, 99f);
        [SerializeField] Vector2 userNameBoxSize = new(218f, 100f);
        [SerializeField] Color characterNameColor = new(0.92f, 0.45f, 0.65f, 1f);
        [SerializeField] Color userNameColor = Color.white;

        [Header("주인공 전용 버블 (옵션)")]
        [SerializeField] GameObject userBubbleTemplate;

        bool useUserBubble;

        public override void Init(string speaker, Sprite portrait, bool isUser)
        {
            useUserBubble = isUser;

            if (nameText != null)
            {
                nameText.text = speaker;
                nameText.color = isUser ? userNameColor : characterNameColor;
            }

            if (nameBoxImage != null)
            {
                var s = isUser ? userNameBoxSprite : characterNameBoxSprite;
                if (s != null) nameBoxImage.sprite = s;
            }

            var size = isUser ? userNameBoxSize : characterNameBoxSize;
            if (nameBoxRect != null) nameBoxRect.sizeDelta = size;
            if (nameBoxLayout != null)
            {
                nameBoxLayout.preferredWidth = size.x;
                nameBoxLayout.preferredHeight = size.y;
            }

            bool hasPortrait = !isUser && portrait != null && portraitImage != null;
            if (portraitImage != null) portraitImage.sprite = portrait;
            if (portraitContainer != null) portraitContainer.SetActive(hasPortrait);
        }

        protected override GameObject ResolveBubbleTemplate()
            => useUserBubble && userBubbleTemplate != null ? userBubbleTemplate : bubbleTemplate;
    }
}
