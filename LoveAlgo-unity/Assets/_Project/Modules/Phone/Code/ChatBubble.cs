using TMPro;
using UnityEngine;
using LoveAlgo.Contracts;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 채팅 버블 (SelfBubble / OtherBubble 프리팹에 연결)
    /// </summary>
    public class ChatBubble : MonoBehaviour
    {
        [SerializeField] TMP_Text messageText;
        [SerializeField] TMP_Text timestampText;

        /// <summary>메시지 설정</summary>
        public void Setup(ChatMessage msg)
        {
            if (messageText != null) messageText.text = msg.Text;
            if (timestampText != null) timestampText.text = msg.Timestamp;
        }
    }
}
