using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // ScreenPhase
using LoveAlgo.Events; // ScreenPhaseChangedEvent
using LoveAlgo.UI;     // UIManager

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// UIManager PlayMode 검증: OnEnable 구독 경로 — ScreenPhaseChangedEvent 발행 시 대상 그룹만 활성/나머지 비활성.
    /// </summary>
    public class UIManagerPlayModeTests
    {
        [UnityTest]
        public IEnumerator ScreenPhaseChanged_Toggles_Groups()
        {
            var go = new GameObject("UIManager_PlayTest");
            var ui = go.AddComponent<UIManager>(); // OnEnable → 구독
            try
            {
                yield return null;

                EventBus.Publish(new ScreenPhaseChangedEvent(ScreenPhase.Schedule, ScreenPhase.Story));

                Assert.IsTrue(ui.GetGroupRoot(ScreenPhase.Story).gameObject.activeSelf, "대상 그룹 활성");
                Assert.IsFalse(ui.GetGroupRoot(ScreenPhase.Schedule).gameObject.activeSelf, "나머지 비활성");
                Assert.IsFalse(ui.GetGroupRoot(ScreenPhase.Ending).gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
