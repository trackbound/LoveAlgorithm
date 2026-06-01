using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.Schedule;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M4 slice1 검증: 스케줄 효과 적용 공식(ScheduleEffects, 순수) + ScheduleTable 이식 무결성.
    /// 적용기 테스트는 손으로 만든 ScheduleEffect를 써서 에셋과 무관하게 결정적으로 검증한다.
    /// 테이블 테스트는 §5 구조(카테고리 매핑·9종 해소·Loading 1일1회)를 확인한다.
    /// </summary>
    [TestFixture]
    public class ScheduleEffectsTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        // ── 적용기 (순수) ──────────────────────────────────────
        [Test]
        public void Apply_Adds_Stats_And_Money()
        {
            var so = MakeState();
            try
            {
                // (name, desc, money, str, intel, soc, per, fatigue)
                var e = new ScheduleEffect("테스트", "", 20000, 1, 2, 3, 4, 5);
                ScheduleEffects.Apply(so, e);

                Assert.AreEqual(1, so.GetStat("Str"));
                Assert.AreEqual(2, so.GetStat("Int"));
                Assert.AreEqual(3, so.GetStat("Soc"));
                Assert.AreEqual(4, so.GetStat("Per"));
                Assert.AreEqual(5, so.GetStat("Fatigue"));
                Assert.AreEqual(20000, so.Money);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Apply_Clamps_Stat_At_Max()
        {
            var so = MakeState();
            try
            {
                so.SetStat("Fatigue", 98);
                ScheduleEffects.Apply(so, new ScheduleEffect("t", "", 0, 0, 0, 0, 0, 5));
                Assert.AreEqual(GameStateSO.MaxStat, so.GetStat("Fatigue"), "피로는 MaxStat에서 클램프");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Apply_Money_Floors_At_Zero()
        {
            var so = MakeState();
            try
            {
                so.Money = 100;
                ScheduleEffects.Apply(so, new ScheduleEffect("t", "", -5000, 0, 0, 0, 0, 0));
                Assert.AreEqual(0, so.Money, "소지금은 0 미만으로 떨어지지 않음");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ApplyInvest_Positive_Multiplier_Gains()
        {
            var so = MakeState();
            try
            {
                so.Money = 1000;
                long delta = ScheduleEffects.ApplyInvest(so, 1.0f); // +100%
                Assert.AreEqual(2000, so.Money, "배수 +1.0 → 2배");
                Assert.AreEqual(1000, delta, "반영된 변화액");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ApplyInvest_Negative_Multiplier_Loses_And_Floors()
        {
            var so = MakeState();
            try
            {
                so.Money = 1000;
                long delta1 = ScheduleEffects.ApplyInvest(so, -0.5f); // -50%
                Assert.AreEqual(500, so.Money, "배수 -0.5 → 절반");
                Assert.AreEqual(-500, delta1);

                long delta2 = ScheduleEffects.ApplyInvest(so, -2.0f); // 과손실 → 0 바닥
                Assert.AreEqual(0, so.Money, "손실이 잔액 초과 시 0 바닥");
                Assert.AreEqual(-500, delta2, "실제 반영액은 0 바닥 반영(−500)");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ApplyInvest_Null_State_Is_NoOp()
        {
            Assert.AreEqual(0, ScheduleEffects.ApplyInvest(null, 1.0f));
        }

        // ── 테이블 이식 무결성 (§5 구조) ───────────────────────
        [Test]
        public void Categories_Map_Per_Spec()
        {
            Assert.AreEqual(ScheduleCategory.PartTime, ScheduleTable.GetCategory(ScheduleType.PartTime_Store));
            Assert.AreEqual(ScheduleCategory.PartTime, ScheduleTable.GetCategory(ScheduleType.PartTime_Loading));
            Assert.AreEqual(ScheduleCategory.PartTime, ScheduleTable.GetCategory(ScheduleType.Invest));
            Assert.AreEqual(ScheduleCategory.Exercise, ScheduleTable.GetCategory(ScheduleType.Exercise_A));
            Assert.AreEqual(ScheduleCategory.Study, ScheduleTable.GetCategory(ScheduleType.Study_D));
        }

        [Test]
        public void GetTypes_Returns_Three_Per_Category()
        {
            Assert.AreEqual(3, ScheduleTable.GetTypes(ScheduleCategory.PartTime).Length);
            Assert.AreEqual(3, ScheduleTable.GetTypes(ScheduleCategory.Exercise).Length);
            Assert.AreEqual(3, ScheduleTable.GetTypes(ScheduleCategory.Study).Length);
        }

        [Test]
        public void All_Nine_Types_Resolve_To_Named_Effect()
        {
            foreach (ScheduleType t in System.Enum.GetValues(typeof(ScheduleType)))
            {
                var e = ScheduleTable.Get(t);
                Assert.IsFalse(string.IsNullOrEmpty(e.displayName), $"{t} 효과가 해소돼야 함(에셋/폴백)");
            }
        }

        [Test]
        public void Loading_Is_Daily_Limited()
        {
            Assert.IsTrue(ScheduleTable.Get(ScheduleType.PartTime_Loading).isLimited,
                "상하차는 1일 1회 제한(§5)");
        }
    }
}
