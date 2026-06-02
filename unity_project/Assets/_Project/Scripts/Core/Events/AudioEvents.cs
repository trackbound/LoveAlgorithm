namespace LoveAlgo.Events
{
    // ── 오디오 명령 이벤트(입력) ──
    // 발행자(내러티브 Sound 명령/HUD 등, 후속)는 "무엇을 재생하라"만 알리고, AudioManager 구독자가
    // AudioSource로 실제 재생한다(ADR-007, Service Locator 없음). Fade<0 = AudioManager 기본 페이드.

    /// <summary>BGM 재생 명령. <see cref="Fade"/>&lt;0이면 AudioManager 기본 페이드.</summary>
    public readonly struct PlayBgmCommand
    {
        public readonly string Name;
        public readonly float Fade;
        public PlayBgmCommand(string name, float fade = -1f) { Name = name; Fade = fade; }
    }

    /// <summary>BGM 정지 명령.</summary>
    public readonly struct StopBgmCommand
    {
        public readonly float Fade;
        public StopBgmCommand(float fade = -1f) { Fade = fade; }
    }

    /// <summary>효과음(SFX) 1회 재생 명령.</summary>
    public readonly struct PlaySfxCommand
    {
        public readonly string Name;
        public PlaySfxCommand(string name) { Name = name; }
    }

    /// <summary>보이스 재생 명령(이전 보이스 정지 후 재생).</summary>
    public readonly struct PlayVoiceCommand
    {
        public readonly string Name;
        public PlayVoiceCommand(string name) { Name = name; }
    }

    /// <summary>보이스 정지 명령.</summary>
    public readonly struct StopVoiceCommand { }

    // ── 오디오 통지 이벤트(출력) ──

    /// <summary>현재 BGM이 바뀌면 발행(정지 시 Name=null). 구 <c>BGMChangedEvent</c> 이식 — 세이브/디버그/UI용.</summary>
    public readonly struct BgmChangedEvent
    {
        public readonly string Name;
        public BgmChangedEvent(string name) { Name = name; }
    }
}
