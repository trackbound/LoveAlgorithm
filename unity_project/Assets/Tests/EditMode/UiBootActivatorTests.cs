using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// UiBootActivator 단위테스트 — "에디터=inactive 저장 + 부팅 일괄 활성화" 정책의 방어 동작:
    /// null 배열/슬롯 안전, inactive→active, 이미 active 무해(드리프트 내성), 부모 inactive 사고 경고.
    /// EditMode에선 AddComponent가 Awake를 호출하지 않으므로 ActivateAll() 직접 호출로 검증.
    /// </summary>
    public class UiBootActivatorTests
    {
        GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("ActivatorTest");
        [TearDown] public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        UiBootActivator MakeActivator() => _root.AddComponent<UiBootActivator>();

        GameObject MakeChild(string name, bool active, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root.transform);
            go.SetActive(active);
            return go;
        }

        [Test]
        public void ActivateAll_NullTargets_NoThrow()
        {
            var a = MakeActivator();
            a.Targets = null;
            Assert.DoesNotThrow(a.ActivateAll);
        }

        [Test]
        public void ActivateAll_ActivatesInactive_KeepsActiveUntouched()
        {
            var a = MakeActivator();
            var off = MakeChild("Off", false);
            var on = MakeChild("On", true);
            a.Targets = new[] { off, on };

            a.ActivateAll();

            Assert.IsTrue(off.activeSelf, "inactive 저장 대상 → 부팅 활성화");
            Assert.IsTrue(on.activeSelf, "이미 active 대상 → 무해(드리프트 내성)");
        }

        [Test]
        public void ActivateAll_NullSlot_WarnsAndContinues()
        {
            var a = MakeActivator();
            var off = MakeChild("Off", false);
            a.Targets = new[] { null, off };

            LogAssert.Expect(LogType.Warning, new Regex("미배선"));
            a.ActivateAll();

            Assert.IsTrue(off.activeSelf, "null 슬롯 다음 대상도 계속 처리");
        }

        [Test]
        public void ActivateAll_ParentInactive_Warns()
        {
            var a = MakeActivator();
            var parent = MakeChild("InactiveContainer", false);
            var child = MakeChild("Child", false, parent.transform);
            a.Targets = new[] { child };

            LogAssert.Expect(LogType.Warning, new Regex("비활성 계층"));
            a.ActivateAll();

            Assert.IsTrue(child.activeSelf, "자신은 켜짐");
            Assert.IsFalse(child.activeInHierarchy, "부모 inactive라 계층 비활성 — 경고로 사고 감지");
        }
    }
}
