using System;
using NUnit.Framework;
using LoveAlgo.UI; // SaveLoadSlot, SaveLoadView

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 세이브/로드 순수 로직: 슬롯 날짜 포맷(<see cref="SaveLoadSlot.FormatDate"/>) ·
    /// 페이지↔슬롯 매핑(<see cref="SaveLoadView.SlotForCell"/>). 타임존 비의존(로컬↔UTC 왕복으로 검증).
    /// </summary>
    public class SaveLoadSlotTests
    {
        [Test]
        public void FormatDate_Empty_ReturnsEmpty()
        {
            Assert.AreEqual("", SaveLoadSlot.FormatDate(""));
            Assert.AreEqual("", SaveLoadSlot.FormatDate(null));
        }

        [Test]
        public void FormatDate_Unparseable_ReturnsOriginal()
        {
            Assert.AreEqual("not-a-date", SaveLoadSlot.FormatDate("not-a-date"));
        }

        [Test]
        public void FormatDate_RoundTripsLocalThroughUtc()
        {
            // 로컬 시각 → UTC ISO(저장 포맷)로 만든 뒤 FormatDate가 같은 로컬 표현으로 되돌리는지(겨울철=DST 무관).
            var local = new DateTime(2025, 1, 30, 17, 24, 0, DateTimeKind.Local);
            string iso = local.ToUniversalTime().ToString("o");
            Assert.AreEqual("2025/01/30 17:24", SaveLoadSlot.FormatDate(iso));
        }

        [Test]
        public void SlotForCell_Maps_Page_And_Cell()
        {
            // 0-base: 1페이지 첫 칸 = 슬롯0(자동저장), 이후 연속.
            Assert.AreEqual(0, SaveLoadView.SlotForCell(0, 0, 6), "1페이지 첫 칸 = 슬롯0(자동저장)");
            Assert.AreEqual(5, SaveLoadView.SlotForCell(0, 5, 6), "1페이지 끝 칸 = 슬롯5");
            Assert.AreEqual(6, SaveLoadView.SlotForCell(1, 0, 6), "2페이지 첫 칸 = 슬롯6");
            Assert.AreEqual(17, SaveLoadView.SlotForCell(2, 5, 6), "3페이지 끝 칸 = 슬롯17");
        }
    }
}
