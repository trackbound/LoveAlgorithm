using NUnit.Framework;
using LoveAlgo.Core; // ScreenPhase
using LoveAlgo.Game; // PhaseService, PhaseTransitionResult

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 순수 PhaseService FSM 검증(ADR-013 전환표): Story↔Schedule·*→Ending 허용, Ending→X·동일 페이즈 거부.
    /// </summary>
    [TestFixture]
    public class PhaseServiceTests
    {
        [Test]
        public void Valid_Transitions_Are_Ok()
        {
            Assert.IsTrue(PhaseService.Resolve(ScreenPhase.Story, ScreenPhase.Schedule).Ok);
            Assert.IsTrue(PhaseService.Resolve(ScreenPhase.Schedule, ScreenPhase.Story).Ok);
            Assert.IsTrue(PhaseService.Resolve(ScreenPhase.Story, ScreenPhase.Ending).Ok, "*→Ending");
            Assert.IsTrue(PhaseService.Resolve(ScreenPhase.Schedule, ScreenPhase.Ending).Ok, "*→Ending");
        }

        [Test]
        public void Result_Carries_From_And_To()
        {
            var r = PhaseService.Resolve(ScreenPhase.Schedule, ScreenPhase.Story);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(ScreenPhase.Schedule, r.From);
            Assert.AreEqual(ScreenPhase.Story, r.To);
            Assert.IsNull(r.Reason);
        }

        [Test]
        public void Same_Phase_Is_Rejected()
        {
            Assert.IsFalse(PhaseService.Resolve(ScreenPhase.Story, ScreenPhase.Story).Ok);
            Assert.IsFalse(PhaseService.Resolve(ScreenPhase.Schedule, ScreenPhase.Schedule).Ok);
            Assert.IsFalse(PhaseService.Resolve(ScreenPhase.Ending, ScreenPhase.Ending).Ok);
        }

        [Test]
        public void Ending_Is_Terminal()
        {
            Assert.IsFalse(PhaseService.Resolve(ScreenPhase.Ending, ScreenPhase.Story).Ok);
            Assert.IsFalse(PhaseService.Resolve(ScreenPhase.Ending, ScreenPhase.Schedule).Ok);
        }

        [Test]
        public void Rejected_Carries_Reason()
        {
            var r = PhaseService.Resolve(ScreenPhase.Ending, ScreenPhase.Story);
            Assert.IsFalse(r.Ok);
            Assert.IsNotNull(r.Reason);
        }
    }
}
