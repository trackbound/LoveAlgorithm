using System;
using System.Collections.Generic;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 메신저 세이브 데이터.
    /// C4-Phase A Group H에서 LoveAlgo.Phone → LoveAlgo.Contracts 로 이동.
    /// </summary>
    [Serializable]
    public class MessengerSaveData
    {
        /// <summary>대화방별 메시지 기록</summary>
        public Dictionary<string, List<ChatMessage>> ChatRooms = new();

        /// <summary>안 읽은 메시지 수</summary>
        public Dictionary<string, int> UnreadCounts = new();

        /// <summary>상태 메시지</summary>
        public Dictionary<string, string> StatusMessages = new();
    }
}
