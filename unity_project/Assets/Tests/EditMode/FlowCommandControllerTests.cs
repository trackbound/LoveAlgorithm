using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Core;     // GameStateSO
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Events;   // FlowCommandRequestedEvent, AffinityChangedEvent, DayChangedEvent
using LoveAlgo.Affinity; // AffinityFormula
using LoveAlgo.Story.StoryEngine.Flow; // FlowCommandController

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 slice3 검증: <see cref="FlowCommandController"/>(순수 인터프리터의 EventBus 어댑터)가 명령 종류에 따라
    /// AffinityChangedEvent / DayChangedEvent를 정확히 발행하는지, 실패/미바인딩 가드를 확인한다.
    /// </summary>
    [TestFixture]
    public class FlowCommandControllerTests
    {
        [SetUp]
        public void Reset() => AffinityFormula.ResetToFallback();

        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        static FlowCommandController MakeRouter(GameStateSO state, out GameObject go)
        {
            go = new GameObject("FlowCommandController_Test");
            var r = go.AddComponent<FlowCommandController>();
            r.State = state;
            return r;
        }

        [Test]
        public void Affinity_Command_Publishes_AffinityChanged()
        {
            var so = MakeState();
            GameObject go = null;
            bool fired = false;
            AffinityChangedEvent ev = default;
            var sub = EventBus.Subscribe<AffinityChangedEvent>(e => { fired = true; ev = e; });
            try
            {
                var r = MakeRouter(so, out go);
                r.OnFlowRequested(new FlowCommandRequestedEvent("Affinity:EventChoice:HaYeEun:Event1:3"));

                Assert.IsTrue(fired, "AffinityChangedEvent 발행");
                Assert.AreEqual("HaYeEun", ev.HeroineId);
                Assert.AreEqual(3, ev.NewScore);
            }
            finally
            {
                sub.Dispose();
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Day_Command_Publishes_DayChanged()
        {
            var so = MakeState();
            GameObject go = null;
            bool fired = false;
            DayChangedEvent ev = default;
            var sub = EventBus.Subscribe<DayChangedEvent>(e => { fired = true; ev = e; });
            try
            {
                var r = MakeRouter(so, out go);
                r.OnFlowRequested(new FlowCommandRequestedEvent("Day:5"));

                Assert.IsTrue(fired, "DayChangedEvent 발행");
                Assert.AreEqual(1, ev.PreviousDay);
                Assert.AreEqual(5, ev.NewDay);
                Assert.AreEqual(5, so.Day);
            }
            finally
            {
                sub.Dispose();
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Malformed_Command_Publishes_Nothing()
        {
            var so = MakeState();
            GameObject go = null;
            bool any = false;
            var s1 = EventBus.Subscribe<AffinityChangedEvent>(e => any = true);
            var s2 = EventBus.Subscribe<DayChangedEvent>(e => any = true);
            try
            {
                var r = MakeRouter(so, out go);
                r.OnFlowRequested(new FlowCommandRequestedEvent("Affinity:Point:Nobody:Gift:1"));

                Assert.IsFalse(any, "실패 명령은 어떤 통지도 발행 안 함");
            }
            finally
            {
                s1.Dispose(); s2.Dispose();
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Null_State_Logs_Error_And_Publishes_Nothing()
        {
            GameObject go = null;
            bool any = false;
            var sub = EventBus.Subscribe<AffinityChangedEvent>(e => any = true);
            try
            {
                go = new GameObject("FlowCommandController_NullState");
                var r = go.AddComponent<FlowCommandController>();
                LogAssert.Expect(LogType.Error, new Regex("FlowCommandController.*미바인딩"));

                r.OnFlowRequested(new FlowCommandRequestedEvent("Affinity:Point:HaYeEun:Gift:1"));

                Assert.IsFalse(any);
            }
            finally
            {
                sub.Dispose();
                if (go != null) Object.DestroyImmediate(go);
            }
        }
    }
}
