using NUnit.Framework;
using UnityEngine;
using LoveAlgo;          // GameConstants
using LoveAlgo.Core;     // SettingsSO
using LoveAlgo.Settings; // SettingsStore

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// SettingsStore(PlayerPrefs 영속) 순수 I/O 테스트. 설정 키만 건드리고 Set/TearDown에서 청소(에디터 prefs 격리).
    /// </summary>
    public class SettingsStoreTests
    {
        [SetUp] public void SetUp() => SettingsStore.Clear();
        [TearDown] public void TearDown() => SettingsStore.Clear();

        static SettingsSO New() => ScriptableObject.CreateInstance<SettingsSO>();

        [Test]
        public void Load_NoPrefs_UsesGameConstantsDefaults()
        {
            var s = New();
            SettingsStore.Load(s);
            Assert.AreEqual(GameConstants.DefaultBGMVolume, s.BgmVolume, 1e-4f);
            Assert.AreEqual(GameConstants.DefaultSFXVolume, s.SfxVolume, 1e-4f);
            Assert.AreEqual(GameConstants.DefaultTextSpeed, s.TextSpeed, 1e-4f);
            Assert.AreEqual(GameConstants.DefaultAutoSpeed, s.AutoSpeed, 1e-4f);
            Assert.AreEqual(GameConstants.DefaultResolutionIndex, s.ResolutionIndex);
            Assert.IsTrue(s.Fullscreen);
            Object.DestroyImmediate(s);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            var a = New();
            a.BgmVolume = 0.3f; a.SfxVolume = 0.6f; a.TextSpeed = 0.9f; a.AutoSpeed = 0.1f;
            a.ResolutionIndex = 2; a.Fullscreen = false;
            SettingsStore.Save(a);

            var b = New();
            SettingsStore.Load(b);
            Assert.AreEqual(0.3f, b.BgmVolume, 1e-4f);
            Assert.AreEqual(0.6f, b.SfxVolume, 1e-4f);
            Assert.AreEqual(0.9f, b.TextSpeed, 1e-4f);
            Assert.AreEqual(0.1f, b.AutoSpeed, 1e-4f);
            Assert.AreEqual(2, b.ResolutionIndex);
            Assert.IsFalse(b.Fullscreen);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void ResetToDefaults_RestoresGameConstants()
        {
            var s = New();
            s.BgmVolume = 0f; s.ResolutionIndex = 0; s.Fullscreen = false;
            SettingsStore.ResetToDefaults(s);
            Assert.AreEqual(GameConstants.DefaultBGMVolume, s.BgmVolume, 1e-4f);
            Assert.AreEqual(GameConstants.DefaultResolutionIndex, s.ResolutionIndex);
            Assert.IsTrue(s.Fullscreen);
            Object.DestroyImmediate(s);
        }
    }
}
