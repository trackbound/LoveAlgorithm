using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Events;
using LoveAlgo.Story.StoryEngine;
using LoveAlgo.Story.StoryEngine.Flow;

namespace LoveAlgo.Tests.PlayMode
{
    public class NarrativeRoaDevicePlayModeTests
    {
        GameStateSO _gs;
        GameObject _playerGo, _routerGo;
        readonly List<IDisposable> _subs = new();

        NarrativeController Make()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>())
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var r in UnityEngine.Object.FindObjectsByType<FlowCommandController>())
                UnityEngine.Object.DestroyImmediate(r.gameObject);

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();
            _routerGo = new GameObject("Router");
            _routerGo.AddComponent<FlowCommandController>().State = _gs;
            _playerGo = new GameObject("Player");
            var pc = _playerGo.AddComponent<NarrativeController>();
            pc.State = _gs;
            return pc;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_playerGo != null) UnityEngine.Object.DestroyImmediate(_playerGo);
            if (_routerGo != null) UnityEngine.Object.DestroyImmediate(_routerGo);
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        static IEnumerator WaitDone(NarrativeController p)
        {
            int g = 0;
            while (p.IsRunning && g++ < 600) yield return null;
        }

        [UnityTest]
        public IEnumerator RoaDevice_Line_Publishes_And_Records()
        {
            var p = Make();
            RoaDevice? got = null;
            // 미러는 엔진이 라인 처리 시점에 기록한다 — 정상 종료의 ClearStoryPosition이 비우기 전에 스냅샷한다.
            string mirrorAtPublish = null;
            _subs.Add(EventBus.Subscribe<SetRoaDeviceCommand>(e => { got = e.Device; mirrorAtPublish = _gs.Data.storyRoaDevice; }));
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => e.Handle.Complete()));
            yield return null;

            string csv = "LineID,Type,Speaker,Value,Next\n,RoaDevice,,모바일,>\n,Text,,끝,click\n";
            EventBus.Publish(new PlayScriptCommand(csv, "t"));
            yield return WaitDone(p);

            Assert.AreEqual(RoaDevice.Mobile, got);
            Assert.AreEqual("모바일", mirrorAtPublish);
        }

        [UnityTest]
        public IEnumerator Enter_DeviceToken_Publishes_Before_Char()
        {
            var p = Make();
            var order = new List<string>();
            // 미러는 디바이스 명령 발행 시점에 기록 — 정상 종료의 ClearStoryPosition 전에 스냅샷한다.
            string mirrorAtPublish = null;
            _subs.Add(EventBus.Subscribe<SetRoaDeviceCommand>(e => { order.Add("device:" + e.Device); mirrorAtPublish = _gs.Data.storyRoaDevice; }));
            _subs.Add(EventBus.Subscribe<ShowCharacterCommand>(e => { order.Add("char:" + e.Action); e.Handle?.Complete(); }));
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => e.Handle.Complete()));
            yield return null;

            string csv = "LineID,Type,Speaker,Value,Next\n,Char,,Enter:roa:00:pc,await\n,Text,,끝,click\n";
            EventBus.Publish(new PlayScriptCommand(csv, "t"));
            yield return WaitDone(p);

            Assert.AreEqual("device:Pc", order[0]);
            Assert.AreEqual("char:Enter", order[1]);
            Assert.AreEqual("pc", mirrorAtPublish);
        }
    }
}
