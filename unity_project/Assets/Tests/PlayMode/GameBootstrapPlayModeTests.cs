using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo;        // GameConstants
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // PlayScriptCommand
using LoveAlgo.Game;   // GameBootstrap, GameEntry, BootMode

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 부팅 와이어링 PlayMode 검증: GameBootstrap이 Start에서 새 게임을 부팅(상태 리셋+1일차)하는지.
    /// </summary>
    public class GameBootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator Start_Boots_NewGame()
        {
            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            gs.ResetRuntime();
            gs.Day = 9; // 더럽힘

            var go = new GameObject("GameBootstrap_PlayTest");
            var boot = go.AddComponent<GameBootstrap>();
            boot.State = gs; // Start 실행 전 바인딩(Start는 다음 프레임)
            try
            {
                yield return null; // Start → Boot → NewGame

                Assert.AreEqual(1, gs.Day, "Start 부팅으로 1일차");
                Assert.AreEqual(GameConstants.ActionsPerDay, gs.RemainingActions, "행동 풀충전");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(gs);
            }
        }

        [UnityTest]
        public IEnumerator NewGame_Boot_Publishes_Prologue()
        {
            int count = 0;
            string capturedName = null;
            var sub = EventBus.Subscribe<PlayScriptCommand>(e => { count++; capturedName = e.Name; });

            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            GameEntry.PendingMode = BootMode.NewGame;
            var go = new GameObject("Bootstrap_Prologue");
            go.SetActive(false); // Start 자동부팅 억제 — Boot() 직접 호출로 결정적 검증
            var boot = go.AddComponent<GameBootstrap>();
            boot.State = gs;
            boot.BootLoadingSeconds = 0f; // 로딩 지연 없이 즉시 프롤로그 발행(이 검증의 관심사 아님 + 비활성 GO 코루틴 회피)
            try
            {
                boot.Boot();
                Assert.AreEqual(1, count, "새 게임 부팅 시 정확히 1회 발행");
                Assert.AreEqual("prologue", capturedName, "프롤로그(Prologue.csv)를 'prologue' 이름으로 발행");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(gs);
                GameEntry.PendingMode = BootMode.NewGame; // 격리 복원
            }
            yield return null;
        }
    }
}
