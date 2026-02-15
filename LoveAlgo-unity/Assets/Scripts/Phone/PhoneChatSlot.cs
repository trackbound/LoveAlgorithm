using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 채팅 목록 슬롯 (PhoneChatSlot 프리팹에 연결)
    /// </summary>
    public class PhoneChatSlot : MonoBehaviour
    {
        [SerializeField] Image profileImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text lastMessageText;
        [SerializeField] GameObject newBadge;
        [SerializeField] TMP_Text unreadCountText;
        [SerializeField] Button slotButton;

        string ownerId;
        Action<string> onClick;

        /// <summary>슬롯 설정</summary>
        public void Setup(ChatRoom room, Action<string> onClicked)
        {
            ownerId = room.OwnerId;
            onClick = onClicked;

            if (nameText != null) nameText.text = room.DisplayName;
            if (lastMessageText != null) lastMessageText.text = room.LastMessagePreview;

            // 안 읽은 메시지 뱃지
            bool hasUnread = room.UnreadCount > 0;
            if (newBadge != null) newBadge.SetActive(hasUnread);
            if (unreadCountText != null)
            {
                unreadCountText.gameObject.SetActive(hasUnread);
                unreadCountText.text = room.UnreadCount.ToString();
            }

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => onClick?.Invoke(ownerId));
            }
        }
    }
}
