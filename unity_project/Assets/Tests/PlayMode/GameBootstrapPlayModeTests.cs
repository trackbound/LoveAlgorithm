using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo;        // GameConstants
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Game;   // GameBootstrap

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
    }
}
