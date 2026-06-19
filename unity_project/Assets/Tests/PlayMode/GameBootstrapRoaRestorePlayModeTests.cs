using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Events;
using LoveAlgo.Game;

namespace LoveAlgo.Tests.PlayMode
{
    public class GameBootstrapRoaRestorePlayModeTests
    {
        GameObject _go;
        GameStateSO _gs;
        readonly List<IDisposable> _subs = new();
        readonly List<string> _order = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            _order.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        [UnityTest]
        public IEnumerator Restore_Publishes_Device_Before_RoaEnter()
        {
            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();
            _gs.Data.storyScriptId = "prologue";
            _gs.Data.storyLineIndex = 0;
            _gs.Data.storyRoaDevice = "모바일";
            _gs.Data.storyChars.Add(new GameStateData.StoryCharRecord { slot = (int)CharSlot.C, id = "roa", emote = "41" });

            // 비활성으로 생성 — GameBootstrap.Start()/Boot()이 실행되면 NewGame→ResetRuntime이
            // 위에서 세팅한 복원 데이터(storyRoaDevice/storyChars)를 지워버린다. 본 테스트는 Start 없이
            // TryResumeStory()를 직접 호출해 복원 스냅샷 발행 순서만 검증한다.
            _go = new GameObject("GameBootstrap");
            _go.SetActive(false);
            var b = _go.AddComponent<GameBootstrap>();
            b.State = _gs;
            b.PrologueCsv = ""; // 복원 후 프롤로그 재생 분기를 무력화(스냅샷 발행만 검증)

            _subs.Add(EventBus.Subscribe<SetRoaDeviceCommand>(e => _order.Add("device:" + e.Device)));
            _subs.Add(EventBus.Subscribe<ShowCharacterCommand>(e => _order.Add("char:" + e.Character)));
            yield return null;

            b.TryResumeStory();
            yield return null;

            int di = _order.IndexOf("device:Mobile");
            int ci = _order.IndexOf("char:roa");
            Assert.GreaterOrEqual(di, 0, "디바이스 명령이 발행되어야 함");
            Assert.GreaterOrEqual(ci, 0, "로아 Enter가 발행되어야 함");
            Assert.Less(di, ci, "디바이스가 로아 Enter보다 먼저여야 함");
        }
    }
}
