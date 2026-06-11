using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo;          // GameConstants
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Core;     // GameStateSO, JsonSaveStore
using LoveAlgo.Events;   // ShowDialogueCommand, ShowChoiceCommand
using LoveAlgo.Schedule; // ScheduleActionButton, ScheduleType
using LoveAlgo.UI;       // EndingView
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 슬라이스 C: 30일 루프의 종료점. 마지막 날 행동을 소진하면 저녁 이벤트 씨임이 Confession.csv(Day30 매핑)를
    /// 재생하고, 종료 후 하루 전환 → PhaseController 그룹 토글로 EndingView가 표시되는지 실 씬에서 검증.
    /// 정적 스케줄 액션 버튼(편의점=PartTime_Store)을 눌러 루프를 구동한다. 대사는 핸들을 즉시 완료시켜 진행.
    /// </summary>
    public class GameSceneEndingPlayModeTests
    {
        static ScheduleActionButton FindAction(ScheduleType type)
        {
            foreach (var b in UnityEngine.Object.FindObjectsByType<ScheduleActionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.Type == type) return b;
            return null;
        }

        [UnityTest]
        public IEnumerator LastDay_Exhausted_Plays_Confession_Then_Shows_EndingView()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<LoveAlgo.Game.GameBootstrap>();
            if (bootstrap != null) bootstrap.PrologueCsv = ""; // 프롤로그 스킵 — 이 테스트는 엔딩 진입만 격리 검증
            yield return null; // 부팅 + UI 활성

            var ending = UnityEngine.Object.FindAnyObjectByType<EndingView>(FindObjectsInactive.Include);
            Assert.IsNotNull(ending, "씬에 EndingView 존재");
            Assert.IsFalse(ending.IsShown, "평소 엔딩 루트는 숨김");

            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            var state = gm.State;

            // 정적 액션 버튼(PartTime_Store) 활성까지 대기(부팅→스케줄 페이즈, 1프레임 초과 가능).
            ScheduleActionButton action = null;
            float uiDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < uiDeadline)
            {
                action = FindAction(ScheduleType.PartTime_Store);
                if (action != null && action.isActiveAndEnabled) break;
                action = null;
                yield return null;
            }
            Assert.IsNotNull(action, "PartTime_Store 정적 액션 버튼이 활성화되어야 함");
            var button = action.GetComponent<Button>();

            state.Day = GameConstants.MaxDay; // 마지막 날로 점프
            JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);

            // 고백 내러티브 자동 진행: 대사 핸들 즉시 완료(클릭 대체), 선택지는 첫 항목(현 Confession엔 없음 — 안전망).
            var subs = new List<IDisposable>
            {
                EventBus.Subscribe<ShowDialogueCommand>(e => e.Handle?.Complete()),
                EventBus.Subscribe<ShowChoiceCommand>(e => e.Handle?.Select(0)),
            };
            bool confessionPlayed = false;
            subs.Add(EventBus.Subscribe<PlayScriptCommand>(e => { if (e.Name != null && e.Name.Contains("Confession")) confessionPlayed = true; }));

            try
            {
                // 마지막 날 행동 소진 → DayEndRequested → 저녁 이벤트(Confession) 재생·대기 → AdvanceDay(Day>MaxDay)
                // → RequestPhaseCommand(Ending) → 그룹 토글
                int apd = GameConstants.ActionsPerDay;
                for (int i = 0; i < apd; i++)
                    button.onClick.Invoke();

                // 연출(BG 크로스/배너)이 실시간 코루틴이라 종료까지 프레임 대기(상한 30s).
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!ending.IsShown && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.IsTrue(confessionPlayed, "Day30 저녁 이벤트로 Confession 스크립트 재생");
                Assert.IsTrue(ending.IsShown, "고백 종료 → 30일 종료 → 엔딩 화면 표시");
            }
            finally
            {
                foreach (var s in subs) s.Dispose();
                JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
            }
        }
    }
}
