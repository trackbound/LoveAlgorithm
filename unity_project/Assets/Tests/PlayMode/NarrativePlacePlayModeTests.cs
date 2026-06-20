using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Core;              // GameStateSO
using LoveAlgo.Events;            // ShowPlaceCommand 등
using LoveAlgo.Story.StoryEngine; // NarrativeController
using LoveAlgo.Game;             // PhaseController (격리 청소)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 위치 배너 검증: <see cref="NarrativeController"/>가 <c>Place</c> 라인("제목 | 장소")을 받아
    /// <see cref="ShowPlaceCommand"/>를 발행하는지(제목/장소 분리, 미바인딩 시 동결 폴백 지속 0.45/2.0/0.35),
    /// 비블로킹으로 다음 대사가 진행되는지. 뷰 없이 명령을 구독·캡처(핸들 즉시 완료)해 검증한다.
    /// </summary>
    public class NarrativePlacePlayModeTests
    {
        GameStateSO _gs;
        GameObject _playerGo;
        readonly List<IDisposable> _subs = new();

        readonly List<string> _dialogues = new();
        bool _finished;
        bool _placeSeen;
        string _placeTitle, _placePlace;
        float _enter, _hold, _exit;

        NarrativeController SetUp()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>())
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>())
                UnityEngine.Object.DestroyImmediate(pc.gameObject);

            _dialogues.Clear();
            _finished = false; _placeSeen = false; _placeTitle = null; _placePlace = null;

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { _dialogues.Add(e.Text); e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(_ => _finished = true));
            _subs.Add(EventBus.Subscribe<ShowPlaceCommand>(e =>
            {
                _placeSeen = true; _placeTitle = e.Title; _placePlace = e.Place;
                _enter = e.EnterDuration; _hold = e.HoldDuration; _exit = e.ExitDuration;
                e.Handle?.Complete();
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
        public IEnumerator Place_Emits_Banner_With_Title_And_Place_And_Continues()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Place,,[새 학기 첫날] | 침대 위,>\n" +
                ",Text,로아,...나.,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "place"));
            yield return WaitUntilDone(player);

            Assert.IsTrue(_placeSeen, "Place → ShowPlaceCommand 발행");
            Assert.AreEqual("[새 학기 첫날]", _placeTitle);
            Assert.AreEqual("침대 위", _placePlace);
            Assert.AreEqual(0.45f, _enter, 1e-4f, "동결 폴백 등장");
            Assert.AreEqual(2.0f, _hold, 1e-4f, "동결 폴백 유지");
            Assert.AreEqual(0.35f, _exit, 1e-4f, "동결 폴백 퇴장");
            CollectionAssert.AreEqual(new[] { "...나." }, _dialogues, "배너는 비블로킹 — 다음 대사 진행");
            Assert.IsTrue(_finished);
        }
    }
}
