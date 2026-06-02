using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // PlayBgmCommand, BgmChangedEvent
using LoveAlgo.Audio;  // AudioManager

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// Audio 슬라이스1 PlayMode 검증: AudioManager의 OnEnable 구독 경로 —
    /// 실제 런타임에서 PlayBgmCommand 발행 → 재생 + BgmChangedEvent 통지.
    /// </summary>
    public class AudioManagerPlayModeTests
    {
        [UnityTest]
        public IEnumerator OnEnable_Subscribes_So_PlayBgmCommand_Plays_And_Notifies()
        {
            var go = new GameObject("AudioManager_PlayTest");
            var am = go.AddComponent<AudioManager>(); // OnEnable → 구독 + AudioSource 생성
            am.ClipLoader = (cat, name) => AudioClip.Create("t", 64, 1, 44100, false);

            bool fired = false; string evName = null;
            var sub = EventBus.Subscribe<BgmChangedEvent>(e => { fired = true; evName = e.Name; });
            try
            {
                yield return null; // 라이프사이클 활성

                EventBus.Publish(new PlayBgmCommand("title_theme", 0f));

                Assert.IsTrue(fired, "OnEnable 구독으로 PlayBgmCommand→BgmChanged 발행");
                Assert.AreEqual("title_theme", evName);
                Assert.AreEqual("title_theme", am.CurrentBgm);
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
            }
        }
    }
}
