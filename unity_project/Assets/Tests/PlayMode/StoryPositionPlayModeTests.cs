using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Core;              // GameStateSO
using LoveAlgo.Events;            // PlayScriptCommand, ShowDialogueCommand, Show*Command, CompletionHandle
using LoveAlgo.Game;              // GameBootstrap, GameManager, PhaseController
using LoveAlgo.Story.StoryEngine; // NarrativeController
using LoveAlgo.UI;                // DialogueView(상주 중화)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 스토리 위치 세이브(🔴) 런타임 계약: ① 엔진이 대기 라인(Text)에서 앵커+무대 스냅샷(해석된 코드ID)을
    /// 상태에 기록하고 정상 종료 시 비운다 ② StartIndex 재개가 앵커 이전 라인(효과 포함)을 재실행하지 않는다
    /// ③ GameBootstrap.TryResumeStory가 무대 재현+재개 발행을 한다(프롤로그=직발행 / 저녁=GameManager 씨임 /
    /// 해석 불가=fail-open 클리어).
    /// </summary>
    public class StoryPositionPlayModeTests
    {
        GameStateSO _gs;
        readonly List<GameObject> _roots = new();
        readonly List<IDisposable> _subs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            foreach (var r in _roots) if (r != null) UnityEngine.Object.DestroyImmediate(r);
            _roots.Clear();
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        static void DestroyResidents<T>() where T : MonoBehaviour
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include))
                UnityEngine.Object.DestroyImmediate(v.gameObject);
        }

        NarrativeController CreateEngine()
        {
            DestroyResidents<NarrativeController>();
            DestroyResidents<PhaseController>();
            DestroyResidents<DialogueView>(); // 같은 명령·핸들 이중 처리 방지

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();
            var go = new GameObject("Engine");
            _roots.Add(go);
            var engine = go.AddComponent<NarrativeController>();
            engine.State = _gs;
            return engine;
        }

        static IEnumerator WaitUntilOrFrames(Func<bool> cond, int frames = 600)
        {
            int guard = 0;
            while (!cond() && guard++ < frames) yield return null;
        }

        [UnityTest]
        public IEnumerator Engine_RecordsAnchorAndStageSnapshot_ClearsOnFinish()
        {
            var engine = CreateEngine();
            yield return null;

            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",BG,,bg_x:Cut,>\n" +          // index 0
                ",Sound,,BGM:bgm_y,>\n" +      // index 1
                ",Char,,C:Enter:c01,>\n" +     // index 2
                ",Text,로아,한 줄,click\n" +    // index 3 ← 대기 앵커
                ",Flow,,End,>\n";              // index 4

            CompletionHandle dialogue = null;
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => dialogue = e.Handle));

            EventBus.Publish(new PlayScriptCommand(csv, "EvtX.csv"));
            yield return WaitUntilOrFrames(() => dialogue != null);
            Assert.IsNotNull(dialogue, "Text 라인 도달");

            var d = _gs.Data;
            Assert.AreEqual("EvtX.csv", d.storyScriptId, "대기 라인에서 스크립트 id 기록");
            Assert.AreEqual(3, d.storyLineIndex, "앵커 = Text 라인 인덱스");
            Assert.AreEqual("bg_x", d.storyBg, "BG 미러(해석된 이름)");
            Assert.AreEqual("bgm_y", d.storyBgm, "BGM 미러");
            Assert.AreEqual(1, d.storyChars.Count, "슬롯 캐릭터 미러");
            Assert.AreEqual((int)CharSlot.C, d.storyChars[0].slot);
            Assert.AreEqual("c01", d.storyChars[0].id);

            dialogue.Complete();
            yield return WaitUntilOrFrames(() => !engine.IsRunning);
            Assert.IsFalse(engine.IsRunning, "스크립트 종료");
            Assert.AreEqual("", d.storyScriptId, "정상 종료 → 위치 클리어(이후 세이브 = 스케줄 재개)");
            Assert.AreEqual("", d.storyBg, "무대 스냅샷도 클리어");
            Assert.AreEqual(0, d.storyChars.Count);
        }

        [UnityTest]
        public IEnumerator Engine_MirrorsStageState_TintEyeLayers_ClearsOnFinish()
        {
            var engine = CreateEngine();
            yield return null;

            // > = Immediate(WaitNext 비대기)라 핸들 미완료여도 통과 → 마지막 Text에서만 대기.
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",FX,,ColorTint:Sepia,>\n" +   // index 0 — 틴트 미러
                ",FX,,EyeClose,>\n" +          // index 1 — 아이마스크 닫힘 미러
                ",SD,,sd_x,>\n" +              // index 2 — SD 레이어 미러
                ",Overlay,,ov_y,>\n" +         // index 3 — Overlay 레이어 미러
                ",Text,로아,anchor,click\n" +   // index 4 ← 대기 앵커
                ",Flow,,End,>\n";

            CompletionHandle dialogue = null;
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => dialogue = e.Handle));

            EventBus.Publish(new PlayScriptCommand(csv, "EvtX.csv"));
            yield return WaitUntilOrFrames(() => dialogue != null);
            Assert.IsNotNull(dialogue, "Text 앵커 도달");

            var d = _gs.Data;
            Assert.Greater(d.storyTintA, 0f, "틴트 미러(활성)");
            Assert.IsTrue(d.storyEyeClosed, "아이마스크 닫힘 미러");
            Assert.AreEqual("sd_x", d.storySd, "SD 레이어 미러");
            Assert.AreEqual("ov_y", d.storyOverlay, "Overlay 레이어 미러");

            dialogue.Complete();
            yield return WaitUntilOrFrames(() => !engine.IsRunning);
            Assert.IsFalse(engine.IsRunning, "스크립트 종료");
            Assert.AreEqual(0f, d.storyTintA, "정상 종료 → 틴트 클리어");
            Assert.IsFalse(d.storyEyeClosed, "아이마스크 클리어");
            Assert.AreEqual("", d.storySd, "SD 클리어");
            Assert.AreEqual("", d.storyOverlay, "Overlay 클리어");
        }

        [UnityTest]
        public IEnumerator Engine_StartIndex_ResumesAtAnchor_WithoutReplayingEarlierLines()
        {
            var engine = CreateEngine();
            yield return null;

            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,Flag:PreEffect,>\n" +  // index 0 — 앵커 이전 효과(재실행 금지 대상)
                ",Text,로아,A,click\n" +        // index 1
                ",Text,로아,B,click\n" +        // index 2 ← 재개 앵커
                ",Flow,,End,>\n";

            var texts = new List<string>();
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { texts.Add(e.Text); e.Handle.Complete(); }));

            EventBus.Publish(new PlayScriptCommand(csv, "EvtX.csv", 2));
            yield return WaitUntilOrFrames(() => !engine.IsRunning && texts.Count > 0);

            Assert.AreEqual(new[] { "B" }, texts, "앵커(인덱스 2)부터 재개 — 이전 라인(A·효과) 미실행");
            Assert.IsFalse(_gs.GetFlag("PreEffect"), "앵커 이전 효과 라인 재실행 없음(이중 적용 방지)");
        }

        // ── GameBootstrap.TryResumeStory ──

        GameBootstrap CreateBootstrap(GameManager manager)
        {
            DestroyResidents<NarrativeController>(); // 재개 발행을 실제 재생으로 잇지 않게(발행만 캡처)
            DestroyResidents<GameBootstrap>();

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            var go = new GameObject("Bootstrap");
            _roots.Add(go);
            go.SetActive(false); // Start 자동 부팅 차단 — TryResumeStory만 직호출
            var boot = go.AddComponent<GameBootstrap>();
            boot.State = _gs;
            boot.Manager = manager;
            return boot;
        }

        [UnityTest]
        public IEnumerator Bootstrap_ResumesEveningEvent_ViaGameManagerSeam()
        {
            DestroyResidents<GameManager>();
            var gmGo = new GameObject("GM");
            _roots.Add(gmGo);
            var gm = gmGo.AddComponent<GameManager>();
            var boot = CreateBootstrap(gm);
            gm.State = _gs;
            yield return null;

            var d = _gs.Data;
            d.storyScriptId = "Event1.csv"; // 실존 StreamingAssets/Story 파일(씨임이 직접 읽음)
            d.storyLineIndex = 5;
            d.storyBg = "bg_z";
            d.storyChars.Add(new GameStateData.StoryCharRecord { slot = 0, id = "c02", emote = "00" });

            string bg = null;
            ShowCharacterCommand? charCmd = null;
            PlayScriptCommand? play = null;
            _subs.Add(EventBus.Subscribe<ShowBackgroundCommand>(e => { bg = e.Name; e.Handle?.Complete(); }));
            _subs.Add(EventBus.Subscribe<ShowCharacterCommand>(e => { charCmd = e; e.Handle?.Complete(); }));
            _subs.Add(EventBus.Subscribe<PlayScriptCommand>(e => play = e));

            boot.TryResumeStory();
            yield return null; // 씨임 코루틴이 발행할 프레임

            Assert.AreEqual("bg_z", bg, "무대 스냅샷 재현(BG Cut)");
            Assert.IsTrue(charCmd.HasValue, "무대 스냅샷 재현(캐릭터)");
            Assert.AreEqual("c02", charCmd.Value.Character);
            Assert.AreEqual(CharSlot.L, charCmd.Value.Slot);
            Assert.IsTrue(play.HasValue, "스크립트 재개 발행(GameManager 씨임 경유 — 종료 후 하루 전환 보존)");
            Assert.AreEqual("Event1.csv", play.Value.Name);
            Assert.AreEqual(5, play.Value.StartIndex, "저장 앵커부터");

            // 정리: 씨임은 finish 대기 중 — finish를 보내면 AdvanceDay→오토세이브 요청이 상주 SaveManager의
            // 실 슬롯0을 덮을 수 있어 금지. TearDown의 GM 파괴(OnDisable)가 게이트를 해제하며 코루틴을 정리한다.
        }

        [UnityTest]
        public IEnumerator Bootstrap_ResumesPrologue_Directly()
        {
            DestroyResidents<GameManager>();
            var boot = CreateBootstrap(null);
            yield return null;

            _gs.Data.storyScriptId = "prologue";
            _gs.Data.storyLineIndex = 10;

            PlayScriptCommand? play = null;
            _subs.Add(EventBus.Subscribe<PlayScriptCommand>(e => play = e));

            boot.TryResumeStory();

            Assert.IsTrue(play.HasValue, "프롤로그 직발행(원 부팅 흐름과 동일)");
            Assert.AreEqual("prologue", play.Value.Name);
            Assert.AreEqual(10, play.Value.StartIndex);
        }

        [UnityTest]
        public IEnumerator Bootstrap_UnresolvableId_FailsOpen_ClearsPosition()
        {
            DestroyResidents<GameManager>(); // 위임 대상 부재 → fail-open 경로
            var boot = CreateBootstrap(null);
            yield return null;

            _gs.Data.storyScriptId = "no_such_script_xyz";
            _gs.Data.storyLineIndex = 7;

            PlayScriptCommand? play = null;
            _subs.Add(EventBus.Subscribe<PlayScriptCommand>(e => play = e));

            boot.TryResumeStory();

            Assert.IsFalse(play.HasValue, "재개 발행 없음");
            Assert.AreEqual("", _gs.Data.storyScriptId, "위치 클리어 → 스케줄 재개(진행 안 막힘)");
            yield return null;
        }
    }
}
