using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo;          // GameConstants
using LoveAlgo.Core;     // GameStateSO, DayLoop
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Events;   // DayEndRequested/DayChanged/RequestPhase
using LoveAlgo.Schedule; // ScheduleController, ScheduleSelectedCommand, ScheduleType
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// M5 slice1 PlayMode 검증: EditMode 단위테스트가 못 덮는 라이프사이클 구독 경로(OnEnable).
    /// 런타임에 GameManager/ScheduleController를 띄워 EventBus 체인을 실제 발행으로 검증한다 —
    /// ScheduleSelectedCommand → DayEndRequested → DayChanged/EnteredEnding (Service Locator 없이).
    /// dev 하니스로 임시 검증하던 것을 Test Runner에서 돌아가는 영구 테스트로 승격.
    /// </summary>
    public class GameManagerPlayModeTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            DayLoop.BeginRun(so);
            return so;
        }

        [UnityTest]
        public IEnumerator OnEnable_Subscribes_So_DayEndRequest_Advances_Day()
        {
            var so = MakeState();
            var go = new GameObject("GM_PlayTest");
            var gm = go.AddComponent<GameManager>(); // 플레이모드 → OnEnable → DayEndRequested 구독
            gm.State = so;

            bool changed = false;
            int newDay = 0;
            var sub = EventBus.Subscribe<DayChangedEvent>(e => { changed = true; newDay = e.NewDay; });
            try
            {
                yield return null; // 한 프레임 경과 — 라이프사이클 활성

                EventBus.Publish(new DayEndRequestedEvent(so.Day));

                Assert.IsTrue(changed, "OnEnable 구독으로 DayEndRequested→DayChanged 발행");
                Assert.AreEqual(2, newDay);
                Assert.AreEqual(2, so.Day, "일차 +1");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go); // OnDisable 강제 → 구독 해제(테스트 간 누수 방지)
                Object.DestroyImmediate(so);
            }
        }

        [UnityTest]
        public IEnumerator Full_Chain_ScheduleSelect_To_DayTransition()
        {
            var so = MakeState();
            var gmGo = new GameObject("GM_PlayTest2");
            var gm = gmGo.AddComponent<GameManager>();
            gm.State = so;
            var scGo = new GameObject("SC_PlayTest2");
            var sc = scGo.AddComponent<ScheduleController>();
            sc.State = so;

            int dayEnds = 0;
            bool changed = false;
            var s1 = EventBus.Subscribe<DayEndRequestedEvent>(e => dayEnds++);
            var s2 = EventBus.Subscribe<DayChangedEvent>(e => changed = true);
            try
            {
                yield return null;

                int apd = GameConstants.ActionsPerDay;
                for (int i = 0; i < apd; i++)
                    EventBus.Publish(new ScheduleSelectedCommand(ScheduleType.Exercise_A));

                Assert.AreEqual(1, dayEnds, "행동 소진 시 ScheduleController가 DayEndRequested 1회 발행");
                Assert.IsTrue(changed, "GameManager가 받아 DayChanged 발행");
                Assert.AreEqual(2, so.Day);
            }
            finally
            {
                s1.Dispose(); s2.Dispose();
                Object.DestroyImmediate(gmGo);
                Object.DestroyImmediate(scGo);
                Object.DestroyImmediate(so);
            }
        }

        [UnityTest]
        public IEnumerator OnEnable_LastDay_Publishes_Ending()
        {
            var so = MakeState();
            so.Day = GameConstants.MaxDay;
            var go = new GameObject("GM_PlayTest3");
            var gm = go.AddComponent<GameManager>();
            gm.State = so;

            bool ending = false, changed = false;
            ScreenPhase endingTarget = default;
            var s1 = EventBus.Subscribe<RequestPhaseCommand>(e => { ending = true; endingTarget = e.Target; });
            var s2 = EventBus.Subscribe<DayChangedEvent>(e => changed = true);
            try
            {
                yield return null;

                EventBus.Publish(new DayEndRequestedEvent(so.Day));

                Assert.IsTrue(ending, "MaxDay 초과 진입 시 RequestPhaseCommand 발행");
                Assert.AreEqual(ScreenPhase.Ending, endingTarget, "엔딩 페이즈 요청");
                Assert.IsFalse(changed, "엔딩 진입 시 DayChangedEvent 미발행");
            }
            finally
            {
                s1.Dispose(); s2.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
            }
        }
    }
}
