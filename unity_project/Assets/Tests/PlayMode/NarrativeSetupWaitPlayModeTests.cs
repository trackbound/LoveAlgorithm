using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Core;              // GameStateSO
using LoveAlgo.Events;            // Stage/Audio/EyeMask 명령 + enum
using LoveAlgo.Story.StoryEngine; // NarrativeController
using LoveAlgo.Game;             // PhaseController (격리 청소)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// FX 매크로 Setup/Wait 검증: <see cref="NarrativeController"/>가 <c>Setup</c> 라인을 받아 기존 명령
    /// (ShowBackground/PlayBgm/ShowCharacter/ShowStageLayer/EyeMask)을 <b>즉시(Cut)</b>로 재발행하는지,
    /// <c>Wait</c> 라인이 흐름을 끊지 않고 통과하는지(앞/뒤 대사 보존), await Next에서도 hang하지 않는지.
    /// 뷰 없이 명령을 구독·캡처(핸들 즉시 완료)해 결정적으로 검증한다.
    /// </summary>
    public class NarrativeSetupWaitPlayModeTests
    {
        GameStateSO _gs;
        GameObject _playerGo;
        readonly List<IDisposable> _subs = new();

        readonly List<string> _dialogues = new();
        bool _finished;
        // Setup 캡처
        string _bgName; BgTransition _bgTrans;
        string _bgmName;
        string _charName; CharSlot _charSlot; CharAction _charAction;
        string _overlayName; StageLayerKind _overlayKind; bool _overlayClose;
        bool _eyeSeen; EyeMaskAction _eyeAction;

        NarrativeController SetUp()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(pc.gameObject);

            _dialogues.Clear();
            _finished = false;
            _bgName = null; _bgmName = null; _charName = null; _overlayName = null; _eyeSeen = false;

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { _dialogues.Add(e.Text); e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(_ => _finished = true));
            _subs.Add(EventBus.Subscribe<ShowBackgroundCommand>(e => { _bgName = e.Name; _bgTrans = e.Transition; e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<PlayBgmCommand>(e => { _bgmName = e.Name; }));
            _subs.Add(EventBus.Subscribe<ShowCharacterCommand>(e => { _charName = e.Character; _charSlot = e.Slot; _charAction = e.Action; e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<ShowStageLayerCommand>(e => { _overlayName = e.Name; _overlayKind = e.Kind; _overlayClose = e.IsClose; e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<EyeMaskCommand>(e => { _eyeSeen = true; _eyeAction = e.Action; e.Handle.Complete(); }));

            _playerGo = new GameObject("Player");
            var player = _playerGo.AddComponent<NarrativeController>();
            player.State = _gs;
            return player;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_playerGo != null) UnityEngine.Object.DestroyImmediate(_playerGo);
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        static IEnumerator WaitUntilDone(NarrativeController player)
        {
            int guard = 0;
            while (player.IsRunning && guard++ < 600) yield return null;
            Assert.IsFalse(player.IsRunning, "스크립트가 600프레임 내 종료되어야 함");
        }

        [UnityTest]
        public IEnumerator Setup_Reemits_Existing_Commands_Instantly()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",FX,,Setup:BG=bg1|BGM=로아|Char=하예은:L|Overlay=비|Eye=Close,>\n" +
                ",Text,,셋업후,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "setup"));
            yield return WaitUntilDone(player);

            Assert.AreEqual("bg1", _bgName, "Setup BG → ShowBackgroundCommand");
            Assert.AreEqual(BgTransition.Cut, _bgTrans, "Setup은 즉시(Cut) 전환");
            Assert.AreEqual("로아", _bgmName, "Setup BGM → PlayBgmCommand");
            Assert.AreEqual("하예은", _charName, "Setup Char 이름");
            Assert.AreEqual(CharSlot.L, _charSlot, "Setup Char 슬롯 L");
            Assert.AreEqual(CharAction.Enter, _charAction, "Setup Char은 Enter(등장)");
            Assert.AreEqual("비", _overlayName, "Setup Overlay → ShowStageLayerCommand");
            Assert.AreEqual(StageLayerKind.Overlay, _overlayKind);
            Assert.IsFalse(_overlayClose, "표시(닫기 아님)");
            Assert.IsTrue(_eyeSeen, "Setup Eye → EyeMaskCommand");
            Assert.AreEqual(EyeMaskAction.CloseImmediate, _eyeAction, "Eye=Close는 즉시 감김");
            CollectionAssert.AreEqual(new[] { "셋업후" }, _dialogues, "셋업 후 대사 진행");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator Wait_Does_Not_Break_Flow()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Text,,앞,click\n" +
                ",FX,,Wait:0.05,>\n" +
                ",Text,,뒤,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "wait"));
            yield return WaitUntilDone(player);

            CollectionAssert.AreEqual(new[] { "앞", "뒤" }, _dialogues, "Wait가 흐름을 끊지 않음(앞·뒤 모두 진행)");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator Setup_With_Await_Next_Does_Not_Hang()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",FX,,Setup:BG=bg1,await\n" + // 즉시 연출이라 await여도 즉시 통과해야 함
                ",Text,,통과,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "setup-await"));
            yield return WaitUntilDone(player);

            Assert.AreEqual("bg1", _bgName);
            CollectionAssert.AreEqual(new[] { "통과" }, _dialogues, "await Setup이 hang하지 않고 다음 대사 진행");
            Assert.IsTrue(_finished);
        }
    }
}
