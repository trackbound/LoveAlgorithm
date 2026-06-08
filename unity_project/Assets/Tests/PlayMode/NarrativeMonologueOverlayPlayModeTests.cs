using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Core;              // GameStateSO
using LoveAlgo.Events;            // SetMonologueOverlayCommand, ShowDialogueCommand 등
using LoveAlgo.Story.StoryEngine; // NarrativeController
using LoveAlgo.Game;             // PhaseController (격리 청소)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 독백 오버레이 토글 검증: <see cref="NarrativeController"/>가 Text 라인의 화자 유무에 따라
    /// <see cref="SetMonologueOverlayCommand"/>(Active)를 발행하는지. 화자 빈 칸=독백(true),
    /// 화자명/<c>{{player}}</c>=대사(false). 뷰 없이 명령을 구독·캡처해 검증한다(요구사항 1:1).
    /// </summary>
    public class NarrativeMonologueOverlayPlayModeTests
    {
        GameStateSO _gs;
        GameObject _playerGo;
        readonly List<IDisposable> _subs = new();

        readonly List<bool> _mono = new();
        bool _finished;

        NarrativeController SetUp()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(pc.gameObject);

            _mono.Clear();
            _finished = false;

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            // 대사 핸들을 즉시 완료해 스크립트가 진행되게 한다(뷰 대체).
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => e.Handle.Complete()));
            _subs.Add(EventBus.Subscribe<SetMonologueOverlayCommand>(e => _mono.Add(e.Active)));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(_ => _finished = true));

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
        public IEnumerator EmptySpeaker_Is_Monologue_NamedAndPlayer_Are_Not()
        {
            // 화자 빈 칸=독백(true), 로아=대사(false), {{player}}=대사(false), 다시 빈 칸=독백(true).
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Text,,혼잣말이다,click\n" +
                ",Text,로아,안녕,click\n" +
                ",Text,{{player}},나도 안녕,click\n" +
                ",Text,,또 혼잣말,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "mono"));
            yield return WaitUntilDone(player);

            CollectionAssert.AreEqual(new[] { true, false, false, true }, _mono,
                "빈 화자=독백(true), 로아/{{player}}=대사(false)");
            Assert.IsTrue(_finished);
        }
    }
}
