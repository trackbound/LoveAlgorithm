using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using LoveAlgo.Schedule;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 스케줄 카테고리 탭 신 위젯(CategoryTab/CategoryTabBar) 런타임 검증. 구 ButtonEX/TabGroup을 대체하며,
    /// 신 코드가 0구독이라 죽어있던 카테고리 전환을 실제 동작으로 배선한 것의 회귀 가드:
    /// 탭 클릭 → CategoryTabBar.Select → ScheduleView.ShowCategory → 해당 카테고리 슬롯 재구성.
    /// CategoryTabBar는 자식 CategoryTab을 자동 수집한다(인스펙터 배열은 MCP로 못 채워 자식 수집이 단일 진실원).
    /// </summary>
    public class CategoryTabBarPlayModeTests
    {
        readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        static void SetPrivate(object o, string field, object val)
            => o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, val);

        static ScheduleCategory SlotCategory(ScheduleSlot s)
        {
            var type = (ScheduleType)typeof(ScheduleSlot)
                .GetField("_type", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(s);
            return ScheduleTable.GetCategory(type);
        }

        GameObject Track(GameObject go) { _spawned.Add(go); return go; }

        [UnityTest]
        public IEnumerator TabClick_Drives_ScheduleView_Category()
        {
            // ScheduleView: container + ScheduleSlot 프리팹(런타임 GO). 세팅 동안 OnEnable 지연.
            var viewGo = Track(new GameObject("ScheduleUI"));
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<ScheduleView>();
            var container = Track(new GameObject("Container")).transform;
            container.SetParent(viewGo.transform, false);
            var slotPrefab = Track(new GameObject("SlotPrefab")).AddComponent<ScheduleSlot>();
            view.SlotContainer = container;
            view.SlotPrefab = slotPrefab;
            view.StartCategory = ScheduleCategory.Exercise;

            // Tabs(CategoryTabBar) + 자식 CategoryTab 3개(각 Button). 자동 수집 대상.
            var barGo = Track(new GameObject("Tabs"));
            barGo.SetActive(false);
            var tabs = new List<CategoryTab>();
            for (int i = 0; i < 3; i++)
            {
                var t = new GameObject($"Tab{i}", typeof(Button));
                t.transform.SetParent(barGo.transform, false);
                tabs.Add(t.AddComponent<CategoryTab>());
            }
            var bar = barGo.AddComponent<CategoryTabBar>();
            SetPrivate(bar, "scheduleView", view);
            SetPrivate(bar, "categories", new[] { ScheduleCategory.PartTime, ScheduleCategory.Exercise, ScheduleCategory.Study });
            SetPrivate(bar, "defaultIndex", 1); // Exercise

            viewGo.SetActive(true); // ScheduleView.OnEnable → ShowCategory(Exercise)
            barGo.SetActive(true);  // CategoryTabBar.Awake(자동수집) + Start(Select(1))
            yield return null;

            // 기본 = Exercise(defaultIndex 1): 표시 슬롯이 전부 Exercise 카테고리
            Assert.AreEqual(1, bar.CurrentIndex, "기본 탭 = Exercise(index1)");
            Assert.Greater(view.Slots.Count, 0, "Exercise 슬롯 생성");
            foreach (var s in view.Slots)
                Assert.AreEqual(ScheduleCategory.Exercise, SlotCategory(s), "기본 표시는 Exercise 카테고리");

            // 탭0(PartTime) 클릭 → PartTime 카테고리로 전환
            tabs[0].Button.onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, bar.CurrentIndex, "PartTime 선택");
            Assert.Greater(view.Slots.Count, 0);
            foreach (var s in view.Slots)
                Assert.AreEqual(ScheduleCategory.PartTime, SlotCategory(s), "탭 클릭 후 PartTime 카테고리");

            // 자동 수집 확인: tabs 인스펙터 미배선이어도 자식 3개 수집
            var collected = (CategoryTab[])typeof(CategoryTabBar)
                .GetField("tabs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bar);
            Assert.AreEqual(3, collected.Length, "자식 CategoryTab 3개 자동 수집");
        }
    }
}
