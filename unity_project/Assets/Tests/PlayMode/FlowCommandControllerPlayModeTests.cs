using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Core;     // GameStateSO
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Events;   // FlowCommandRequestedEvent, AffinityChangedEvent
using LoveAlgo.Affinity; // AffinityFormula
using LoveAlgo.Story.StoryEngine.Flow; // FlowCommandController

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// M3 slice3 PlayMode 검증: FlowCommandController의 OnEnable 구독 경로 — 실제 런타임에서
    /// FlowCommandRequestedEvent 발행 → 순수 인터프리터 적용 → AffinityChangedEvent 통지.
    /// </summary>
    public class FlowCommandControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator OnEnable_Subscribes_So_FlowRequested_Publishes_AffinityChanged()
        {
            AffinityFormula.ResetToFallback();

            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();

            var go = new GameObject("FlowRouter_PlayTest");
            var router = go.AddComponent<FlowCommandController>(); // OnEnable → 구독
            router.State = so;

            bool fired = false;
            string hid = null;
            int score = 0;
            var sub = EventBus.Subscribe<AffinityChangedEvent>(e => { fired = true; hid = e.HeroineId; score = e.NewScore; });
            try
            {
                yield return null; // 라이프사이클 활성

                EventBus.Publish(new FlowCommandRequestedEvent("Affinity:EventChoice:HaYeEun:Event1:3"));

                Assert.IsTrue(fired, "OnEnable 구독으로 FlowRequested→AffinityChanged 발행");
                Assert.AreEqual("HaYeEun", hid);
                Assert.AreEqual(3, score);
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
            }
        }
    }
}
