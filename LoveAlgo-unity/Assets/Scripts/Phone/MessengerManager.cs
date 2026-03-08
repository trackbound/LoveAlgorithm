using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 메신저 관리자 (정적 클래스)
    /// 
    /// 기능:
    ///   - 친구 목록 관리
    ///   - 대화방/메시지 관리
    ///   - 스토리 진행에 따른 자동 메시지 발송
    ///   - 안 읽은 메시지 수 추적
    /// </summary>
    public static class MessengerManager
    {
        /// <summary>친구 프로필 목록</summary>
        static readonly Dictionary<string, FriendProfile> friends = new();

        /// <summary>대화방 목록 (heroineId → ChatRoom)</summary>
        static readonly Dictionary<string, ChatRoom> chatRooms = new();

        /// <summary>새 메시지 도착 이벤트 (UI 갱신용)</summary>
        public static event Action<string> OnNewMessage;

        /// <summary>전체 안 읽은 메시지 수 변경 이벤트</summary>
        public static event Action<int> OnUnreadCountChanged;

        static MessengerManager()
        {
            Reset();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload() => Reset();

        /// <summary>초기화 (새 게임 시)</summary>
        public static void Reset()
        {
            friends.Clear();
            chatRooms.Clear();

            // 정적 이벤트 클리어 — 파괴된 구독자 방지
            OnNewMessage = null;
            OnUnreadCountChanged = null;

            // 기본 친구 등록
            RegisterFriend("Roa", "로아", "화면 너머에서 응원 중! ✨");
            RegisterFriend("Yeun", "하예은", "오늘도 운동 완료 💪");
            RegisterFriend("Daeun", "서다은", "…");
            RegisterFriend("Bom", "이봄", "오늘 뭐 먹지~? 🍕");
            RegisterFriend("Heewon", "도희원", "");
        }

        #region 친구 관리

        /// <summary>친구 등록</summary>
        static void RegisterFriend(string heroineId, string name, string status,
            string imagePath = null)
        {
            friends[heroineId] = new FriendProfile(heroineId, name, status, imagePath);

            if (!chatRooms.ContainsKey(heroineId))
                chatRooms[heroineId] = new ChatRoom(heroineId, name);
        }

        /// <summary>친구 프로필 조회</summary>
        public static FriendProfile GetFriend(string heroineId)
        {
            return friends.GetValueOrDefault(heroineId);
        }

        /// <summary>전체 친구 목록</summary>
        public static IReadOnlyCollection<FriendProfile> GetAllFriends()
        {
            return friends.Values;
        }

        /// <summary>상태 메시지 변경</summary>
        public static void UpdateStatus(string heroineId, string newStatus)
        {
            if (friends.TryGetValue(heroineId, out var profile))
                profile.StatusMessage = newStatus;
        }

        #endregion

        #region 메시지 전송

        /// <summary>
        /// 히로인으로부터 메시지 수신 (스토리/이벤트에서 호출)
        /// </summary>
        public static void ReceiveMessage(string heroineId, string text, int day, string timestamp = null)
        {
            if (!chatRooms.ContainsKey(heroineId))
            {
                var friendName = friends.ContainsKey(heroineId) ? friends[heroineId].DisplayName : heroineId;
                chatRooms[heroineId] = new ChatRoom(heroineId, friendName);
            }

            var msg = new ChatMessage(MessageSender.Other, text, day,
                timestamp ?? GetDefaultTimestamp());

            chatRooms[heroineId].AddMessage(msg);

            Debug.Log($"[Messenger] {heroineId} → \"{text}\" (Day {day})");
            OnNewMessage?.Invoke(heroineId);
            NotifyUnreadChanged();
        }

        /// <summary>
        /// 플레이어 메시지 전송 (대화방 내 선택지 등)
        /// </summary>
        public static void SendMessage(string heroineId, string text, int day)
        {
            if (!chatRooms.ContainsKey(heroineId)) return;

            var msg = new ChatMessage(MessageSender.Self, text, day, GetDefaultTimestamp());
            chatRooms[heroineId].AddMessage(msg, markUnread: false);
        }

        #endregion

        #region 대화방 관리

        /// <summary>대화방 조회</summary>
        public static ChatRoom GetChatRoom(string heroineId)
        {
            return chatRooms.GetValueOrDefault(heroineId);
        }

        /// <summary>
        /// 메시지가 있는 대화방 목록 (최근 메시지 순 정렬)
        /// </summary>
        public static IEnumerable<ChatRoom> GetActiveChatRooms()
        {
            return chatRooms.Values
                .Where(r => r.Messages.Count > 0)
                .OrderByDescending(r => r.Messages[^1].Day);
        }

        /// <summary>읽음 처리</summary>
        public static void MarkAsRead(string heroineId)
        {
            if (chatRooms.TryGetValue(heroineId, out var room))
            {
                room.MarkAsRead();
                NotifyUnreadChanged();
            }
        }

        /// <summary>전체 안 읽은 메시지 수</summary>
        public static int GetTotalUnreadCount()
        {
            return chatRooms.Values.Sum(r => r.UnreadCount);
        }

        #endregion

        #region 스토리 연동

        /// <summary>
        /// 일차별 자동 메시지 발송 (GameManager에서 호출)
        /// </summary>
        public static void TriggerDayMessages(int day)
        {
            // 데모용 기본 메시지들
            switch (day)
            {
                case 1:
                    ReceiveMessage("Roa", "입학 축하해! 🎉\n오늘부터 잘 부탁해~", day, "오전 8:00");
                    break;
                case 2:
                    ReceiveMessage("Yeun", "어제 고마웠어! 내일 만나면 인사해~", day, "오후 9:30");
                    break;
                case 3:
                    ReceiveMessage("Daeun", "수업 필기 빌려줄까?", day, "오후 1:15");
                    break;
                case 5:
                    ReceiveMessage("Bom", "이번 주말에 놀러 가자~! 🎡", day, "오후 3:00");
                    break;
                case 6:
                    ReceiveMessage("Roa", "오늘 이벤트가 있대! 확인해봐!", day, "오전 9:00");
                    break;
                case 9:
                    ReceiveMessage("Yeun", "내일부터 축제다! 기대된다~", day, "오후 8:00");
                    break;
                case 10:
                    ReceiveMessage("Roa", "축제 시작! 🎊 재밌게 놀아!", day, "오전 10:00");
                    break;
                case 15:
                    ReceiveMessage("Heewon", "…도서관 옆자리 비어있어.", day, "오후 2:00");
                    break;
                case 19:
                    ReceiveMessage("Bom", "MT 간다~! 신난다~! 🏕️", day, "오후 6:00");
                    break;
                case 25:
                    ReceiveMessage("Daeun", "…곧 종강이네.", day, "오후 11:00");
                    break;
                case 29:
                    ReceiveMessage("Roa", "내일이 마지막 날이야…\n후회 없이 보내!", day, "오후 10:00");
                    break;
            }
        }

        /// <summary>이벤트 완료 후 메시지 (이벤트에서 선택한 히로인이 보냄)</summary>
        public static void TriggerEventMessage(string heroineId, string eventTag, int day)
        {
            string text = eventTag switch
            {
                "Event1" => "오늘 같이 있어서 좋았어!",
                "Event2" => "다음에도 또 만나자 😊",
                "Event3" => "…정말 고마워.",
                "Festival_Day3" => "축제 피날레 재밌었어!",
                "MT_Day2" => "담력 시험 무섭긴 했는데… 같이여서 괜찮았어.",
                _ => null
            };

            if (text != null)
                ReceiveMessage(heroineId, text, day, "오후 11:00");
        }

        #endregion

        #region Save / Load

        /// <summary>세이브용 데이터</summary>
        public static MessengerSaveData GetSaveData()
        {
            var data = new MessengerSaveData();

            foreach (var kv in chatRooms)
            {
                data.ChatRooms[kv.Key] = new List<ChatMessage>(kv.Value.Messages);
                data.UnreadCounts[kv.Key] = kv.Value.UnreadCount;
            }

            foreach (var kv in friends)
            {
                data.StatusMessages[kv.Key] = kv.Value.StatusMessage;
            }

            return data;
        }

        /// <summary>로드 시 복원</summary>
        public static void RestoreFromSave(MessengerSaveData data)
        {
            Reset();
            if (data == null) return;

            foreach (var kv in data.ChatRooms)
            {
                if (!chatRooms.ContainsKey(kv.Key)) continue;
                chatRooms[kv.Key].Messages.Clear();
                chatRooms[kv.Key].Messages.AddRange(kv.Value);
            }

            if (data.UnreadCounts != null)
            {
                foreach (var kv in data.UnreadCounts)
                {
                    if (chatRooms.ContainsKey(kv.Key))
                        chatRooms[kv.Key].UnreadCount = kv.Value;
                }
            }

            if (data.StatusMessages != null)
            {
                foreach (var kv in data.StatusMessages)
                    UpdateStatus(kv.Key, kv.Value);
            }
        }

        #endregion

        static string GetDefaultTimestamp()
        {
            var hour = UnityEngine.Random.Range(8, 23);
            var min = UnityEngine.Random.Range(0, 60);
            string period = hour < 12 ? "오전" : "오후";
            int h12 = hour > 12 ? hour - 12 : hour;
            return $"{period} {h12}:{min:D2}";
        }

        static void NotifyUnreadChanged()
        {
            OnUnreadCountChanged?.Invoke(GetTotalUnreadCount());
        }
    }

    /// <summary>
    /// 메신저 세이브 데이터
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
