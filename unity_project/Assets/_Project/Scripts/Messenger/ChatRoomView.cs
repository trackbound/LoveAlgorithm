using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO, GameStateData
using LoveAlgo.Events; // FlowCommandRequestedEvent, MessengerSequenceReadEvent

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 채팅창(*View) — 방의 도착 시퀀스들을 말풍선 이력으로 렌더하고, 응답 대기 선택지를 하단에 표시한다
    /// (기획서: 개인톡/단톡 동일, 이력 전체 스크롤, 선택지는 독립 영역).
    /// 선택 → 기록(MessengerService) + 효과를 Flow 명령으로 발행(EventBus 너머 FlowCommandController가 적용)
    /// → 전체 리렌더. 시퀀스를 끝까지 소비하면 읽음 처리 + <see cref="MessengerSequenceReadEvent"/> 발행
    /// (컨트롤러가 "확인 필수" 대기 핸들 완료). 표시만 책임 — 해석/적용은 순수층과 Flow 구독자 몫(ADR-007).
    /// </summary>
    public class ChatRoomView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Transform bubbleContainer;
        [Tooltip("상대 말풍선 프리팹(좌측, 발신자명 표시).")]
        [SerializeField] MessengerBubble bubbleInPrefab;
        [Tooltip("내 말풍선 프리팹(우측).")]
        [SerializeField] MessengerBubble bubbleOutPrefab;
        [SerializeField] Transform optionContainer;
        [SerializeField] MessengerOptionSlot optionPrefab;
        [SerializeField] GameStateSO state;
        [SerializeField] MessengerScriptCatalogSO catalog;
        [SerializeField] FriendCatalogSO friends;

        public GameObject Root { get => root; set => root = value; }
        public Transform BubbleContainer { get => bubbleContainer; set => bubbleContainer = value; }
        public MessengerBubble BubbleInPrefab { get => bubbleInPrefab; set => bubbleInPrefab = value; }
        public MessengerBubble BubbleOutPrefab { get => bubbleOutPrefab; set => bubbleOutPrefab = value; }
        public Transform OptionContainer { get => optionContainer; set => optionContainer = value; }
        public MessengerOptionSlot OptionPrefab { get => optionPrefab; set => optionPrefab = value; }
        public GameStateSO State { get => state; set => state = value; }
        public MessengerScriptCatalogSO Catalog { get => catalog; set => catalog = value; }
        public FriendCatalogSO Friends { get => friends; set => friends = value; }

        public string CurrentRoomId { get; private set; }

        readonly List<GameObject> _spawned = new();
        GameStateData.MessengerSeqRecord _pendingRecord; // 현재 하단 선택지가 속한 시퀀스
        MessengerLine _pendingChoice;

        /// <summary>방 열기/리렌더. 도착 순서대로 시퀀스 이력을 쌓고, 첫 미답 선택지에서 멈춘다
        /// (이전 시퀀스 응답 전에 다음 시퀀스가 먼저 보이면 대화 순서가 깨지므로).</summary>
        public void Show(string roomId)
        {
            CurrentRoomId = roomId;
            if (root != null) root.SetActive(true);
            Rebuild();
        }

        public void Hide()
        {
            CurrentRoomId = null;
            Clear();
            if (root != null) root.SetActive(false);
        }

        void Rebuild()
        {
            Clear();
            if (state == null || catalog == null || string.IsNullOrEmpty(CurrentRoomId)) return;

            var records = MessengerService.RoomRecords(state, CurrentRoomId);
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var entry = catalog.Resolve(record.seqId);
                if (entry == null)
                {
                    Log.Warn($"[ChatRoomView] 기록된 시퀀스 '{record.seqId}'가 카탈로그에 없음 — 건너뜀.");
                    continue;
                }

                var history = MessengerService.BuildHistory(MessengerScriptStore.Get(entry.csvPath), record);
                SpawnBubbles(history);

                if (history.PendingChoice != null)
                {
                    _pendingRecord = record;
                    _pendingChoice = history.PendingChoice;
                    SpawnOptions(history.PendingChoice);
                    return; // 미답 선택지에서 정지 — 이후 시퀀스는 응답 후 렌더
                }

                // 끝까지 소비된 시퀀스 — 열람했으니 읽음 처리(+대기 핸들 완료 통지).
                if (MessengerService.MarkRead(state, record.seqId))
                    EventBus.Publish(new MessengerSequenceReadEvent(record.seqId));
            }
        }

        void SpawnBubbles(MessengerHistory history)
        {
            if (bubbleContainer == null) return;
            for (int i = 0; i < history.Bubbles.Count; i++)
            {
                var b = history.Bubbles[i];
                var prefab = b.IsMine ? bubbleOutPrefab : bubbleInPrefab;
                if (prefab == null) continue;
                var bubble = Instantiate(prefab, bubbleContainer);
                string sender = b.IsMine ? "" : (friends != null ? friends.DisplayName(b.SenderId) : b.SenderId);
                bubble.Bind(sender, b.Text);
                _spawned.Add(bubble.gameObject);
            }
        }

        void SpawnOptions(MessengerLine choice)
        {
            if (optionContainer == null || optionPrefab == null)
            {
                Debug.LogError("[ChatRoomView] optionContainer/optionPrefab 미바인딩 — 선택지 표시 불가.");
                return;
            }
            // 조건(if:) 평가는 메신저 콘텐츠에 소비처가 생길 때 후속(현재 기획 시퀀스는 무조건 노출).
            for (int i = 0; i < choice.Options.Count; i++)
            {
                var slot = Instantiate(optionPrefab, optionContainer);
                slot.Bind(i, choice.Options[i].Text, OnOptionSelected);
                _spawned.Add(slot.gameObject);
            }
        }

        void OnOptionSelected(int index)
        {
            if (_pendingRecord == null || _pendingChoice == null) return;
            if (index < 0 || index >= _pendingChoice.Options.Count) return;

            MessengerService.RecordChoice(state, _pendingRecord.seqId, index);

            // 효과 → Flow 명령 발행(Love→Affinity Dialogue 위임, 적용은 FlowCommandController).
            var commands = MessengerEffectMapper.ToFlowCommands(_pendingChoice.Options[index].Effects);
            for (int i = 0; i < commands.Count; i++)
                EventBus.Publish(new FlowCommandRequestedEvent(commands[i]));

            Rebuild(); // 내 말풍선 반영 + 다음 진행(완주 시 읽음 처리 포함)
        }

        void Clear()
        {
            _pendingRecord = null;
            _pendingChoice = null;
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
        }

        void OnDisable() => Clear();
    }
}
