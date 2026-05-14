#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using LoveAlgo.NarrativeEditor.Mappings;
using LoveAlgo.Story;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// xlsx 4종(Character_Emotion / SD / BG / CG) → 매핑 SO 일괄 import.
    /// 메뉴: Tools > LoveAlgo > Story > Import Mappings from xlsx
    /// </summary>
    public static class StoryMappingImporter
    {
        const string DATA_DIR = "Assets/_Project/Modules/Narrative/Data";
        const string MAP_DIR  = "Assets/_Project/Modules/Narrative/Editor/Mappings";
        const string MAP_RUNTIME_DIR = "Assets/Resources/Data";  // 런타임 노출이 필요한 SO 전용 (EmoteMap)

        const string XLSX_EMOTE = DATA_DIR + "/Character_Emotion_List.xlsx";
        const string XLSX_SD    = DATA_DIR + "/SD_List.xlsx";
        const string XLSX_BG    = DATA_DIR + "/BG_List.xlsx";
        const string XLSX_CG    = DATA_DIR + "/CG_List.xlsx";

        [MenuItem("Tools/LoveAlgo/Story/Import Mappings from xlsx")]
        public static void ImportAll()
        {
            EnsureDir(MAP_DIR);
            int totalChar = 0, totalEmote = 0, totalBg = 0, totalCg = 0, totalSd = 0;

            if (File.Exists(XLSX_EMOTE))
            {
                ImportCharacterAndEmote(XLSX_EMOTE, out totalChar, out totalEmote);
            }
            else Debug.LogWarning($"[StoryMappingImporter] xlsx 없음: {XLSX_EMOTE}");

            if (File.Exists(XLSX_BG)) totalBg = ImportBg(XLSX_BG);
            else Debug.LogWarning($"[StoryMappingImporter] xlsx 없음: {XLSX_BG}");

            if (File.Exists(XLSX_CG)) totalCg = ImportCg(XLSX_CG);
            else Debug.LogWarning($"[StoryMappingImporter] xlsx 없음: {XLSX_CG}");

            if (File.Exists(XLSX_SD)) totalSd = ImportSd(XLSX_SD);
            else Debug.LogWarning($"[StoryMappingImporter] xlsx 없음: {XLSX_SD}");

            EnsureSoundMapAsset();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[StoryMappingImporter] 완료 — Character {totalChar}, Emote {totalEmote}, BG {totalBg}, CG {totalCg}, SD {totalSd}");
        }

        // ─── Character + Emote ────────────────────────────
        static void ImportCharacterAndEmote(string path, out int charCount, out int emoteCount)
        {
            var sheets = MiniXlsx.Read(path);
            EnsureDir(MAP_RUNTIME_DIR);
            // CharacterMetaDatabase가 정전 (Single Source) — Resources/Data/에서 로드 또는 생성
            var meta = LoadOrCreate<CharacterMetaDatabase>($"{MAP_RUNTIME_DIR}/CharacterMetaDatabase.asset");
            var emoteMap = LoadOrCreate<EmoteMap>($"{MAP_RUNTIME_DIR}/EmoteMap.asset");

            // 기존 speakerAliases 보존
            var existingAliases = new Dictionary<string, List<string>>();
            foreach (var c in meta.characters)
                if (!string.IsNullOrEmpty(c.characterId))
                    existingAliases[c.characterId] = c.speakerAliases != null ? new List<string>(c.speakerAliases) : new();

            var charEntries = new List<CharacterMeta>();
            var emoteEntries = new Dictionary<string, EmoteMap.Entry>();

            foreach (var kv in sheets)
            {
                var sheet = kv.Value;
                if (sheet.Rows.Count < 2) continue;
                string code = Cell(sheet.Rows[1], 0);   // c01
                string ko = Cell(sheet.Rows[1], 1);      // 로아
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(ko))
                {
                    existingAliases.TryGetValue(code, out var aliases);
                    charEntries.Add(new CharacterMeta
                    {
                        characterId = code,
                        displayName = ko,
                        speakerAliases = aliases ?? new List<string>(),
                    });
                }

                int headerRow = FindHeaderRow(sheet.Rows, "감정", "감정 ID");
                if (headerRow < 0) continue;
                for (int r = headerRow + 1; r < sheet.Rows.Count; r++)
                {
                    string emoKo = Cell(sheet.Rows[r], 1);
                    string emoId = Cell(sheet.Rows[r], 2);
                    if (string.IsNullOrEmpty(emoKo) || string.IsNullOrEmpty(emoId)) continue;
                    if (!emoteEntries.ContainsKey(emoKo))
                        emoteEntries[emoKo] = new EmoteMap.Entry { ko = emoKo, id = emoId };
                }
            }

            meta.characters = charEntries;
            emoteMap.entries = new List<EmoteMap.Entry>(emoteEntries.Values);
            EditorUtility.SetDirty(meta);
            EditorUtility.SetDirty(emoteMap);
            charCount = charEntries.Count;
            emoteCount = emoteMap.entries.Count;
        }

        // ─── BG ───────────────────────────────────────────
        // 정전(ASSET_NAMING.md §2): 리소스명 = `bg_{장소ID}_{장소키}_{인덱스}`
        // xlsx의 "리소스명" 컬럼이 정전을 따른다고 가정. (xlsx 자체가 source of truth)
        static int ImportBg(string path)
        {
            var sheets = MiniXlsx.Read(path);
            var bgMap = LoadOrCreate<BgMap>($"{MAP_DIR}/BgMap.asset");

            var entries = new List<BgMap.Entry>();
            foreach (var kv in sheets)
            {
                string category = kv.Key;
                var sheet = kv.Value;
                int headerRow = FindHeaderRow(sheet.Rows, "리소스명", "설명");
                if (headerRow < 0) continue;

                for (int r = headerRow + 1; r < sheet.Rows.Count; r++)
                {
                    var row = sheet.Rows[r];
                    string engineId = Cell(row, 2);  // bg_10_room_06 (정전 패턴)
                    string ko = Cell(row, 3);
                    string filename = Cell(row, 4);
                    if (string.IsNullOrEmpty(engineId)) continue;

                    entries.Add(new BgMap.Entry
                    {
                        category = category,
                        ko = ko,
                        semanticId = engineId,
                        legacyCode = "",
                        filename = filename,
                    });
                }
            }
            bgMap.entries = entries;
            EditorUtility.SetDirty(bgMap);
            return entries.Count;
        }

        // ─── CG ───────────────────────────────────────────
        static int ImportCg(string path)
        {
            var sheets = MiniXlsx.Read(path);
            var map = LoadOrCreate<CgMap>($"{MAP_DIR}/CgMap.asset");
            var entries = new List<CgMap.Entry>();
            foreach (var kv in sheets)
            {
                var sheet = kv.Value;
                int headerRow = FindHeaderRow(sheet.Rows, "리소스명", "설명");
                if (headerRow < 0) continue;
                for (int r = headerRow + 1; r < sheet.Rows.Count; r++)
                {
                    var row = sheet.Rows[r];
                    // No.(0) / 리소스명(1) / 설명(2) / 작업(3) / 파일(4)
                    string engineId = Cell(row, 1);
                    string ko = Cell(row, 2);
                    string filename = Cell(row, 4);
                    if (string.IsNullOrEmpty(engineId)) continue;
                    entries.Add(new CgMap.Entry { ko = ko, engineId = engineId, filename = filename });
                }
            }
            map.entries = entries;
            EditorUtility.SetDirty(map);
            return entries.Count;
        }

        // ─── SD ───────────────────────────────────────────
        static int ImportSd(string path)
        {
            var sheets = MiniXlsx.Read(path);
            var map = LoadOrCreate<SdMap>($"{MAP_DIR}/SdMap.asset");
            var entries = new List<SdMap.Entry>();
            foreach (var kv in sheets)
            {
                var sheet = kv.Value;
                int headerRow = FindHeaderRow(sheet.Rows, "리소스명", "설명");
                if (headerRow < 0) continue;
                for (int r = headerRow + 1; r < sheet.Rows.Count; r++)
                {
                    var row = sheet.Rows[r];
                    string engineId = Cell(row, 1);
                    string ko = Cell(row, 2);
                    string filename = Cell(row, 4);
                    if (string.IsNullOrEmpty(engineId)) continue;
                    entries.Add(new SdMap.Entry { ko = ko, engineId = engineId, filename = filename });
                }
            }
            map.entries = entries;
            EditorUtility.SetDirty(map);
            return entries.Count;
        }

        // ─── Sound (xlsx 없음 — 빈 SO만 보장) ─────────────
        static void EnsureSoundMapAsset() => LoadOrCreate<SoundMap>($"{MAP_DIR}/SoundMap.asset");

        // ─── 유틸 ─────────────────────────────────────────
        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var so = AssetDatabase.LoadAssetAtPath<T>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(so, path);
            }
            return so;
        }

        static string Cell(List<string> row, int col) =>
            row != null && col >= 0 && col < row.Count ? (row[col] ?? "").Trim() : "";

        /// <summary>지정 키워드 2개를 모두 포함하는 첫 행 인덱스 반환.</summary>
        static int FindHeaderRow(List<List<string>> rows, params string[] keywords)
        {
            for (int r = 0; r < rows.Count; r++)
            {
                int found = 0;
                foreach (var c in rows[r])
                {
                    foreach (var k in keywords) if (c == k) found++;
                }
                if (found >= keywords.Length) return r;
            }
            return -1;
        }

        static void EnsureDir(string assetPath)
        {
            var sys = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(sys)) Directory.CreateDirectory(sys);
            AssetDatabase.Refresh();
        }
    }
}
#endif
