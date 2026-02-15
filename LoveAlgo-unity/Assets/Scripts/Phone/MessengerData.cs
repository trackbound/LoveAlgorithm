using System;
using System.Collections.Generic;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 메시지 방향
    /// </summary>
    public enum MessageSender
    {
        Self,   // 플레이어 메시지
        Other   // 상대방 메시지
    }

    /// <summary>
    /// 채팅 메시지 데이터
    /// </summary>
    [Serializable]
    public class ChatMessage
    {
        public MessageSender Sender;
        public string Text;
        public int Day;             // 수신 일차
        public string Timestamp;    // 표시용 시간 (예: "오전 10:32")

        public ChatMessage(MessageSender sender, string text, int day, string timestamp = null)
        {
            Sender = sender;
            Text = text;
            Day = day;
            Timestamp = timestamp ?? "";
        }
    }

    /// <summary>
    /// 대화방 데이터
    /// </summary>
    [Serializable]
    public class ChatRoom
    {
        /// <summary>히로인 ID (또는 "Group" 등)</summary>
        public string OwnerId;

        /// <summary>대화방 이름 (표시용)</summary>
        public string DisplayName;

        /// <summary>메시지 목록</summary>
        public List<ChatMessage> Messages = new();

        /// <summary>읽지 않은 메시지 수</summary>
        public int UnreadCount;

        /// <summary>마지막 메시지 텍스트 (채팅 리스트 미리보기용)</summary>
        public string LastMessagePreview =>
            Messages.Count > 0 ? Messages[^1].Text : "";

        public ChatRoom(string ownerId, string displayName)
        {
            OwnerId = ownerId;
            DisplayName = displayName;
        }

        /// <summary>메시지 추가</summary>
        public void AddMessage(ChatMessage msg, bool markUnread = true)
        {
            Messages.Add(msg);
            if (markUnread && msg.Sender == MessageSender.Other)
                UnreadCount++;
        }

        /// <summary>읽음 처리</summary>
        public void MarkAsRead()
        {
            UnreadCount = 0;
        }
    }

    /// <summary>
    /// 친구 프로필 데이터
    /// </summary>
    [Serializable]
    public class FriendProfile
    {
        public string HeroineId;
        public string DisplayName;
        public string StatusMessage;
        public string ProfileImagePath; // Resources 경로

        public FriendProfile(string id, string name, string status, string imagePath = null)
        {
            HeroineId = id;
            DisplayName = name;
            StatusMessage = status;
            ProfileImagePath = imagePath;
        }
    }
}
