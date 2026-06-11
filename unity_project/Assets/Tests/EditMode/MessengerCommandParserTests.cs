using NUnit.Framework;
using LoveAlgo.Story;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// Flow 메신저 명령 순수 파서 검증 — 가족 판별(Messenger/구 별칭 Message), Wait 토큰,
    /// 시퀀스 누락 invalid, 케이스/공백 관용.
    /// </summary>
    public class MessengerCommandParserTests
    {
        [Test]
        public void Parses_Sequence_Without_Wait()
        {
            var intent = MessengerCommandParser.Parse("Messenger:DateInvite_C01");
            Assert.IsTrue(intent.IsValid);
            Assert.AreEqual("DateInvite_C01", intent.SequenceId);
            Assert.IsFalse(intent.Wait);
        }

        [Test]
        public void Wait_Token_Case_Insensitive()
        {
            var intent = MessengerCommandParser.Parse("messenger:Seq1:wait");
            Assert.IsTrue(intent.IsValid);
            Assert.IsTrue(intent.Wait);
        }

        [Test]
        public void Message_Alias_Accepted()
        {
            Assert.IsTrue(MessengerCommandParser.IsMessenger("Message:Seq1"));
            var intent = MessengerCommandParser.Parse("Message:Seq1:Wait");
            Assert.IsTrue(intent.IsValid);
            Assert.AreEqual("Seq1", intent.SequenceId);
            Assert.IsTrue(intent.Wait);
        }

        [Test]
        public void Missing_Sequence_Is_Invalid()
        {
            Assert.IsFalse(MessengerCommandParser.Parse("Messenger").IsValid);
            Assert.IsFalse(MessengerCommandParser.Parse("Messenger:").IsValid);
        }

        [Test]
        public void Other_Families_Are_Not_Messenger()
        {
            Assert.IsFalse(MessengerCommandParser.IsMessenger("Flag:Promised"));
            Assert.IsFalse(MessengerCommandParser.Parse("LockScreen:Normal").IsValid);
        }

        [Test]
        public void Unknown_Trailing_Token_Is_Ignored_Not_Wait()
        {
            // 오타("Wiat")는 hang(영원 대기)보다 안전한 "안 기다림"으로 강등.
            var intent = MessengerCommandParser.Parse("Messenger:Seq1:Wiat");
            Assert.IsTrue(intent.IsValid);
            Assert.IsFalse(intent.Wait);
        }
    }
}
