using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;  // GameStateSO
using LoveAlgo.Story; // ChoiceEffectInterpreter, ChoiceEffectResult

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 slice1 검증: 순수 <see cref="ChoiceEffectInterpreter"/>. Stat/Flag/Money는 즉시 gs에 적용하고,
    /// Love는 Affinity 카테고리 Flow 명령으로 위임, SFX는 이름만 수집하는지 결정적으로 확인.
    /// (호감도 정본은 AffinityFormula 단일화 — 감독 결정. 여기서 lovePoints를 직접 건드리지 않음.)
    /// </summary>
    [TestFixture]
    public class ChoiceEffectInterpreterTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        [Test]
        public void Stat_Applied_And_Reported()
        {
            var gs = MakeState();
            try
            {
                var r = ChoiceEffectInterpreter.Apply(gs, new[] { "Stat:Int:5" });

                Assert.AreEqual(5, gs.GetStat("Int"), "스탯 즉시 적용");
                Assert.AreEqual(1, r.StatChanges.Count);
                Assert.AreEqual("Int", r.StatChanges[0].StatId);
                Assert.AreEqual(0, r.StatChanges[0].OldValue);
                Assert.AreEqual(5, r.StatChanges[0].NewValue);
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Money_Applied_And_Reported()
        {
            var gs = MakeState();
            try
            {
                var r = ChoiceEffectInterpreter.Apply(gs, new[] { "Money:120" });

                Assert.AreEqual(120, gs.Money);
                Assert.IsTrue(r.MoneyChanged);
                Assert.AreEqual(120, r.NewMoney);
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Flag_Applied_With_And_Without_Value()
        {
            var gs = MakeState();
            try
            {
                ChoiceEffectInterpreter.Apply(gs, new[] { "Flag:met:true", "Flag:bare", "Flag:off:false" });

                Assert.IsTrue(gs.GetFlag("met"), "명시 true");
                Assert.IsTrue(gs.GetFlag("bare"), "값 생략 → true");
                Assert.IsFalse(gs.GetFlag("off"), "명시 false");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Love_Delegated_To_Affinity_Flow_Command_Not_Applied_Directly()
        {
            var gs = MakeState();
            try
            {
                var r = ChoiceEffectInterpreter.Apply(gs, new[] { "Love:HaYeEun:3" });

                CollectionAssert.AreEqual(new[] { "Affinity:Point:HaYeEun:Dialogue:3" }, r.FlowCommands);
                Assert.AreEqual(0, gs.GetLove("HaYeEun"), "Love는 인터프리터가 직접 적용하지 않음(Router 위임)");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Sfx_Collected_Not_Applied()
        {
            var gs = MakeState();
            try
            {
                var r = ChoiceEffectInterpreter.Apply(gs, new[] { "SFX:click" });
                CollectionAssert.AreEqual(new[] { "click" }, r.SfxNames);
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Command_Aliases_Work()
        {
            var gs = MakeState();
            try
            {
                var r = ChoiceEffectInterpreter.Apply(gs, new[] { "AddStat:Soc:4", "AddMoney:50", "AddLove:SeoDaEun:2" });

                Assert.AreEqual(4, gs.GetStat("Soc"));
                Assert.AreEqual(50, gs.Money);
                CollectionAssert.Contains(r.FlowCommands, "Affinity:Point:SeoDaEun:Dialogue:2");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Null_And_Malformed_Are_Safe_NoOp()
        {
            var gs = MakeState();
            try
            {
                Assert.DoesNotThrow(() => ChoiceEffectInterpreter.Apply(null, new[] { "Stat:Int:5" }));
                var r = ChoiceEffectInterpreter.Apply(gs, new[] { "", "Bogus", "Stat" });
                Assert.IsEmpty(r.StatChanges);
                Assert.IsFalse(r.MoneyChanged);
            }
            finally { Object.DestroyImmediate(gs); }
        }
    }
}
