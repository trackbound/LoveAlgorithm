using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 캐릭터 데이터베이스 - 전체 캐릭터 + 매핑 통합 관리
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "LoveAlgo/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        static CharacterDatabase _instance;

        /// <summary>Resources/Data/CharacterDatabase 자동 로드 싱글톤</summary>
        public static CharacterDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
                    if (_instance == null)
                        Debug.LogError("[CharacterDatabase] Resources/Data/CharacterDatabase.asset 을 찾을 수 없습니다");
                }
                return _instance;
            }
        }

        [Header("캐릭터 목록")]
        public List<CharacterEntry> characters = new();

        [Header("표정 별칭 (한글 → 영문)")]
        [Tooltip("한글 표정명을 영문 파일명으로 변환. 아트팀이 새 표정 추가 시 여기에 항목 추가")]
        public List<EmoteAlias> emoteAliases = new();

        Dictionary<string, string> _emoteAliasCache;

        /// <summary>
        /// 표정 이름 변환 (한글 → 영문). 매칭 없으면 원본 그대로 반환.
        /// </summary>
        public string ResolveEmoteName(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            if (_emoteAliasCache == null)
            {
                _emoteAliasCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var alias in emoteAliases)
                {
                    if (!string.IsNullOrEmpty(alias.alias) && !string.IsNullOrEmpty(alias.emoteName))
                        _emoteAliasCache[alias.alias] = alias.emoteName;
                }
            }

            return _emoteAliasCache.TryGetValue(input, out var resolved) ? resolved : input;
        }

        /// <summary>CharacterID로 캐릭터 데이터 가져오기</summary>
        public CharacterEntry GetCharacterById(string characterId)
        {
            return characters.Find(c =>
                c.characterId.Equals(characterId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>표시 이름으로 캐릭터 데이터 가져오기</summary>
        public CharacterEntry GetCharacterByDisplayName(string displayName)
        {
            return characters.Find(c =>
                c.displayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Speaker 이름으로 CharacterID 변환 (displayName + speakerAliases 조회)</summary>
        public string SpeakerToCharacterId(string speaker)
        {
            foreach (var c in characters)
            {
                if (c.displayName.Equals(speaker, StringComparison.OrdinalIgnoreCase))
                    return c.characterId;

                if (c.speakerAliases != null)
                {
                    foreach (var alias in c.speakerAliases)
                    {
                        if (alias.Equals(speaker, StringComparison.OrdinalIgnoreCase))
                            return c.characterId;
                    }
                }
            }

            return null;
        }

        /// <summary>CharacterID로 표시 이름 변환</summary>
        public string CharacterIdToDisplayName(string characterId)
        {
            var character = GetCharacterById(characterId);
            return character?.displayName ?? characterId;
        }

        /// <summary>캐릭터의 표정 스프라이트 가져오기</summary>
        public Sprite GetCharacterSprite(string characterId, string emoteName = "Default")
        {
            var character = GetCharacterById(characterId);
            if (character == null) return null;

            emoteName = ResolveEmoteName(emoteName);

            string path = $"Characters/Char_{characterId}_{emoteName}";
            var sprite = Resources.Load<Sprite>(path);

            if (sprite == null && emoteName != EmoteNames.Default)
            {
                path = $"Characters/Char_{characterId}_{EmoteNames.Default}";
                sprite = Resources.Load<Sprite>(path);
            }

            return sprite;
        }
    }

    /// <summary>
    /// 캐릭터 항목 — CharacterDatabase 내 인라인 데이터
    /// </summary>
    [Serializable]
    public class CharacterEntry
    {
        [Tooltip("캐릭터 고유 ID (영문)")]
        public string characterId = "";

        [Tooltip("표시 이름 (한글, UI)")]
        public string displayName = "";

        [Tooltip("CSV Speaker 별칭 (닉네임 등 displayName 외 추가 이름)")]
        public List<string> speakerAliases = new();

        [Header("스프라이트 트랜스폼")]
        [Tooltip("스케일 배율 (1 = 기본)")]
        public float spriteScale = 1f;

        [Tooltip("X 오프셋")]
        public float offsetX = 0f;

        [Tooltip("Y 오프셋")]
        public float offsetY = 0f;

        [Tooltip("피벗 Y (0=하단, 0.5=중앙, 1=상단)")]
        [Range(0f, 1f)]
        public float pivotY = 0f;

        [Header("오버레이 (가상 캐릭터)")]
        [Tooltip("오버레이 프리픽스 (예: Roa_Mob). 비어있으면 오버레이 미사용")]
        public string overlayPrefix = "";

        [Tooltip("긍정 무드 표정 목록 (예: BrightSmile, EyeSmile, Happy)")]
        public List<string> positiveEmotes = new();

        [Tooltip("부정 무드 표정 목록 (예: Glare, Tearful)")]
        public List<string> negativeEmotes = new();

        /// <summary>오버레이 사용 캐릭터인지 여부</summary>
        public bool UseOverlay => !string.IsNullOrEmpty(overlayPrefix);

        /// <summary>
        /// 표정에 맞는 오버레이 이름 반환 (예: "Roa_Mob_Positive").
        /// overlayPrefix가 비어있으면 null.
        /// </summary>
        public string GetOverlayName(string emote)
        {
            if (!UseOverlay) return null;

            if (!string.IsNullOrEmpty(emote))
            {
                foreach (var e in positiveEmotes)
                {
                    if (e.Equals(emote, StringComparison.OrdinalIgnoreCase))
                        return $"{overlayPrefix}_Positive";
                }
                foreach (var e in negativeEmotes)
                {
                    if (e.Equals(emote, StringComparison.OrdinalIgnoreCase))
                        return $"{overlayPrefix}_Negative";
                }
            }

            return $"{overlayPrefix}_Default";
        }

        public void GetTransform(out float scale, out float oX, out float oY, out float pY)
        {
            scale = spriteScale;
            oX = offsetX;
            oY = offsetY;
            pY = pivotY;
        }
    }

    [Serializable]
    public class EmoteAlias
    {
        [Tooltip("CSV/인라인 태그에서 사용하는 별칭 (예: 깜짝)")]
        public string alias;

        [Tooltip("실제 파일명 (예: Surprise)")]
        public string emoteName;
    }

    /// <summary>
    /// 공통 표정 이름 상수 (참고용 — 실제 매핑은 CharacterDatabase.emoteAliases)
    /// </summary>
    public static class EmoteNames
    {
        public const string Default = "Default";
        public const string Happy = "Happy";
        public const string Laugh = "Laugh";
        public const string Smile = "Smile";
        public const string Sad = "Sad";
        public const string Shy = "Shy";
        public const string Surprised = "Surprised";
    }
}
