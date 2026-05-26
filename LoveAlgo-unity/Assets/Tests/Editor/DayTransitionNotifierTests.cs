using NUnit.Framework;
using LoveAlgo.Modules.DayLoop;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D3 일자 트랜지션 카드 — 모든 EventPhase에 라벨이 매핑돼 있는지 검증.
    /// 새 페이즈 추가 시 라벨 빠뜨리면 여기서 즉시 잡힘.
    /// </summary>
    [TestFixture]
    public class DayTransitionNotifierTests
    {
        [Test]
        public void PhaseLabel_HasNonEmptyLabelForEveryPhase()
        {
            foreach (EventPhase phase in System.Enum.GetValues(typeof(EventPhase)))
            {
                string label = DayTransitionNotifier.PhaseLabel(phase);
                Assert.IsFalse(string.IsNullOrWhiteSpace(label),
                    $"EventPhase.{phase} 에 대한 트랜지션 라벨이 비어 있음");
            }
        }

        [Test]
        public void PhaseLabel_BigEventDays_AreDistinctFromAftermath()
        {
            // Event1/Festival/Event2/MT/Event3/Confession 같은 굵직한 날은
            // 다음 며칠의 "After..." 라벨과 같으면 안 됨 (덤덤한 일자카드가 되버림).
            Assert.AreNotEqual(
                DayTransitionNotifier.PhaseLabel(EventPhase.Event1),
                DayTransitionNotifier.PhaseLabel(EventPhase.AfterEvent1));
            Assert.AreNotEqual(
                DayTransitionNotifier.PhaseLabel(EventPhase.Festival),
                DayTransitionNotifier.PhaseLabel(EventPhase.AfterFestival));
            Assert.AreNotEqual(
                DayTransitionNotifier.PhaseLabel(EventPhase.MT),
                DayTransitionNotifier.PhaseLabel(EventPhase.AfterMT));
            Assert.AreNotEqual(
                DayTransitionNotifier.PhaseLabel(EventPhase.Confession),
                DayTransitionNotifier.PhaseLabel(EventPhase.Ending));
        }
    }
}
