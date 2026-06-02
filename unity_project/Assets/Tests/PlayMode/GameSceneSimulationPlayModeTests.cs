using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo;          // GameConstants
using LoveAlgo.Core;     // GameStateSO, JsonSaveStore
using LoveAlgo.Schedule; // ScheduleUI, ScheduleSlot
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 슬라이스 B2: 실 씬에서 스케줄 선택 UI가 시뮬레이션 루프를 구동하는지 엔드투엔드 검증.
    /// 코드로 명령을 쏘는 게 아니라, 씬에 배선된 ScheduleUI의 슬롯 버튼을 실제로 눌러
    /// 행동 소진→하루 전환이 일어나는지 본다(UI→EventBus→매니저 협업 전 경로).
    /// </summary>
    public class GameSceneSimulationPlayModeTests
    {
        [UnityTest]
        public IEnumerator ScheduleUI_SlotClicks_DriveDayLoop()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; // Start 부팅 + ScheduleUI.OnEnable(슬롯 생성)

            var ui = Object.FindAnyObjectByType<ScheduleUI>();
            Assert.IsNotNull(ui, "씬에 ScheduleUI 존재");
            Assert.AreEqual(3, ui.Slots.Count, "기본 카테고리(운동) 슬롯 3개 생성·배선됨");
            Assert.IsNotNull(ui.Slots[0].Button, "슬롯 프리팹 Button 배선");

            var gm = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gm, "씬에 GameManager 존재");
            var state = gm.State;
            Assert.AreEqual(1, state.Day, "부팅 시 1일차");

            JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
            try
            {
                // 첫 슬롯을 ActionsPerDay 번 클릭 → 행동 소진 → 하루 전환
                int apd = GameConstants.ActionsPerDay;
                for (int i = 0; i < apd; i++)
                    ui.Slots[0].Button.onClick.Invoke();

                Assert.AreEqual(2, state.Day, "슬롯 클릭으로 행동 소진→하루 전환(UI→매니저 EventBus 경로)");
                Assert.AreEqual(apd, state.RemainingActions, "새 날 행동 재충전");
            }
            finally
            {
                JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
            }
        }
    }
}
