using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo;         // GameConstants
using LoveAlgo.Core;    // GameStateSO, DayLoop
using LoveAlgo.Common;  // EventBus
using LoveAlgo.Events;  // StatChangedEvent, DayEndRequestedEvent
using LoveAlgo.Schedule;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M4 slice2 검증: Schedule 통합층(순수 <see cref="ScheduleService"/> + 어댑터 <see cref="ScheduleController"/>).
    /// 게이트(투자 자금·1일1회 제한)·효과 적용·행동 소모·하루종료 신호·StatChange 산출을 결정적으로 확인하고,
    /// 컨트롤러가 그 결과를 EventBus 통지로 정확히 발행하는지(거부/적용/하루종료) 검증한다.
    /// 수치는 GameConstants에서 읽어 에셋/폴백 어느 쪽이든 무관하게 통과하도록 한다.
    /// </summary>
    [TestFixture]
    public class ScheduleServiceTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            DayLoop.BeginRun(so); // 1일차 + 행동 풀충전
            return so;
        }

        static StatChangedEvent? FindChange(ScheduleResult r, string statId)
        {
            foreach (var c in r.StatChanges)
                if (c.StatId == statId) return c;
            return null;
        }

        // ── 순수 ScheduleService.Execute ──────────────────────────

        [Test]
        public void Execute_NonInvest_Applies_Effect_And_Consumes_Action()
        {
            var so = MakeState();
            try
            {
                int before = so.RemainingActions;
                var effect = ScheduleTable.Get(ScheduleType.PartTime_Store);
                var r = ScheduleService.Execute(so, ScheduleType.PartTime_Store, 0f);

                Assert.IsFalse(r.Rejected);
                Assert.AreEqual(effect.perseveranceChange, so.GetStat("Per"));
                Assert.AreEqual(effect.fatigueChange, so.GetStat("Fatigue"));
                Assert.AreEqual(effect.moneyChange, so.Money);
                Assert.AreEqual(effect.moneyChange, r.MoneyDelta);
                Assert.AreEqual(before - 1, so.RemainingActions, "행동 1회 소모");
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void Execute_Emits_StatChange_Only_For_Changed_Stats()
        {
            var so = MakeState();
            try
            {
                var effect = ScheduleTable.Get(ScheduleType.PartTime_Store); // per+, fatigue+, 나머지 0
                var r = ScheduleService.Execute(so, ScheduleType.PartTime_Store, 0f);

                Assert.IsNull(FindChange(r, "Str"), "변화 없는 스탯은 이벤트 미포함");
                Assert.IsNull(FindChange(r, "Int"));
                Assert.IsNull(FindChange(r, "Soc"));

                var per = FindChange(r, "Per");
                Assert.IsTrue(per.HasValue);
                Assert.AreEqual(0, per.Value.OldValue);
                Assert.AreEqual(effect.perseveranceChange, per.Value.NewValue);
                Assert.AreEqual(effect.perseveranceChange, per.Value.Delta);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void Execute_Invest_Below_MinFunds_Rejected_With_No_SideEffect()
        {
            var so = MakeState();
            try
            {
                so.Money = GameConstants.MinInvestMoney - 1;
                int actionsBefore = so.RemainingActions;
                long moneyBefore = so.Money;

                var r = ScheduleService.Execute(so, ScheduleType.Invest, 1.0f);

                Assert.IsTrue(r.Rejected);
                Assert.AreEqual(ScheduleRejection.InsufficientFunds, r.Reason);
                Assert.AreEqual(moneyBefore, so.Money, "거부 시 소지금 불변");
                Assert.AreEqual(actionsBefore, so.RemainingActions, "거부 시 행동 미소모");
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void Execute_Invest_Above_MinFunds_Applies_Multiplier_And_Consumes()
        {
            var so = MakeState();
            try
            {
                so.Money = GameConstants.MinInvestMoney;
                long before = so.Money;
                int actionsBefore = so.RemainingActions;

                var r = ScheduleService.Execute(so, ScheduleType.Invest, 1.0f); // +100%

                Assert.IsFalse(r.Rejected);
                Assert.AreEqual(before * 2, so.Money, "배수 +1.0 → 2배");
                Assert.AreEqual(before, r.MoneyDelta);
                Assert.AreEqual(0, r.StatChanges.Length, "투자는 스탯 변화 없음");
                Assert.AreEqual(actionsBefore - 1, so.RemainingActions);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void Execute_DailyLimited_Second_Time_Rejected()
        {
            var so = MakeState();
            try
            {
                var first = ScheduleService.Execute(so, ScheduleType.PartTime_Loading, 0f);
                Assert.IsFalse(first.Rejected, "상하차 첫 수행은 허용");
                int actionsAfterFirst = so.RemainingActions;

                var second = ScheduleService.Execute(so, ScheduleType.PartTime_Loading, 0f);
                Assert.IsTrue(second.Rejected, "같은 날 두 번째 상하차는 거부");
                Assert.AreEqual(ScheduleRejection.DailyLimitReached, second.Reason);
                Assert.AreEqual(actionsAfterFirst, so.RemainingActions, "거부 시 행동 미소모");
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void AdvanceDay_Resets_DailyLimit()
        {
            var so = MakeState();
            try
            {
                ScheduleService.Execute(so, ScheduleType.PartTime_Loading, 0f);
                Assert.IsTrue(so.HasUsedLimited(ScheduleType.PartTime_Loading.ToString()));

                DayLoop.AdvanceDay(so);
                Assert.IsFalse(so.HasUsedLimited(ScheduleType.PartTime_Loading.ToString()),
                    "하루 전환 시 1일1회 제한 리셋");

                var again = ScheduleService.Execute(so, ScheduleType.PartTime_Loading, 0f);
                Assert.IsFalse(again.Rejected, "다음 날 상하차 재허용");
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void Execute_Signals_DayEnd_When_Last_Action_Consumed()
        {
            var so = MakeState();
            try
            {
                int actions = GameConstants.ActionsPerDay;
                Assert.GreaterOrEqual(actions, 1);

                ScheduleResult last = default;
                for (int i = 0; i < actions; i++)
                {
                    last = ScheduleService.Execute(so, ScheduleType.Exercise_A, 0f);
                    bool isLast = i == actions - 1;
                    Assert.AreEqual(isLast, last.DayEnded, $"{i + 1}번째 행동의 하루종료 신호");
                }
                Assert.AreEqual(0, so.RemainingActions);
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }

        [Test]
        public void Execute_Null_State_Is_Rejected_NoOp()
        {
            var r = ScheduleService.Execute(null, ScheduleType.Exercise_A, 0f);
            Assert.IsTrue(r.Rejected);
        }

        // ── 어댑터 ScheduleController (EventBus 발행) ──────────────

        static ScheduleController MakeController(GameStateSO state, out GameObject go)
        {
            go = new GameObject("ScheduleController_Test");
            var c = go.AddComponent<ScheduleController>();
            c.State = state;
            return c;
        }

        [Test]
        public void Controller_Publishes_StatChanged_And_Applied_On_NonInvest()
        {
            var so = MakeState();
            GameObject go = null;
            var stats = new List<StatChangedEvent>();
            bool appliedFired = false;
            ScheduleAppliedEvent applied = default;

            var t1 = EventBus.Subscribe<StatChangedEvent>(e => stats.Add(e));
            var t2 = EventBus.Subscribe<ScheduleAppliedEvent>(e => { appliedFired = true; applied = e; });
            try
            {
                var c = MakeController(so, out go);
                c.HandleSelection(new ScheduleSelectedCommand(ScheduleType.PartTime_Store));

                Assert.IsTrue(appliedFired, "ScheduleAppliedEvent 발행");
                Assert.AreEqual(ScheduleType.PartTime_Store, applied.Type);
                Assert.Greater(stats.Count, 0, "변경 스탯별 StatChangedEvent 발행");
            }
            finally
            {
                t1.Dispose(); t2.Dispose();
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Controller_Publishes_Rejected_And_No_Applied_When_Invest_Without_Funds()
        {
            var so = MakeState();
            so.Money = 0;
            GameObject go = null;
            bool rejectedFired = false, appliedFired = false;
            ScheduleRejectedEvent rejected = default;

            var t1 = EventBus.Subscribe<ScheduleRejectedEvent>(e => { rejectedFired = true; rejected = e; });
            var t2 = EventBus.Subscribe<ScheduleAppliedEvent>(e => appliedFired = true);
            try
            {
                var c = MakeController(so, out go);
                c.HandleSelection(new ScheduleSelectedCommand(ScheduleType.Invest));

                Assert.IsTrue(rejectedFired);
                Assert.AreEqual(ScheduleRejection.InsufficientFunds, rejected.Reason);
                Assert.IsFalse(appliedFired, "거부 시 적용 이벤트 미발행");
            }
            finally
            {
                t1.Dispose(); t2.Dispose();
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Controller_Publishes_DayEnd_When_Actions_Exhausted()
        {
            var so = MakeState();
            GameObject go = null;
            int dayEndCount = 0;

            var t1 = EventBus.Subscribe<DayEndRequestedEvent>(e => dayEndCount++);
            try
            {
                var c = MakeController(so, out go);
                int actions = GameConstants.ActionsPerDay;
                for (int i = 0; i < actions; i++)
                    c.HandleSelection(new ScheduleSelectedCommand(ScheduleType.Exercise_A));

                Assert.AreEqual(1, dayEndCount, "행동 소진 시 DayEndRequestedEvent 1회");
            }
            finally
            {
                t1.Dispose();
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(so);
            }
        }
    }
}
