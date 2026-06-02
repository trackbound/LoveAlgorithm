using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Events; // UIGroup
using LoveAlgo.UI;     // UIManager

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// UIManager 슬라이스1 검증: 그룹 루트 제공(자동 생성·중복 방지) + ShowGroup(대상 활성/나머지 비활성).
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
                var n = ui.GetGroupRoot(UIGroup.Narrative);
                var s = ui.GetGroupRoot(UIGroup.Simulation);
                var t = ui.GetGroupRoot(UIGroup.Title);

                Assert.IsNotNull(n); Assert.IsNotNull(s); Assert.IsNotNull(t);
                Assert.AreNotSame(n, s);
                Assert.AreNotSame(s, t);
                Assert.AreSame(go.transform, n.parent, "그룹 루트는 매니저의 자식");
                Assert.AreSame(n, ui.GetGroupRoot(UIGroup.Narrative), "재호출 시 동일 루트(중복 생성 안 함)");
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
                ui.ShowGroup(UIGroup.Simulation);
                Assert.IsFalse(ui.GetGroupRoot(UIGroup.Narrative).gameObject.activeSelf);
                Assert.IsTrue(ui.GetGroupRoot(UIGroup.Simulation).gameObject.activeSelf);
                Assert.IsFalse(ui.GetGroupRoot(UIGroup.Title).gameObject.activeSelf);

                ui.ShowGroup(UIGroup.Title);
                Assert.IsFalse(ui.GetGroupRoot(UIGroup.Simulation).gameObject.activeSelf);
                Assert.IsTrue(ui.GetGroupRoot(UIGroup.Title).gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
