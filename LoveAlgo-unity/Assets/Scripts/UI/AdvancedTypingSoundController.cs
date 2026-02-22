using UnityEngine;
using System.Collections.Generic;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 향상된 타이핑 사운드 컨트롤러
    /// - 글자별 다양한 사운드 (모음/자음/문장부호)
    /// - 캐릭터별 타이핑 톤 변화
    /// - 리듬감 있는 타이핑 (연속된 글자는 약간 빠르게)
    /// </summary>
    public class AdvancedTypingSoundController : MonoBehaviour
    {
        [Header("Sound Variations")]
        [SerializeField] AudioClip[] consonantSounds;   // 자음 사운드 (짧고 강함)
        [SerializeField] AudioClip[] vowelSounds;       // 모음 사운드 (부드럽고 긴)
        [SerializeField] AudioClip[] punctuationSounds; // 문장부호 (딱딱한 클릭음)
        [SerializeField] AudioClip[] spaceSounds;       // 공백 (짧은 무음 또는 미세한 사운드)

        [Header("Character Voice Profiles")]
        [SerializeField] List<CharacterVoiceProfile> characterProfiles;

        [Header("Audio Settings")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] float basePitch = 1.0f;
        [SerializeField] float pitchVariation = 0.1f;  // ±10%
        [SerializeField] float baseVolume = 0.5f;
        [SerializeField] float rhythmSpeedup = 1.2f;   // 연속 타이핑 시 속도 증가

        string currentCharacter;
        int consecutiveChars;  // 연속 타이핑 카운터
        float lastTypingTime;
        Dictionary<string, CharacterVoiceProfile> profileMap;

        void Awake()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            // 프로필 맵 생성
            profileMap = new Dictionary<string, CharacterVoiceProfile>();
            if (characterProfiles != null)
            {
                foreach (var profile in characterProfiles)
                {
                    if (!string.IsNullOrEmpty(profile.characterId))
                    {
                        profileMap[profile.characterId] = profile;
                    }
                }
            }
        }

        /// <summary>
        /// 현재 화자 설정
        /// </summary>
        public void SetCurrentCharacter(string characterId)
        {
            if (currentCharacter != characterId)
            {
                currentCharacter = characterId;
                consecutiveChars = 0;
            }
        }

        /// <summary>
        /// 글자 타이핑 사운드 재생
        /// </summary>
        public void PlayTypingSound(char character)
        {
            // 공백/줄바꿈은 스킵 (또는 특수 사운드)
            if (char.IsWhiteSpace(character))
            {
                if (spaceSounds != null && spaceSounds.Length > 0)
                {
                    PlaySound(spaceSounds, 0.3f);
                }
                consecutiveChars = 0;
                return;
            }

            // 문장부호
            if (IsPunctuation(character))
            {
                PlaySound(punctuationSounds, 0.8f);
                consecutiveChars = 0;
                return;
            }

            // 한글 분리 (자음/모음 구분)
            if (IsKorean(character))
            {
                PlayKoreanSound(character);
            }
            else
            {
                // 영문 등: 자음/모음 간단 판별
                if (IsVowel(character))
                {
                    PlaySound(vowelSounds, 0.6f);
                }
                else
                {
                    PlaySound(consonantSounds, 0.7f);
                }
            }

            // 연속 타이핑 리듬
            UpdateRhythm();
        }

        /// <summary>
        /// 한글 자음/모음 판별 및 사운드 재생
        /// </summary>
        void PlayKoreanSound(char character)
        {
            // 한글 유니코드 분해 (초성/중성/종성)
            int unicode = character - 0xAC00;
            if (unicode < 0 || unicode > 11171) return;

            int choIndex = unicode / (21 * 28);      // 초성 (자음)
            int jungIndex = (unicode % (21 * 28)) / 28; // 중성 (모음)
            int jongIndex = unicode % 28;            // 종성 (자음)

            // 초성 있으면 자음 사운드, 중성만 있으면 모음 사운드
            if (choIndex > 0)
            {
                PlaySound(consonantSounds, 0.7f);
            }
            else if (jungIndex > 0)
            {
                PlaySound(vowelSounds, 0.6f);
            }
            else
            {
                PlaySound(consonantSounds, 0.5f);
            }
        }

        /// <summary>
        /// 사운드 재생 (캐릭터 프로필 반영)
        /// </summary>
        void PlaySound(AudioClip[] clips, float volumeMultiplier)
        {
            if (clips == null || clips.Length == 0) return;
            if (audioSource == null) return;

            // 랜덤 클립 선택
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            // 피치/볼륨 계산
            float pitch = basePitch;
            float volume = baseVolume * volumeMultiplier;

            // 캐릭터 프로필 적용
            if (!string.IsNullOrEmpty(currentCharacter) && profileMap.TryGetValue(currentCharacter, out var profile))
            {
                pitch *= profile.pitchMultiplier;
                volume *= profile.volumeMultiplier;
            }

            // 피치 변동
            pitch += Random.Range(-pitchVariation, pitchVariation);

            // 연속 타이핑 리듬 (빨라짐)
            if (consecutiveChars > 3)
            {
                pitch *= rhythmSpeedup;
            }

            // 재생
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// 연속 타이핑 리듬 업데이트
        /// </summary>
        void UpdateRhythm()
        {
            float currentTime = Time.time;
            if (currentTime - lastTypingTime < 0.1f)
            {
                consecutiveChars++;
            }
            else
            {
                consecutiveChars = 0;
            }
            lastTypingTime = currentTime;
        }

        /// <summary>
        /// 문장부호 판별
        /// </summary>
        bool IsPunctuation(char c)
        {
            return c == '.' || c == ',' || c == '!' || c == '?' || c == '~' || c == ';' || c == ':' || c == '…';
        }

        /// <summary>
        /// 한글 판별
        /// </summary>
        bool IsKorean(char c)
        {
            return (c >= 0xAC00 && c <= 0xD7A3);  // 한글 완성형
        }

        /// <summary>
        /// 영문 모음 판별
        /// </summary>
        bool IsVowel(char c)
        {
            c = char.ToLower(c);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }
    }

    /// <summary>
    /// 캐릭터별 음성 프로필
    /// </summary>
    [System.Serializable]
    public class CharacterVoiceProfile
    {
        public string characterId;          // 캐릭터 ID
        public float pitchMultiplier = 1.0f;   // 피치 배율 (높은 목소리 = 1.2, 낮은 목소리 = 0.8)
        public float volumeMultiplier = 1.0f;  // 볼륨 배율
        [TextArea]
        public string description;          // 설명 (에디터용)
    }
}
