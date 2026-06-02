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

        /// <summary>
        /// clip 해석 주입점: (category, name) → AudioClip. 기본은 Resources/Audio/{category}/{name}.
        /// 부팅 시 카탈로그 기반 로더로 교체하거나 테스트가 대체할 수 있다(순수 재생 로직 격리).
        /// </summary>
        public Func<string, string, AudioClip> ClipLoader =
            (category, name) => Resources.Load<AudioClip>($"Audio/{category}/{name}");

        string _currentBgm;
        /// <summary>현재 재생 중인 BGM 이름(세이브/디버그용). 정지 시 null.</summary>
        public string CurrentBgm => _currentBgm;

        Coroutine _bgmFade;
        readonly List<IDisposable> _subs = new();

        void OnEnable()
        {
            EnsureSources();
            _subs.Add(EventBus.Subscribe<PlayBgmCommand>(e => PlayBgm(e.Name, e.Fade)));
            _subs.Add(EventBus.Subscribe<StopBgmCommand>(e => StopBgm(e.Fade)));
            _subs.Add(EventBus.Subscribe<PlaySfxCommand>(e => PlaySfx(e.Name)));
            _subs.Add(EventBus.Subscribe<PlayVoiceCommand>(e => PlayVoice(e.Name)));
            _subs.Add(EventBus.Subscribe<StopVoiceCommand>(_ => StopVoice()));
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
            if (bgmSource == null) { bgmSource = gameObject.AddComponent<AudioSource>(); bgmSource.playOnAwake = false; bgmSource.loop = true; }
            if (sfxSource == null) { sfxSource = gameObject.AddComponent<AudioSource>(); sfxSource.playOnAwake = false; }
            if (voiceSource == null) { voiceSource = gameObject.AddComponent<AudioSource>(); voiceSource.playOnAwake = false; }
        }

        // ── BGM ──

        public void PlayBgm(string name, float fade = -1f)
        {
            EnsureSources();
            if (string.Equals(_currentBgm, name)) return;

            var clip = ClipLoader?.Invoke("BGM", name);
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
            bgmSource.volume = 1f;
            bgmSource.Play();
        }

        IEnumerator CrossfadeTo(AudioClip clip, float fade)
        {
            if (bgmSource.isPlaying)
                yield return FadeVolume(bgmSource.volume, 0f, fade * 0.4f); // 아웃은 빠르게

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
            yield return FadeVolume(0f, 1f, fade);
            bgmSource.volume = 1f;
            _bgmFade = null;
        }

        IEnumerator FadeOutThenStop(float fade)
        {
            yield return FadeVolume(bgmSource.volume, 0f, fade);
            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.volume = 1f;
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
            var clip = ClipLoader?.Invoke("SFX", name);
            if (clip == null) { Debug.LogWarning($"[AudioManager] SFX 없음: {name}"); return; }
            sfxSource.PlayOneShot(clip);
        }

        public void PlayVoice(string name)
        {
            EnsureSources();
            var clip = ClipLoader?.Invoke("Voice", name);
            if (clip == null) { Debug.LogWarning($"[AudioManager] Voice 없음: {name}"); return; }
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        public void StopVoice()
        {
            if (voiceSource != null) voiceSource.Stop();
        }
    }
}
