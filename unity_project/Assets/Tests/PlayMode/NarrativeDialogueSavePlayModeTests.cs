using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Core;              // GameStateSO
using LoveAlgo.Events;            // SetDialogueVisibleCommand, SaveRequestedEvent 등
using LoveAlgo.Story.StoryEngine; // NarrativeController
using LoveAlgo.Game;             // PhaseController (격리 청소)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// DialogueShow/Hide + Flow Save 검증: <see cref="NarrativeController"/>가 <c>FX,,DialogueShow/Hide</c>를
    /// <see cref="SetDialogueVisibleCommand"/>(true/false)로, <c>Flow,,Save</c>를 <see cref="SaveRequestedEvent"/>
    /// (자동저장 슬롯 0, reason="story-save")로 발행하는지. 뷰 없이 명령 구독·캡처해 검증한다.
    /// </summary>
    public class NarrativeDialogueSavePlayModeTests
    {
        GameStateSO _gs;
        GameObject _playerGo;
        readonly List<IDisposable> _subs = new();

        readonly List<string> _dialogues = new();
        readonly List<bool> _visible = new();
        bool _finished;
        bool _saveSeen; int _saveSlot = int.MinValue; string _saveReason;

        NarrativeController SetUp()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(pc.gameObject);

            _dialogues.Clear(); _visible.Clear();
            _finished = false; _saveSeen = false; _saveSlot = int.MinValue; _saveReason = null;

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { _dialogues.Add(e.Text); e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(_ => _finished = true));
            _subs.Add(EventBus.Subscribe<SetDialogueVisibleCommand>(e => _visible.Add(e.Visible)));
            _subs.Add(EventBus.Subscribe<SaveRequestedEvent>(e => { _saveSeen = true; _saveSlot = e.Slot; _saveReason = e.Reason; }));

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
        public IEnumerator DialogueHide_Then_Show_Toggles_Visibility()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",FX,,DialogueHide,await\n" +
                ",FX,,DialogueShow,await\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "dlg"));
            yield return WaitUntilDone(player);

            CollectionAssert.AreEqual(new[] { false, true }, _visible, "DialogueHide→false, DialogueShow→true");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator Flow_Save_Publishes_SaveRequest_To_Autosave()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Text,,체크포인트,click\n" +
                ",Flow,,Save,>\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "save"));
            yield return WaitUntilDone(player);

            Assert.IsTrue(_saveSeen, "Flow Save → SaveRequestedEvent 발행");
            Assert.AreEqual(0, _saveSlot, "자동저장 슬롯(0)");
            Assert.AreEqual("story-save", _saveReason);
            CollectionAssert.AreEqual(new[] { "체크포인트" }, _dialogues, "Save는 흐름을 끊지 않음");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator Flow_Value_Schedule_Is_NoOp_And_Continues()
        {
            // Value:Schedule = 풀게임 스케줄 지점 마커. 프롤로그는 선형이라 의도적 no-op(감독 결정) — 흐름 무차단.
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Text,,낮끝,click\n" +
                ",Flow,,Value:Schedule,>\n" +
                ",Text,,밤,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "value"));
            yield return WaitUntilDone(player);

            CollectionAssert.AreEqual(new[] { "낮끝", "밤" }, _dialogues, "Value:Schedule은 no-op — 앞/뒤 대사 그대로 진행");
            Assert.IsFalse(_saveSeen, "Value는 세이브 등 부수효과 없음");
            Assert.IsTrue(_finished);
        }
    }
}
