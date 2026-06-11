namespace LoveAlgo.Events
{
    // ── 설정 커맨드(SettingsView 발행 → AudioManager/DialogueView/SettingsController가 적용·영속) ──
    // ADR-007: 표시·발행은 뷰, 적용은 각 도메인 오너. 볼륨·속도는 라이브, 해상도/전체화면은 적용 버튼 스테이징.

    /// <summary>볼륨 채널. 캐릭터별 음성(Voice)은 v1 보류(오디오 단일 채널) — 추후 추가.</summary>
    public enum AudioChannel { Bgm, Sfx }

    /// <summary>설정 팝업 열기 요청(타이틀/인게임 CONFIG). SettingsView가 구독해 표시(ADR-013 Overlay).</summary>
    public readonly struct ShowSettingsCommand { }

    /// <summary>채널 볼륨(0~1) 설정. AudioManager가 적용, SettingsController가 영속.</summary>
    public readonly struct SetVolumeCommand
    {
        public readonly AudioChannel Channel;
        public readonly float Value;
        public SetVolumeCommand(AudioChannel channel, float value) { Channel = channel; Value = value; }
    }

    /// <summary>대사 출력 속도(정규화 0=느림~1=빠름). DialogueView가 초로 매핑·적용.</summary>
    public readonly struct SetTextSpeedCommand
    {
        public readonly float Value01;
        public SetTextSpeedCommand(float value01) { Value01 = value01; }
    }

    /// <summary>자동 진행 속도(정규화 0=느림~1=빠름). DialogueView가 초로 매핑·적용.</summary>
    public readonly struct SetAutoSpeedCommand
    {
        public readonly float Value01;
        public SetAutoSpeedCommand(float value01) { Value01 = value01; }
    }

    /// <summary>화면 설정 적용(스테이징된 해상도 인덱스 + 전체화면 여부). SettingsController가 Screen에 적용·영속.</summary>
    public readonly struct ApplyDisplayCommand
    {
        public readonly int ResolutionIndex;
        public readonly bool Fullscreen;
        public ApplyDisplayCommand(int resolutionIndex, bool fullscreen) { ResolutionIndex = resolutionIndex; Fullscreen = fullscreen; }
    }

    /// <summary>설정 기본값 복원. SettingsController가 기본값 적용·영속·재발행.</summary>
    public readonly struct ResetSettingsCommand { }
}
