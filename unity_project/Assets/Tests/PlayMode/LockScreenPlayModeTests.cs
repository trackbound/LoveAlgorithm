using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using LoveAlgo.Common;                  // EventBus
using LoveAlgo.Core;                    // GameStateSO
using LoveAlgo.Events;                  // ShowLockScreenCommand, SubmitPasswordCommand, LockMode, CompletionHandle 등
using LoveAlgo.Story.StoryEngine;       // NarrativeController
using LoveAlgo.Story.StoryEngine.Flow;  // LockScreenController
using LoveAlgo.UI;                       // LockScreenView
using LoveAlgo.Game;                     // PhaseController (격리 청소)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 잠금화면(LockScreen FirstSetup) 인터랙티브 검증 — 대기형 Flow.
    /// 엔진(<see cref="NarrativeController"/>: ShowLockScreenCommand 발행 + 핸들 await)·컨트롤러
    /// (<see cref="LockScreenController"/>: 비번 저장 + 핸들 완료)·뷰(<see cref="LockScreenView"/>: 오버레이/발행)
    /// 단위와, 셋을 함께 배선한 **hang 안전 통합**(입력 전엔 await, 입력하면 저장 후 진행)을 검증한다.
    /// </summary>
    public class LockScreenPlayModeTests
    {
        GameStateSO _gs;
        readonly List<IDisposable> _subs = new();
        readonly List<GameObject> _spawned = new();
        readonly List<string> _dialogues = new();
        bool _finished;
        bool _showSeen;
        ShowLockScreenCommand _shown;

        void BaseSetUp()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(pc.gameObject);
            foreach (var c in UnityEngine.Object.FindObjectsByType<LockScreenController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(c.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<LockScreenView>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(v.gameObject);

            _dialogues.Clear();
            _finished = false; _showSeen = false;

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { _dialogues.Add(e.Text); e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(_ => _finished = true));
            _subs.Add(EventBus.Subscribe<ShowLockScreenCommand>(e => { _showSeen = true; _shown = e; }));
        }

        NarrativeController NewEngine()
        {
            var go = new GameObject("NarrativeController");
            _spawned.Add(go);
            var nc = go.AddComponent<NarrativeController>();
            nc.State = _gs;
            return nc;
        }

        LockScreenController NewController()
        {
            var go = new GameObject("LockScreenController");
            _spawned.Add(go);
            var c = go.AddComponent<LockScreenController>();
            c.State = _gs;
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        static IEnumerator WaitUntilDone(NarrativeController nc, int max = 600)
        {
            int guard = 0;
            while (nc.IsRunning && guard++ < max) yield return null;
        }

        // ── 엔진: PlayLockScreen이 ShowLockScreenCommand 발행(FirstSetup/FadeOut), 핸들 완료 시 흐름 진행 ──
        [UnityTest]
        public IEnumerator Engine_Publishes_ShowLockScreen_And_Continues_On_Complete()
        {
            BaseSetUp();
            _subs.Add(EventBus.Subscribe<ShowLockScreenCommand>(e => e.Handle?.Complete())); // 즉시 완료 → 결정적

            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,LockScreen:FirstSetup:FadeOut,await\n" +
                ",Text,,설정완료,click\n" +
                ",Flow,,End,>\n";

            var nc = NewEngine();
            yield return null;
            EventBus.Publish(new PlayScriptCommand(csv, "lock"));
            yield return WaitUntilDone(nc);

            Assert.IsTrue(_showSeen, "LockScreen → ShowLockScreenCommand 발행");
            Assert.AreEqual(LockMode.FirstSetup, _shown.Mode);
            Assert.IsTrue(_shown.FadeOut, "FadeOut 옵션 전달");
            CollectionAssert.AreEqual(new[] { "설정완료" }, _dialogues, "핸들 완료(await) 후 다음 대사 진행");
            Assert.IsTrue(_finished);
        }

        // ── 컨트롤러: FirstSetup이면 비번 저장 + 핸들 완료 ──
        [UnityTest]
        public IEnumerator Controller_FirstSetup_Saves_Password_And_Completes_Handle()
        {
            BaseSetUp();
            NewController();
            yield return null;

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowLockScreenCommand(LockMode.FirstSetup, false, null, handle));
            EventBus.Publish(new SubmitPasswordCommand("9876"));

            Assert.AreEqual("9876", _gs.Password, "FirstSetup 비번 저장");
            Assert.IsTrue(_gs.IsPasswordSet);
            Assert.IsTrue(handle.IsComplete, "핸들 완료(엔진 진행 재개)");
        }

        // ── 컨트롤러: 활성 잠금화면(Show) 없이 들어온 Submit은 무시 ──
        [UnityTest]
        public IEnumerator Controller_Submit_Without_Active_LockScreen_Ignored()
        {
            BaseSetUp();
            NewController();
            yield return null;

            EventBus.Publish(new SubmitPasswordCommand("xxxx")); // Show 없이
            Assert.AreEqual("", _gs.Password, "활성 잠금화면 없으면 저장 안 함");
            Assert.IsFalse(_gs.IsPasswordSet);
        }

        // ── 뷰: Show → 오버레이 활성, 빈 입력 확정은 발행 없이 유지(재입력) ──
        [UnityTest]
        public IEnumerator View_Show_Activates_Overlay_And_Empty_Confirm_Ignored()
        {
            BaseSetUp();
            string published = null;
            _subs.Add(EventBus.Subscribe<SubmitPasswordCommand>(e => published = e.Password));

            var viewGo = new GameObject("LockScreenView");
            viewGo.SetActive(false); // OnEnable 전에 Overlay 바인딩(런타임 생성 순서 — 씬은 바인딩된 채 Enable)
            _spawned.Add(viewGo);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            view.Overlay = overlay;
            // input 미바인딩(null) — 빈 입력 경로 검증
            viewGo.SetActive(true);
            yield return null; // OnEnable → HideImmediate

            Assert.IsFalse(overlay.activeSelf, "부팅 시 숨김");

            EventBus.Publish(new ShowLockScreenCommand(LockMode.FirstSetup, false, null, new CompletionHandle()));
            Assert.IsTrue(overlay.activeSelf, "Show → 오버레이 활성");

            view.Confirm(); // input null → 빈 비번 → 무시
            Assert.IsNull(published, "빈 입력은 발행 안 함");
            Assert.IsTrue(overlay.activeSelf, "빈 입력이면 닫지 않음(재입력 유도)");
        }

        // ── 통합(핵심): 엔진+컨트롤러+뷰 배선 → 입력 전 await, 입력 확정 시 저장 후 진행(hang 0) ──
        [UnityTest]
        public IEnumerator Integration_View_Controller_Engine_NoHang_Saves_On_Submit()
        {
            BaseSetUp();
            var nc = NewEngine();
            NewController();

            var viewGo = new GameObject("LockScreenView");
            viewGo.SetActive(false); // OnEnable 전에 Overlay/Input 바인딩(런타임 생성 순서)
            _spawned.Add(viewGo);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(viewGo.transform);
            var input = inputGo.AddComponent<TMP_InputField>();
            view.Overlay = overlay;
            view.Input = input;
            viewGo.SetActive(true);
            yield return null; // OnEnable 구독

            EventBus.Publish(new PlayScriptCommand(
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,LockScreen:FirstSetup,await\n" +
                ",Text,,설정완료,click\n" +
                ",Flow,,End,>\n", "lock-integration"));
            yield return null; // PlayLockScreen → Show → View 오버레이 on + Controller 핸들 보관

            Assert.IsTrue(overlay.activeSelf, "잠금화면 표시");
            Assert.IsTrue(nc.IsRunning, "입력 전엔 await로 대기");
            CollectionAssert.IsEmpty(_dialogues, "입력 전 다음 대사 미진행");

            input.text = "1234";
            view.Confirm(); // → SubmitPasswordCommand 발행 → Controller 저장 + 핸들 완료

            yield return WaitUntilDone(nc);

            Assert.AreEqual("1234", _gs.Password, "입력 비번 저장(FirstSetup)");
            Assert.IsTrue(_gs.IsPasswordSet);
            Assert.IsFalse(overlay.activeSelf, "확정 후 닫힘");
            CollectionAssert.AreEqual(new[] { "설정완료" }, _dialogues, "입력 후 흐름 진행(hang 0)");
            Assert.IsTrue(_finished);
        }
    }
}
