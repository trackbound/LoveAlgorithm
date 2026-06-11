using LoveAlgo;       // GameConstants
using LoveAlgo.Core;
using UnityEngine;

namespace LoveAlgo.Settings
{
    /// <summary>
    /// 설정 영속(PlayerPrefs — 앱 설정, 세이브게임 JsonSaveStore와 분리). 기본값=<see cref="GameConstants"/>.
    /// 순수 I/O — SettingsController가 부팅 로드/변경 저장에 사용. 테스트는 Clear로 키 청소.
    /// </summary>
    public static class SettingsStore
    {
        const string P = "lovealgo.settings.";
        const string K_Bgm = P + "bgm", K_Sfx = P + "sfx", K_Text = P + "text",
                     K_Auto = P + "auto", K_Res = P + "res", K_Fs = P + "fullscreen";

        public static void Load(SettingsSO s)
        {
            if (s == null) return;
            s.BgmVolume = PlayerPrefs.GetFloat(K_Bgm, GameConstants.DefaultBGMVolume);
            s.SfxVolume = PlayerPrefs.GetFloat(K_Sfx, GameConstants.DefaultSFXVolume);
            s.TextSpeed = PlayerPrefs.GetFloat(K_Text, GameConstants.DefaultTextSpeed);
            s.AutoSpeed = PlayerPrefs.GetFloat(K_Auto, GameConstants.DefaultAutoSpeed);
            s.ResolutionIndex = PlayerPrefs.GetInt(K_Res, GameConstants.DefaultResolutionIndex);
            s.Fullscreen = PlayerPrefs.GetInt(K_Fs, 1) == 1;
        }

        public static void Save(SettingsSO s)
        {
            if (s == null) return;
            PlayerPrefs.SetFloat(K_Bgm, s.BgmVolume);
            PlayerPrefs.SetFloat(K_Sfx, s.SfxVolume);
            PlayerPrefs.SetFloat(K_Text, s.TextSpeed);
            PlayerPrefs.SetFloat(K_Auto, s.AutoSpeed);
            PlayerPrefs.SetInt(K_Res, s.ResolutionIndex);
            PlayerPrefs.SetInt(K_Fs, s.Fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>SettingsSO를 기본값(GameConstants)으로 되돌린다(저장은 호출 측).</summary>
        public static void ResetToDefaults(SettingsSO s)
        {
            if (s == null) return;
            s.BgmVolume = GameConstants.DefaultBGMVolume;
            s.SfxVolume = GameConstants.DefaultSFXVolume;
            s.TextSpeed = GameConstants.DefaultTextSpeed;
            s.AutoSpeed = GameConstants.DefaultAutoSpeed;
            s.ResolutionIndex = GameConstants.DefaultResolutionIndex;
            s.Fullscreen = true;
        }

        /// <summary>설정 키 전체 삭제(테스트 청소/공장 초기화).</summary>
        public static void Clear()
        {
            foreach (var k in new[] { K_Bgm, K_Sfx, K_Text, K_Auto, K_Res, K_Fs })
                PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }
    }
}
