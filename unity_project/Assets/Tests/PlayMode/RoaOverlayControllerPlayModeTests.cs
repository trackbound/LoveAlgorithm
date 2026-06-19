using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;
using LoveAlgo.Events;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    public class RoaOverlayControllerPlayModeTests
    {
        GameObject _go;
        RoaOverlaySO _cfg;
        readonly List<IDisposable> _subs = new();
        readonly List<ShowStageLayerCommand> _cmds = new();

        RoaOverlayController Make()
        {
            _cfg = ScriptableObject.CreateInstance<RoaOverlaySO>();
            _cfg.Configure("roa", new[] { "41" }, new[] { "51" }, RoaDevice.Pc);
            _go = new GameObject("RoaOverlayController");
            var c = _go.AddComponent<RoaOverlayController>();
            c.Config = _cfg;
            c.FadeSeconds = 0f;
            _subs.Add(EventBus.Subscribe<ShowStageLayerCommand>(e =>
            {
                if (e.Kind == StageLayerKind.Overlay) _cmds.Add(e);
                e.Handle?.Complete();
            }));
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            _cmds.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_cfg != null) UnityEngine.Object.DestroyImmediate(_cfg);
        }

        static ShowCharacterCommand Char(CharAction a, string id, string emote) =>
            new ShowCharacterCommand(CharSlot.C, a, id, emote, 0f, new CompletionHandle());

        [UnityTest]
        public IEnumerator Enter_Shows_Overlay_DeviceCategory()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "00"));
            yield return null;
            Assert.AreEqual(1, _cmds.Count);
            Assert.IsFalse(_cmds[0].IsClose);
            Assert.AreEqual("pc_기본", _cmds[0].Name);
        }

        [UnityTest]
        public IEnumerator Emote_Positive_Switches_Overlay()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "00"));
            EventBus.Publish(Char(CharAction.Emote, "roa", "41"));
            yield return null;
            Assert.AreEqual("pc_긍정", _cmds[_cmds.Count - 1].Name);
        }

        [UnityTest]
        public IEnumerator InlineEmote_Switches_Overlay()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "00"));
            EventBus.Publish(new ShowSpeakerEmoteCommand("roa", "51"));
            yield return null;
            Assert.AreEqual("pc_부정", _cmds[_cmds.Count - 1].Name);
        }

        [UnityTest]
        public IEnumerator Device_Switch_Keeps_Category()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "41")); // 긍정
            EventBus.Publish(new SetRoaDeviceCommand(RoaDevice.Mobile));
            yield return null;
            Assert.AreEqual("모바일_긍정", _cmds[_cmds.Count - 1].Name);
        }

        [UnityTest]
        public IEnumerator Exit_Closes_Overlay()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "00"));
            EventBus.Publish(Char(CharAction.Exit, "roa", ""));
            yield return null;
            Assert.IsTrue(_cmds[_cmds.Count - 1].IsClose);
        }

        [UnityTest]
        public IEnumerator NonRoa_Char_Ignored()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "c01", "00"));
            yield return null;
            Assert.AreEqual(0, _cmds.Count);
        }
    }
}
