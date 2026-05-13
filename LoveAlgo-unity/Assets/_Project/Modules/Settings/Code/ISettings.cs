namespace LoveAlgo.Settings
{
    /// <summary>
    /// 사용자 설정 모듈 외부 계약.
    /// 볼륨·해상도·대사 속도 등 PlayerPrefs 보관 설정의 단일 접근점.
    /// 구현: <see cref="SettingsModule"/>.
    /// </summary>
    public interface ISettings
    {
        // ── Volume (0~1, set 시 즉시 IAudio 반영) ─────────────
        float MasterVolume { get; set; }
        float BGMVolume { get; set; }
        float SFXVolume { get; set; }
        float GetCharacterVoice(string character);
        void SetCharacterVoice(string character, float value);

        // ── Dialogue 속도 ─────────────────────────────────────
        float TextSpeed { get; set; }   // 0~1 normalized
        float AutoSpeed { get; set; }   // 0~1 normalized

        // ── Resolution ────────────────────────────────────────
        int ResolutionIndex { get; set; }
        bool IsFullscreen { get; set; }
        int ResolutionCount { get; }
        (int width, int height) GetResolution(int index);

        /// <summary>현재 ResolutionIndex/IsFullscreen 기준으로 Screen.SetResolution 호출 + Prefs 저장.</summary>
        void ApplyResolution();

        // ── Snapshot (Cancel 시 되돌리기) ──────────────────────
        void TakeSnapshot();
        void RevertToSnapshot();

        // ── 영속화 ────────────────────────────────────────────
        /// <summary>현재 메모리 값 → PlayerPrefs (해상도 제외).</summary>
        void Save();

        /// <summary>PlayerPrefs → 메모리/오디오 (Awake 시 호출 권장).</summary>
        void Load();

        /// <summary>기본값으로 리셋 (메모리만, Save로 영속화).</summary>
        void ResetToDefaults();

        // ── UI 진입점 ────────────────────────────────
        /// <summary>설정 팝업 표시 (모달).</summary>
        void ShowSettingsUI();
    }
}
