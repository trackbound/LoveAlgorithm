using System.Collections.Generic;
using NUnit.Framework;
using LoveAlgo.Messenger;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 메신저 시퀀스 카탈로그 순수 룩업 검증 — id 해석(대소문자 구분·미등록 null)과
    /// 자동 도착일 필터(0 = 스토리 트리거 전용 예약값).
    /// </summary>
    public class MessengerScriptCatalogTests
    {
        static List<MessengerScriptCatalogSO.Entry> Entries() => new()
        {
            new() { sequenceId = "DateInvite_Roa", roomId = "roa", csvPath = "DateInvite_Roa.csv", deliverDay = 5 },
            new() { sequenceId = "Festival_Group", roomId = "club", csvPath = "Festival_Group.csv", deliverDay = 5 },
            new() { sequenceId = "StoryOnly", roomId = "c01", csvPath = "StoryOnly.csv", deliverDay = 0 },
        };

        [Test]
        public void Resolve_Finds_Entry_Or_Null()
        {
            var entries = Entries();
            Assert.AreEqual("DateInvite_Roa.csv", MessengerScriptCatalogSO.Resolve(entries, "DateInvite_Roa").csvPath);
            Assert.IsNull(MessengerScriptCatalogSO.Resolve(entries, "dateinvite_roa")); // 대소문자 구분
            Assert.IsNull(MessengerScriptCatalogSO.Resolve(entries, "없음"));
            Assert.IsNull(MessengerScriptCatalogSO.Resolve(entries, null));
            Assert.IsNull(MessengerScriptCatalogSO.Resolve(null, "DateInvite_Roa"));
        }

        [Test]
        public void ForDay_Returns_All_Matches_For_That_Day()
        {
            var hits = MessengerScriptCatalogSO.ForDay(Entries(), 5);
            Assert.AreEqual(2, hits.Count);
        }

        [Test]
        public void ForDay_Zero_Or_Negative_Is_Empty()
        {
            // deliverDay 0은 "자동 도착 없음" 예약값 — day 0 조회로 끌려나오면 안 된다.
            Assert.AreEqual(0, MessengerScriptCatalogSO.ForDay(Entries(), 0).Count);
            Assert.AreEqual(0, MessengerScriptCatalogSO.ForDay(Entries(), -1).Count);
        }
    }
}
