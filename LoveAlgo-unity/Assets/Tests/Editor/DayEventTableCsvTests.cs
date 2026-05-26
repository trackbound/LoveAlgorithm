using NUnit.Framework;
using LoveAlgo.Core;
using UnityEngine;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D1 스토리 슬롯 매니페스트 — events.csv가 정상 로드되는지 검증.
    /// 정적 ctor가 PlayMode/Edit 전환 시 한 번 실행되므로 직접 호출하지 않고
    /// 결과(events 슬롯 채워짐)만 확인. CSV 파일이 변경되면 fired/카운트가 바뀔 수 있어
    /// 절대 카운트가 아니라 "최소 N개 슬롯 채워짐"으로 보수적으로 검증.
    /// </summary>
    [TestFixture]
    public class DayEventTableCsvTests
    {
        [Test]
        public void Day1_Morning_Returns_Day1_Morning_Event()
        {
            // CSV의 Day1 Morning 슬롯이 채워졌는지 — fired되지 않은 깨끗한 상태에서만 유효
            DayEventTable.ResetFired();

            var evt = DayEventTable.GetEvent(1, DayTiming.Morning);

            Assert.IsNotNull(evt, "Day 1 Morning 슬롯이 비어 있음 — events.csv 미로드 의심");
            Assert.AreEqual("Day1_Morning", evt.ScriptName, "CSV에 등록된 Day1_Morning이 매니페스트 그대로 로드되어야 함");
        }

        [Test]
        public void Day_Without_Event_Returns_Null()
        {
            DayEventTable.ResetFired();

            // Day 6은 GameTimeline에서 처리 — events.csv에 슬롯 없음
            var evt = DayEventTable.GetEvent(6, DayTiming.Morning);

            Assert.IsNull(evt, "Day 6은 GameTimeline 전용이라 events.csv에 슬롯이 없어야 함");
        }

        [Test]
        public void MarkFired_Prevents_Reentry()
        {
            DayEventTable.ResetFired();

            var first = DayEventTable.GetEvent(1, DayTiming.Morning);
            Assert.IsNotNull(first);

            DayEventTable.MarkFired(first.ScriptName);
            var afterFire = DayEventTable.GetEvent(1, DayTiming.Morning);

            Assert.IsNull(afterFire, "Fired된 이벤트는 다시 반환되지 않아야 함 (1회성)");
        }

        [TearDown]
        public void TearDown()
        {
            // 다른 테스트에 fired 잔재가 새지 않도록
            DayEventTable.ResetFired();
        }
    }
}
