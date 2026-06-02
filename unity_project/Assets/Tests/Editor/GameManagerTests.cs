using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo;        // GameConstants
using LoveAlgo.Core;   // GameStateSO, DayLoop
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // DayEndRequestedEvent, DayChangedEvent, EnteredEndingEvent
// 구 LoveAlgo.Core.GameManager(레거시, 소비처 이식 시 삭제 예정)와 단순명 충돌 → 별칭으로 신규 타입 고정.
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M5 slice1 검증: 하루전환 오케스트레이션(GameManager). DayEndRequestedEvent 구독 →
    /// DayLoop.AdvanceDay(일차+1·행동 풀충전·제한 리셋) → DayChangedEvent/EnteredEndingEvent 발행.
    /// 정상 진행·엔딩 경계·EventBus 구독 경로·null 가드를 결정적으로 확인한다.
    /// 수치는 GameConstants에서 읽어 에셋/폴백 어느 쪽이든 무관하게 통과하도록 한다.
    /// </summary>
    [TestFixture]
    public class GameManagerTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            DayLoop.BeginRun(so); // 1일차 + 행동 풀충전 + 제한 리셋
            return so;
        }

        static GameManager MakeManager(GameStateSO state, out GameObject go)
        {
            go = new GameObject("GameManager_Test");
            var gm = go.AddComponent<GameManager>();
            gm.State = state;
            return gm;
        }

        [Test]
        public void DayEndRequested_Advances_Day_And_Publishes_DayChanged()
        {
            var so = MakeState();
            GameObject go = null;
            bool dayChangedFired = false, endingFired = false;
            DayChangedEvent changed = default;

            var t1 = EventBus.Subscribe<DayChangedEvent>(e => { dayChangedFired = true; changed = e; });
            var t2 = EventBus.Subscribe<EnteredEndingEvent>(e => endingFired = true);
            try
            {
                var gm = MakeManager(so, out go);
                Assert.AreEqual(1, so.Day);
                so.RemainingActions = 0; // 행동 소진 상태 가정

                gm.OnDayEndRequested(new DayEndRequestedEvent(so.Day));

                Assert.IsTrue(dayChangedFired, "DayChangedEvent 발행");
                Assert.AreEqual(1, changed.PreviousDay);
                Assert.AreEqual(2, changed.NewDay);
                Assert.AreEqual(2, so.Day, "일차 +1");
                Assert.AreEqual(GameConstants.ActionsPerDay, so.RemainingActions, "행동 풀충전");
                Assert.IsFalse(endingFired, "정상 진행은 엔딩 미발행");
            }
            finally
            {
                t1.Dispose(); t2.Dispose();
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void DayEndRequested_On_LastDay_Publishes_Ending_And_No_DayChanged()
        {
            var so = MakeState();
            GameObject go = null;
            bool dayChangedFired = false, endingFired = false;
            EnteredEndingEvent ending = default;

            var t1 = EventBus.Subscribe<DayChangedEvent>(e => dayChangedFired = true);
            var t2 = EventBus.Subscribe<EnteredEndingEvent>(e => { endingFired = true; ending = e; });
            try
            {
                var gm = MakeManager(so, out go);
                so.Day = GameConstants.MaxDay; // 마지막 날

                gm.OnDayEndRequested(new DayEndRequestedEvent(so.Day));

                Assert.IsTrue(endingFired, "MaxDay 초과 진입 시 EnteredEndingEvent 발행");
                Assert.AreEqual(GameConstants.MaxDay + 1, ending.Day);
                Assert.AreEqual(GameConstants.MaxDay + 1, so.Day, "AdvanceDay는 일차를 올림(엔딩 경계)");
                Assert.IsFalse(dayChangedFired, "엔딩 진입 시 DayChangedEvent 미발행");
            }
            finally
            {
                t1.Dispose(); t2.Dispose();
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // 비고: OnEnable 구독 경로는 EditMode에서 라이프사이클 메시지가 호출되지 않아 검증 불가
        // (기존 ScheduleController 테스트도 동일 — 직접 호출만 검증). 구독/해제는 검증된 어댑터와 동일 관용구.

        [Test]
        public void Null_State_Is_NoOp_With_Error_Log()
        {
            var go = new GameObject("GM_NullState");
            var gm = go.AddComponent<GameManager>(); // state 미바인딩
            bool any = false;
            var t1 = EventBus.Subscribe<DayChangedEvent>(e => any = true);
            var t2 = EventBus.Subscribe<EnteredEndingEvent>(e => any = true);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("GameManager.*미바인딩"));
                gm.OnDayEndRequested(new DayEndRequestedEvent(0));
                Assert.IsFalse(any, "state 미바인딩 시 어떤 이벤트도 발행 안 함");
            }
            finally
            {
                t1.Dispose(); t2.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
