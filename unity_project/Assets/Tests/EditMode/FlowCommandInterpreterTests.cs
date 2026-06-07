using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;     // GameStateSO
using LoveAlgo.Affinity; // AffinityFormula
using LoveAlgo.Story.StoryEngine.Flow; // FlowCommandInterpreter, FlowCommandResult, FlowCommandKind

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 slice2 검증: 순수 <see cref="FlowCommandInterpreter"/>. CSV Flow 문법(Affinity:/Day:)을
    /// GameStateSO에 적용하고 결과를 반환하는지 결정적으로 확인. 채점은 AffinityFormula에 위임하므로
    /// 폴백 정의표로 격리(스탯 0 → 보너스 0 → NewScore=기본점수)해 수치를 못박는다.
    /// </summary>
    [TestFixture]
    public class FlowCommandInterpreterTests
    {
        [SetUp]
        public void Reset() => AffinityFormula.ResetToFallback();

        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        [Test]
        public void EventChoice_Records_Points_And_Selection()
        {
            var gs = MakeState();
            try
            {
                var r = FlowCommandInterpreter.Apply(gs, "Affinity:EventChoice:HaYeEun:Event1:3");

                Assert.IsTrue(r.Ok);
                Assert.AreEqual(FlowCommandKind.AffinityEventChoice, r.Kind);
                Assert.AreEqual("HaYeEun", r.HeroineId);
                Assert.AreEqual(3, r.NewScore, "스탯 0 → 보너스 0 → 기본 3점");
                Assert.AreEqual(3, AffinityFormula.BasePoints(gs, "HaYeEun"));
                Assert.AreEqual(1, AffinityFormula.EventSelections(gs, "HaYeEun"));
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Event3_Reselection_Grants_Plus2()
        {
            var gs = MakeState();
            try
            {
                FlowCommandInterpreter.Apply(gs, "Affinity:EventChoice:HaYeEun:Event1:3"); // 3
                var r = FlowCommandInterpreter.Apply(gs, "Affinity:EventChoice:HaYeEun:Event3:3"); // +3 +2 재선택

                Assert.IsTrue(r.Ok);
                // Event1(3) + Event3(3) + 재선택 보정(2) = 8
                Assert.AreEqual(8, AffinityFormula.BasePoints(gs, "HaYeEun"));
                Assert.AreEqual(8, r.NewScore);
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Point_AddsCategory()
        {
            var gs = MakeState();
            try
            {
                var r = FlowCommandInterpreter.Apply(gs, "Affinity:Point:SeoDaEun:Gift:4");

                Assert.IsTrue(r.Ok);
                Assert.AreEqual(FlowCommandKind.AffinityPoint, r.Kind);
                Assert.AreEqual("SeoDaEun", r.HeroineId);
                Assert.AreEqual(4, r.NewScore);
                Assert.AreEqual(0, AffinityFormula.EventSelections(gs, "SeoDaEun"), "Point는 선택 횟수 미증가");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Day_SetsDay()
        {
            var gs = MakeState();
            try
            {
                var r = FlowCommandInterpreter.Apply(gs, "Day:5");

                Assert.IsTrue(r.Ok);
                Assert.AreEqual(FlowCommandKind.Day, r.Kind);
                Assert.AreEqual(5, r.Day);
                Assert.AreEqual(5, gs.Day);
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Malformed_Commands_Fail_Without_Mutating()
        {
            var gs = MakeState();
            try
            {
                Assert.IsFalse(FlowCommandInterpreter.Apply(gs, "Affinity:EventChoice:HaYeEun:Event1").Ok, "인자 부족");
                Assert.IsFalse(FlowCommandInterpreter.Apply(gs, "Affinity:Point:HaYeEun:BadCat:1").Ok, "카테고리 오류");
                Assert.IsFalse(FlowCommandInterpreter.Apply(gs, "Affinity:Point:Nobody:Gift:1").Ok, "미지의 히로인");
                Assert.IsFalse(FlowCommandInterpreter.Apply(gs, "Day:abc").Ok, "Day 숫자 아님");
                Assert.IsFalse(FlowCommandInterpreter.Apply(gs, "Bogus:x").Ok, "미지의 Flow 명령");

                Assert.AreEqual(0, AffinityFormula.BasePoints(gs, "HaYeEun"), "실패 시 상태 불변");
                Assert.AreEqual(1, gs.Day, "Day 실패 시 기본값 유지");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        // ── Ending — 고백 자동 판정(Day30, 감독 결정 2026-06-07) ──

        [Test]
        public void Ending_Winner_Sets_Single_Heroine_Flag()
        {
            var gs = MakeState();
            try
            {
                // 하예은: 이벤트 선택 1 + 총점 ≥ 임계치(32) → 판정.
                FlowCommandInterpreter.Apply(gs, "Affinity:EventChoice:HaYeEun:Event1:3");
                FlowCommandInterpreter.Apply(gs, "Affinity:Point:HaYeEun:Event:24"); // 27
                FlowCommandInterpreter.Apply(gs, "Affinity:Point:HaYeEun:Gift:8");   // 35 ≥ 32

                var r = FlowCommandInterpreter.Apply(gs, "Ending");

                Assert.IsTrue(r.Ok);
                Assert.AreEqual(FlowCommandKind.Ending, r.Kind);
                Assert.AreEqual("HaYeEun", r.HeroineId);
                Assert.IsTrue(gs.GetFlag("Ending_HaYeEun"), "판정 히로인 플래그 set");
                Assert.IsFalse(gs.GetFlag("Ending_Roa"), "나머지는 false");
                Assert.IsFalse(gs.GetFlag("Ending_None"), "판정 성공 시 None=false");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Ending_NoWinner_Sets_None()
        {
            var gs = MakeState();
            try
            {
                var r = FlowCommandInterpreter.Apply(gs, "Ending"); // 아무 점수 없음 → 노멀

                Assert.IsTrue(r.Ok);
                Assert.IsNull(r.HeroineId);
                Assert.IsTrue(gs.GetFlag("Ending_None"));
                Assert.IsFalse(gs.GetFlag("Ending_HaYeEun"));
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Ending_Reapply_Overwrites_Deterministically()
        {
            var gs = MakeState();
            try
            {
                FlowCommandInterpreter.Apply(gs, "Ending");
                Assert.IsTrue(gs.GetFlag("Ending_None"), "1차: 노멀");

                FlowCommandInterpreter.Apply(gs, "Affinity:EventChoice:HaYeEun:Event1:3");
                FlowCommandInterpreter.Apply(gs, "Affinity:Point:HaYeEun:Event:32"); // 35 ≥ 32
                FlowCommandInterpreter.Apply(gs, "Ending");

                Assert.IsTrue(gs.GetFlag("Ending_HaYeEun"), "2차: 판정 갱신");
                Assert.IsFalse(gs.GetFlag("Ending_None"), "None 해제(전체 덮어쓰기)");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Ending_Roa_Hidden_Requires_Fatigue()
        {
            var gs = MakeState();
            try
            {
                // 로아 총점 ≥ 46이어도 피로 < 70이면 히든 루트 미발동(이벤트 선택 없으면 일반 판정도 불가).
                FlowCommandInterpreter.Apply(gs, "Affinity:Point:Roa:Event:27");
                FlowCommandInterpreter.Apply(gs, "Affinity:Point:Roa:Dialogue:15");
                FlowCommandInterpreter.Apply(gs, "Affinity:Point:Roa:Gift:8"); // 50 ≥ 46
                FlowCommandInterpreter.Apply(gs, "Ending");
                Assert.IsTrue(gs.GetFlag("Ending_None"), "피로 미달 → 히든 미발동");

                gs.SetStat("Fatigue", 75);
                var r = FlowCommandInterpreter.Apply(gs, "Ending");
                Assert.AreEqual("Roa", r.HeroineId, "피로 ≥70 + 총점 ≥46 → 로아 히든");
                Assert.IsTrue(gs.GetFlag("Ending_Roa"));
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Null_Or_Empty_Is_Failed_NoOp()
        {
            Assert.IsFalse(FlowCommandInterpreter.Apply(null, "Day:2").Ok);
            var gs = MakeState();
            try
            {
                Assert.IsFalse(FlowCommandInterpreter.Apply(gs, "").Ok);
                Assert.IsFalse(FlowCommandInterpreter.Apply(gs, "   ").Ok);
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Flag_Sets_Clears_And_Alias()
        {
            var gs = MakeState();
            try
            {
                var r = FlowCommandInterpreter.Apply(gs, "Flag:met_roa");
                Assert.IsTrue(r.Ok);
                Assert.AreEqual(FlowCommandKind.Flag, r.Kind);
                Assert.IsTrue(gs.GetFlag("met_roa"), "값 생략 = true");

                FlowCommandInterpreter.Apply(gs, "Flag:met_roa:false");
                Assert.IsFalse(gs.GetFlag("met_roa"), "false로 해제");

                Assert.IsTrue(FlowCommandInterpreter.Apply(gs, "Set:seen_intro").Ok, "Set 별칭");
                Assert.IsTrue(gs.GetFlag("seen_intro"));

                Assert.IsFalse(FlowCommandInterpreter.Apply(gs, "Flag:").Ok, "이름 없음 → 실패");
            }
            finally { Object.DestroyImmediate(gs); }
        }
    }
}
