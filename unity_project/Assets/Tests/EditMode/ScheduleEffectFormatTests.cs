using NUnit.Framework;
using LoveAlgo.Schedule; // ScheduleTable, ScheduleEffect

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 순수 효과 요약 포매터(<see cref="ScheduleTable.FormatEffect(ScheduleEffect)"/>) 검증 — 확인 팝업의
    /// 두 번째 메시지("힘 +3, 피로도 +10"). 0 항목 생략·부호·천단위·빈 효과 처리.
    /// </summary>
    public class ScheduleEffectFormatTests
    {
        [Test]
        public void NonZero_Stats_With_Signs_In_Order()
        {
            // (name, desc, money, str, intel, soc, per, fatigue)
            var e = new ScheduleEffect("x", "", 0, 3, 0, 0, 0, 10);
            Assert.AreEqual("힘 +3, 피로도 +10", ScheduleTable.FormatEffect(e));
        }

        [Test]
        public void Money_With_Thousands_Separator_And_Order()
        {
            var e = new ScheduleEffect("x", "", 20000, 0, 0, 0, 1, 5);
            Assert.AreEqual("끈기 +1, 피로도 +5, 돈 +20,000", ScheduleTable.FormatEffect(e));
        }

        [Test]
        public void Negative_Keeps_Minus_Sign()
        {
            var e = new ScheduleEffect("x", "", -5000, 0, 0, 0, 0, 0);
            Assert.AreEqual("돈 -5,000", ScheduleTable.FormatEffect(e));
        }

        [Test]
        public void AllZero_Effect_Is_Empty()
        {
            var e = new ScheduleEffect("x", "", 0, 0, 0, 0, 0, 0);
            Assert.AreEqual("", ScheduleTable.FormatEffect(e));
        }
    }
}
