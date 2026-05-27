using System;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 메시지 방향.
    /// C4-Phase A Group H에서 LoveAlgo.Phone → LoveAlgo.Contracts 로 이동 (ChatMessage sub-dep).
    /// </summary>
    public enum MessageSender
    {
        Self,   // 플레이어 메시지
        Other   // 상대방 메시지
    }

    /// <summary>
    /// 채팅 메시지 데이터.
    /// C4-Phase A Group H에서 LoveAlgo.Phone → LoveAlgo.Contracts 로 이동 (MessengerSaveData sub-dep).
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
}
