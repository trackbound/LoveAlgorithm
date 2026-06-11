using System.Collections.Generic;
using LoveAlgo.Core; // GameStateSO, GameStateData

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 상태 결정 로직(순수 static, EventBus 무관 — EditMode 테스트).
    /// 세이브는 "시퀀스 id + 도착일 + 읽음 + 선택 인덱스"만 보유하고(GameStateData.messengerSeqs),
    /// 대화 이력은 시퀀스 CSV 정의에서 재구성한다 — 작가가 텍스트를 고쳐도 세이브 호환(감독 결정 2026-06-11).
    /// EventBus 발행·CSV 로딩은 어댑터(컨트롤러) 몫.
    /// </summary>
    public static class MessengerService
    {
        /// <summary>
        /// 시퀀스 도착 처리. 이미 도착했으면 무시(false) — 일자 자동 + 스토리 트리거가 겹쳐도 안전.
        /// roomId는 카탈로그 정의에서 받아 레코드에 비정규화한다(세이브 자족).
        /// </summary>
        public static bool Deliver(GameStateSO gs, string seqId, string roomId, int day)
        {
            if (gs == null || string.IsNullOrEmpty(seqId)) return false;
            if (FindRecord(gs, seqId) != null) return false;

            gs.Data.messengerSeqs.Add(new GameStateData.MessengerSeqRecord
            {
                seqId = seqId,
                roomId = roomId ?? "",
                deliveredDay = day,
                read = false
            });
            return true;
        }

        public static bool IsDelivered(GameStateSO gs, string seqId) => FindRecord(gs, seqId) != null;

        /// <summary>읽음 처리. 미도착이거나 이미 읽었으면 false(변경 없음).</summary>
        public static bool MarkRead(GameStateSO gs, string seqId)
        {
            var record = FindRecord(gs, seqId);
            if (record == null || record.read) return false;
            record.read = true;
            return true;
        }

        /// <summary>선택지 그룹 1개의 선택 결과를 기록(등장 순 append). 미도착 시 무시.</summary>
        public static bool RecordChoice(GameStateSO gs, string seqId, int optionIndex)
        {
            var record = FindRecord(gs, seqId);
            if (record == null || optionIndex < 0) return false;
            record.choices.Add(optionIndex);
            return true;
        }

        /// <summary>안 읽은 시퀀스 수(폰 버튼 배지·채팅 탭 New 아이콘 입력).</summary>
        public static int UnreadCount(GameStateSO gs)
        {
            if (gs == null) return 0;
            int count = 0;
            var list = gs.Data.messengerSeqs;
            for (int i = 0; i < list.Count; i++)
                if (!list[i].read) count++;
            return count;
        }

        /// <summary>해당 방에 안 읽은 시퀀스가 있는가(채팅 리스트 New 배지).</summary>
        public static bool HasUnread(GameStateSO gs, string roomId)
        {
            if (gs == null || string.IsNullOrEmpty(roomId)) return false;
            var list = gs.Data.messengerSeqs;
            for (int i = 0; i < list.Count; i++)
                if (!list[i].read && list[i].roomId == roomId) return true;
            return false;
        }

        /// <summary>방의 도착 시퀀스 기록(도착 순서 = 리스트 순서 보존 — 채팅방 이력 재구성 입력).</summary>
        public static List<GameStateData.MessengerSeqRecord> RoomRecords(GameStateSO gs, string roomId)
        {
            var records = new List<GameStateData.MessengerSeqRecord>();
            if (gs == null || string.IsNullOrEmpty(roomId)) return records;
            var list = gs.Data.messengerSeqs;
            for (int i = 0; i < list.Count; i++)
                if (list[i].roomId == roomId) records.Add(list[i]);
            return records;
        }

        public static GameStateData.MessengerSeqRecord FindRecord(GameStateSO gs, string seqId)
        {
            if (gs == null || string.IsNullOrEmpty(seqId)) return null;
            var list = gs.Data.messengerSeqs;
            for (int i = 0; i < list.Count; i++)
                if (list[i].seqId == seqId) return list[i];
            return null;
        }

        /// <summary>
        /// 시퀀스 정의 + 기록 → 말풍선 이력 재구성(순수). 기록된 선택은 "내 말풍선"으로 변환되고,
        /// 아직 안 고른 선택지 그룹을 만나면 거기서 멈춘다(<see cref="MessengerHistory.PendingChoice"/>) —
        /// 선택 대기 상태로 닫았다 재진입해도 이어서 진행(기획서: 지난 대화 유지 + 끝까지 스크롤).
        /// </summary>
        public static MessengerHistory BuildHistory(IReadOnlyList<MessengerLine> lines, GameStateData.MessengerSeqRecord record)
        {
            var history = new MessengerHistory();
            if (lines == null) { history.Completed = true; return history; }

            int choiceCursor = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                switch (line.Kind)
                {
                    case MessengerLineKind.Message:
                        history.Bubbles.Add(new MessengerHistory.Bubble(line.SenderId, line.Text, isMine: false));
                        break;

                    case MessengerLineKind.MyMessage:
                        history.Bubbles.Add(new MessengerHistory.Bubble("", line.Text, isMine: true));
                        break;

                    case MessengerLineKind.Choice:
                        int recorded = ChoiceAt(record, choiceCursor);
                        if (recorded < 0)
                        {
                            history.PendingChoice = line; // 선택 대기 — 여기서 멈춤
                            return history;
                        }
                        choiceCursor++;
                        // 정의가 바뀌어 인덱스가 범위를 벗어나면 마지막 옵션으로 보정(세이브 호환 안전망).
                        int safe = recorded < line.Options.Count ? recorded : line.Options.Count - 1;
                        history.Bubbles.Add(new MessengerHistory.Bubble("", line.Options[safe].Text, isMine: true));
                        break;
                }
            }
            history.Completed = true;
            return history;
        }

        static int ChoiceAt(GameStateData.MessengerSeqRecord record, int cursor)
        {
            if (record == null || record.choices == null || cursor >= record.choices.Count) return -1;
            return record.choices[cursor];
        }
    }

    /// <summary>이력 재구성 결과 — 말풍선 목록 + (있다면) 현재 응답 대기 중인 선택지 그룹.</summary>
    public sealed class MessengerHistory
    {
        public readonly List<Bubble> Bubbles = new();
        /// <summary>응답 대기 중인 선택지 그룹(null=대기 없음).</summary>
        public MessengerLine PendingChoice;
        /// <summary>시퀀스 끝까지 소비됨(선택 대기 없음).</summary>
        public bool Completed;

        public readonly struct Bubble
        {
            public readonly string SenderId; // 빈 문자열=주인공
            public readonly string Text;
            public readonly bool IsMine;
            public Bubble(string senderId, string text, bool isMine)
            {
                SenderId = senderId; Text = text; IsMine = isMine;
            }
        }
    }
}
