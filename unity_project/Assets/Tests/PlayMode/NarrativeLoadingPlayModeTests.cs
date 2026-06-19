using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Core;              // GameStateSO
using LoveAlgo.Events;            // ShowLoadingCommand 등
using LoveAlgo.Story.StoryEngine; // NarrativeController
using LoveAlgo.Game;             // PhaseController (격리 청소)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 로딩 화면 검증: <see cref="NarrativeController"/>가 대기형 Flow <c>LoadingScene[:time]</c>(별칭 <c>Loading</c>)을
    /// <see cref="ShowLoadingCommand"/>(time 생략 시 동결 2.0s)로 발행하고, 핸들 완료까지 await한 뒤 흐름을 잇는지.
    /// 뷰 없이 명령 구독·캡처(핸들 즉시 완료 → 실제 2s 대기 없이 결정적)해 검증한다.
    /// </summary>
    public class NarrativeLoadingPlayModeTests
    {
        GameStateSO _gs;
        GameObject _playerGo;
        readonly List<IDisposable> _subs = new();

        readonly List<string> _dialogues = new();
        bool _finished;
        bool _loadSeen;
        float _loadSeconds;
        string _loadKey;

        NarrativeController SetUp()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(pc.gameObject);

            _dialogues.Clear();
            _finished = false; _loadSeen = false; _loadSeconds = -1f; _loadKey = "<unset>";

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { _dialogues.Add(e.Text); e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(_ => _finished = true));
            _subs.Add(EventBus.Subscribe<ShowLoadingCommand>(e => { _loadSeen = true; _loadSeconds = e.Seconds; _loadKey = e.Key; e.Handle?.Complete(); }));

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
        public IEnumerator LoadingScene_NoArg_Defaults_To_2s_And_Continues()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,LoadingScene,await\n" +
                ",Text,,로딩후,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "loading"));
            yield return WaitUntilDone(player);

            Assert.IsTrue(_loadSeen, "LoadingScene → ShowLoadingCommand 발행");
            Assert.AreEqual(2.0f, _loadSeconds, 1e-4f, "displayTime 생략 시 동결 2.0s");
            Assert.IsNull(_loadKey, "키 생략 시 Key=null(전체 무작위)");
            CollectionAssert.AreEqual(new[] { "로딩후" }, _dialogues, "로딩 완료(await) 후 다음 대사 진행");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator LoadingScene_KeyOnly_Parses_Key_With_Default_Time()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,LoadingScene:Roa,await\n" + // 시간 생략 + 캐릭터 키만
                ",Text,,로딩후,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "loading-key"));
            yield return WaitUntilDone(player);

            Assert.IsTrue(_loadSeen);
            Assert.AreEqual(2.0f, _loadSeconds, 1e-4f, "키만 줘도 시간은 동결 기본 2.0s");
            Assert.AreEqual("Roa", _loadKey, "비숫자 토큰 → 스플래시 키");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator LoadingScene_Time_And_Key_Both_Parsed()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,Loading:0.05:DoHeewon,await\n" + // 별칭 + 시간 + 키
                ",Text,,로딩후,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "loading-time-key"));
            yield return WaitUntilDone(player);

            Assert.IsTrue(_loadSeen);
            Assert.AreEqual(0.05f, _loadSeconds, 1e-4f, "명시 displayTime");
            Assert.AreEqual("DoHeewon", _loadKey, "시간+키 동시 파싱");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator Loading_Alias_With_Explicit_Time()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,Loading:0.05,await\n" + // 별칭 Loading + 명시 시간
                ",Text,,로딩후,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "loading-alias"));
            yield return WaitUntilDone(player);

            Assert.IsTrue(_loadSeen, "Loading 별칭도 인식");
            Assert.AreEqual(0.05f, _loadSeconds, 1e-4f, "명시 displayTime");
            Assert.IsNull(_loadKey, "시간만 줄 땐 Key=null");
            Assert.IsTrue(_finished);
        }
    }
}
