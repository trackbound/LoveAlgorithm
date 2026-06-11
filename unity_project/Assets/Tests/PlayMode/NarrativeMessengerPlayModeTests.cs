using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Core;              // GameStateSO
using LoveAlgo.Events;            // DeliverMessengerSequenceCommand 등
using LoveAlgo.Story.StoryEngine; // NarrativeController
using LoveAlgo.Game;              // PhaseController (격리 청소)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// Flow <c>Messenger:{id}[:Wait]</c> 검증(NarrativeLoadingPlayModeTests 미러) — 도착 명령 발행,
    /// Wait면 읽힘 핸들까지 대기 후 진행, 별칭 Message 인식. 메신저 컨트롤러 없이 명령 구독·캡처로
    /// 결정적 검증(핸들 즉시 완료).
    /// </summary>
    public class NarrativeMessengerPlayModeTests
    {
        GameStateSO _gs;
        GameObject _playerGo;
        readonly List<IDisposable> _subs = new();

        readonly List<string> _dialogues = new();
        readonly List<DeliverMessengerSequenceCommand> _delivers = new();
        bool _finished;

        NarrativeController SetUp(bool completeOnDeliver)
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>())
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>())
                UnityEngine.Object.DestroyImmediate(pc.gameObject);

            _dialogues.Clear(); _delivers.Clear(); _finished = false;

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { _dialogues.Add(e.Text); e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(_ => _finished = true));
            _subs.Add(EventBus.Subscribe<DeliverMessengerSequenceCommand>(e =>
            {
                _delivers.Add(e);
                if (completeOnDeliver) e.OnRead?.Complete(); // 유저가 즉시 읽은 셈 — 결정적 진행
            }));

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
        public IEnumerator Messenger_NoWait_Delivers_And_Continues_Immediately()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,Messenger:Seq1,>\n" +
                ",Text,,도착후,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp(completeOnDeliver: false); // 아무도 안 읽어도
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "messenger-nowait"));
            yield return WaitUntilDone(player);

            Assert.AreEqual(1, _delivers.Count, "도착 명령 발행");
            Assert.AreEqual("Seq1", _delivers[0].SequenceId);
            Assert.IsNull(_delivers[0].OnRead, "Wait 없음 = 대기 핸들 없음");
            CollectionAssert.AreEqual(new[] { "도착후" }, _dialogues, "안 읽어도 스토리는 즉시 계속");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator Messenger_Wait_Blocks_Until_Read_Handle()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,Messenger:Seq1:Wait,await\n" +
                ",Text,,읽은후,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp(completeOnDeliver: true); // 구독자가 즉시 읽음 처리
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "messenger-wait"));
            yield return WaitUntilDone(player);

            Assert.AreEqual(1, _delivers.Count);
            Assert.IsNotNull(_delivers[0].OnRead, "Wait = 읽힘 대기 핸들 동승");
            CollectionAssert.AreEqual(new[] { "읽은후" }, _dialogues, "읽힘(핸들 완료) 후 진행");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator Message_Alias_Recognized()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,Message:Seq1,>\n" +
                ",Flow,,End,>\n";

            var player = SetUp(completeOnDeliver: false);
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "messenger-alias"));
            yield return WaitUntilDone(player);

            Assert.AreEqual(1, _delivers.Count, "구 어휘 Message도 도착 명령으로 인식");
            Assert.IsTrue(_finished);
        }
    }
}
