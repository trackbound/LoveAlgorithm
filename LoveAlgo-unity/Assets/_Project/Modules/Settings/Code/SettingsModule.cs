using System.Collections.Generic;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Modules.Audio;
using LoveAlgo.Narrative;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Settings
{
    /// <summary>
    /// 사용자 설정 모듈 진입점.
    /// PlayerPrefs를 흡수하고 IAudio/INarrative로 적용.
    /// 씬 하이어라키: _Modules/SettingsModule
    /// </summary>
    [DefaultExecutionOrder(-450)] // AudioModule(-500)/NarrativeModule(-500)보다 뒤
    public class SettingsModule : MonoBehaviour, ISettings
    {
        static class Keys
        {
            public const string Fullscreen = "Fullscreen";
            public const string ResolutionIndex = "ResolutionIndex";
            public const string MasterVolume = "MasterVolume";
            public const string BGMVolume = "BGMVolume";
            public const string SFXVolume = "SFXVolume";
            public const string TextSpeed = "TextSpeed";
            public const string AutoSpeed = "AutoSpeed";
            public static string Voice(string character) => $"Voice_{character}";
        }

        // 메모리 상태
        float masterVolume, bgmVolume, sfxVolume;
        float textSpeed, autoSpeed;
        int resolutionIndex;
        bool isFullscreen;
        readonly Dictionary<string, float> characterVoices = new Dictionary<string, float>();

        // 스냅샷
        struct Snapshot
        {
            public float Master, BGM, SFX, TextSpd, AutoSpd;
            public Dictionary<string, float> Voices;
        }
        Snapshot snapshot;

        // 캐릭터 키 (Voice_* PlayerPrefs 로드 범위)
        static readonly string[] DefaultCharacters = { "Yeun", "Daeun", "Bom", "Heewon", "Roa" };

        [Header("UI Prefab (모듈 응집)")]
        [SerializeField] SettingsPopup settingsPopupPrefab;

        IAudio audioSvc;
        INarrative narrative;
        SettingsPopup popupInstance;

        void Awake()
        {
            Services.Register<ISettings>(this);
            audioSvc = Services.Get<IAudio>();
            narrative = Services.Get<INarrative>();
            if (settingsPopupPrefab != null && PopupManager.Instance != null)
                popupInstance = PopupManager.Instance.Register(settingsPopupPrefab);
            Load();
        }

        void OnDestroy()
        {
            if (Services.TryGet<ISettings>() == (ISettings)this)
                Services.Unregister<ISettings>();
        }

        // ── Volume ────────────────────────────────────────────
        public float MasterVolume
        {
            get => masterVolume;
            set { masterVolume = value; audioSvc?.SetMasterVolume(value); }
        }

        public float BGMVolume
        {
            get => bgmVolume;
            set { bgmVolume = value; audioSvc?.SetBGMVolume(value); }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set { sfxVolume = value; audioSvc?.SetSFXVolume(value); }
        }

        public float GetCharacterVoice(string character)
        {
            if (characterVoices.TryGetValue(character, out var v)) return v;
            return GameConstants.DefaultVoiceVolume;
        }

        public void SetCharacterVoice(string character, float value)
        {
            characterVoices[character] = value;
            audioSvc?.SetCharacterVoiceVolume(character, value);
        }

        // ── Dialogue ──────────────────────────────────────────
        public float TextSpeed
        {
            get => textSpeed;
            set
            {
                textSpeed = value;
                // DialogueUI는 INarrative에 없으므로 직접 호출 — UIManager 경유
                var ui = LoveAlgo.UI.UIManager.Instance;
                if (ui != null && ui.DialogueUI != null) ui.DialogueUI.SetTextSpeed(value);
            }
        }

        public float AutoSpeed
        {
            get => autoSpeed;
            set { autoSpeed = value; narrative?.SetAutoDelay(value); }
        }

        // ── Resolution ────────────────────────────────────────
        public int ResolutionIndex
        {
            get => resolutionIndex;
            set => resolutionIndex = Mathf.Clamp(value, 0, ResolutionCount - 1);
        }

        public bool IsFullscreen
        {
            get => isFullscreen;
            set => isFullscreen = value;
        }

        public int ResolutionCount => GameConstants.Resolutions.Length;

        public (int width, int height) GetResolution(int index)
        {
            if (index < 0 || index >= GameConstants.Resolutions.Length) index = 0;
            var r = GameConstants.Resolutions[index];
            return (r.w, r.h);
        }

        public void ApplyResolution()
        {
            var (w, h) = GetResolution(resolutionIndex);
            Screen.SetResolution(w, h, isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            PlayerPrefs.SetInt(Keys.Fullscreen, isFullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Keys.ResolutionIndex, resolutionIndex);
            PlayerPrefs.Save();
        }

        // ── Snapshot ──────────────────────────────────────────
        public void TakeSnapshot()
        {
            snapshot = new Snapshot
            {
                Master = masterVolume,
                BGM = bgmVolume,
                SFX = sfxVolume,
                TextSpd = textSpeed,
                AutoSpd = autoSpeed,
                Voices = new Dictionary<string, float>(characterVoices)
            };
        }

        public void RevertToSnapshot()
        {
            if (snapshot.Voices == null) return; // 스냅샷 없음
            MasterVolume = snapshot.Master;
            BGMVolume = snapshot.BGM;
            SFXVolume = snapshot.SFX;
            TextSpeed = snapshot.TextSpd;
            AutoSpeed = snapshot.AutoSpd;
            foreach (var kv in snapshot.Voices)
                SetCharacterVoice(kv.Key, kv.Value);
        }

        // ── Save / Load ───────────────────────────────────────
        public void Save()
        {
            PlayerPrefs.SetFloat(Keys.MasterVolume, masterVolume);
            PlayerPrefs.SetFloat(Keys.BGMVolume, bgmVolume);
            PlayerPrefs.SetFloat(Keys.SFXVolume, sfxVolume);
            PlayerPrefs.SetFloat(Keys.TextSpeed, textSpeed);
            PlayerPrefs.SetFloat(Keys.AutoSpeed, autoSpeed);
            foreach (var kv in characterVoices)
                PlayerPrefs.SetFloat(Keys.Voice(kv.Key), kv.Value);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            isFullscreen = PlayerPrefs.GetInt(Keys.Fullscreen, 1) == 1;
            resolutionIndex = PlayerPrefs.GetInt(Keys.ResolutionIndex, GameConstants.DefaultResolutionIndex);

            masterVolume = PlayerPrefs.GetFloat(Keys.MasterVolume, GameConstants.DefaultMasterVolume);
            bgmVolume = PlayerPrefs.GetFloat(Keys.BGMVolume, GameConstants.DefaultBGMVolume);
            sfxVolume = PlayerPrefs.GetFloat(Keys.SFXVolume, GameConstants.DefaultSFXVolume);
            textSpeed = PlayerPrefs.GetFloat(Keys.TextSpeed, GameConstants.DefaultTextSpeed);
            autoSpeed = PlayerPrefs.GetFloat(Keys.AutoSpeed, GameConstants.DefaultAutoSpeed);

            foreach (var c in DefaultCharacters)
                characterVoices[c] = PlayerPrefs.GetFloat(Keys.Voice(c), GameConstants.DefaultVoiceVolume);

            // 메모리 → 런타임 적용
            audioSvc?.SetMasterVolume(masterVolume);
            audioSvc?.SetBGMVolume(bgmVolume);
            audioSvc?.SetSFXVolume(sfxVolume);
            foreach (var kv in characterVoices)
                audioSvc?.SetCharacterVoiceVolume(kv.Key, kv.Value);
            narrative?.SetAutoDelay(autoSpeed);
            // TextSpeed는 DialogueUI 준비 시점 따라 setter 호출이 안전 — 직접 호출
            var ui = LoveAlgo.UI.UIManager.Instance;
            if (ui != null && ui.DialogueUI != null) ui.DialogueUI.SetTextSpeed(textSpeed);
        }

        public void ResetToDefaults()
        {
            MasterVolume = GameConstants.DefaultMasterVolume;
            BGMVolume = GameConstants.DefaultBGMVolume;
            SFXVolume = GameConstants.DefaultSFXVolume;
            TextSpeed = GameConstants.DefaultTextSpeed;
            AutoSpeed = GameConstants.DefaultAutoSpeed;
            foreach (var c in DefaultCharacters)
                SetCharacterVoice(c, GameConstants.DefaultVoiceVolume);
        }

        // ── UI 진입점 ────────────────────────────────
        public void ShowSettingsUI()
        {
            var popup = popupInstance != null ? popupInstance : PopupManager.Instance?.Get<SettingsPopup>();
            popup?.Show();
        }
    }
}
