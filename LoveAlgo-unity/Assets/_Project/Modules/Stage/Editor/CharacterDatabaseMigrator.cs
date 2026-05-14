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
        const string OLD_PATH    = "Assets/Resources/Data/CharacterDatabase.asset";
        const string META_PATH   = "Assets/Resources/Data/CharacterMetaDatabase.asset";
        const string STAGE_PATH  = "Assets/Resources/Data/CharacterStageDatabase.asset";
        const string OVERLAY_PATH = "Assets/Resources/Data/VirtualOverlayDatabase.asset";

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

        // 영문 emote alias → 숫자 정전 (`_NN`) — ASSET_NAMING.md §3 감정 ID 매핑 기반
        static readonly System.Collections.Generic.Dictionary<string, string> EmoteEnToNumeric = new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Default",     "_00" },
            { "EyeSmile",    "_11" },
            { "BrightSmile", "_12" },
            { "Activate",    "_13" },  // 활짝
            { "Happy",       "_14" },
            { "Glare",       "_21" },
            { "Pout",        "_22" },  // 삐짐
            { "Tearful",     "_31" },  // 울먹
            { "Shy",         "_34" },  // 부끄러워
            { "Surprise",    "_41" },  // 깜짝
            { "Surprised",   "_41" },
        };
        static string RemapEmote(string s) => EmoteEnToNumeric.TryGetValue(s ?? "", out var n) ? n : s;

        [MenuItem("Tools/LoveAlgo/Migrate/Split CharacterDatabase")]
        public static void Run()
        {
#pragma warning disable CS0618 // 마이그레이션 1회용 — 의도된 Obsolete 사용
            var old = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(OLD_PATH);
#pragma warning restore CS0618
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
            // 구 emoteAliases는 EmoteMap.asset(런타임 SO)으로 마이그레이션
            var emoteMap = AssetDatabase.LoadAssetAtPath<LoveAlgo.Story.EmoteMap>("Assets/Resources/Data/EmoteMap.asset")
                          ?? AssetDatabase.LoadAssetAtPath<LoveAlgo.Story.EmoteMap>("Assets/_Project/Modules/Narrative/Editor/Mappings/EmoteMap.asset");
            if (emoteMap != null)
            {
                emoteMap.entries = old.emoteAliases
                    .Select(a => new LoveAlgo.Story.EmoteMap.Entry { ko = a.alias, id = a.emoteName })
                    .ToList();
                EditorUtility.SetDirty(emoteMap);
            }

            // Stage SO (트랜스폼만)
            var stage = ScriptableObject.CreateInstance<CharacterStageDatabase>();
            stage.entries = old.characters.Select(c => new CharacterStageEntry
            {
                characterId = Remap(c.characterId),
                spriteScale = c.spriteScale,
                offsetX = c.offsetX,
                offsetY = c.offsetY,
                pivotY = c.pivotY,
            }).ToList();

            // VirtualOverlay SO (overlay 보유 캐릭터만)
            var overlayDb = ScriptableObject.CreateInstance<VirtualOverlayDatabase>();
            overlayDb.entries = old.characters
                .Where(c => !string.IsNullOrEmpty(c.overlayPrefix))
                .Select(c =>
                {
                    string prefix = c.overlayPrefix ?? "";
                    string defaultMode = "";
                    var modes = new System.Collections.Generic.List<string>();
                    // 구 overlayPrefix가 "Roa_Mob" 같은 형태면 모드 분리
                    if (prefix.Contains('_'))
                    {
                        var idx = prefix.LastIndexOf('_');
                        var maybeMode = prefix.Substring(idx + 1);
                        if (maybeMode.Length <= 4)
                        {
                            prefix = prefix.Substring(0, idx);
                            defaultMode = maybeMode;
                            modes.Add(maybeMode);
                            if (maybeMode == "Mob") modes.Add("PC");
                            else if (maybeMode == "PC") modes.Add("Mob");
                        }
                    }
                    return new VirtualOverlayEntry
                    {
                        characterId = Remap(c.characterId),
                        overlayPrefix = prefix,
                        overlayModes = modes,
                        defaultOverlayMode = defaultMode,
                        positiveEmotes = c.positiveEmotes != null ? c.positiveEmotes.Select(RemapEmote).ToList() : new(),
                        negativeEmotes = c.negativeEmotes != null ? c.negativeEmotes.Select(RemapEmote).ToList() : new(),
                    };
                })
                .ToList();

            // 저장
            if (AssetDatabase.LoadAssetAtPath<Object>(META_PATH) != null) AssetDatabase.DeleteAsset(META_PATH);
            if (AssetDatabase.LoadAssetAtPath<Object>(STAGE_PATH) != null) AssetDatabase.DeleteAsset(STAGE_PATH);
            if (AssetDatabase.LoadAssetAtPath<Object>(OVERLAY_PATH) != null) AssetDatabase.DeleteAsset(OVERLAY_PATH);
            AssetDatabase.CreateAsset(meta, META_PATH);
            AssetDatabase.CreateAsset(stage, STAGE_PATH);
            AssetDatabase.CreateAsset(overlayDb, OVERLAY_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterDatabaseMigrator] Meta {meta.characters.Count} chars, Stage {stage.entries.Count} entries, Overlay {overlayDb.entries.Count} entries 생성 완료.");
            foreach (var e in overlayDb.entries)
                Debug.Log($"  Overlay {e.characterId}: prefix='{e.overlayPrefix}', modes=[{string.Join(",", e.overlayModes)}], default='{e.defaultOverlayMode}'");
        }
    }
}
#endif
