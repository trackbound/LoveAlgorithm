using System;
using LoveAlgo.Contracts;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;

namespace LoveAlgo.Modules.Audio
{
    /// <summary>
    /// 오디오 모듈 진입점.
    /// 기존 AudioManager(Singleton MonoBehaviour)를 IAudio로 래핑.
    /// BGM 재생/정지 시 BGMChangedEvent 발행.
    /// C3-5: Stage/MiniGame이 발행하는 Character* 이벤트 + SFXClipRequestedEvent 구독.
    /// 씬 하이어라키: _Modules/AudioModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class AudioModule : MonoBehaviour, IAudio
    {
        IDisposable _subCharEntered;
        IDisposable _subCharExited;
        IDisposable _subAllExited;
        IDisposable _subEntrySFX;
        IDisposable _subSFXClip;

        void Awake()
        {
            Services.Register<IAudio>(this);
            // C3-5: Stage→Audio 라이프사이클 이벤트 구독 (수동 — 부착처 GO 파괴와 별개로 모듈 수명 동안 유지)
            _subCharEntered = EventBus.Subscribe<CharacterEnteredEvent>(OnCharacterEntered);
            _subCharExited  = EventBus.Subscribe<CharacterExitedEvent>(OnCharacterExited);
            _subAllExited   = EventBus.Subscribe<AllCharactersExitedEvent>(OnAllCharactersExited);
            _subEntrySFX    = EventBus.Subscribe<CharacterEntrySFXRequestedEvent>(OnCharacterEntrySFXRequested);
            _subSFXClip     = EventBus.Subscribe<SFXClipRequestedEvent>(OnSFXClipRequested);
        }

        void OnDestroy()
        {
            _subCharEntered?.Dispose();
            _subCharExited?.Dispose();
            _subAllExited?.Dispose();
            _subEntrySFX?.Dispose();
            _subSFXClip?.Dispose();
            if (Services.TryGet<IAudio>() == (IAudio)this)
                Services.Unregister<IAudio>();
        }

        // ── C3-5 EventBus 핸들러 ─────────────────────────────────
        void OnCharacterEntered(CharacterEnteredEvent e)
            => AudioManager.Instance?.OnCharacterEnter(e.Character);

        void OnCharacterExited(CharacterExitedEvent e)
            => AudioManager.Instance?.OnCharacterExit(e.Character);

        void OnAllCharactersExited(AllCharactersExitedEvent _)
            => AudioManager.Instance?.OnAllCharactersExit();

        void OnCharacterEntrySFXRequested(CharacterEntrySFXRequestedEvent e)
            => AudioManager.Instance?.PlayCharacterEntrySFX(e.Character);

        void OnSFXClipRequested(SFXClipRequestedEvent e)
        {
            if (e.Clip != null)
                AudioManager.Instance?.PlaySFXClip(e.Clip);
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

        public void StopBGMImmediate()
        {
            var am = AudioManager.Instance;
            if (am == null) return;
            am.StopBGMImmediate();
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
