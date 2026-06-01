using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M2 slice4 검증: 데이루프 진행 공식(DayLoop) + GameStateSO 카운터/소지금 접근자.
    /// 순수 정적 함수 + SO 인스턴스만 다루므로 EditMode로 충분(프로젝트 관행).
    /// MaxDay/ActionsPerDay는 GameConstants에서 읽으므로 기대값도 상수에서 가져와 SO/폴백 무관하게 검증.
    /// </summary>
    [TestFixture]
    public class DayLoopTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        [Test]
        public void BeginRun_Sets_Day1_And_Full_Actions()
        {
            var so = MakeState();
            try
            {
                so.Day = 17;
                so.RemainingActions = 0;

                DayLoop.BeginRun(so);

                Assert.AreEqual(1, so.Day, "새 게임은 1일차");
                Assert.AreEqual(GameConstants.ActionsPerDay, so.RemainingActions, "행동 풀충전");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ConsumeAction_Decrements_And_Signals_EndOfDay_At_Zero()
        {
            var so = MakeState();
            try
            {
                DayLoop.BeginRun(so);
                Assert.AreEqual(2, GameConstants.ActionsPerDay, "전제: §5 ActionsPerDay=2");

                bool endAfterFirst = DayLoop.ConsumeAction(so);
                Assert.AreEqual(1, so.RemainingActions, "1회 소모 후 1 남음");
                Assert.IsFalse(endAfterFirst, "행동 남았으면 하루 종료 아님");

                bool endAfterSecond = DayLoop.ConsumeAction(so);
                Assert.AreEqual(0, so.RemainingActions, "2회 소모 후 0");
                Assert.IsTrue(endAfterSecond, "행동 소진 시 하루 종료 신호");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ConsumeAction_Does_Not_Go_Negative()
        {
            var so = MakeState();
            try
            {
                so.RemainingActions = 0;
                bool end = DayLoop.ConsumeAction(so);
                Assert.AreEqual(0, so.RemainingActions, "0에서 더 내려가지 않음");
                Assert.IsTrue(end, "0이면 종료 신호 유지");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void AdvanceDay_Increments_Day_And_Refills_Actions()
        {
            var so = MakeState();
            try
            {
                DayLoop.BeginRun(so);
                DayLoop.ConsumeAction(so);

                var result = DayLoop.AdvanceDay(so);

                Assert.AreEqual(2, so.Day, "다음 날 진입");
                Assert.AreEqual(2, result.Day, "결과 Day 일치");
                Assert.AreEqual(GameConstants.ActionsPerDay, so.RemainingActions, "새 날 행동 풀충전");
                Assert.IsFalse(result.EnteredEnding, "진행 중에는 엔딩 아님");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void AdvanceDay_From_MaxDay_Enters_Ending()
        {
            var so = MakeState();
            try
            {
                so.Day = GameConstants.MaxDay; // 30일차에서 하루 종료
                var result = DayLoop.AdvanceDay(so);

                Assert.AreEqual(GameConstants.MaxDay + 1, so.Day, "MaxDay 다음 날");
                Assert.IsTrue(result.EnteredEnding, "MaxDay 초과 시 엔딩 진입");
                Assert.IsTrue(DayLoop.IsEndingReached(so), "IsEndingReached 동일 판정");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void AdvanceDay_Before_MaxDay_Is_Not_Ending()
        {
            var so = MakeState();
            try
            {
                so.Day = GameConstants.MaxDay - 1;
                var result = DayLoop.AdvanceDay(so);

                Assert.AreEqual(GameConstants.MaxDay, so.Day, "아직 MaxDay 도달일");
                Assert.IsFalse(result.EnteredEnding, "MaxDay 당일은 엔딩 아님");
                Assert.IsFalse(DayLoop.IsEndingReached(so), "IsEndingReached false");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Money_Setter_Floors_At_Zero()
        {
            var so = MakeState();
            try
            {
                so.Money = 1000;
                so.AddMoney(500);
                Assert.AreEqual(1500, so.Money, "양수 가산 누적");

                so.AddMoney(-5000); // 1500 - 5000 < 0 → 0 바닥
                Assert.AreEqual(0, so.Money, "소지금은 0 미만으로 떨어지지 않음");

                so.Money = -1; // 직접 음수 세팅도 바닥 클램프
                Assert.AreEqual(0, so.Money, "세터 직접 음수도 0으로 클램프");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void RemainingActions_Survives_Save_RoundTrip()
        {
            var so = MakeState();
            try
            {
                DayLoop.BeginRun(so);
                DayLoop.ConsumeAction(so); // 1 남김

                var json = JsonUtility.ToJson(so.Data);
                var restored = JsonUtility.FromJson<GameStateData>(json);

                Assert.AreEqual(1, restored.remainingActions, "remainingActions 직렬화 라운드트립");
                Assert.AreEqual(1, restored.day, "day 동반 직렬화");
            }
            finally { Object.DestroyImmediate(so); }
        }
    }
}
