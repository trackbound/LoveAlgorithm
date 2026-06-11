using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Core; // GameStateSO

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 친구 탭 목록(*View) — 최상단 플레이어 행 + 카탈로그 순서대로 친구 행(기획서 친구 탭).
    /// 플레이어 행은 이름=playerName, 상메=메신저 프로필 상태메시지(빈 값이면 기본 문구).
    /// 클릭 통지는 <see cref="FriendClicked"/>로 위임(프로필 패널은 후속 슬라이스).
    /// </summary>
    public class FriendListView : MonoBehaviour
    {
        public const string PlayerRowId = "player"; // 카탈로그 친구 id와 충돌하지 않는 예약 id

        [SerializeField] Transform container;
        [SerializeField] FriendSlot slotPrefab;
        [SerializeField] FriendCatalogSO friends;
        [SerializeField] GameStateSO state;
        [Tooltip("상태메시지 미설정 시 기본 문구(기획서 '기본 상태메시지').")]
        [SerializeField] string defaultPlayerStatus = "상태 메세지입니다.";

        public Transform Container { get => container; set => container = value; }
        public FriendSlot SlotPrefab { get => slotPrefab; set => slotPrefab = value; }
        public FriendCatalogSO Friends { get => friends; set => friends = value; }
        public GameStateSO State { get => state; set => state = value; }

        /// <summary>행 클릭 통지(플레이어 행은 <see cref="PlayerRowId"/>). 소비처: 프로필 패널(후속).</summary>
        public event Action<string> FriendClicked;

        readonly List<FriendSlot> _spawned = new();

        public void Refresh()
        {
            Clear();
            if (container == null || slotPrefab == null)
            {
                Debug.LogError("[FriendListView] container/slotPrefab 미바인딩 — 목록 표시 불가.");
                return;
            }

            // 플레이어 행(최상단 고정 — 기획서 친구 탭 구조)
            string playerName = state != null ? state.Data.playerName : "";
            string playerStatus = state != null && !string.IsNullOrEmpty(state.Data.messengerStatusMessage)
                ? state.Data.messengerStatusMessage
                : defaultPlayerStatus;
            Spawn(PlayerRowId, playerName, playerStatus, null);

            if (friends == null) return;
            var entries = friends.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                Spawn(e.id, friends.DisplayName(e.id), e.defaultStatus, e.portrait);
            }
        }

        void Spawn(string id, string displayName, string status, Sprite portrait)
        {
            var slot = Instantiate(slotPrefab, container);
            slot.Bind(id, displayName, status, portrait, OnSlotClicked);
            _spawned.Add(slot);
        }

        void OnSlotClicked(string id) => FriendClicked?.Invoke(id);

        public void Clear()
        {
            foreach (var s in _spawned)
                if (s != null) Destroy(s.gameObject);
            _spawned.Clear();
        }

        void OnDisable() => Clear();
    }
}
