using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 캐릭터 메타데이터 DB (정체성 — id/이름/발화자 alias/감정 alias).
    /// Narrative 모듈 소유. Stage 모듈의 시각 표현은 CharacterStageDatabase 분리.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterMetaDatabase", menuName = "LoveAlgo/Character Meta Database")]
    public class CharacterMetaDatabase : ScriptableObject
    {
        static CharacterMetaDatabase _instance;

        /// <summary>Resources/Data/CharacterMetaDatabase 자동 로드 싱글톤</summary>
        public static CharacterMetaDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CharacterMetaDatabase>("Data/CharacterMetaDatabase");
                    if (_instance == null)
                        Debug.LogError("[CharacterMetaDatabase] Resources/Data/CharacterMetaDatabase.asset 을 찾을 수 없습니다");
                }
                return _instance;
            }
        }

        [Header("캐릭터 목록")]
        public List<CharacterMeta> characters = new();

        [Header("표정 별칭 (한글 → 영문)")]
        [Tooltip("CSV 인라인 태그 등에서 한글 표정명을 엔진 표정 ID로 변환")]
        public List<EmoteAlias> emoteAliases = new();

        Dictionary<string, string> _emoteAliasCache;

        /// <summary>표정 alias 변환. 매칭 없으면 원본 그대로.</summary>
        public string ResolveEmoteName(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (_emoteAliasCache == null)
            {
                _emoteAliasCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in emoteAliases)
                    if (!string.IsNullOrEmpty(a.alias) && !string.IsNullOrEmpty(a.emoteName))
                        _emoteAliasCache[a.alias] = a.emoteName;
            }
            return _emoteAliasCache.TryGetValue(input, out var r) ? r : input;
        }

        public CharacterMeta GetById(string characterId) =>
            characters.Find(c => c.characterId.Equals(characterId, StringComparison.OrdinalIgnoreCase));

        public CharacterMeta GetByDisplayName(string displayName) =>
            characters.Find(c => c.displayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

        /// <summary>Speaker(한글) → characterId. displayName + speakerAliases 모두 조회.</summary>
        public string SpeakerToCharacterId(string speaker)
        {
            foreach (var c in characters)
            {
                if (c.displayName.Equals(speaker, StringComparison.OrdinalIgnoreCase))
                    return c.characterId;
                if (c.speakerAliases != null)
                    foreach (var alias in c.speakerAliases)
                        if (alias.Equals(speaker, StringComparison.OrdinalIgnoreCase))
                            return c.characterId;
            }
            return null;
        }

        public string CharacterIdToDisplayName(string characterId) =>
            GetById(characterId)?.displayName ?? characterId;
    }

    [Serializable]
    public class CharacterMeta
    {
        [Tooltip("캐릭터 고유 ID (예: Roa, c01)")]
        public string characterId = "";

        [Tooltip("표시 이름 (한글, UI)")]
        public string displayName = "";

        [Tooltip("CSV Speaker 별칭 (닉네임 등 displayName 외 추가 이름)")]
        public List<string> speakerAliases = new();
    }

    [Serializable]
    public class EmoteAlias
    {
        [Tooltip("CSV/인라인 태그에서 사용하는 별칭 (예: 깜짝)")]
        public string alias;

        [Tooltip("엔진 표정 ID (예: Surprise 또는 _41)")]
        public string emoteName;
    }
}
