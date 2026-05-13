// LEGACY — 마이그레이션 1회용. CharacterDatabaseMigrator가 구 .asset을 읽기 위해 유지.
// 마이그레이션 완료 후 이 파일과 .meta, Resources/Data/CharacterDatabase.asset 삭제.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    [Obsolete("Split into CharacterMetaDatabase + CharacterStageDatabase. Run Tools > LoveAlgo > Migrate > Split CharacterDatabase.")]
    public class CharacterDatabase : ScriptableObject
    {
        public List<CharacterEntry> characters = new();
        public List<LegacyEmoteAlias> emoteAliases = new();
    }

    [Serializable]
    public class CharacterEntry
    {
        public string characterId = "";
        public string displayName = "";
        public List<string> speakerAliases = new();
        public float spriteScale = 1f;
        public float offsetX = 0f;
        public float offsetY = 0f;
        public float pivotY = 0f;
        public string overlayPrefix = "";
        public List<string> positiveEmotes = new();
        public List<string> negativeEmotes = new();
    }

    [Serializable]
    public class LegacyEmoteAlias
    {
        public string alias;
        public string emoteName;
    }
}
