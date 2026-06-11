using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Core;     // SettingsSO
using LoveAlgo.Events;   // 설정 커맨드
using LoveAlgo.Settings; // SettingsController, SettingsStore

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// SettingsController 런타임: 설정 커맨드 → SettingsSO 갱신 + PlayerPrefs 영속. 부팅 로드/재발행은 OnEnable.
    /// (Screen.SetResolution은 에디터 PlayMode에서 무해 — 검증 대상은 볼륨/영속.)
    /// </summary>
    public class SettingsControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator Commands_UpdateSettings_AndPersist()
        {
            SettingsStore.Clear();
            var settings = ScriptableObject.CreateInstance<SettingsSO>();
            var go = new GameObject("SettingsController");
            go.SetActive(false);
            var ctl = go.AddComponent<SettingsController>();
            typeof(SettingsController)
                .GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(ctl, settings); // private 주입(테스트)
            go.SetActive(true); // OnEnable: Load + republish
            yield return null;

            try
            {
                EventBus.Publish(new SetVolumeCommand(AudioChannel.Bgm, 0.42f));
                EventBus.Publish(new SetTextSpeedCommand(0.9f));
                Assert.AreEqual(0.42f, settings.BgmVolume, 1e-4f, "Bgm 볼륨 갱신");
                Assert.AreEqual(0.9f, settings.TextSpeed, 1e-4f, "대사 속도 갱신");

                // 영속: 새 SO에 Load → 동일
                var reloaded = ScriptableObject.CreateInstance<SettingsSO>();
                SettingsStore.Load(reloaded);
                Assert.AreEqual(0.42f, reloaded.BgmVolume, 1e-4f, "Bgm 볼륨 영속");
                Assert.AreEqual(0.9f, reloaded.TextSpeed, 1e-4f, "대사 속도 영속");
                Object.DestroyImmediate(reloaded);

                // 리셋 → 기본값
                EventBus.Publish(new ResetSettingsCommand());
                Assert.AreEqual(LoveAlgo.GameConstants.DefaultBGMVolume, settings.BgmVolume, 1e-4f, "리셋=기본값");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(settings);
                SettingsStore.Clear();
            }
        }
    }
}
