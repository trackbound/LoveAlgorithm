using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스토리 오디오 매니저 (BGM, SFX, Voice)
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("오디오 소스 (인스펙터 바인딩)")]
        [SerializeField] AudioSource bgmSource;
        [SerializeField] AudioSource sfxSource;
        [SerializeField] AudioSource voiceSource;

        [Header("오디오 믹서")]
        [SerializeField] AudioMixer audioMixer;
        [SerializeField] string masterVolumeParam = "MasterVolume";
        [SerializeField] string bgmVolumeParam = "BGMVolume";
        [SerializeField] string sfxVolumeParam = "SFXVolume";
        [SerializeField] string voiceVolumeParam = "VoiceVolume";

        [Header("오디오 설정")]
        [SerializeField] AudioSettings audioSettings;

        [Header("설정")]
        [SerializeField] float defaultFadeDuration = 3.0f;

        string currentBGM;
        string currentCharacterBGM;

        /// <summary>
        /// 현재 재생 중인 BGM 이름 (세이브용)
        /// </summary>
        public string CurrentBGM => currentBGM;
        HashSet<string> presentCharacters = new();
        
        readonly Dictionary<string, float> characterVoiceVolumes = new();

        AudioClip DefaultBGM => audioSettings?.defaultBGM;
        float BgmFadeDuration => audioSettings?.bgmFadeDuration ?? 1f;
        bool AutoSwitchBGMOnCharacterEnter => audioSettings?.autoSwitchOnCharacterEnter ?? false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject);  // 데모: 단일 씬
                ValidateAudioSources();
                LoadCharacterVoiceVolumes();
                LoadMixerVolumes();
                
                if (audioSettings == null)
                {
                    Debug.LogWarning("[AudioManager] AudioSettings가 할당되지 않았습니다.");
                }
            }
            else
            {
                Destroy(gameObject);
            }
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

        async UniTask HandleBGMAsync(string name, string[] parts, CancellationToken ct)
        {
            // BGM:Stop or BGM:Stop:Fade:1.0
            if (name == "Stop")
            {
                float fadeDuration = defaultFadeDuration;
                if (parts.Length >= 4 && parts[2] == "Fade" && float.TryParse(parts[3], out float fd))
                {
                    fadeDuration = fd;
                }
                await StopBGMAsync(fadeDuration, ct);
                return;
            }

            // BGM:Name or BGM:Name:Fade:1.0
            float crossfadeDuration = -1f;  // -1 = 기본 페이드 (3초)
            if (parts.Length >= 4 && parts[2] == "Fade" && float.TryParse(parts[3], out float cfd))
            {
                crossfadeDuration = cfd;
            }

            await PlayBGMAsync(name, crossfadeDuration, ct);
        }

        public async UniTask PlayBGMAsync(string name, float fadeDuration = -1f, CancellationToken ct = default)
        {
            if (currentBGM == name) return;

            var clip = LoadAudioClip($"Audio/BGM/{name}");
            
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] BGM 없음: {name}");
                return;
            }

            // -1이면 기본 페이드 (3초)
            if (fadeDuration < 0) fadeDuration = defaultFadeDuration;

            // 기존 BGM 페이드 트윈 정리 (경합 방지)
            DOTween.Kill(bgmSource);

            currentBGM = name;
            bgmSource.loop = true;

            float targetVolume = 1f;

            if (fadeDuration > 0 && bgmSource.isPlaying)
            {
                // 크로스페이드: 현재 곡 페이드아웃 후 새 곡 페이드인
                targetVolume = bgmSource.volume;
                await bgmSource.DOFade(0f, fadeDuration).ToUniTask(cancellationToken: ct);
                bgmSource.clip = clip;
                bgmSource.Play();
                await bgmSource.DOFade(targetVolume, fadeDuration).ToUniTask(cancellationToken: ct);
            }
            else if (fadeDuration > 0)
            {
                // 새로 시작: 페이드인만
                bgmSource.volume = 0f;
                bgmSource.clip = clip;
                bgmSource.Play();
                await bgmSource.DOFade(targetVolume, fadeDuration).ToUniTask(cancellationToken: ct);
            }
            else
            {
                bgmSource.clip = clip;
                bgmSource.Play();
            }
        }

        public async UniTask StopBGMAsync(float fadeDuration = -1f, CancellationToken ct = default)
        {
            if (!bgmSource.isPlaying) return;

            // -1이면 기본 페이드 (3초)
            if (fadeDuration < 0) fadeDuration = defaultFadeDuration;

            // 기존 페이드 트윈 정리
            DOTween.Kill(bgmSource);

            currentBGM = null;
            float originalVolume = bgmSource.volume;

            if (fadeDuration > 0)
            {
                await bgmSource.DOFade(0f, fadeDuration).ToUniTask(cancellationToken: ct);
            }

            bgmSource.Stop();
            bgmSource.volume = originalVolume;
        }

        public void StopBGMImmediate()
        {
            DOTween.Kill(bgmSource);
            bgmSource.Stop();
            bgmSource.volume = 1f;
            currentBGM = null;
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

            currentBGM = clip.name;
            bgmSource.loop = true;

            float targetVolume = 1f;

            if (fadeDuration > 0 && bgmSource.isPlaying)
            {
                targetVolume = bgmSource.volume;
                await bgmSource.DOFade(0f, fadeDuration).ToUniTask(cancellationToken: ct);
                bgmSource.clip = clip;
                bgmSource.Play();
                await bgmSource.DOFade(targetVolume, fadeDuration).ToUniTask(cancellationToken: ct);
            }
            else if (fadeDuration > 0)
            {
                // 페이드 인만
                bgmSource.volume = 0f;
                bgmSource.clip = clip;
                bgmSource.Play();
                await bgmSource.DOFade(targetVolume, fadeDuration).ToUniTask(cancellationToken: ct);
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
            // Resources.LoadAll로 SFX 폴더 전체 검색
            var allSFX = Resources.LoadAll<AudioClip>("Audio/SFX");
            foreach (var sfx in allSFX)
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
            var allSFX = Resources.LoadAll<AudioClip>("Audio/SFX");
            string lowerName = name.ToLower();
            foreach (var sfx in allSFX)
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
            voiceSource.Stop();
        }

        #endregion

        #region 캐릭터별 음성 볼륨

        /// <summary>
        /// PlayerPrefs에서 BGM/SFX 볼륨 복원 (게임 시작 시 호출)
        /// </summary>
        void LoadMixerVolumes()
        {
            SetMasterVolume(PlayerPrefs.GetFloat("MasterVolume", 0.8f));
            SetBGMVolume(PlayerPrefs.GetFloat("BGMVolume", 0.4f));
            SetSFXVolume(PlayerPrefs.GetFloat("SFXVolume", 0.4f));
        }

        void LoadCharacterVoiceVolumes()
        {
            string[] characters = { "Yeun", "Daeun", "Bom", "Heewon", "Roa" };
            foreach (var c in characters)
            {
                characterVoiceVolumes[c] = PlayerPrefs.GetFloat($"Voice_{c}", 0.4f);
            }
        }

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

        AudioClip LoadAudioClip(string path)
        {
            return Resources.Load<AudioClip>(path);
        }
    }
}
