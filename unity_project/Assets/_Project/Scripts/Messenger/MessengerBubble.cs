using TMPro;
using UnityEngine;

namespace LoveAlgo.Messenger
{
    /// <summary>채팅 말풍선 1개(상대/내 것 공용 컴포넌트 — 좌우 배치/색은 프리팹 2종이 가른다).</summary>
    public class MessengerBubble : MonoBehaviour
    {
        [SerializeField] TMP_Text messageText;
        [SerializeField] TMP_Text senderText; // 상대 말풍선만 사용(옵션)

        public TMP_Text MessageText { get => messageText; set => messageText = value; }
        public TMP_Text SenderText { get => senderText; set => senderText = value; }

        public void Bind(string senderDisplay, string message)
        {
            if (senderText != null) senderText.text = senderDisplay ?? "";
            if (messageText != null) messageText.text = message;
        }
    }
}
