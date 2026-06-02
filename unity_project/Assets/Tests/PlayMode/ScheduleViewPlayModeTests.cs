using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Schedule; // ScheduleView, ScheduleSlot, ScheduleType, ScheduleCategory, ScheduleSelectedCommand

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 슬라이스 B: 스케줄 선택 UI 검증. 얇은 뷰가 ScheduleTable로 슬롯을 구성하고
    /// 슬롯 클릭 시 올바른 ScheduleSelectedCommand를 발행하는지(상태 변경 없음).
    /// </summary>
    public class ScheduleViewPlayModeTests
    {
        static ScheduleSlot MakeSlotPrefab()
        {
            var go = new GameObject("SlotPrefab", typeof(RectTransform));
            var slot = go.AddComponent<ScheduleSlot>();
            slot.Button = go.AddComponent<Button>();

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(go.transform);
            slot.NameText = nameGo.AddComponent<TextMeshProUGUI>();

            var descGo = new GameObject("Desc", typeof(RectTransform));
            descGo.transform.SetParent(go.transform);
            slot.DescText = descGo.AddComponent<TextMeshProUGUI>();
            return slot;
        }

        [UnityTest]
        public IEnumerator ShowCategory_Spawns_Slots_And_Click_Publishes_Command()
        {
            var container = new GameObject("Container", typeof(RectTransform)).transform;
            var prefab = MakeSlotPrefab();

            var uiGo = new GameObject("ScheduleView");
            var ui = uiGo.AddComponent<ScheduleView>();
            ui.SlotContainer = container;
            ui.SlotPrefab = prefab;
            ui.StartCategory = ScheduleCategory.Exercise;

            ScheduleType? published = null;
            var sub = EventBus.Subscribe<ScheduleSelectedCommand>(e => published = e.Type);
            try
            {
                ui.ShowCategory(ScheduleCategory.Exercise);
                yield return null;

                Assert.AreEqual(3, ui.Slots.Count, "운동 카테고리 = 3슬롯");
                Assert.AreEqual("헬스장", ui.Slots[0].NameText.text, "ScheduleTable 표시 데이터 바인딩");

                ui.Slots[0].Button.onClick.Invoke(); // 첫 슬롯 클릭

                Assert.IsTrue(published.HasValue, "슬롯 클릭 → ScheduleSelectedCommand 발행");
                Assert.AreEqual(ScheduleType.Exercise_A, published.Value, "Exercise 첫 타입(Exercise_A) 발행");

                // 카테고리 전환 시 슬롯 재구성
                ui.ShowCategory(ScheduleCategory.Study);
                Assert.AreEqual(3, ui.Slots.Count, "공부 카테고리 = 3슬롯");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(uiGo);
                Object.DestroyImmediate(prefab.gameObject);
                Object.DestroyImmediate(container.gameObject);
            }
        }
    }
}
