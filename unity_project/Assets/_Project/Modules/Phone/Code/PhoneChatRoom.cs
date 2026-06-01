using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 개별 채팅방 컨트롤러 (ChatRoomPanel에 연결).
    /// 메시지 버블 스크롤 + 헤더(이름/상메) + 새 메시지 동적 추가.
    /// </summary>
    public class PhoneChatRoom : MonoBehaviour
    {
        [Header("헤더")]
        [SerializeField] TMP_Text headerNameText;
        [SerializeField] TMP_Text headerStatusText;
        [SerializeField] Image headerProfileImage;

        [Header("메시지")]
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] Transform messageContainer;

        [Header("버블 프리팹")]
        [SerializeField] ChatBubble selfBubblePrefab;
        [SerializeField] ChatBubble otherBubblePrefab;

        readonly List<ChatBubble> activeBubbles = new();
        string currentHeroineId;

        public string CurrentHeroineId => currentHeroineId;

        /// <summary>채팅방 열기 — 전체 메시지 로드</summary>
        public void Open(string heroineId)
        {
            currentHeroineId = heroineId;

            // 헤더
            var friend = MessengerSystem.GetFriend(heroineId);
            if (headerNameText != null) headerNameText.text = friend?.DisplayName ?? heroineId;
            if (headerStatusText != null) headerStatusText.text = friend?.StatusMessage ?? "";
            if (headerProfileImage != null && friend != null && !string.IsNullOrEmpty(friend.ProfileImagePath))
            {
                var sprite = Resources.Load<Sprite>(friend.ProfileImagePath);
                if (sprite != null) headerProfileImage.sprite = sprite;
            }

            PopulateMessages();
            MessengerSystem.MarkAsRead(heroineId);
        }

        void PopulateMessages()
        {
            // 기존 버블 제거
            foreach (var bubble in activeBubbles)
                if (bubble != null) Destroy(bubble.gameObject);
            activeBubbles.Clear();

            if (messageContainer == null) return;

            var room = MessengerSystem.GetChatRoom(currentHeroineId);
            if (room == null) return;

            foreach (var msg in room.Messages)
                SpawnBubble(msg);

            ScrollToBottomDelayed();
        }

        /// <summary>외부 신규 메시지 1개 추가 (PhonePopup.OnExternalNewMessage에서 호출)</summary>
        public void AppendLatestMessage()
        {
            if (string.IsNullOrEmpty(currentHeroineId)) return;
            var room = MessengerSystem.GetChatRoom(currentHeroineId);
            if (room == null || room.Messages.Count == 0) return;

            var latest = room.Messages[room.Messages.Count - 1];
            SpawnBubble(latest);
            ScrollToBottomDelayed();
        }

        void SpawnBubble(ChatMessage msg)
        {
            var prefab = msg.Sender == MessageSender.Self ? selfBubblePrefab : otherBubblePrefab;
            if (prefab == null) return;
            var bubble = Instantiate(prefab, messageContainer);
            bubble.Setup(msg);
            activeBubbles.Add(bubble);
        }

        void ScrollToBottomDelayed()
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
