namespace LoveAlgo.Modules.Audio
{
    /// <summary>
    /// 오디오 모듈 외부 계약 (최소 표면).
    /// 볼륨 슬라이더 같은 설정 UI는 기존 AudioManager 직접 사용. 이 인터페이스는 cross-module 호출만.
    /// 구현: <see cref="AudioModule"/>.
    /// </summary>
    public interface IAudio
    {
        /// <summary>BGM 재생. fadeDuration 음수면 기본값 사용.</summary>
        void PlayBGM(string name, float fadeDuration = -1f);

        /// <summary>BGM 정지 (페이드 아웃).</summary>
        void StopBGM(float fadeDuration = -1f);

        /// <summary>SFX 1회 재생.</summary>
        void PlaySFX(string name);

        /// <summary>지정 캐릭터 보이스 재생.</summary>
        void PlayVoice(string character, string voiceName);

        /// <summary>보이스 정지.</summary>
        void StopVoice();
    }
}
