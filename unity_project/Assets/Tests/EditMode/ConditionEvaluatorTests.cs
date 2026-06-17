using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;  // GameStateSO
using LoveAlgo.Story; // ConditionEvaluator

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 순수 ConditionEvaluator 검증(분기 게이트 토대): Flag/!Flag · Love/Stat/베어 비교 · 연산자 5종 · 빈=true ·
    /// 미지원/널=false. 문법은 구 GameState.EvaluateCondition과 1:1(요구사항).
    /// </summary>
    [TestFixture]
    public class ConditionEvaluatorTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        [Test]
        public void Empty_Or_Null_Is_True()
        {
            var gs = MakeState();
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, ""));
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "   "));
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, null));
            Assert.IsTrue(ConditionEvaluator.Evaluate(null, ""), "빈 조건은 널 상태에서도 참(빈 검사 우선)");
        }

        [Test]
        public void Flag_And_Negation()
        {
            var gs = MakeState();
            gs.SetFlag("met_roa", true);
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Flag:met_roa"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "!Flag:met_roa"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Flag:unknown"), "미설정 플래그 = false");
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "!Flag:unknown"));
        }

        [Test]
        public void Chose_And_Negation()
        {
            var gs = MakeState();
            gs.RecordChoice("met_roa");
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Chose:met_roa"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "!Chose:met_roa"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Chose:unknown"), "미선택 = false");
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "!Chose:unknown"));
        }

        [Test]
        public void Chose_Combines_With_And_Or()
        {
            var gs = MakeState();
            gs.RecordChoice("a");
            gs.SetFlag("vip", true);
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Chose:a&Flag:vip"), "둘 다 참");
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Chose:b&Flag:vip"), "AND 일부 거짓");
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Chose:b|Flag:vip"), "OR 하나 참");
        }

        [Test]
        public void Stat_All_Operators()
        {
            var gs = MakeState();
            gs.SetStat("Int", 30);
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Stat:Int>=30"));
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Stat:Int<=30"));
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Stat:Int==30"));
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Stat:Int>20"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Stat:Int>30"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Stat:Int<30"));
        }

        [Test]
        public void Bare_Stat_Comparison()
        {
            var gs = MakeState();
            gs.SetStat("Per", 50);
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Per>=50"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Per<50"));
        }

        [Test]
        public void Love_Comparison()
        {
            var gs = MakeState();
            gs.SetLove("로아", 46); // 인벤토리 임계치 예
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Love:로아>=46"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Love:로아>=47"));
        }

        [Test]
        public void Malformed_Or_Unsupported_Is_False()
        {
            var gs = MakeState();
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Stat:Int>=abc"), "숫자 아님");
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "JustText"), "비교식 아님");
            Assert.IsFalse(ConditionEvaluator.Evaluate(null, "Flag:x"), "널 상태");
        }

        [Test]
        public void Compound_And()
        {
            var gs = MakeState();
            gs.SetFlag("a", true);
            gs.SetStat("Int", 30);
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Flag:a&Stat:Int>=20"), "둘 다 참 → AND 참");
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Flag:a&Stat:Int>=40"), "하나 거짓 → AND 거짓");
        }

        [Test]
        public void Compound_Or()
        {
            var gs = MakeState();
            gs.SetFlag("a", true);
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Flag:a|Flag:b"), "하나 참 → OR 참");
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Flag:x|Flag:y"), "둘 다 거짓 → OR 거짓");
        }

        [Test]
        public void Compound_And_Binds_Before_Or()
        {
            var gs = MakeState();
            gs.SetFlag("vip", true); // a·b 미설정
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Flag:a&Flag:b|Flag:vip"), "(a∧b)∨vip = false∨true = true");
            gs.SetFlag("vip", false);
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Flag:a&Flag:b|Flag:vip"), "(a∧b)∨vip 모두 거짓");
        }
    }
}
