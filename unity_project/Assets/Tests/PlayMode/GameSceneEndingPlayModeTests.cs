using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LoveAlgo;          // GameConstants
using LoveAlgo.Core;     // GameStateSO, JsonSaveStore
using LoveAlgo.Schedule; // ScheduleView
using LoveAlgo.UI;       // EndingView
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 슬라이스 C: 30일 루프의 종료점. 마지막 날 행동을 소진해 엔딩에 진입하면
    /// EndingView이 EnteredEndingEvent를 받아 엔딩 루트를 표시하는지 실 씬에서 검증.
    /// </summary>
    public class GameSceneEndingPlayModeTests
    {
        [UnityTest]
        public IEnumerator LastDay_Exhausted_Shows_EndingView()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; // 부팅 + UI OnEnable

            var ending = Object.FindAnyObjectByType<EndingView>(FindObjectsInactive.Include);
            Assert.IsNotNull(ending, "씬에 EndingView 존재");
            Assert.IsFalse(ending.IsShown, "평소 엔딩 루트는 숨김");

            var gm = Object.FindAnyObjectByType<GameManager>();
            var state = gm.State;
            var ui = Object.FindAnyObjectByType<ScheduleView>();
            Assert.IsNotNull(ui, "씬에 ScheduleView 존재");

            state.Day = GameConstants.MaxDay; // 마지막 날로 점프
            JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
            try
            {
                // 마지막 날 행동 소진 → DayEndRequested → AdvanceDay(Day>MaxDay) → EnteredEndingEvent
                int apd = GameConstants.ActionsPerDay;
                for (int i = 0; i < apd; i++)
                    ui.Slots[0].Button.onClick.Invoke();

                Assert.IsTrue(ending.IsShown, "30일 종료 → 엔딩 화면 표시");
            }
            finally
            {
                JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
            }
        }
    }
}
