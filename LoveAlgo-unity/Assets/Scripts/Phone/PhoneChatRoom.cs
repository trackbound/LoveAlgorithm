using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 개별 채팅방 컨트롤러 (ChatRoomPanel에 연결)
    /// 
    /// 메시지 버블을 스크롤 가능한 리스트로 표시
    /// </summary>
    public class PhoneChatRoom : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] TMP_Text headerText;
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] Transform messageContainer;

        [Header("버블 프리팹")]
        [SerializeField] ChatBubble selfBubblePrefab;
        [SerializeField] ChatBubble otherBubblePrefab;

        readonly List<ChatBubble> activeBubbles = new();
        string currentHeroineId;

        /// <summary>채팅방 열기</summary>
        public void Open(string heroineId)
        {
            currentHeroineId = heroineId;

            // 헤더 설정
            var friend = MessengerManager.GetFriend(heroineId);
            if (headerText != null)
                headerText.text = friend?.DisplayName ?? heroineId;

            // 메시지 표시
            PopulateMessages();

            // 읽음 처리
            MessengerManager.MarkAsRead(heroineId);
        }

        void PopulateMessages()
        {
            // 기존 버블 제거
            foreach (var bubble in activeBubbles)
                if (bubble != null) Destroy(bubble.gameObject);
            activeBubbles.Clear();

            if (messageContainer == null) return;

            var room = MessengerManager.GetChatRoom(currentHeroineId);
            if (room == null) return;

            foreach (var msg in room.Messages)
            {
                var prefab = msg.Sender == MessageSender.Self ? selfBubblePrefab : otherBubblePrefab;
                if (prefab == null) continue;

                var bubble = Instantiate(prefab, messageContainer);
                bubble.Setup(msg);
                activeBubbles.Add(bubble);
            }

            // 스크롤 최하단
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
