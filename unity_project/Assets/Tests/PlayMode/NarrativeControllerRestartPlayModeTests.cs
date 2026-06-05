using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;                 // EventBus
using LoveAlgo.Core;                   // ScreenPhase
using LoveAlgo.Events;                 // PlayScriptCommand, RequestPhaseCommand
using LoveAlgo.Story.StoryEngine;      // NarrativeController
using LoveAlgo.Story.StoryEngine.Flow; // FlowCommandController (격리 청소)
using LoveAlgo.Game;                   // PhaseController (격리 청소)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 재적용(stop/restart) 검증: 진행 중 두 번째 <see cref="PlayScriptCommand"/>가 무시되지 않고 재생되는지 —
    /// 기획 도구(Story CSV Planner)의 Apply 재실행 토대. 대사 완료를 채우지 않아 첫 스크립트가 첫 Text에서
    /// 대기(IsRunning 유지)하게 한 뒤 두 번째를 발행한다. 두 번째가 Story 페이즈를 다시 요청하면 재시작 성공.
    /// 빈(인라인·에셋 없음) 명령은 순수 Stop(재시작 없음)인지도 본다.
    /// </summary>
    public class NarrativeControllerRestartPlayModeTests
    {
        GameObject _go;
        readonly List<IDisposable> _subs = new();

        // GameScene 테스트가 Game.unity를 Single 로드한 채 남길 수 있는 인스턴스 제거(중복 처리 방지).
        static void DestroyLeftovers()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var r in UnityEngine.Object.FindObjectsByType<FlowCommandController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(r.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(pc.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        // 첫 Text에서 멈춘다(대사 핸들을 아무도 완료하지 않음 → WaitUntil 무한 대기 → IsRunning 유지).
        const string OneTextScript =
            "LineID,Type,Speaker,Value,Next\n" +
            ",Text,,대기,click\n" +
            ",Flow,,End,>\n";

        [UnityTest]
        public IEnumerator ReApply_While_Running_Restarts_Not_Ignored()
        {
            DestroyLeftovers();

            int storyRequests = 0;
            _subs.Add(EventBus.Subscribe<RequestPhaseCommand>(e => { if (e.Target == ScreenPhase.Story) storyRequests++; }));

            _go = new GameObject("NC_Restart");
            var nc = _go.AddComponent<NarrativeController>();
            yield return null; // OnEnable 구독 정착

            EventBus.Publish(new PlayScriptCommand(OneTextScript, "first"));
            yield return null; // Run 시작 → Story 요청 → 첫 Text에서 대기
            Assert.IsTrue(nc.IsRunning, "첫 스크립트 진행 중(대사 대기)");
            Assert.AreEqual(1, storyRequests, "첫 재생 Story 요청 1회");

            EventBus.Publish(new PlayScriptCommand(OneTextScript, "second"));
            yield return null; // 첫 코루틴 중단 → 두 번째 재생 → Story 다시 요청
            Assert.AreEqual(2, storyRequests, "재적용 시 두 번째가 무시되지 않고 재생(Story 재요청)");
            Assert.IsTrue(nc.IsRunning, "두 번째 진행 중");
        }

        [UnityTest]
        public IEnumerator Empty_Command_Stops_Without_Restart()
        {
            DestroyLeftovers();

            int storyRequests = 0;
            _subs.Add(EventBus.Subscribe<RequestPhaseCommand>(e => { if (e.Target == ScreenPhase.Story) storyRequests++; }));

            _go = new GameObject("NC_Stop");
            var nc = _go.AddComponent<NarrativeController>();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(OneTextScript, "first"));
            yield return null;
            Assert.IsTrue(nc.IsRunning, "첫 스크립트 진행 중");

            // 빈(인라인·에셋 없음) 명령 = 순수 Stop: 진행분 중단, 새로 시작·경고·에러 없음.
            EventBus.Publish(new PlayScriptCommand("", "story-stop"));
            yield return null;
            Assert.IsFalse(nc.IsRunning, "빈 명령 → 중단(정지)");
            Assert.AreEqual(1, storyRequests, "정지는 새 Story 요청 없음");
        }
    }
}
