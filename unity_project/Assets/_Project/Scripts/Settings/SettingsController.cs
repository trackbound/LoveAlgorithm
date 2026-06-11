using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // SettingsSO
using LoveAlgo;        // GameConstants
using LoveAlgo.Events; // 설정 커맨드
using UnityEngine;

namespace LoveAlgo.Settings
{
    /// <summary>
    /// 설정 적용·영속의 단일 어댑터(*Controller, ADR-007). 부팅 시 PlayerPrefs→SettingsSO 로드 후 영속값을
    /// 커맨드로 재발행해 도메인 오너(AudioManager 볼륨·DialogueView 속도)가 채택 + 화면 적용. 이후 Set*/
    /// ApplyDisplay/Reset 커맨드를 구독해 SettingsSO 갱신 + 저장(해상도/전체화면은 Screen에 적용).
    /// 씬 하이어라키: _Bootstrap (앱 시작 시 1회 로드). SettingsSO는 인스펙터 주입(미바인딩 시 Shared).
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [SerializeField] SettingsSO settings;

        readonly List<IDisposable> _subs = new();
        bool _suppressSave;

        SettingsSO S => settings != null ? settings : (settings = SettingsSO.Shared);

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<SetVolumeCommand>(OnSetVolume));
            _subs.Add(EventBus.Subscribe<SetTextSpeedCommand>(e => { if (S != null) { S.TextSpeed = e.Value01; SaveIfNotSuppressed(); } }));
            _subs.Add(EventBus.Subscribe<SetAutoSpeedCommand>(e => { if (S != null) { S.AutoSpeed = e.Value01; SaveIfNotSuppressed(); } }));
            _subs.Add(EventBus.Subscribe<ApplyDisplayCommand>(OnApplyDisplay));
            _subs.Add(EventBus.Subscribe<ResetSettingsCommand>(_ => OnReset()));

            if (S == null) { Debug.LogWarning("[SettingsController] SettingsSO 미바인딩(Shared 부재) — 설정 비활성."); return; }
            SettingsStore.Load(S);
            RepublishAndApply(); // 부팅: 오너들이 영속값 채택 + 화면 적용
        }

        void OnDisable()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
        }

        void OnSetVolume(SetVolumeCommand e)
        {
            if (S == null) return;
            if (e.Channel == AudioChannel.Bgm) S.BgmVolume = e.Value;
            else if (e.Channel == AudioChannel.Sfx) S.SfxVolume = e.Value;
            SaveIfNotSuppressed();
        }

        void OnApplyDisplay(ApplyDisplayCommand e)
        {
            if (S == null) return;
            S.ResolutionIndex = Mathf.Clamp(e.ResolutionIndex, 0, GameConstants.Resolutions.Length - 1);
            S.Fullscreen = e.Fullscreen;
            ApplyDisplay();
            SaveIfNotSuppressed();
        }

        void OnReset()
        {
            if (S == null) return;
            SettingsStore.ResetToDefaults(S);
            SettingsStore.Save(S);
            RepublishAndApply();
        }

        void ApplyDisplay()
        {
            int idx = Mathf.Clamp(S.ResolutionIndex, 0, GameConstants.Resolutions.Length - 1);
            var (w, h) = GameConstants.Resolutions[idx];
            Screen.SetResolution(w, h, S.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        // 부팅/리셋: 영속값을 커맨드로 흘려 오너(Audio/Dialogue)가 채택 + 화면 적용. 저장은 억제(이미 저장본).
        void RepublishAndApply()
        {
            _suppressSave = true;
            EventBus.Publish(new SetVolumeCommand(AudioChannel.Bgm, S.BgmVolume));
            EventBus.Publish(new SetVolumeCommand(AudioChannel.Sfx, S.SfxVolume));
            EventBus.Publish(new SetTextSpeedCommand(S.TextSpeed));
            EventBus.Publish(new SetAutoSpeedCommand(S.AutoSpeed));
            _suppressSave = false;
            ApplyDisplay();
        }

        void SaveIfNotSuppressed() { if (!_suppressSave) SettingsStore.Save(S); }
    }
}
