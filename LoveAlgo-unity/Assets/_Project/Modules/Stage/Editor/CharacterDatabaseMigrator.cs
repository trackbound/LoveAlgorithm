#if UNITY_EDITOR
using System.Linq;
using LoveAlgo.Story;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.StageEditor
{
    /// <summary>
    /// 구 CharacterDatabase.asset → 신규 CharacterMetaDatabase + CharacterStageDatabase 분리 마이그레이션.
    /// 1회 실행: Tools > LoveAlgo > Migrate > Split CharacterDatabase
    /// </summary>
    public static class CharacterDatabaseMigrator
    {
        const string OLD_PATH  = "Assets/Resources/Data/CharacterDatabase.asset";
        const string META_PATH = "Assets/Resources/Data/CharacterMetaDatabase.asset";
        const string STAGE_PATH = "Assets/Resources/Data/CharacterStageDatabase.asset";

        // ASSET_NAMING.md §3 — 캐릭터 ID 정전 매핑 (구 ID → c0N)
        static readonly System.Collections.Generic.Dictionary<string, string> IdRemap = new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Roa",      "c01" },
            { "SeoDaEun", "c02" },
            { "HaYeEun",  "c03" },
            { "DoHeewon", "c04" },
            { "LeeBom",   "c05" },
        };
        static string Remap(string oldId) => IdRemap.TryGetValue(oldId ?? "", out var n) ? n : oldId;

        [MenuItem("Tools/LoveAlgo/Migrate/Split CharacterDatabase")]
        public static void Run()
        {
            var old = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(OLD_PATH);
            if (old == null)
            {
                EditorUtility.DisplayDialog("Migration", $"원본 없음: {OLD_PATH}", "OK");
                return;
            }

            // Meta SO
            var meta = ScriptableObject.CreateInstance<CharacterMetaDatabase>();
            meta.characters = old.characters
                .Select(c => new CharacterMeta
                {
                    characterId = Remap(c.characterId),
                    displayName = c.displayName,
                    speakerAliases = c.speakerAliases != null ? new System.Collections.Generic.List<string>(c.speakerAliases) : new(),
                })
                .ToList();
            meta.emoteAliases = old.emoteAliases
                .Select(a => new LoveAlgo.Story.EmoteAlias { alias = a.alias, emoteName = a.emoteName })
                .ToList();

            // Stage SO
            var stage = ScriptableObject.CreateInstance<CharacterStageDatabase>();
            stage.entries = old.characters.Select(c =>
            {
                // 구 overlayPrefix가 "Roa_Mob" 같은 형태면 모드 분리 시도
                string prefix = c.overlayPrefix ?? "";
                string defaultMode = "";
                var modes = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(prefix) && prefix.Contains('_'))
                {
                    var idx = prefix.LastIndexOf('_');
                    var maybeMode = prefix.Substring(idx + 1);
                    // "Mob"/"PC" 같은 짧은 토큰이면 모드로 추출
                    if (maybeMode.Length <= 4)
                    {
                        prefix = prefix.Substring(0, idx);
                        defaultMode = maybeMode;
                        modes.Add(maybeMode);
                        // 다른 모드도 자동 추가 (Mob/PC 양립 기획)
                        if (maybeMode == "Mob") modes.Add("PC");
                        else if (maybeMode == "PC") modes.Add("Mob");
                    }
                }

                return new CharacterStageEntry
                {
                    characterId = Remap(c.characterId),
                    spriteScale = c.spriteScale,
                    offsetX = c.offsetX,
                    offsetY = c.offsetY,
                    pivotY = c.pivotY,
                    overlayPrefix = prefix,
                    overlayModes = modes,
                    defaultOverlayMode = defaultMode,
                    positiveEmotes = c.positiveEmotes != null ? new System.Collections.Generic.List<string>(c.positiveEmotes) : new(),
                    negativeEmotes = c.negativeEmotes != null ? new System.Collections.Generic.List<string>(c.negativeEmotes) : new(),
                };
            }).ToList();

            // 저장
            if (AssetDatabase.LoadAssetAtPath<Object>(META_PATH) != null) AssetDatabase.DeleteAsset(META_PATH);
            if (AssetDatabase.LoadAssetAtPath<Object>(STAGE_PATH) != null) AssetDatabase.DeleteAsset(STAGE_PATH);
            AssetDatabase.CreateAsset(meta, META_PATH);
            AssetDatabase.CreateAsset(stage, STAGE_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterDatabaseMigrator] Meta {meta.characters.Count} chars, Stage {stage.entries.Count} entries 생성 완료.");
            foreach (var e in stage.entries)
                Debug.Log($"  - {e.characterId}: overlayPrefix='{e.overlayPrefix}', modes=[{string.Join(",", e.overlayModes)}], default='{e.defaultOverlayMode}'");
        }
    }
}
#endif
