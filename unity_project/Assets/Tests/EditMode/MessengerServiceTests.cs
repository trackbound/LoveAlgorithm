using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.Messenger;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 메신저 상태 결정 로직(순수) 검증 — 도착 중복 무시, 읽음, 선택 기록, 미읽음 집계,
    /// 그리고 핵심인 이력 재구성(기록 선택 소비·미답 선택지 정지·범위 밖 인덱스 보정).
    /// </summary>
    public class MessengerServiceTests
    {
        GameStateSO _gs;

        [SetUp]
        public void SetUp() => _gs = ScriptableObject.CreateInstance<GameStateSO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gs);

        [Test]
        public void Deliver_Ignores_Duplicate()
        {
            Assert.IsTrue(MessengerService.Deliver(_gs, "Seq1", "roa", 3));
            Assert.IsFalse(MessengerService.Deliver(_gs, "Seq1", "roa", 4)); // 자동+스토리 중복 트리거 안전
            Assert.AreEqual(1, _gs.Data.messengerSeqs.Count);
            Assert.AreEqual(3, _gs.Data.messengerSeqs[0].deliveredDay);
        }

        [Test]
        public void MarkRead_Only_Once_And_Only_When_Delivered()
        {
            Assert.IsFalse(MessengerService.MarkRead(_gs, "Seq1")); // 미도착
            MessengerService.Deliver(_gs, "Seq1", "roa", 1);
            Assert.IsTrue(MessengerService.MarkRead(_gs, "Seq1"));
            Assert.IsFalse(MessengerService.MarkRead(_gs, "Seq1")); // 이미 읽음
        }

        [Test]
        public void Unread_Count_And_Room_Badge()
        {
            MessengerService.Deliver(_gs, "Seq1", "roa", 1);
            MessengerService.Deliver(_gs, "Seq2", "c01", 1);
            Assert.AreEqual(2, MessengerService.UnreadCount(_gs));
            Assert.IsTrue(MessengerService.HasUnread(_gs, "roa"));

            MessengerService.MarkRead(_gs, "Seq1");
            Assert.AreEqual(1, MessengerService.UnreadCount(_gs));
            Assert.IsFalse(MessengerService.HasUnread(_gs, "roa"));
            Assert.IsTrue(MessengerService.HasUnread(_gs, "c01"));
        }

        [Test]
        public void RoomRecords_Filters_By_Room_In_Arrival_Order()
        {
            MessengerService.Deliver(_gs, "Seq1", "roa", 1);
            MessengerService.Deliver(_gs, "Seq2", "c01", 2);
            MessengerService.Deliver(_gs, "Seq3", "roa", 5);

            var records = MessengerService.RoomRecords(_gs, "roa");
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual("Seq1", records[0].seqId);
            Assert.AreEqual("Seq3", records[1].seqId);
        }

        [Test]
        public void RecordChoice_Appends_In_Order()
        {
            MessengerService.Deliver(_gs, "Seq1", "roa", 1);
            Assert.IsTrue(MessengerService.RecordChoice(_gs, "Seq1", 1));
            Assert.IsTrue(MessengerService.RecordChoice(_gs, "Seq1", 0));
            CollectionAssert.AreEqual(new[] { 1, 0 }, MessengerService.FindRecord(_gs, "Seq1").choices);
            Assert.IsFalse(MessengerService.RecordChoice(_gs, "없는시퀀스", 0));
        }

        // ── 이력 재구성 ──

        static List<MessengerLine> DemoLines() => new()
        {
            new MessengerLine { Kind = MessengerLineKind.Message, SenderId = "roa", Text = "갈래?" },
            new MessengerLine
            {
                Kind = MessengerLineKind.Choice,
                Options = new List<MessengerOption>
                {
                    new() { Text = "좋아!" },
                    new() { Text = "글쎄…" }
                }
            },
            new MessengerLine { Kind = MessengerLineKind.MyMessage, Text = "기대된다" },
        };

        [Test]
        public void BuildHistory_Stops_At_Unanswered_Choice()
        {
            var record = new GameStateData.MessengerSeqRecord { seqId = "Seq1" };

            var history = MessengerService.BuildHistory(DemoLines(), record);

            Assert.AreEqual(1, history.Bubbles.Count); // 첫 메시지까지만
            Assert.IsNotNull(history.PendingChoice);
            Assert.IsFalse(history.Completed);
            Assert.AreEqual(2, history.PendingChoice.Options.Count);
        }

        [Test]
        public void BuildHistory_Consumes_Recorded_Choice_As_My_Bubble()
        {
            var record = new GameStateData.MessengerSeqRecord { seqId = "Seq1", choices = { 1 } };

            var history = MessengerService.BuildHistory(DemoLines(), record);

            Assert.IsTrue(history.Completed);
            Assert.IsNull(history.PendingChoice);
            Assert.AreEqual(3, history.Bubbles.Count);
            Assert.AreEqual("글쎄…", history.Bubbles[1].Text);
            Assert.IsTrue(history.Bubbles[1].IsMine);
            Assert.IsFalse(history.Bubbles[0].IsMine);
        }

        [Test]
        public void BuildHistory_Clamps_Out_Of_Range_Choice_Index()
        {
            // 작가가 옵션을 줄여 저장된 인덱스가 범위 밖이 된 경우 — 마지막 옵션으로 보정(세이브 호환).
            var record = new GameStateData.MessengerSeqRecord { seqId = "Seq1", choices = { 7 } };

            var history = MessengerService.BuildHistory(DemoLines(), record);

            Assert.IsTrue(history.Completed);
            Assert.AreEqual("글쎄…", history.Bubbles[1].Text);
        }

        [Test]
        public void BuildHistory_Null_Record_Stops_At_First_Choice()
        {
            var history = MessengerService.BuildHistory(DemoLines(), null);

            Assert.AreEqual(1, history.Bubbles.Count);
            Assert.IsNotNull(history.PendingChoice);
        }
    }
}
