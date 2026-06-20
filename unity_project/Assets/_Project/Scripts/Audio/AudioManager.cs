using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // 오디오 명령/통지 이벤트
using UnityEngine;

namespace LoveAlgo.Audio
{
    /// <summary>
    /// 오디오 매니저(승인된 4매니저 중 AudioManager). EventBus 명령(PlayBgm/StopBgm/PlaySfx/PlayVoice/StopVoice)을
    /// 구독해 AudioSource로 재생한다(ADR-007). 구 <c>AudioManager</c>(SingletonMonoBehaviour + IAudio)의
    /// Service Locator 경로를 대체 — 핵심 재생만(금지선4·5).
    ///
    /// <para>슬라이스1 범위: BGM(코루틴 페이드)·SFX·Voice 재생 + <see cref="BgmChangedEvent"/> 통지.
    /// 범위 밖(후속): AudioMixer 볼륨(Settings)·UI 사운드/버튼 자동바인딩·타이핑(M5 UI)·캐릭터 BGM 자동전환(Stage).
    /// DOTween 의존 제거 — 페이드는 자체 코루틴(연출 수치 fadeDuration은 명령/기본값으로 보존).</para>
    ///
    /// 씬 하이어라키: _Managers/AudioManager. AudioSource 미바인딩 시 자동 생성(<see cref="EnsureSources"/>).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("오디오 소스 (미바인딩 시 자동 생성)")]
        [SerializeField] AudioSource bgmSource;
        [SerializeField] AudioSource sfxSource;
        [SerializeField] AudioSource voiceSource;

        [Header("설정")]
        [Tooltip("명령이 Fade<0을 줄 때 사용할 기본 페이드(초).")]
        [SerializeField] float defaultFade = 1f;

        [Header("오디오 뱅크 (직접 바인딩 — 미바인딩 시 Resources/Data/AudioBank 자동 로드)")]
        [Tooltip("키→AudioClip 직접 참조 SO. 여기서 먼저 찾고, 없으면 Resources/Audio/{category}/{name} 폴백.")]
        [SerializeField] AudioBankSO bank;

        /// <summary>
        /// clip 해석 오버라이드 주입점: (category, name) → AudioClip. 설정 시 뱅크/Resources보다 우선한다
        /// (테스트가 순수 재생 로직을 격리하려고 대체). 기본 null → <see cref="LoadClip"/>의 뱅크→Resources 경로.
        /// </summary>
        public Func<string, string, AudioClip> ClipLoader;

        /// <summary>clip 해석: ClipLoader 오버라이드 → 뱅크 직접 바인딩 → Resources/Audio/{category}/{name} 폴백.</summary>
        AudioClip LoadClip(string category, string name)
        {
            if (ClipLoader != null) return ClipLoader(category, name);
            var clip = bank != null ? bank.Resolve(category, name) : null;
            return clip != null ? clip : Resources.Load<AudioClip>($"Audio/{category}/{name}");
        }

        string _currentBgm;
        /// <summary>현재 재생 중인 BGM 이름(세이브/디버그용). 정지 시 null.</summary>
        public string CurrentBgm => _currentBgm;

        Coroutine _bgmFade;
        readonly List<IDisposable> _subs = new();
        float _bgmVolume = 1f, _sfxVolume = 1f; // 설정 볼륨(SetVolumeCommand) — 재생/페이드 타깃에 반영

        void OnEnable()
        {
            EnsureSources();
            _subs.Add(EventBus.Subscribe<PlayBgmCommand>(e => PlayBgm(e.Name, e.Fade)));
            _subs.Add(EventBus.Subscribe<StopBgmCommand>(e => StopBgm(e.Fade)));
            _subs.Add(EventBus.Subscribe<PlaySfxCommand>(e => PlaySfx(e.Name)));
            _subs.Add(EventBus.Subscribe<PlayVoiceCommand>(e => PlayVoice(e.Name)));
            _subs.Add(EventBus.Subscribe<StopVoiceCommand>(_ => StopVoice()));
            _subs.Add(EventBus.Subscribe<SetVolumeCommand>(OnSetVolume));
        }

        void OnDisable()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_bgmFade != null) { StopCoroutine(_bgmFade); _bgmFade = null; }
        }

        /// <summary>AudioSource가 미바인딩이면 런타임 생성(씬 바인딩 누락/테스트 대비).</summary>
        void EnsureSources()
        {
            if (bank == null) bank = Resources.Load<AudioBankSO>("Data/AudioBank");
            if (bgmSource == null) { bgmSource = gameObject.AddComponent<AudioSource>(); bgmSource.playOnAwake = false; bgmSource.loop = true; }
            if (sfxSource == null) { sfxSource = gameObject.AddComponent<AudioSource>(); sfxSource.playOnAwake = false; }
            if (voiceSource == null) { voiceSource = gameObject.AddComponent<AudioSource>(); voiceSource.playOnAwake = false; }
        }

        // ── BGM ──

        public void PlayBgm(string name, float fade = -1f)
        {
            EnsureSources();
            if (string.Equals(_currentBgm, name)) return;

            var clip = LoadClip("BGM", name);
            if (clip == null) { Debug.LogWarning($"[AudioManager] BGM 없음: {name}"); return; }

            _currentBgm = name;
            if (fade < 0) fade = defaultFade;

            if (_bgmFade != null) { StopCoroutine(_bgmFade); _bgmFade = null; }
            if (fade > 0f && isActiveAndEnabled)
                _bgmFade = StartCoroutine(CrossfadeTo(clip, fade));
            else
                SwapBgmImmediate(clip);

            EventBus.Publish(new BgmChangedEvent(name));
        }

        public void StopBgm(float fade = -1f)
        {
            EnsureSources();
            _currentBgm = null;
            if (fade < 0) fade = defaultFade;

            if (_bgmFade != null) { StopCoroutine(_bgmFade); _bgmFade = null; }
            if (fade > 0f && isActiveAndEnabled && bgmSource.isPlaying)
                _bgmFade = StartCoroutine(FadeOutThenStop(fade));
            else
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }

            EventBus.Publish(new BgmChangedEvent(null));
        }

        void SwapBgmImmediate(AudioClip clip)
        {
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.volume = _bgmVolume;
            bgmSource.Play();
        }

        IEnumerator CrossfadeTo(AudioClip clip, float fade)
        {
            if (bgmSource.isPlaying)
                yield return FadeVolume(bgmSource.volume, 0f, fade * 0.4f); // 아웃은 빠르게

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
            yield return FadeVolume(0f, _bgmVolume, fade);
            bgmSource.volume = _bgmVolume;
            _bgmFade = null;
        }

        IEnumerator FadeOutThenStop(float fade)
        {
            yield return FadeVolume(bgmSource.volume, 0f, fade);
            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.volume = _bgmVolume;
            _bgmFade = null;
        }

        IEnumerator FadeVolume(float from, float to, float dur)
        {
            if (dur <= 0f) { bgmSource.volume = to; yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            bgmSource.volume = to;
        }

        // ── SFX / Voice ──

        public void PlaySfx(string name)
        {
            EnsureSources();
            var clip = LoadClip("SFX", name);
            if (clip == null) { Debug.LogWarning($"[AudioManager] SFX 없음: {name}"); return; }
            sfxSource.PlayOneShot(clip, _sfxVolume);
        }

        public void PlayVoice(string name)
        {
            EnsureSources();
            var clip = LoadClip("Voice", name);
            if (clip == null) { Debug.LogWarning($"[AudioManager] Voice 없음: {name}"); return; }
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        public void StopVoice()
        {
            if (voiceSource != null) voiceSource.Stop();
        }

        // ── 볼륨(설정) ──
        // SetVolumeCommand로 채널 볼륨 갱신. 재생/페이드가 _bgmVolume/_sfxVolume를 타깃으로 쓴다(PlayBgm가 1f로
        // 덮어쓰던 문제 해소). 페이드 중이면 코루틴이 _bgmVolume로 수렴하므로 즉시 덮어쓰지 않는다.
        void OnSetVolume(SetVolumeCommand e)
        {
            switch (e.Channel)
            {
                case AudioChannel.Bgm:
                    _bgmVolume = Mathf.Clamp01(e.Value);
                    if (bgmSource != null && _bgmFade == null) bgmSource.volume = _bgmVolume;
                    break;
                case AudioChannel.Sfx:
                    _sfxVolume = Mathf.Clamp01(e.Value);
                    break;
            }
        }
    }
}
