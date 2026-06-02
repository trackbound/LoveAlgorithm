using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // 오디오 명령/통지
using LoveAlgo.Audio;  // AudioManager

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// Audio 슬라이스1 검증: AudioManager가 명령을 받아 BGM 상태/통지를 올바르게 처리하는지.
    /// 오디오 출력 자체는 EditMode에서 검증 불가하므로, clip 해석을 주입(ClipLoader)해 라우팅·CurrentBgm·
    /// BgmChangedEvent·로더 호출(카테고리/이름)을 결정적으로 확인한다. BGM은 fade=0(즉시) 경로로 테스트
    /// (코루틴 페이드는 PlayMode 소관).
    /// </summary>
    [TestFixture]
    public class AudioManagerTests
    {
        static AudioManager MakeManager(out GameObject go, List<string> loaderCalls)
        {
            go = new GameObject("AudioManager_Test");
            var am = go.AddComponent<AudioManager>();
            am.ClipLoader = (cat, name) =>
            {
                loaderCalls.Add($"{cat}:{name}");
                return AudioClip.Create("t", 64, 1, 44100, false);
            };
            return am;
        }

        [Test]
        public void PlayBgm_SetsCurrent_And_Publishes_BgmChanged()
        {
            var calls = new List<string>();
            GameObject go = null;
            bool fired = false; string evName = "x";
            var sub = EventBus.Subscribe<BgmChangedEvent>(e => { fired = true; evName = e.Name; });
            try
            {
                var am = MakeManager(out go, calls);
                am.PlayBgm("white_noise", 0f);

                Assert.AreEqual("white_noise", am.CurrentBgm);
                Assert.IsTrue(fired);
                Assert.AreEqual("white_noise", evName);
                CollectionAssert.Contains(calls, "BGM:white_noise");
            }
            finally { sub.Dispose(); if (go != null) Object.DestroyImmediate(go); }
        }

        [Test]
        public void PlayBgm_Same_Twice_Ignored()
        {
            var calls = new List<string>();
            GameObject go = null;
            int fireCount = 0;
            var sub = EventBus.Subscribe<BgmChangedEvent>(e => fireCount++);
            try
            {
                var am = MakeManager(out go, calls);
                am.PlayBgm("song", 0f);
                am.PlayBgm("song", 0f); // 동일 BGM 재요청 → 무시

                Assert.AreEqual(1, fireCount, "같은 BGM 재요청은 통지 1회만");
            }
            finally { sub.Dispose(); if (go != null) Object.DestroyImmediate(go); }
        }

        [Test]
        public void StopBgm_Clears_Current_And_Publishes_Null()
        {
            var calls = new List<string>();
            GameObject go = null;
            string evName = "x"; int fireCount = 0;
            var sub = EventBus.Subscribe<BgmChangedEvent>(e => { fireCount++; evName = e.Name; });
            try
            {
                var am = MakeManager(out go, calls);
                am.PlayBgm("song", 0f);
                am.StopBgm(0f);

                Assert.IsNull(am.CurrentBgm);
                Assert.IsNull(evName, "정지 통지는 Name=null");
                Assert.AreEqual(2, fireCount);
            }
            finally { sub.Dispose(); if (go != null) Object.DestroyImmediate(go); }
        }

        [Test]
        public void MissingClip_NoChange_NoEvent()
        {
            GameObject go = null;
            bool fired = false;
            var sub = EventBus.Subscribe<BgmChangedEvent>(e => fired = true);
            try
            {
                go = new GameObject("AudioManager_Missing");
                var am = go.AddComponent<AudioManager>();
                am.ClipLoader = (cat, name) => null; // 클립 없음

                am.PlayBgm("nope", 0f);

                Assert.IsNull(am.CurrentBgm, "클립 없으면 CurrentBgm 불변");
                Assert.IsFalse(fired, "클립 없으면 통지 없음");
            }
            finally { sub.Dispose(); if (go != null) Object.DestroyImmediate(go); }
        }

        [Test]
        public void Sfx_And_Voice_Invoke_Loader_With_Category()
        {
            var calls = new List<string>();
            GameObject go = null;
            try
            {
                var am = MakeManager(out go, calls);
                am.PlaySfx("001_Pop");
                am.PlayVoice("roa_001");

                CollectionAssert.Contains(calls, "SFX:001_Pop");
                CollectionAssert.Contains(calls, "Voice:roa_001");
            }
            finally { if (go != null) Object.DestroyImmediate(go); }
        }
    }
}
