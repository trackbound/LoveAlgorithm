using NUnit.Framework;
using LoveAlgo.Events; // ModalRequest

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 범용 모달 완료 핸들(ModalRequest, ChoiceRequest 형제) 순수 검증: 인덱스 회수·완료 플래그·콜백 1회 보장·
    /// 음수/중복 무시. 시스템 모달이 콜백으로 결과를 분기하므로 콜백 계약(1회·정확한 인덱스)이 핵심.
    /// </summary>
    public class ModalRequestTests
    {
        [Test]
        public void Initial_NotComplete_IndexNegative()
        {
            var r = new ModalRequest();
            Assert.IsFalse(r.IsComplete);
            Assert.AreEqual(-1, r.SelectedIndex);
        }

        [Test]
        public void Select_SetsIndex_AndCompletes()
        {
            var r = new ModalRequest();
            r.Select(1);
            Assert.IsTrue(r.IsComplete);
            Assert.AreEqual(1, r.SelectedIndex);
        }

        [Test]
        public void Select_InvokesCallback_Once_WithIndex()
        {
            int calls = 0, got = -99;
            var r = new ModalRequest(i => { calls++; got = i; });
            r.Select(2);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(2, got);
        }

        [Test]
        public void Select_Negative_Ignored()
        {
            int calls = 0;
            var r = new ModalRequest(_ => calls++);
            r.Select(-1);
            Assert.IsFalse(r.IsComplete);
            Assert.AreEqual(-1, r.SelectedIndex);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Select_Twice_FirstWins_CallbackOnce()
        {
            int calls = 0;
            var r = new ModalRequest(_ => calls++);
            r.Select(0);
            r.Select(1);
            Assert.AreEqual(0, r.SelectedIndex, "첫 선택만 유효");
            Assert.AreEqual(1, calls, "콜백은 1회만");
        }

        [Test]
        public void NullCallback_PollingStillWorks()
        {
            var r = new ModalRequest(); // 콜백 없음 — 폴링 전용
            r.Select(0);
            Assert.IsTrue(r.IsComplete);
            Assert.AreEqual(0, r.SelectedIndex);
        }
    }
}
