#if UNITY_EDITOR
using UnityEngine;

namespace LoveAlgo.PhoneEditor
{
    /// <summary>
    /// 채팅창/메신저 UI 디자인 상수 (목업 대조용 라이브 튜닝).
    /// ChatBubbleBuilder + PhoneUIDesignerWindow가 참조.
    /// </summary>
    [CreateAssetMenu(fileName = "PhoneUIDesignConfig", menuName = "LoveAlgo/Phone UI Design Config")]
    public class PhoneUIDesignConfig : ScriptableObject
    {
        [Header("ChatBubble — 사이즈/패딩")]
        public float maxBubbleWidth = 350f;
        public int bubblePadLeft = 18;
        public int bubblePadRight = 18;
        public int bubblePadTop = 12;
        public int bubblePadBottom = 12;
        public float bubbleSpacing = 8f;       // 메시지 간 간격 (PhoneChatRoom VLG.spacing)

        [Header("ChatBubble — 텍스트")]
        public float textFontSize = 18f;
        public float characterSpacing = 0f;
        public float lineSpacing = 0f;
        public Color otherTextColor = new(0.13f, 0.13f, 0.13f, 1f);
        public Color selfTextColor = Color.white;

        [Header("ChatBubble — 시간/이름 (옵션)")]
        public float timestampFontSize = 12f;
        public Color timestampColor = new(0.55f, 0.55f, 0.55f, 1f);

        [Header("탭/사이드바 (기획서 폰트)")]
        public float tabFontSize = 23f;           // 어그로 미디움 23
        public Color tabOffColor = new(1f, 0.843f, 0.922f, 1f);    // #ffd7eb
        public Color tabOnColor = Color.white;
        public Color tabShadowColor = new(1f, 0.4f, 0.616f, 0.7f); // #ff669d 70%
        public float titleFontSize = 25f;         // 친구/채팅 타이틀
        public Color titleColor = new(1f, 0.6f, 0.71f, 1f);        // #ff99b6
        public float actionButtonFontSize = 15f;  // 적용/나가기/편집

        [Header("ChatRoom — 채팅창 내부")]
        public int chatRoomPadLeft = 16;
        public int chatRoomPadRight = 16;
        public int chatRoomPadTop = 16;
        public int chatRoomPadBottom = 16;

        [Header("FriendList/ChatList Item")]
        public float profileImageSize = 60f;
        public float listItemHeight = 80f;
        public int listItemPadLeft = 16;
        public float displayNameFontSize = 18f;
        public float statusMessageFontSize = 14f;

        [Header("PhonePopup — 전체 레이아웃")]
        public Vector2 mainPanelSize = new(1100f, 720f);
        public float sidebarWidth = 90f;
        public float listColumnWidth = 380f;
        // rightPanelWidth는 나머지 (auto)
        public Color sidebarColor = new(1f, 0.78f, 0.86f, 1f);
        public Color listBgColor = Color.white;
        public Color rightPanelTopColor = new(1f, 0.6f, 0.78f, 1f);     // ProfilePanel/ChatRoom 상단 핑크
        public Color rightPanelBottomColor = new(1f, 0.85f, 0.78f, 1f); // 하단 살구색 그라데이션 효과용 (옵션)
        public Color dimColor = new(0f, 0f, 0f, 0.5f);
    }
}
#endif
