using LoveAlgo.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace LoveAlgo.Modules.Audio
{
    /// <summary>
    /// 오디오 매니저 (BGM, SFX, Voice). Singleton MonoBehaviour.
    /// 외부 모듈은 <see cref="IAudio"/> 인터페이스 경유 사용 권장.
    /// </summary>
    public class AudioManager : SingletonMonoBehaviour<AudioManager>
    {

        [Header("오디오 소스 (인스펙터 바인딩)")]
        [SerializeField] AudioSource bgmSource;
        [SerializeField] AudioSource sfxSource;
        [SerializeField] AudioSource voiceSource;
        [SerializeField] AudioSource uiAudioSource;
        [SerializeField] AudioSource uiTypingSource;

        [Header("UI Sound Settings")]
        [SerializeField] AudioClip hoverClip;
        [SerializeField] AudioClip clickClip;
        [SerializeField] AudioClip typingClip;
        [SerializeField] AudioClip dialogueNextClip;
        [SerializeField] AudioClip choiceSelectClip;
        [SerializeField] AudioClip choiceAppearClip;
        [SerializeField] AudioClip choiceHoverClip;
        [SerializeField] AudioClip popupOpenClip;
        [SerializeField] AudioClip popupCloseClip;
        [SerializeField] AudioClip saveCompleteClip;
        [SerializeField] AudioClip loadCompleteClip;
        [Tooltip("볼륨 슬라이더 프리뷰 전용 사운드 (미할당 시 clickClip 사용)")]
        [SerializeField] AudioClip volumePreviewClip;
        [SerializeField] AudioMixerGroup sfxMixerGroup;
        [SerializeField] float hoverVolume = 0.5f;
        [SerializeField] float clickVolume = 0.7f;
        [SerializeField] float minTypingPitch = 0.9f;
        [SerializeField] float maxTypingPitch = 1.1f;
        [SerializeField] float minTypingVolume = 0.35f;
        [SerializeField] float maxTypingVolume = 0.5f;
        [SerializeField] float typingMinInterval = 0.035f;
        [SerializeField] float volumePreviewDebounce = 0.08f;
        [SerializeField] bool autoBindButtons = true;

        [Header("오디오 믹서")]
        [SerializeField] AudioMixer audioMixer;
        [SerializeField] string masterVolumeParam = "MasterVolume";
        [SerializeField] string bgmVolumeParam = "BGMVolume";
        [SerializeField] string sfxVolumeParam = "SFXVolume";
        [SerializeField] string voiceVolumeParam = "VoiceVolume";

        [Header("오디오 설정")]
        [SerializeField] AudioSettings audioSettings;

        [Header("설정")]
        [SerializeField] float defaultFadeDuration = 4.0f;

        [Header("캐릭터 등장 SFX (인스펙터 바인딩)")]
        [Tooltip("CharacterId(c01 등) → 등장 시 1회 재생할 AudioClip. 글리치 등 캐릭터별 시그니처 사운드.")]
        [SerializeField] List<CharacterEntrySFX> characterEntrySFXs = new();

        [Serializable]
        public class CharacterEntrySFX
        {
            [Tooltip("CharacterId (예: c01) 또는 DisplayName/Alias (예: 로아, Roa)")]
            public string Character;
            public AudioClip Clip;
        }

        string currentBGM;
        string currentCharacterBGM;

        /// <summary>이전 BGM 비동기 작업 취소용 (경합 방지)</summary>
        CancellationTokenSource bgmCts;

        /// <summary>
        /// 현재 재생 중인 BGM 이름 (세이브용)
        /// </summary>
        public string CurrentBGM => currentBGM;
        HashSet<string> presentCharacters = new();
        
        readonly Dictionary<string, float> characterVoiceVolumes = new();

        readonly HashSet<Button> registeredButtons = new();
        readonly HashSet<Button> excludedButtons = new();
        float lastTypingPlayTime = -999f;
        float volumePreviewScheduledTime = -1f;
        float volumePreviewScale = 1f;
        int _registerCallsSincePrune;
        const int PruneEveryN = 64;

        /// <summary>
        /// SFX 클립 캐시 (Awake에서 한 번만 로드)
        /// </summary>
        AudioClip[] sfxClipCache;

        AudioClip DefaultBGM => audioSettings?.defaultBGM;
        float BgmFadeDuration => audioSettings?.bgmFadeDuration ?? 1f;
        bool AutoSwitchBGMOnCharacterEnter => audioSettings?.autoSwitchOnCharacterEnter ?? false;

        protected override void OnSingletonAwake()
        {
            ValidateAudioSources();
            CacheSFXClips();
            SetupUIAudioSources();
            WarmUpUIClips();

            // 볼륨 복원은 SettingsModule.Load의 책임 — 마스터 설정 저장소를
            // 한 곳으로 통일하기 위해 AudioManager는 PlayerPrefs를 직접 읽지 않는다.
            // SettingsModule(-450)이 AudioModule(-500) Awake 이후 곧바로 Load를 호출하며,
            // 그때 SetMasterVolume/SetBGMVolume/SetCharacterVoiceVolume이 적용된다.

            if (audioSettings == null)
            {
                Debug.LogWarning("[AudioManager] AudioSettings가 할당되지 않았습니다.");
            }
        }

        void Start()
        {
            if (autoBindButtons)
            {
                BindAllButtons();
            }
        }

        void SetupUIAudioSources()
        {
            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.AddComponent<AudioSource>();
            }
            uiAudioSource.playOnAwake = false;
            uiAudioSource.priority = 0;

            if (uiTypingSource == null)
            {
                uiTypingSource = gameObject.AddComponent<AudioSource>();
            }
            uiTypingSource.playOnAwake = false;
            uiTypingSource.priority = 0;

            if (sfxMixerGroup != null)
            {
                uiAudioSource.outputAudioMixerGroup = sfxMixerGroup;
                uiTypingSource.outputAudioMixerGroup = sfxMixerGroup;
            }
            else if (sfxSource != null && sfxSource.outputAudioMixerGroup != null)
            {
                uiAudioSource.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
                uiTypingSource.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
            }
        }

        void WarmUpUIClips()
        {
            if (hoverClip != null && uiAudioSource != null) uiAudioSource.PlayOneShot(hoverClip, 0f);
            if (clickClip != null && uiAudioSource != null) uiAudioSource.PlayOneShot(clickClip, 0f);
            if (typingClip != null && uiTypingSource != null) uiTypingSource.PlayOneShot(typingClip, 0f);
        }

        /// <summary>
        /// SFX 클립을 한 번만 로드하여 캐싱
        /// </summary>
        void CacheSFXClips()
        {
            sfxClipCache = Resources.LoadAll<AudioClip>("Audio/SFX");
            Debug.Log($"[AudioManager] SFX 클립 {sfxClipCache.Length}개 캐싱 완료");
        }

        void ValidateAudioSources()
        {
            if (bgmSource == null)
                Debug.LogWarning("[AudioManager] bgmSource가 바인딩되지 않았습니다.");
            else
                bgmSource.loop = true;  // BGM은 항상 무한반복

            if (sfxSource == null)
                Debug.LogWarning("[AudioManager] sfxSource가 바인딩되지 않았습니다.");
            if (voiceSource == null)
                Debug.LogWarning("[AudioManager] voiceSource가 바인딩되지 않았습니다.");
        }

        /// <summary>
        /// Sound 명령 실행
        /// Value 형식: 카테고리:이름[:옵션]
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
        {
            var parts = value.Split(':');
            if (parts.Length < 2)
            {
                Debug.LogWarning($"[AudioManager] 잘못된 형식: {value}");
                return;
            }

            string category = parts[0];
            string name = parts[1];

            switch (category)
            {
                case "BGM":
                    await HandleBGMAsync(name, parts, ct);
                    break;

                case "SFX":
                    PlaySFX(name);
                    break;

                case "Voice":
                    PlayVoice(name);
                    break;

                default:
                    Debug.LogWarning($"[AudioManager] 알 수 없는 카테고리: {category}");
                    break;
            }
        }

        #region BGM

        /// <summary>이전 BGM 작업을 취소하고 새 CTS 생성</summary>
        CancellationToken CancelPreviousBGM()
        {
            bgmCts?.Cancel();
            bgmCts?.Dispose();
            bgmCts = new CancellationTokenSource();
            return bgmCts.Token;
        }

        async UniTask HandleBGMAsync(string name, string[] parts, CancellationToken ct)
        {
            // 한글/alias(예: 백색소음1, SeoDaEun) → 엔진 파일명(white_noise, Daeun) 변환
            if (StoryMappings.TryResolveBgm(name, out var resolved))
                name = resolved;

            // BGM:Stop or BGM:Stop:Fade:1.0
            if (name.Equals("Stop", StringComparison.OrdinalIgnoreCase))
            {
                float fadeDuration = defaultFadeDuration;
                if (parts.Length >= 4 && parts[2].Equals("Fade", StringComparison.OrdinalIgnoreCase) && float.TryParse(parts[3], out float fd))
                {
                    fadeDuration = fd;
                }
                await StopBGMAsync(fadeDuration, ct);
                return;
            }

            // BGM:Name or BGM:Name:Fade:1.0
            float crossfadeDuration = -1f;  // -1 = 기본 페이드 (3초)
            if (parts.Length >= 4 && parts[2].Equals("Fade", StringComparison.OrdinalIgnoreCase) && float.TryParse(parts[3], out float cfd))
            {
                crossfadeDuration = cfd;
            }

            // BGM 페이드는 백그라운드로 처리 — 스크립트 진행을 차단하지 않음
            // CancelPreviousBGM()은 PlayBGMAsync 내부에서 호출됨
            PlayBGMAsync(name, crossfadeDuration).Forget();
        }

        public async UniTask PlayBGMAsync(string name, float fadeDuration = -1f, CancellationToken ct = default)
        {
            if (currentBGM == name) return;

            // 1차: ResourceCatalogSO (한글 키 또는 영문 ID)
            AudioClip clip = null;
            var catalog = LoveAlgo.Story.Data.ResourceCatalogSO.Instance;
            if (catalog != null && catalog.TryGetBgm(name, out var byCatalog))
                clip = byCatalog;

            // 2차: 기존 Resources/Audio/BGM 폴백
            if (clip == null)
                clip = LoadAudioClip($"Audio/BGM/{name}");

            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] BGM 없음: {name}");
                return;
            }

            // -1이면 기본 페이드 (3초)
            if (fadeDuration < 0) fadeDuration = defaultFadeDuration;

            // 이전 BGM 비동기 작업 취소 + 트윈 정리
            var token = CancelPreviousBGM();
            DOTween.Kill(bgmSource);

            // [진단 로그] BGM 전환 지점 — 찌직거림 추적용
            Debug.Log($"[AudioManager][BGM] {currentBGM ?? "(none)"} -> {name} | fade={fadeDuration:F2}s | playing={bgmSource.isPlaying} | t={Time.time:F2}");

            // 이전 BGM 클립 참조 보관 (해제용)
            var previousClip = bgmSource.clip;

            currentBGM = name;
            bgmSource.loop = true;

            float targetVolume = 1f;

            if (fadeDuration > 0 && bgmSource.isPlaying)
            {
                // 크로스페이드: 짧은 페이드아웃 → 페이드인 (청각적으로 자연스럽게)
                // targetVolume은 항상 1f — 볼륨 제어는 AudioMixer에서 담당
                float fadeOutTime = fadeDuration * 0.4f;  // 아웃은 빠르게
                float fadeInTime = fadeDuration;           // 인은 풀 duration

                await bgmSource.DOFade(0f, fadeOutTime)
                    .SetEase(Ease.InQuad)     // 처음엔 천천히 줄다가 빠르게 사라짐
                    .ToUniTask(cancellationToken: token);

                bgmSource.clip = clip;
                bgmSource.Play();
                await bgmSource.DOFade(targetVolume, fadeInTime)
                    .SetEase(Ease.InQuad)     // 서서히 올라와 자연스럽게 도달
                    .ToUniTask(cancellationToken: token);

                // 트윈이 중단되어도 목표 볼륨 보장
                if (bgmSource != null) bgmSource.volume = targetVolume;
            }
            else if (fadeDuration > 0)
            {
                // 새로 시작: 자연스러운 페이드인 (초반부터 부드럽게 올라와 끝에서 완만하게 도달)
                // Why OutQuad: InQuad는 거의 끝까지 무음으로 있다가 끝에서 급격히 1로 튀어 "갑자기 시작"처럼 들림.
                bgmSource.volume = 0f;
                bgmSource.clip = clip;
                bgmSource.Play();
                await bgmSource.DOFade(targetVolume, fadeDuration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: token);

                // 트윈이 중단되어도 목표 볼륨 보장
                if (bgmSource != null) bgmSource.volume = targetVolume;
            }
            else
            {
                bgmSource.clip = clip;
                bgmSource.Play();
            }

            // 이전 BGM 클립 해제 (새 클립과 다를 때만)
            if (previousClip != null && previousClip != clip)
                Resources.UnloadAsset(previousClip);
        }

        public async UniTask StopBGMAsync(float fadeDuration = -1f, CancellationToken ct = default)
        {
            if (!bgmSource.isPlaying) return;

            // -1이면 기본 페이드 (3초)
            if (fadeDuration < 0) fadeDuration = defaultFadeDuration;

            // 이전 BGM 비동기 작업 취소 + 트윈 정리
            var token = CancelPreviousBGM();
            DOTween.Kill(bgmSource);

            currentBGM = null;
            float originalVolume = bgmSource.volume;

            if (fadeDuration > 0)
            {
                await bgmSource.DOFade(0f, fadeDuration)
                    .SetEase(Ease.InQuad)   // 처음엔 천천히 줄다가 빠르게 사라짐
                    .ToUniTask(cancellationToken: token);
            }

            bgmSource.Stop();
            var clipToUnload = bgmSource.clip;
            bgmSource.clip = null;
            bgmSource.volume = originalVolume;

            // 정지된 BGM 클립 해제
            if (clipToUnload != null)
                Resources.UnloadAsset(clipToUnload);
        }

        public void StopBGMImmediate()
        {
            // 진행 중인 BGM 비동기 작업 모두 취소
            bgmCts?.Cancel();
            bgmCts?.Dispose();
            bgmCts = null;

            DOTween.Kill(bgmSource);
            var clipToUnload = bgmSource.clip;
            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.volume = 1f;
            currentBGM = null;

            // 즉시 정지된 BGM 클립 해제
            if (clipToUnload != null)
                Resources.UnloadAsset(clipToUnload);
        }

        /// <summary>
        /// 앱 포커스 잃음/복귀 시 오디오 일시정지/재개
        /// </summary>
        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                if (bgmSource != null) bgmSource.Pause();
                if (sfxSource != null) sfxSource.Pause();
                if (voiceSource != null) voiceSource.Pause();
                if (uiAudioSource != null) uiAudioSource.Stop();
                if (uiTypingSource != null) uiTypingSource.Stop();
            }
            else
            {
                if (bgmSource != null && bgmSource.clip != null && !bgmSource.isPlaying)
                    bgmSource.UnPause();
                if (sfxSource != null) sfxSource.UnPause();
                if (voiceSource != null) voiceSource.UnPause();
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                if (bgmSource != null) bgmSource.Pause();
                if (sfxSource != null) sfxSource.Pause();
                if (voiceSource != null) voiceSource.Pause();
                if (uiAudioSource != null) uiAudioSource.Stop();
                if (uiTypingSource != null) uiTypingSource.Stop();
            }
            else
            {
                if (bgmSource != null && bgmSource.clip != null && !bgmSource.isPlaying)
                    bgmSource.UnPause();
                if (sfxSource != null) sfxSource.UnPause();
                if (voiceSource != null) voiceSource.UnPause();
            }
        }

        #endregion

        #region 캐릭터 BGM 자동 전환

        /// <summary>
        /// 캐릭터 등장 시 호출 (CharacterLayer에서 호출)
        /// </summary>
        public void OnCharacterEnter(string characterName)
        {
            presentCharacters.Add(characterName);
            
            if (AutoSwitchBGMOnCharacterEnter)
            {
                UpdateCharacterBGMAsync().Forget();
            }
        }

        /// <summary>
        /// 캐릭터 퇴장 시 호출 (CharacterLayer에서 호출)
        /// </summary>
        public void OnCharacterExit(string characterName)
        {
            presentCharacters.Remove(characterName);
            
            if (AutoSwitchBGMOnCharacterEnter)
            {
                UpdateCharacterBGMAsync().Forget();
            }
        }

        /// <summary>
        /// 모든 캐릭터 퇴장 시 호출
        /// </summary>
        public void OnAllCharactersExit()
        {
            presentCharacters.Clear();
            
            if (AutoSwitchBGMOnCharacterEnter)
            {
                UpdateCharacterBGMAsync().Forget();
            }
        }

        /// <summary>
        /// 현재 등장한 캐릭터 중 가장 우선순위 높은 BGM으로 전환
        /// </summary>
        async UniTask UpdateCharacterBGMAsync()
        {
            if (audioSettings == null) return;
            
            AudioClip bestClip = null;
            string bestCharName = null;
            int highestPriority = int.MinValue;

            foreach (var charName in presentCharacters)
            {
                var mapping = audioSettings.characterBGMs.Find(m =>
                    m.characterName.Equals(charName, System.StringComparison.OrdinalIgnoreCase));
                
                if (mapping != null && mapping.priority >= highestPriority)
                {
                    AudioClip clip = mapping.bgmClip;
                    if (clip == null && !string.IsNullOrEmpty(mapping.bgmResourcePath))
                    {
                        clip = LoadAudioClip($"Audio/BGM/{mapping.bgmResourcePath}");
                    }
                    
                    if (clip != null)
                    {
                        bestClip = clip;
                        bestCharName = charName;
                        highestPriority = mapping.priority;
                    }
                }
            }

            if (currentCharacterBGM == bestCharName) return;

            currentCharacterBGM = bestCharName;

            if (bestClip != null)
            {
                await PlayBGMClipAsync(bestClip, BgmFadeDuration);
            }
            else if (DefaultBGM != null)
            {
                await PlayBGMClipAsync(DefaultBGM, BgmFadeDuration);
            }
        }

        /// <summary>
        /// AudioClip으로 직접 BGM 재생 (페이드 지원)
        /// </summary>
        public async UniTask PlayBGMClipAsync(AudioClip clip, float fadeDuration = -1f, CancellationToken ct = default)
        {
            if (clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            // -1이면 기본 페이드 (3초)
            if (fadeDuration < 0) fadeDuration = defaultFadeDuration;

            // 이전 BGM 비동기 작업 취소 + 트윈 정리
            var token = CancelPreviousBGM();
            DOTween.Kill(bgmSource);

            currentBGM = clip.name;
            bgmSource.loop = true;

            float targetVolume = 1f;

            if (fadeDuration > 0 && bgmSource.isPlaying)
            {
                // 크로스페이드: 짧은 페이드아웃 → 페이드인
                // targetVolume은 항상 1f — 볼륨 제어는 AudioMixer에서 담당
                float fadeOutTime = fadeDuration * 0.4f;
                float fadeInTime = fadeDuration;

                await bgmSource.DOFade(0f, fadeOutTime)
                    .SetEase(Ease.InQuad)
                    .ToUniTask(cancellationToken: token);
                bgmSource.clip = clip;
                bgmSource.Play();
                await bgmSource.DOFade(targetVolume, fadeInTime)
                    .SetEase(Ease.InQuad)
                    .ToUniTask(cancellationToken: token);

                if (bgmSource != null) bgmSource.volume = targetVolume;
            }
            else if (fadeDuration > 0)
            {
                // 새로 시작: 자연스러운 페이드인
                bgmSource.volume = 0f;
                bgmSource.clip = clip;
                bgmSource.Play();
                await bgmSource.DOFade(targetVolume, fadeDuration)
                    .SetEase(Ease.InQuad)
                    .ToUniTask(cancellationToken: token);

                if (bgmSource != null) bgmSource.volume = targetVolume;
            }
            else
            {
                bgmSource.clip = clip;
                bgmSource.Play();
            }
        }

        #endregion

        #region SFX

        /// <summary>
        /// SFX 재생
        /// name: 전체 이름(001_Pop) 또는 숫자만(001, 1) 또는 영문명(Pop)
        /// </summary>
        public void PlaySFX(string name)
        {
            var clip = LoadSFXClip(name);

            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] SFX 없음: {name}");
                return;
            }

            sfxSource.PlayOneShot(clip);
        }

        /// <summary>
        /// 캐릭터 등장 시그니처 SFX 재생 (인스펙터 바인딩)
        /// 로아=글리치 등 캐릭터별 시그니처 사운드.
        /// 미바인딩이면 조용히 스킵 — 캐릭터마다 등장 사운드가 없을 수 있음.
        /// </summary>
        public void PlayCharacterEntrySFX(string character)
        {
            if (string.IsNullOrEmpty(character) || characterEntrySFXs == null || sfxSource == null)
                return;

            // 입력(displayName/alias/id) → characterId(c01) 정규화
            string resolvedId = StoryMappings.SpeakerToCharacterId(character) ?? character;

            foreach (var entry in characterEntrySFXs)
            {
                if (entry?.Clip == null || string.IsNullOrEmpty(entry.Character)) continue;
                // 등록된 키도 다양한 표기 허용 — id/displayName/alias 모두 ok
                string entryId = StoryMappings.SpeakerToCharacterId(entry.Character) ?? entry.Character;
                if (string.Equals(entryId, resolvedId, StringComparison.OrdinalIgnoreCase))
                {
                    sfxSource.PlayOneShot(entry.Clip);
                    return;
                }
            }
        }

        /// <summary>
        /// SFX 클립 로드 (숫자 또는 이름으로 검색)
        /// </summary>
        AudioClip LoadSFXClip(string name)
        {
            // 1. 정확한 이름으로 먼저 시도
            var clip = LoadAudioClip($"Audio/SFX/{name}");
            if (clip != null) return clip;
            
            // 2. 숫자만 입력된 경우 (예: "001", "1", "15")
            if (int.TryParse(name, out int number))
            {
                // 3자리 포맷으로 변환하여 검색
                string prefix = number.ToString("D3");
                clip = FindSFXByPrefix(prefix);
                if (clip != null) return clip;
            }
            
            // 3. 영문 이름만 입력된 경우 (예: "Pop", "Sparkle")
            clip = FindSFXByName(name);
            return clip;
        }

        /// <summary>
        /// 숫자 접두사로 SFX 검색
        /// </summary>
        AudioClip FindSFXByPrefix(string prefix)
        {
            if (sfxClipCache == null) CacheSFXClips();
            foreach (var sfx in sfxClipCache)
            {
                if (sfx.name.StartsWith(prefix + "_"))
                {
                    return sfx;
                }
            }
            return null;
        }

        /// <summary>
        /// 영문 이름으로 SFX 검색 (접두사 뒤 이름 매칭)
        /// </summary>
        AudioClip FindSFXByName(string name)
        {
            if (sfxClipCache == null) CacheSFXClips();
            foreach (var sfx in sfxClipCache)
            {
                // "001_Pop" → "Pop" 추출 후 비교
                int underscoreIndex = sfx.name.IndexOf('_');
                if (underscoreIndex >= 0 && underscoreIndex < sfx.name.Length - 1)
                {
                    string sfxName = sfx.name.Substring(underscoreIndex + 1);
                    if (sfxName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return sfx;
                    }
                }
            }
            return null;
        }

        public void PlaySFXClip(AudioClip clip)
        {
            if (clip == null) return;
            sfxSource.PlayOneShot(clip);
        }

        #endregion

        #region Voice

        public void PlayVoice(string name)
        {
            var clip = LoadAudioClip($"Audio/Voice/{name}");
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] Voice 없음: {name}");
                return;
            }

            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        /// <summary>
        /// 캐릭터별 보이스 재생 (캐릭터 볼륨 적용)
        /// </summary>
        public void PlayVoice(string character, string voiceName)
        {
            var clip = LoadAudioClip($"Audio/Voice/{voiceName}");
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] Voice 없음: {voiceName}");
                return;
            }

            // 캐릭터별 볼륨 적용
            float charVolume = GetCharacterVoiceVolume(character);
            voiceSource.volume = charVolume;
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        public void StopVoice()
        {
            if (voiceSource != null)
                voiceSource.Stop();
        }

        #endregion

        #region 캐릭터별 음성 볼륨

        public void SetCharacterVoiceVolume(string character, float volume)
        {
            characterVoiceVolumes[character] = Mathf.Clamp01(volume);
        }

        public float GetCharacterVoiceVolume(string character)
        {
            return characterVoiceVolumes.TryGetValue(character, out float vol) ? vol : 1f;
        }

        #endregion

        #region 볼륨 (AudioMixer)

        /// <summary>
        /// 0~1 볼륨을 dB로 변환 (-80 ~ 0)
        /// </summary>
        float LinearToDecibel(float linear)
        {
            if (linear <= 0f) return -80f;
            return Mathf.Log10(linear) * 20f;
        }

        /// <summary>
        /// dB를 0~1 볼륨으로 변환
        /// </summary>
        float DecibelToLinear(float dB)
        {
            if (dB <= -80f) return 0f;
            return Mathf.Pow(10f, dB / 20f);
        }

        public void SetBGMVolume(float vol)
        {
            if (audioMixer == null) return;
            audioMixer.SetFloat(bgmVolumeParam, LinearToDecibel(Mathf.Clamp01(vol)));
        }

        public void SetSFXVolume(float vol)
        {
            if (audioMixer == null) return;
            audioMixer.SetFloat(sfxVolumeParam, LinearToDecibel(Mathf.Clamp01(vol)));
        }

        public void SetVoiceVolume(float vol)
        {
            if (audioMixer == null) return;
            audioMixer.SetFloat(voiceVolumeParam, LinearToDecibel(Mathf.Clamp01(vol)));
        }

        public void SetMasterVolume(float vol)
        {
            if (audioMixer == null) return;
            audioMixer.SetFloat(masterVolumeParam, LinearToDecibel(Mathf.Clamp01(vol)));
        }

        public float GetBGMVolume()
        {
            if (audioMixer == null) return 1f;
            audioMixer.GetFloat(bgmVolumeParam, out float dB);
            return DecibelToLinear(dB);
        }

        public float GetSFXVolume()
        {
            if (audioMixer == null) return 1f;
            audioMixer.GetFloat(sfxVolumeParam, out float dB);
            return DecibelToLinear(dB);
        }

        public float GetVoiceVolume()
        {
            if (audioMixer == null) return 1f;
            audioMixer.GetFloat(voiceVolumeParam, out float dB);
            return DecibelToLinear(dB);
        }

        public float GetMasterVolume()
        {
            if (audioMixer == null) return 1f;
            audioMixer.GetFloat(masterVolumeParam, out float dB);
            return DecibelToLinear(dB);
        }

        #endregion

        #region UI Sound System (Merged from UISoundManager)

        public void BindAllButtons()
        {
            PruneDestroyedButtons();
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (var button in buttons)
            {
                RegisterButton(button);
            }
        }

        public void PruneDestroyedButtons()
        {
            registeredButtons.RemoveWhere(b => b == null);
            excludedButtons.RemoveWhere(b => b == null);
        }

        public void RegisterButton(Button button)
        {
            if (++_registerCallsSincePrune >= PruneEveryN)
            {
                _registerCallsSincePrune = 0;
                PruneDestroyedButtons();
            }

            if (button == null || registeredButtons.Contains(button)) return;

            registeredButtons.Add(button);

            button.onClick.AddListener(() => { if (!excludedButtons.Contains(button)) PlayClick(); });

            var trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            bool hasEnterEvent = false;
            foreach (var entry in trigger.triggers)
            {
                if (entry.eventID == EventTriggerType.PointerEnter)
                {
                    entry.callback.AddListener(_ => { if (!excludedButtons.Contains(button)) PlayHover(); });
                    hasEnterEvent = true;
                    break;
                }
            }

            if (!hasEnterEvent)
            {
                var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener(_ => { if (!excludedButtons.Contains(button)) PlayHover(); });
                trigger.triggers.Add(enterEntry);
            }
        }

        public void ExcludeButton(Button button)
        {
            if (button != null) excludedButtons.Add(button);
        }

        public void ClearExcludedButtons()
        {
            excludedButtons.Clear();
        }

        public void BindButtonsInTransform(Transform root)
        {
            PruneDestroyedButtons();
            var buttons = root.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                RegisterButton(button);
            }
        }

        public void PlayHover() => PlayUIClipped(hoverClip, hoverVolume);
        public void PlayClick() => PlayUIClipped(clickClip, clickVolume);

        public void PlayTyping()
        {
            if (typingClip == null || uiTypingSource == null) return;
            if (Time.unscaledTime - lastTypingPlayTime < typingMinInterval) return;
            lastTypingPlayTime = Time.unscaledTime;

            if (uiTypingSource.mute) uiTypingSource.mute = false;

            uiTypingSource.clip = typingClip;
            uiTypingSource.pitch = UnityEngine.Random.Range(minTypingPitch, maxTypingPitch);
            uiTypingSource.volume = UnityEngine.Random.Range(minTypingVolume, maxTypingVolume);
            uiTypingSource.Play();
        }

        public void PlayVolumePreview(float volumeScale = 1f)
        {
            volumePreviewScheduledTime = Time.unscaledTime + volumePreviewDebounce;
            volumePreviewScale = Mathf.Clamp01(volumeScale);
        }

        public void PlayDialogueNext() => PlayUIClipped(dialogueNextClip, clickVolume);
        public void PlayChoiceSelect() => PlayUIClipped(choiceSelectClip, clickVolume);
        public void PlayChoiceHover() => PlayUIClipped(choiceHoverClip != null ? choiceHoverClip : hoverClip, hoverVolume);
        public void PlayChoiceAppear() => PlayUIClipped(choiceAppearClip, clickVolume);
        public void PlayPopupOpen() => PlayUIClipped(popupOpenClip, clickVolume);
        public void PlayPopupClose() => PlayUIClipped(popupCloseClip, hoverVolume);
        public void PlaySaveComplete() => PlayUIClipped(saveCompleteClip, clickVolume);
        public void PlayLoadComplete() => PlayUIClipped(loadCompleteClip, clickVolume);

        void PlayUIClipped(AudioClip clip, float volume)
        {
            if (clip != null && uiAudioSource != null)
            {
                uiAudioSource.PlayOneShot(clip, volume);
            }
        }

        #endregion

        AudioClip LoadAudioClip(string path)
        {
            return Resources.Load<AudioClip>(path);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            DOTween.Kill(bgmSource);
            // 디바운스 트윈이 동작할 수 있으므로 DOTween이나 기타 정리
        }
    }
}
