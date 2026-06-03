using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core; // ScreenPhase
using LoveAlgo.UI;   // UIManager

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// UIManager 검증: 화면 그룹 루트 제공(자동 생성·중복 방지) + ShowGroup(대상 활성/나머지 비활성). 그룹 = ScreenPhase.
    /// </summary>
    [TestFixture]
    public class UIManagerTests
    {
        [Test]
        public void GetGroupRoot_Creates_Distinct_Children_And_Caches()
        {
            var go = new GameObject("UIManager_Test");
            var ui = go.AddComponent<UIManager>();
            try
            {
                var story = ui.GetGroupRoot(ScreenPhase.Story);
                var sched = ui.GetGroupRoot(ScreenPhase.Schedule);
                var end   = ui.GetGroupRoot(ScreenPhase.Ending);

                Assert.IsNotNull(story); Assert.IsNotNull(sched); Assert.IsNotNull(end);
                Assert.AreNotSame(story, sched);
                Assert.AreNotSame(sched, end);
                Assert.AreSame(go.transform, story.parent, "그룹 루트는 매니저의 자식");
                Assert.AreSame(story, ui.GetGroupRoot(ScreenPhase.Story), "재호출 시 동일 루트(중복 생성 안 함)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShowGroup_Activates_Target_Hides_Others()
        {
            var go = new GameObject("UIManager_Test2");
            var ui = go.AddComponent<UIManager>();
            try
            {
                ui.ShowGroup(ScreenPhase.Schedule);
                Assert.IsFalse(ui.GetGroupRoot(ScreenPhase.Story).gameObject.activeSelf);
                Assert.IsTrue(ui.GetGroupRoot(ScreenPhase.Schedule).gameObject.activeSelf);
                Assert.IsFalse(ui.GetGroupRoot(ScreenPhase.Ending).gameObject.activeSelf);

                ui.ShowGroup(ScreenPhase.Ending);
                Assert.IsFalse(ui.GetGroupRoot(ScreenPhase.Schedule).gameObject.activeSelf);
                Assert.IsTrue(ui.GetGroupRoot(ScreenPhase.Ending).gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
