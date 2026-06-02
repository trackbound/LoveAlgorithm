using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // DayChangedEvent, AffinityChangedEvent, SaveCompletedEvent
using LoveAlgo.UI;     // HudView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// HUD 슬라이스1 PlayMode 검증: HudView가 OnEnable에서 통지를 구독해 바인딩된 TMP 텍스트를
    /// 실제로 갱신하는지(떠 있던 DayChanged/AffinityChanged/SaveCompleted 소비) 확인.
    /// </summary>
    public class HudViewPlayModeTests
    {
        static TMP_Text MakeText(string name)
        {
            var go = new GameObject(name);
            return go.AddComponent<TextMeshProUGUI>();
        }

        [UnityTest]
        public IEnumerator Subscribes_And_Updates_Bound_Texts()
        {
            var hudGo = new GameObject("HudView_PlayTest");
            var hud = hudGo.AddComponent<HudView>(); // OnEnable → 구독
            var dayT = MakeText("day");
            var affT = MakeText("aff");
            var statusT = MakeText("status");
            hud.DayText = dayT;        // 구독 이후 바인딩해도 OK(이벤트 시점에 필드 읽음)
            hud.AffinityText = affT;
            hud.StatusText = statusT;

            try
            {
                yield return null;

                EventBus.Publish(new DayChangedEvent(1, 5));
                EventBus.Publish(new AffinityChangedEvent("HaYeEun", 12));
                EventBus.Publish(new SaveCompletedEvent(0, true));

                Assert.AreEqual("Day 5", dayT.text);
                Assert.AreEqual("HaYeEun ♥ 12", affT.text);
                Assert.AreEqual("저장됨", statusT.text);
            }
            finally
            {
                Object.DestroyImmediate(hudGo);
                Object.DestroyImmediate(dayT.gameObject);
                Object.DestroyImmediate(affT.gameObject);
                Object.DestroyImmediate(statusT.gameObject);
            }
        }
    }
}
