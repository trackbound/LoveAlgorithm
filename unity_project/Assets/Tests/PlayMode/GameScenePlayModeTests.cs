using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LoveAlgo;          // GameConstants
using LoveAlgo.Core;     // GameStateSO, JsonSaveStore
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Schedule; // ScheduleSelectedCommand, ScheduleType
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 슬라이스 A: 실 게임 부팅 씬(Game.unity) 통합 검증.
    /// 코드로 띄운 컴포넌트(다른 PlayMode 테스트)가 아니라 실제 씬을 로드해
    /// 부팅(GameBootstrap)·매니저 EventBus 협업 와이어링을 회귀 보호한다.
    /// </summary>
    public class GameScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator GameScene_Boots_And_RunsDayLoop()
        {
            // 실 씬 로드 → GameBootstrap.Start가 NewGame 부팅(ResetRuntime+Configure+BeginRun)
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; // Start 한 프레임 경과

            var gm = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gm, "씬에 GameManager 존재");
            Assert.IsNotNull(gm.State, "GameManager.State(GameState_Main) 인스펙터 배선됨");

            var state = gm.State;
            Assert.AreEqual(1, state.Day, "부팅 시 1일차");
            Assert.AreEqual(GameConstants.ActionsPerDay, state.RemainingActions, "행동 풀충전");

            JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot); // 직전 잔여 오토세이브 제거
            try
            {
                // 행동 소진 → ScheduleController가 DayEndRequested 발행 → GameManager가 AdvanceDay + 오토세이브
                int startDay = state.Day;
                for (int i = 0; i < GameConstants.ActionsPerDay; i++)
                    EventBus.Publish(new ScheduleSelectedCommand(ScheduleType.Exercise_A));

                Assert.AreEqual(startDay + 1, state.Day, "행동 소진→하루 전환(매니저 간 EventBus 협업)");
                Assert.AreEqual(GameConstants.ActionsPerDay, state.RemainingActions, "새 날 행동 재충전");
                Assert.IsTrue(JsonSaveStore.Exists(JsonSaveStore.AutoSaveSlot), "하루 종료 시 SaveManager 오토세이브");
            }
            finally
            {
                JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot); // 테스트 부수효과(세이브 파일) 정리
            }
        }
    }
}
