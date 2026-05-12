using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;

namespace LoveAlgo.Modules.Audio
{
    /// <summary>
    /// 오디오 모듈 진입점.
    /// 기존 AudioManager(Singleton MonoBehaviour)를 IAudio로 래핑.
    /// BGM 재생/정지 시 BGMChangedEvent 발행.
    /// 씬 하이어라키: _Modules/AudioModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class AudioModule : MonoBehaviour, IAudio
    {
        void Awake()
        {
            Services.Register<IAudio>(this);
        }

        void OnDestroy()
        {
            if (Services.TryGet<IAudio>() == (IAudio)this)
                Services.Unregister<IAudio>();
        }

        public void PlayBGM(string name, float fadeDuration = -1f)
        {
            var am = AudioManager.Instance;
            if (am == null) return;
            am.PlayBGMAsync(name, fadeDuration).Forget();
            EventBus.Publish(new BGMChangedEvent(name));
        }

        public void StopBGM(float fadeDuration = -1f)
        {
            var am = AudioManager.Instance;
            if (am == null) return;
            am.StopBGMAsync(fadeDuration).Forget();
            EventBus.Publish(new BGMChangedEvent(null));
        }

        public void PlaySFX(string name) => AudioManager.Instance?.PlaySFX(name);
        public void PlayVoice(string character, string voiceName) => AudioManager.Instance?.PlayVoice(character, voiceName);
        public void StopVoice() => AudioManager.Instance?.StopVoice();

        public void SetMasterVolume(float v) => AudioManager.Instance?.SetMasterVolume(v);
        public void SetBGMVolume(float v) => AudioManager.Instance?.SetBGMVolume(v);
        public void SetSFXVolume(float v) => AudioManager.Instance?.SetSFXVolume(v);
        public void SetCharacterVoiceVolume(string character, float v)
            => AudioManager.Instance?.SetCharacterVoiceVolume(character, v);
    }
}
