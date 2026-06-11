using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo;          // GameConstants
using LoveAlgo.Core;     // GameStateSO, JsonSaveStore
using LoveAlgo.Schedule; // ScheduleActionButton, ScheduleType
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 슬라이스 B2(정적 UI 전환): 실 씬에서 정적 스케줄 액션 버튼이 시뮬레이션 루프를 구동하는지 엔드투엔드 검증.
    /// 코드로 명령을 쏘는 게 아니라, 씬에 배선된 <see cref="ScheduleActionButton"/>(편의점=PartTime_Store)을 실제로
    /// 눌러 행동 소진→하루 전환이 일어나는지 본다(UI→EventBus→매니저 협업 전 경로). 구 동적 ScheduleView 슬롯
    /// 생성을 대체한 정적 버튼 설계 반영.
    /// </summary>
    public class GameSceneSimulationPlayModeTests
    {
        // 지정 타입의 정적 액션 버튼을 찾는다(비활성 포함).
        static ScheduleActionButton FindAction(ScheduleType type)
        {
            foreach (var b in Object.FindObjectsByType<ScheduleActionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.Type == type) return b;
            return null;
        }

        [UnityTest]
        public IEnumerator ActionButtonClicks_DriveDayLoop()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            var bootstrap = Object.FindAnyObjectByType<LoveAlgo.Game.GameBootstrap>();
            if (bootstrap != null) bootstrap.PrologueCsv = ""; // 프롤로그 스킵 — 이 테스트는 스케줄 루프만 격리 검증
            yield return null; // Start 부팅 + 스케줄 UI 활성

            // 정적 액션 버튼(알바/편의점=PartTime_Store, 비제한)이 활성화될 때까지 대기.
            // 스케줄 UI 활성 타이밍이 1프레임을 넘을 수 있다(HANDOFF) — 프레임-1 가정 제거.
            ScheduleActionButton action = null;
            float readyDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < readyDeadline)
            {
                action = FindAction(ScheduleType.PartTime_Store);
                if (action != null && action.isActiveAndEnabled) break;
                action = null;
                yield return null;
            }
            Assert.IsNotNull(action, "PartTime_Store 정적 액션 버튼이 활성화되어야 함(부팅→스케줄 페이즈)");
            var button = action.GetComponent<Button>();
            Assert.IsNotNull(button, "액션 버튼에 Button 컴포넌트");

            var gm = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gm, "씬에 GameManager 존재");
            var state = gm.State;
            Assert.AreEqual(1, state.Day, "부팅 시 1일차");

            JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
            try
            {
                // 편의점(PartTime_Store, 비제한)을 ActionsPerDay 번 클릭 → 행동 소진 → 하루 전환
                int apd = GameConstants.ActionsPerDay;
                for (int i = 0; i < apd; i++)
                    button.onClick.Invoke();

                Assert.AreEqual(2, state.Day, "정적 액션 버튼 클릭으로 행동 소진→하루 전환(UI→매니저 EventBus 경로)");
                Assert.AreEqual(apd, state.RemainingActions, "새 날 행동 재충전");
            }
            finally
            {
                JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
            }
        }
    }
}
