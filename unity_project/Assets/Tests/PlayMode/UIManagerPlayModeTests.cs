using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // UIGroup, ShowUiGroupCommand
using LoveAlgo.UI;     // UIManager

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// UIManager 슬라이스1 PlayMode 검증: OnEnable 구독 경로 — ShowUiGroupCommand 발행 시
    /// 대상 그룹만 활성/나머지 비활성으로 토글되는지 실제 런타임에서 확인.
    /// </summary>
    public class UIManagerPlayModeTests
    {
        [UnityTest]
        public IEnumerator ShowUiGroupCommand_Toggles_Groups()
        {
            var go = new GameObject("UIManager_PlayTest");
            var ui = go.AddComponent<UIManager>(); // OnEnable → 구독
            try
            {
                yield return null;

                EventBus.Publish(new ShowUiGroupCommand(UIGroup.Narrative));

                Assert.IsTrue(ui.GetGroupRoot(UIGroup.Narrative).gameObject.activeSelf, "대상 그룹 활성");
                Assert.IsFalse(ui.GetGroupRoot(UIGroup.Simulation).gameObject.activeSelf, "나머지 비활성");
                Assert.IsFalse(ui.GetGroupRoot(UIGroup.Title).gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
