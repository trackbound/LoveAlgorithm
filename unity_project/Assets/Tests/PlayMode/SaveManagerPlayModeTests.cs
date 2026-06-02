using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Core;   // GameStateSO, DayLoop, JsonSaveStore
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // DayEndRequestedEvent, SaveCompletedEvent
using LoveAlgo.Save;   // SaveManager
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// Save 슬라이스 PlayMode 검증: GameManager 하루전환 → 오토세이브 풀체인을 실제 런타임에서.
    /// DayEndRequested → GameManager.AdvanceDay → SaveRequested(슬롯0) → SaveManager.SaveService.Save →
    /// 파일 생성 + SaveCompleted, 그리고 저장된 새 일차가 재로드로 복원되는지(OnEnable 구독 경로 포함).
    /// </summary>
    public class SaveManagerPlayModeTests
    {
        [UnityTest]
        public IEnumerator Autosave_On_DayTransition_Writes_Slot0_And_Persists_NewDay()
        {
            JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot); // 깨끗한 시작

            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            DayLoop.BeginRun(so); // Day 1 + 행동 풀충전

            var gmGo = new GameObject("GM_SavePlayTest");
            var gm = gmGo.AddComponent<GameManager>();
            gm.State = so;
            var smGo = new GameObject("SM_SavePlayTest");
            var sm = smGo.AddComponent<SaveManager>();
            sm.State = so;

            bool completed = false;
            bool success = false;
            int slot = -1;
            var sub = EventBus.Subscribe<SaveCompletedEvent>(e => { completed = true; success = e.Success; slot = e.Slot; });
            try
            {
                yield return null; // OnEnable 구독 활성

                EventBus.Publish(new DayEndRequestedEvent(so.Day)); // 하루 종료 → 전환 → 오토세이브

                Assert.IsTrue(completed, "오토세이브 완료 통지(SaveCompletedEvent)");
                Assert.IsTrue(success, "저장 성공");
                Assert.AreEqual(JsonSaveStore.AutoSaveSlot, slot, "슬롯0(자동저장)");
                Assert.IsTrue(JsonSaveStore.Exists(JsonSaveStore.AutoSaveSlot), "슬롯0 파일 생성");
                Assert.AreEqual(2, so.Day, "하루전환으로 일차 +1");

                // 저장된 상태가 새 일차(2)를 담고 있는지 — 새 SO로 재로드 검증
                var reloaded = ScriptableObject.CreateInstance<GameStateSO>();
                Assert.IsTrue(SaveService.Load(JsonSaveStore.AutoSaveSlot, reloaded));
                Assert.AreEqual(2, reloaded.Day, "오토세이브가 전환된 새 일차를 영구화");
                Object.DestroyImmediate(reloaded);
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(gmGo);
                Object.DestroyImmediate(smGo);
                Object.DestroyImmediate(so);
                JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
            }
        }
    }
}
