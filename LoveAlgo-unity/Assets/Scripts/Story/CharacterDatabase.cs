using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 캐릭터 데이터베이스 - 전체 캐릭터 컬렉션
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "LoveAlgo/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        [Header("캐릭터 목록")]
        public List<CharacterData> characters = new();

        [Header("매핑 설정")]
        [Tooltip("Speaker 이름 → CharacterID 매핑 (한글 이름 → 영문 ID)")]
        public List<SpeakerMapping> speakerMappings = new();

        /// <summary>
        /// CharacterID로 캐릭터 데이터 가져오기
        /// </summary>
        public CharacterData GetCharacterById(string characterId)
        {
            return characters.Find(c => 
                c.characterId.Equals(characterId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 표시 이름으로 캐릭터 데이터 가져오기
        /// </summary>
        public CharacterData GetCharacterByDisplayName(string displayName)
        {
            return characters.Find(c => 
                c.displayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Speaker 이름으로 CharacterID 변환
        /// </summary>
        public string SpeakerToCharacterId(string speaker)
        {
            // 매핑 테이블에서 찾기
            var mapping = speakerMappings.Find(m => 
                m.speakerName.Equals(speaker, StringComparison.OrdinalIgnoreCase));
            
            if (mapping != null)
                return mapping.characterId;

            // 캐릭터 displayName에서 찾기
            var character = GetCharacterByDisplayName(speaker);
            if (character != null)
                return character.characterId;

            return null;
        }

        /// <summary>
        /// CharacterID로 표시 이름 변환
        /// </summary>
        public string CharacterIdToDisplayName(string characterId)
        {
            var character = GetCharacterById(characterId);
            return character?.displayName ?? characterId;
        }

        /// <summary>
        /// 모든 캐릭터 ID 목록
        /// </summary>
        public List<string> GetAllCharacterIds()
        {
            var ids = new List<string>();
            foreach (var c in characters)
            {
                if (!string.IsNullOrEmpty(c.characterId))
                    ids.Add(c.characterId);
            }
            return ids;
        }

        /// <summary>
        /// 모든 표시 이름 목록
        /// </summary>
        public List<string> GetAllDisplayNames()
        {
            var names = new List<string>();
            foreach (var c in characters)
            {
                if (!string.IsNullOrEmpty(c.displayName))
                    names.Add(c.displayName);
            }
            return names;
        }

        /// <summary>
        /// 캐릭터의 표정 스프라이트 가져오기 (Resources에서 동적 로드)
        /// </summary>
        public Sprite GetCharacterSprite(string characterId, string emoteName = "Default")
        {
            var character = GetCharacterById(characterId);
            return character?.LoadEmoteSprite(emoteName);
        }

        /// <summary>
        /// 캐릭터의 이름 색상 가져오기
        /// </summary>
        // 이름 색상 기능 삭제됨
    }

    /// <summary>
    /// Speaker → CharacterID 매핑
    /// </summary>
    [Serializable]
    public class SpeakerMapping
    {
        [Tooltip("CSV에서 사용하는 Speaker 이름")]
        public string speakerName;
        
        [Tooltip("대응하는 CharacterID")]
        public string characterId;
    }
}
