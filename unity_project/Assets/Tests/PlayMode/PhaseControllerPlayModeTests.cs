using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Core;   // GameStateSO, ScreenPhase
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // RequestPhaseCommand, ScreenPhaseChangedEvent
using LoveAlgo.Game;   // PhaseController

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// PhaseController PlayMode 검증: OnEnable 구독 경로 — RequestPhaseCommand 발행 → 순수 PhaseService 검증 →
    /// state.phase 갱신 + ScreenPhaseChangedEvent 통지. 무효 전환은 상태 불변·통지 없음.
    /// </summary>
    public class PhaseControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator Valid_Request_Updates_Phase_And_Publishes_Changed()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime(); // 부팅 기본 = Story(순수 VN)
            var go = new GameObject("PhaseController_PlayTest");
            var pc = go.AddComponent<PhaseController>(); // OnEnable → 구독
            pc.State = so;

            bool fired = false;
            ScreenPhase from = default, to = default;
            var sub = EventBus.Subscribe<ScreenPhaseChangedEvent>(e => { fired = true; from = e.From; to = e.To; });
            try
            {
                yield return null; // 라이프사이클 활성

                EventBus.Publish(new RequestPhaseCommand(ScreenPhase.Ending)); // Story→Ending(유효, *→Ending)

                Assert.IsTrue(fired, "유효 전환 시 ScreenPhaseChangedEvent 발행");
                Assert.AreEqual(ScreenPhase.Story, from, "이전 = 부팅 기본 Story");
                Assert.AreEqual(ScreenPhase.Ending, to);
                Assert.AreEqual(ScreenPhase.Ending, so.Phase, "state.phase 갱신");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
            }
        }

        [UnityTest]
        public IEnumerator Invalid_Request_Is_NoOp()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            so.Phase = ScreenPhase.Ending; // 인게임 종착 — 나가는 전환 없음
            var go = new GameObject("PhaseController_PlayTest2");
            var pc = go.AddComponent<PhaseController>();
            pc.State = so;

            bool fired = false;
            var sub = EventBus.Subscribe<ScreenPhaseChangedEvent>(e => fired = true);
            try
            {
                yield return null;

                EventBus.Publish(new RequestPhaseCommand(ScreenPhase.Story)); // Ending→Story = 무효(경고 로그 1건)

                Assert.IsFalse(fired, "무효 전환은 통지 없음");
                Assert.AreEqual(ScreenPhase.Ending, so.Phase, "상태 불변");
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
