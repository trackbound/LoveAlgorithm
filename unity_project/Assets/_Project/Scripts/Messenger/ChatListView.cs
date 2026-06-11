using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Core; // GameStateSO

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 채팅 탭 목록(*View) — 도착 시퀀스가 있는 방만, 마지막 말풍선 미리보기 + New 배지(기획서 채팅 탭).
    /// 방 순서는 친구 카탈로그 순서(고정 소수 인원이라 최근순 정렬은 과설계 게이트 — 필요 시 후속).
    /// </summary>
    public class ChatListView : MonoBehaviour
    {
        [SerializeField] Transform container;
        [SerializeField] ChatRoomSlot slotPrefab;
        [SerializeField] FriendCatalogSO friends;
        [SerializeField] MessengerScriptCatalogSO catalog;
        [SerializeField] GameStateSO state;

        public Transform Container { get => container; set => container = value; }
        public ChatRoomSlot SlotPrefab { get => slotPrefab; set => slotPrefab = value; }
        public FriendCatalogSO Friends { get => friends; set => friends = value; }
        public MessengerScriptCatalogSO Catalog { get => catalog; set => catalog = value; }
        public GameStateSO State { get => state; set => state = value; }

        /// <summary>방 클릭 통지 — MessengerView가 채팅창 열기로 배선.</summary>
        public event Action<string> RoomSelected;

        readonly List<ChatRoomSlot> _spawned = new();

        public void Refresh()
        {
            Clear();
            if (container == null || slotPrefab == null)
            {
                Debug.LogError("[ChatListView] container/slotPrefab 미바인딩 — 목록 표시 불가.");
                return;
            }
            if (friends == null || state == null) return;

            var entries = friends.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;

                var records = MessengerService.RoomRecords(state, e.id);
                if (records.Count == 0) continue; // 대화 없는 방은 채팅 탭에 안 뜸

                var slot = Instantiate(slotPrefab, container);
                slot.Bind(
                    e.id,
                    friends.DisplayName(e.id),
                    LastPreview(records),
                    MessengerService.HasUnread(state, e.id),
                    e.portrait,
                    id => RoomSelected?.Invoke(id));
                _spawned.Add(slot);
            }
        }

        /// <summary>방의 마지막 말풍선 텍스트(미리보기). 아직 본문이 없으면 빈 문자열.</summary>
        string LastPreview(List<GameStateData.MessengerSeqRecord> records)
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var entry = catalog != null ? catalog.Resolve(records[i].seqId) : null;
                if (entry == null) continue;
                var history = MessengerService.BuildHistory(MessengerScriptStore.Get(entry.csvPath), records[i]);
                if (history.Bubbles.Count > 0) return history.Bubbles[history.Bubbles.Count - 1].Text;
            }
            return "";
        }

        public void Clear()
        {
            foreach (var s in _spawned)
                if (s != null) Destroy(s.gameObject);
            _spawned.Clear();
        }

        void OnDisable() => Clear();
    }
}
