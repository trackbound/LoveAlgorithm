using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Core;   // GameStateSO, JsonSaveStore
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // SaveRequestedEvent, SaveCompletedEvent
using LoveAlgo.Save;   // SaveService, SaveManager

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// Save 슬라이스 검증: 순수 <see cref="SaveService"/>(캡처/저장/로드 라운드트립·가드) +
    /// 어댑터 <see cref="SaveManager"/>(SaveRequested→SaveCompleted 발행). 실제 세이브 슬롯과 충돌하지
    /// 않도록 전용 테스트 슬롯을 쓰고 매 케이스 전후로 삭제한다.
    /// </summary>
    [TestFixture]
    public class SaveServiceTests
    {
        const int TestSlot = 987; // 0(자동)/1+(유저) 회피

        [SetUp]
        public void Before() => JsonSaveStore.Delete(TestSlot);

        [TearDown]
        public void After() => JsonSaveStore.Delete(TestSlot);

        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        [Test]
        public void RoundTrip_SaveThenLoad_RestoresState()
        {
            var src = MakeState();
            var dst = MakeState();
            try
            {
                src.Day = 5;
                src.RemainingActions = 2;
                src.Money = 1234;
                src.SetStat("Int", 42);
                src.SetStat("Soc", 17);
                src.AddLove("roa", 7);
                src.SetFlag("met_roa", true);

                Assert.IsTrue(SaveService.Save(TestSlot, src, "Day 5"), "저장 성공");
                Assert.IsTrue(JsonSaveStore.Exists(TestSlot), "슬롯 파일 생성");

                Assert.IsTrue(SaveService.Load(TestSlot, dst), "로드 성공");
                Assert.AreEqual(5, dst.Day);
                Assert.AreEqual(2, dst.RemainingActions);
                Assert.AreEqual(1234, dst.Money);
                Assert.AreEqual(42, dst.GetStat("Int"));
                Assert.AreEqual(17, dst.GetStat("Soc"));
                Assert.AreEqual(7, dst.GetLove("roa"));
                Assert.IsTrue(dst.GetFlag("met_roa"));
            }
            finally
            {
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(dst);
            }
        }

        [Test]
        public void Save_NullState_ReturnsFalse()
        {
            Assert.IsFalse(SaveService.Save(TestSlot, null, "x"));
            Assert.IsFalse(JsonSaveStore.Exists(TestSlot), "null 상태는 파일 미생성");
        }

        [Test]
        public void Load_MissingSlot_ReturnsFalse_StateUnchanged()
        {
            var so = MakeState();
            try
            {
                so.Day = 9;
                Assert.IsFalse(JsonSaveStore.Exists(TestSlot));
                Assert.IsFalse(SaveService.Load(TestSlot, so), "없는 슬롯 로드는 false");
                Assert.AreEqual(9, so.Day, "실패 시 상태 불변");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Load_NullState_ReturnsFalse()
        {
            Assert.IsFalse(SaveService.Load(TestSlot, null));
        }

        // ── 어댑터 SaveManager (EventBus 발행) ──

        static SaveManager MakeManager(GameStateSO state, out GameObject go)
        {
            go = new GameObject("SaveManager_Test");
            var m = go.AddComponent<SaveManager>();
            m.State = state;
            return m;
        }

        [Test]
        public void Manager_Publishes_SaveCompleted_True_On_Request()
        {
            var so = MakeState();
            GameObject go = null;
            bool fired = false;
            SaveCompletedEvent done = default;
            var sub = EventBus.Subscribe<SaveCompletedEvent>(e => { fired = true; done = e; });
            try
            {
                so.Day = 3;
                var m = MakeManager(so, out go);
                m.OnSaveRequested(new SaveRequestedEvent(TestSlot, "test"));

                Assert.IsTrue(fired, "SaveCompletedEvent 발행");
                Assert.AreEqual(TestSlot, done.Slot);
                Assert.IsTrue(done.Success);
                Assert.IsTrue(JsonSaveStore.Exists(TestSlot), "실제 파일 생성");
            }
            finally
            {
                sub.Dispose();
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Manager_NullState_Publishes_SaveCompleted_False_With_Error()
        {
            GameObject go = null;
            bool fired = false;
            SaveCompletedEvent done = default;
            var sub = EventBus.Subscribe<SaveCompletedEvent>(e => { fired = true; done = e; });
            try
            {
                go = new GameObject("SaveManager_NullState");
                var m = go.AddComponent<SaveManager>(); // state 미바인딩
                LogAssert.Expect(LogType.Error, new Regex("SaveManager.*미바인딩"));

                m.OnSaveRequested(new SaveRequestedEvent(TestSlot, "test"));

                Assert.IsTrue(fired);
                Assert.IsFalse(done.Success, "미바인딩 시 실패 통지");
                Assert.IsFalse(JsonSaveStore.Exists(TestSlot), "파일 미생성");
            }
            finally
            {
                sub.Dispose();
                if (go != null) Object.DestroyImmediate(go);
            }
        }
    }
}
