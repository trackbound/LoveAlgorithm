using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// Resources 폴더를 스캔하여 리소스 경로 매핑 파일을 자동 생성
    /// BG, Character, CG, SD 레이어 + CSV 검증 지원
    /// </summary>
    public static class ResourceMappingGenerator
    {
        const string BgMappingPath = "Assets/Scripts/Data/BgPathMapping.cs";
        const string CharMappingPath = "Assets/Scripts/Data/CharacterEmoteMapping.cs";
        const string CgMappingPath = "Assets/Scripts/Data/CgPathMapping.cs";
        const string SdMappingPath = "Assets/Scripts/Data/SdPathMapping.cs";

        // ═══════════════════════════════════════════════════════════
        // 마스터 리소스 CSV (단일 진실 공급원)
        // ═══════════════════════════════════════════════════════════

        const string MasterCsvPath = "Assets/Resources/Data/master_resources.csv";

        /// <summary>마스터 CSV 한 행</summary>
        struct MasterEntry
        {
            public string Id;      // BG01, CG01, CH01, EM01 등
            public string Type;    // BG, CG, Char, Emote, BGM, SFX, Overlay
            public string KeyEn;   // 영어 이름 (Unity 리소스 이름)
            public string NameKr;  // 한글 이름 (아트팀/시나리오 이름)
            public string Alias;   // 영어 단축 별칭 (선택)
        }

        /// <summary>마스터 리소스 CSV 로딩 (# 주석, 빈 줄 무시)</summary>
        static List<MasterEntry> LoadMasterResources()
        {
            var entries = new List<MasterEntry>();
            if (!File.Exists(MasterCsvPath))
            {
                Debug.LogError($"[ResourceMappingGenerator] 마스터 CSV를 찾을 수 없음: {MasterCsvPath}");
                return entries;
            }

            var lines = File.ReadAllLines(MasterCsvPath);
            for (int i = 1; i < lines.Length; i++) // 헤더 스킵
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var cols = line.Split(',');
                if (cols.Length < 4) continue;

                entries.Add(new MasterEntry
                {
                    Id     = cols[0].Trim(),
                    Type   = cols[1].Trim(),
                    KeyEn  = cols[2].Trim(),
                    NameKr = cols[3].Trim(),
                    Alias  = cols.Length >= 5 ? cols[4].Trim() : ""
                });
            }
            Debug.Log($"[ResourceMappingGenerator] 마스터 CSV 로딩: {entries.Count}개 항목");
            return entries;
        }

        /// <summary>특정 타입의 마스터 항목 필터</summary>
        static List<MasterEntry> ByType(List<MasterEntry> all, string type)
            => all.Where(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

        // ─── BG 별칭 빌더 ──────────────────────────────────────

        /// <summary>BG ID 별칭 (BG01 → BG_MyRoom_Interior_Day)</summary>
        static Dictionary<string, string> BuildBgIdAliases(List<MasterEntry> all)
            => ByType(all, "BG").ToDictionary(e => e.Id, e => e.KeyEn, StringComparer.OrdinalIgnoreCase);

        /// <summary>BG 한글 별칭 (자취방_전경_낮 → BG_MyRoom_Interior_Day)</summary>
        static Dictionary<string, string> BuildBgKoreanAliases(List<MasterEntry> all)
            => ByType(all, "BG")
                .Where(e => !string.IsNullOrEmpty(e.NameKr))
                .ToDictionary(e => e.NameKr, e => e.KeyEn, StringComparer.OrdinalIgnoreCase);

        /// <summary>BG 영어 단축 별칭 (MyRoom → BG_MyRoom_Interior_Day)</summary>
        static Dictionary<string, string> BuildBgShortAliases(List<MasterEntry> all)
            => ByType(all, "BG")
                .Where(e => !string.IsNullOrEmpty(e.Alias))
                .ToDictionary(e => e.Alias, e => e.KeyEn, StringComparer.OrdinalIgnoreCase);

        // ─── Emote 별칭 빌더 ───────────────────────────────────

        /// <summary>표정 파일명 → 코드명 (01_Default → Default). EM ID에서 번호 추출</summary>
        static Dictionary<string, string> BuildEmoteFileToCode(List<MasterEntry> all)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in ByType(all, "Emote"))
            {
                string num = e.Id.Replace("EM", "");          // "EM01" → "01"
                result[$"{num}_{e.KeyEn}"] = e.KeyEn;         // "01_Default" → "Default"
            }
            return result;
        }

        /// <summary>표정 한글 → 코드명 (기본 → Default)</summary>
        static Dictionary<string, string> BuildEmoteKoreanToCode(List<MasterEntry> all)
            => ByType(all, "Emote")
                .Where(e => !string.IsNullOrEmpty(e.NameKr))
                .ToDictionary(e => e.NameKr, e => e.KeyEn, StringComparer.OrdinalIgnoreCase);

        /// <summary>표정 ID → 코드명 (EM01 → Default)</summary>
        static Dictionary<string, string> BuildEmoteIdToCode(List<MasterEntry> all)
            => ByType(all, "Emote").ToDictionary(e => e.Id, e => e.KeyEn, StringComparer.OrdinalIgnoreCase);

        // ─── Character 별칭 빌더 ───────────────────────────────

        /// <summary>캐릭터 한글 → 영어 (로아 → Roa)</summary>
        static Dictionary<string, string> BuildCharKoreanToEn(List<MasterEntry> all)
            => ByType(all, "Char")
                .Where(e => !string.IsNullOrEmpty(e.NameKr))
                .ToDictionary(e => e.NameKr, e => e.KeyEn, StringComparer.OrdinalIgnoreCase);

        /// <summary>캐릭터 ID → 영어 (CH01 → Roa)</summary>
        static Dictionary<string, string> BuildCharIdToEn(List<MasterEntry> all)
            => ByType(all, "Char").ToDictionary(e => e.Id, e => e.KeyEn, StringComparer.OrdinalIgnoreCase);

        // ─── CG / SD 별칭 빌더 ─────────────────────────────────

        /// <summary>CG 별칭 (ID + 한글) → key_en</summary>
        static Dictionary<string, string> BuildCgAliases(List<MasterEntry> all)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in ByType(all, "CG"))
            {
                result[e.Id] = e.KeyEn;
                if (!string.IsNullOrEmpty(e.NameKr)) result[e.NameKr] = e.KeyEn;
            }
            return result;
        }

        /// <summary>SD 별칭 (ID + 한글) → key_en</summary>
        static Dictionary<string, string> BuildSdAliases(List<MasterEntry> all)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in ByType(all, "SD"))
            {
                result[e.Id] = e.KeyEn;
                if (!string.IsNullOrEmpty(e.NameKr)) result[e.NameKr] = e.KeyEn;
            }
            return result;
        }

        // ─── BGM 별칭 빌더 ─────────────────────────────────────

        /// <summary>BGM 전방향 별칭 (한글/ID → key_en)</summary>
        static Dictionary<string, string> BuildBgmAliases(List<MasterEntry> all)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in ByType(all, "BGM"))
            {
                result[e.Id] = e.KeyEn;
                if (!string.IsNullOrEmpty(e.NameKr)) result[e.NameKr] = e.KeyEn;
            }
            return result;
        }

        // ═══════════════════════════════════════════════════════════
        // Generate All
        // ═══════════════════════════════════════════════════════════

        [MenuItem("LoveAlgo/Tools/Generate All Mappings", priority = 200)]
        public static void GenerateAll()
        {
            GenerateBackgroundMapping();
            GenerateCharacterMapping();
            GenerateCGMapping();
            GenerateSDMapping();
            AssetDatabase.Refresh();
            Debug.Log("[ResourceMappingGenerator] ✅ 모든 매핑 파일 생성 완료!");
        }

        [MenuItem("LoveAlgo/Tools/Generate Background Mapping", priority = 201)]
        public static void GenerateBackgroundMapping()
        {
            var resourcesPath = "Assets/Resources/Backgrounds";
            var mappings = ScanSprites(resourcesPath);

            // 마스터 CSV에서 별칭 로딩
            var master = LoadMasterResources();
            var idAliases = BuildBgIdAliases(master);
            var shortAliases = BuildBgShortAliases(master);
            var koreanAliases = BuildBgKoreanAliases(master);

            var sb = new StringBuilder();
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("// 이 파일은 ResourceMappingGenerator에 의해 자동 생성됩니다.");
            sb.AppendLine("// 수동으로 수정하지 마세요! (LoveAlgo > Tools > Generate Background Mapping)");
            sb.AppendLine($"// 생성 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace LoveAlgo.Data");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 배경 이름 → Resources 경로 매핑 (자동 생성)");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class BgPathMapping");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");

            // 폴더별로 그룹화하여 출력
            var grouped = mappings
                .GroupBy(m => GetFolderName(m.Value))
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                sb.AppendLine($"            // {group.Key}");
                foreach (var mapping in group.OrderBy(m => m.Key))
                {
                    sb.AppendLine($"            {{ \"{mapping.Key}\", \"{mapping.Value}\" }},");
                }
                sb.AppendLine();
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            
            // LegacyNames — ID/영어/한글 별칭 (마스터 CSV에서 자동 생성)
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 별칭 → BG 이름 매핑 (ID + 영어 단축 + 한글)");
            sb.AppendLine("        /// master_resources.csv 기준 자동 생성");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly Dictionary<string, string> LegacyNames = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");
            
            sb.AppendLine("            // ─── ID 별칭 (BG01 등) ───");
            foreach (var alias in idAliases.OrderBy(a => a.Key))
            {
                sb.AppendLine($"            {{ \"{alias.Key}\", \"{alias.Value}\" }},");
            }
            sb.AppendLine();
            sb.AppendLine("            // ─── 영어 짧은 별칭 (하위호환) ───");
            foreach (var alias in shortAliases.OrderBy(a => a.Key))
            {
                sb.AppendLine($"            {{ \"{alias.Key}\", \"{alias.Value}\" }},");
            }
            sb.AppendLine();
            sb.AppendLine("            // ─── 한글 별칭 (드라이브/시나리오 호환) ───");
            foreach (var alias in koreanAliases.OrderBy(a => a.Value))
            {
                sb.AppendLine($"            {{ \"{alias.Key}\", \"{alias.Value}\" }},");
            }
            
            sb.AppendLine("        };");
            sb.AppendLine();
            
            // GetPath 메서드
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 배경 이름으로 Resources 경로 조회");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static string GetPath(string bgName)");
            sb.AppendLine("        {");
            sb.AppendLine("            // 별칭(한글/영어 짧은 이름) → 정식 이름 변환");
            sb.AppendLine("            string actualName = bgName;");
            sb.AppendLine("            if (LegacyNames.TryGetValue(bgName, out string newName))");
            sb.AppendLine("            {");
            sb.AppendLine("                actualName = newName;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // 경로 매핑에서 찾기");
            sb.AppendLine("            if (Paths.TryGetValue(actualName, out string path))");
            sb.AppendLine("            {");
            sb.AppendLine("                return path;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // 폴백: Backgrounds/이름");
            sb.AppendLine("            return $\"Backgrounds/{actualName}\";");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            // 파일 저장
            Directory.CreateDirectory(Path.GetDirectoryName(BgMappingPath));
            File.WriteAllText(BgMappingPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(BgMappingPath);

            Debug.Log($"[ResourceMappingGenerator] ✅ 배경 매핑 {mappings.Count}개 → {BgMappingPath}");
        }

        [MenuItem("LoveAlgo/Tools/Generate Character Mapping", priority = 202)]
        public static void GenerateCharacterMapping()
        {
            var resourcesPath = "Assets/Resources/Characters";
            var characters = ScanCharacters(resourcesPath);

            // 마스터 CSV에서 별칭 로딩
            var master = LoadMasterResources();
            var emoteFileToCode = BuildEmoteFileToCode(master);
            var emoteKoreanToCode = BuildEmoteKoreanToCode(master);
            var emoteIdToCode = BuildEmoteIdToCode(master);
            var charKoreanToEn = BuildCharKoreanToEn(master);
            var charIdToEn = BuildCharIdToEn(master);

            var sb = new StringBuilder();
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("// 이 파일은 ResourceMappingGenerator에 의해 자동 생성됩니다.");
            sb.AppendLine("// 수동으로 수정하지 마세요! (LoveAlgo > Tools > Generate Character Mapping)");
            sb.AppendLine($"// 생성 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace LoveAlgo.Data");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 캐릭터별 표정 매핑 (자동 생성)");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class CharacterEmoteMapping");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 캐릭터 이름 → (표정 이름 → Resources 경로)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly Dictionary<string, Dictionary<string, string>> Characters =");
            sb.AppendLine("            new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");

            int totalEmotes = 0;
            foreach (var character in characters.OrderBy(c => c.Key))
            {
                sb.AppendLine("            {");
                sb.AppendLine($"                \"{character.Key}\", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)");
                sb.AppendLine("                {");

                foreach (var emote in character.Value.OrderBy(e => e.Key == "Default" ? "" : e.Key))
                {
                    // 파일명(01_Default 등) → 코드용 이름(Default 등)으로 변환
                    string codeName = emote.Key;
                    if (emoteFileToCode.TryGetValue(emote.Key, out string mapped))
                    {
                        codeName = mapped;
                    }
                    sb.AppendLine($"                    {{ \"{codeName}\", \"{emote.Value}\" }},");
                    totalEmotes++;
                }

                sb.AppendLine("                }");
                sb.AppendLine("            },");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            
            // 한글/ID 표정 별칭 딕셔너리
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 표정 별칭 → 영어 코드 이름 (한글 + ID, master_resources.csv 기준)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly Dictionary<string, string> EmoteAliases = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");
            sb.AppendLine("            // ─── 한글 별칭 ───");
            foreach (var alias in emoteKoreanToCode.OrderBy(a => a.Value))
            {
                sb.AppendLine($"            {{ \"{alias.Key}\", \"{alias.Value}\" }},");
            }
            sb.AppendLine();
            sb.AppendLine("            // ─── ID 별칭 (EM01 등) ───");
            foreach (var alias in emoteIdToCode.OrderBy(a => a.Key))
            {
                sb.AppendLine($"            {{ \"{alias.Key}\", \"{alias.Value}\" }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            // 캐릭터 이름 한글/ID 별칭 딕셔너리
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 캐릭터 별칭 → 영어 이름 (한글 + ID, master_resources.csv 기준)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly Dictionary<string, string> CharacterAliases = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");
            sb.AppendLine("            // ─── 한글 → 영어 ───");
            foreach (var alias in charKoreanToEn.OrderBy(a => a.Value))
            {
                sb.AppendLine($"            {{ \"{alias.Key}\", \"{alias.Value}\" }},");
            }
            sb.AppendLine();
            sb.AppendLine("            // ─── ID → 영어 (CH01 등) ───");
            foreach (var alias in charIdToEn.OrderBy(a => a.Key))
            {
                sb.AppendLine($"            {{ \"{alias.Key}\", \"{alias.Value}\" }},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            
            // 헬퍼 메서드들
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 캐릭터의 표정 경로 조회 (한글/영어/ID 모두 지원)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static string GetPath(string character, string emote)");
            sb.AppendLine("        {");
            sb.AppendLine("            // 캐릭터 한글/ID 별칭 → 영어 변환");
            sb.AppendLine("            string actualChar = character;");
            sb.AppendLine("            if (CharacterAliases.TryGetValue(character, out string enChar))");
            sb.AppendLine("            {");
            sb.AppendLine("                actualChar = enChar;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // 한글/ID 표정 별칭 → 영어 변환");
            sb.AppendLine("            string actualEmote = emote;");
            sb.AppendLine("            if (EmoteAliases.TryGetValue(emote, out string englishEmote))");
            sb.AppendLine("            {");
            sb.AppendLine("                actualEmote = englishEmote;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (Characters.TryGetValue(actualChar, out var emotes))");
            sb.AppendLine("            {");
            sb.AppendLine("                if (emotes.TryGetValue(actualEmote, out string path))");
            sb.AppendLine("                {");
            sb.AppendLine("                    return path;");
            sb.AppendLine("                }");
            sb.AppendLine("                // 표정이 없으면 Default 시도");
            sb.AppendLine("                if (emotes.TryGetValue(\"Default\", out string defaultPath))");
            sb.AppendLine("                {");
            sb.AppendLine("                    return defaultPath;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // 폴백");
            sb.AppendLine("            return $\"Characters/{actualChar}/{actualEmote}\";");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 캐릭터가 특정 표정을 가지고 있는지 확인 (한글/ID 별칭 지원)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static bool HasEmote(string character, string emote)");
            sb.AppendLine("        {");
            sb.AppendLine("            return Characters.TryGetValue(character, out var emotes) &&");
            sb.AppendLine("                   emotes.ContainsKey(emote);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 캐릭터의 모든 표정 목록 조회");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static IEnumerable<string> GetEmotes(string character)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Characters.TryGetValue(character, out var emotes))");
            sb.AppendLine("            {");
            sb.AppendLine("                return emotes.Keys;");
            sb.AppendLine("            }");
            sb.AppendLine("            return Array.Empty<string>();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            // 파일 저장
            Directory.CreateDirectory(Path.GetDirectoryName(CharMappingPath));
            File.WriteAllText(CharMappingPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(CharMappingPath);

            Debug.Log($"[ResourceMappingGenerator] ✅ 캐릭터 {characters.Count}명, 표정 {totalEmotes}개 → {CharMappingPath}");
        }

        /// <summary>
        /// 지정된 경로에서 모든 스프라이트를 스캔하여 이름→경로 매핑 생성
        /// </summary>
        static Dictionary<string, string> ScanSprites(string assetPath)
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(assetPath))
            {
                Debug.LogWarning($"[ResourceMappingGenerator] 경로를 찾을 수 없음: {assetPath}");
                return mappings;
            }

            var files = Directory.GetFiles(assetPath, "*.png", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);

                // Resources 상대 경로 계산
                var relativePath = file
                    .Replace("Assets/Resources/", "")
                    .Replace("\\", "/")
                    .Replace(".png", "");

                if (!mappings.ContainsKey(fileName))
                {
                    mappings[fileName] = relativePath;
                }
                else
                {
                    Debug.LogWarning($"[ResourceMappingGenerator] ⚠️ 중복 파일명: {fileName}");
                }
            }

            return mappings;
        }

        /// <summary>
        /// 캐릭터별 표정 스캔
        /// </summary>
        static Dictionary<string, Dictionary<string, string>> ScanCharacters(string assetPath)
        {
            var characters = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(assetPath))
            {
                Debug.LogWarning($"[ResourceMappingGenerator] 경로를 찾을 수 없음: {assetPath}");
                return characters;
            }

            var characterFolders = Directory.GetDirectories(assetPath);

            foreach (var folder in characterFolders)
            {
                var charName = Path.GetFileName(folder);
                if (charName.EndsWith(".meta")) continue;

                var emotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var files = Directory.GetFiles(folder, "*.png");

                foreach (var file in files)
                {
                    var emoteName = Path.GetFileNameWithoutExtension(file);
                    var relativePath = file
                        .Replace("Assets/Resources/", "")
                        .Replace("\\", "/")
                        .Replace(".png", "");

                    emotes[emoteName] = relativePath;
                }

                if (emotes.Count > 0)
                {
                    characters[charName] = emotes;
                }
            }

            return characters;
        }

        static string GetFolderName(string path)
        {
            var parts = path.Replace("\\", "/").Split('/');
            return parts.Length >= 2 ? parts[1] : "Root";
        }

        /// <summary>
        /// 리포트 생성 - 누락/미사용 리소스 확인
        /// </summary>
        [MenuItem("LoveAlgo/Tools/Resource Usage Report", priority = 210)]
        public static void GenerateUsageReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Resource Usage Report ===");
            sb.AppendLine($"생성 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 배경 스캔
            var bgPath = "Assets/Resources/Backgrounds";
            if (Directory.Exists(bgPath))
            {
                var bgFiles = Directory.GetFiles(bgPath, "*.png", SearchOption.AllDirectories);
                sb.AppendLine($"📁 배경 이미지: {bgFiles.Length}개");
                foreach (var folder in Directory.GetDirectories(bgPath))
                {
                    var folderName = Path.GetFileName(folder);
                    var count = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories).Length;
                    sb.AppendLine($"   - {folderName}: {count}개");
                }
                sb.AppendLine();
            }

            // 캐릭터 스캔
            var charPath = "Assets/Resources/Characters";
            if (Directory.Exists(charPath))
            {
                sb.AppendLine("📁 캐릭터 이미지:");
                foreach (var folder in Directory.GetDirectories(charPath))
                {
                    var charName = Path.GetFileName(folder);
                    if (charName.EndsWith(".meta")) continue;

                    var emotes = Directory.GetFiles(folder, "*.png")
                        .Select(f => Path.GetFileNameWithoutExtension(f))
                        .ToList();
                    sb.AppendLine($"   - {charName}: {emotes.Count}개 ({string.Join(", ", emotes)})");
                }
                sb.AppendLine();
            }

            // CG 스캔
            var cgPath = "Assets/Resources/CG";
            if (Directory.Exists(cgPath))
            {
                var cgFiles = Directory.GetFiles(cgPath, "*.png", SearchOption.AllDirectories);
                sb.AppendLine($"📁 CG 이미지: {cgFiles.Length}개");
                foreach (var f in cgFiles)
                    sb.AppendLine($"   - {Path.GetFileNameWithoutExtension(f)}");
                sb.AppendLine();
            }

            // SD 스캔
            var sdPath = "Assets/Resources/SD";
            if (Directory.Exists(sdPath))
            {
                var sdFiles = Directory.GetFiles(sdPath, "*.png", SearchOption.AllDirectories);
                sb.AppendLine($"📁 SD 이미지: {sdFiles.Length}개");
                foreach (var f in sdFiles)
                    sb.AppendLine($"   - {Path.GetFileNameWithoutExtension(f)}");
                sb.AppendLine();
            }

            // 오디오 스캔
            var audioPath = "Assets/Resources/Audio";
            if (Directory.Exists(audioPath))
            {
                sb.AppendLine("📁 오디오:");
                foreach (var folder in Directory.GetDirectories(audioPath))
                {
                    var folderName = Path.GetFileName(folder);
                    var extensions = new[] { "*.mp3", "*.wav", "*.ogg" };
                    var count = extensions.SelectMany(ext => Directory.GetFiles(folder, ext, SearchOption.AllDirectories)).Count();
                    sb.AppendLine($"   - {folderName}: {count}개");
                }
            }

            // 파일로 저장
            var reportPath = "tools/resource_usage_report.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, sb.ToString());

            Debug.Log($"[ResourceMappingGenerator] 리포트 생성: {reportPath}");
            EditorUtility.RevealInFinder(reportPath);
        }

        // ═══════════════════════════════════════════════════════════
        // CG 매핑 생성
        // ═══════════════════════════════════════════════════════════

        [MenuItem("LoveAlgo/Tools/Generate CG Mapping", priority = 203)]
        public static void GenerateCGMapping()
        {
            var resourcesPath = "Assets/Resources/CG";
            var mappings = ScanSprites(resourcesPath);

            // 마스터 CSV에서 CG 별칭 로딩
            var master = LoadMasterResources();
            var cgAliases = BuildCgAliases(master);

            var sb = new StringBuilder();
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("// 이 파일은 ResourceMappingGenerator에 의해 자동 생성됩니다.");
            sb.AppendLine("// 수동으로 수정하지 마세요! (LoveAlgo > Tools > Generate CG Mapping)");
            sb.AppendLine($"// 생성 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace LoveAlgo.Data");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// CG 이름 → Resources 경로 매핑 (자동 생성)");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class CgPathMapping");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");
            foreach (var m in mappings.OrderBy(m => m.Key))
                sb.AppendLine($"            {{ \"{m.Key}\", \"{m.Value}\" }},");
            sb.AppendLine("        };");
            sb.AppendLine();

            // CG 별칭 (ID + 한글)
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// CG 별칭 → key_en (ID + 한글, master_resources.csv 기준)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");
            foreach (var a in cgAliases.OrderBy(a => a.Key))
                sb.AppendLine($"            {{ \"{a.Key}\", \"{a.Value}\" }},");
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// CG 이름으로 Resources 경로 조회 (ID/한글/영어 모두 지원)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static string GetPath(string cgName)");
            sb.AppendLine("        {");
            sb.AppendLine("            // 별칭(ID/한글) → 정식 이름 변환");
            sb.AppendLine("            if (Aliases.TryGetValue(cgName, out string actual))");
            sb.AppendLine("                cgName = actual;");
            sb.AppendLine("            if (Paths.TryGetValue(cgName, out string path))");
            sb.AppendLine("                return path;");
            sb.AppendLine("            return $\"CG/{cgName}\";");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            Directory.CreateDirectory(Path.GetDirectoryName(CgMappingPath));
            File.WriteAllText(CgMappingPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(CgMappingPath);
            Debug.Log($"[ResourceMappingGenerator] ✅ CG 매핑 {mappings.Count}개 → {CgMappingPath}");
        }

        // ═══════════════════════════════════════════════════════════
        // SD 매핑 생성
        // ═══════════════════════════════════════════════════════════

        [MenuItem("LoveAlgo/Tools/Generate SD Mapping", priority = 204)]
        public static void GenerateSDMapping()
        {
            var resourcesPath = "Assets/Resources/SD";
            var mappings = ScanSprites(resourcesPath);

            // 마스터 CSV에서 SD 별칭 로딩
            var master = LoadMasterResources();
            var sdAliases = BuildSdAliases(master);

            var sb = new StringBuilder();
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("// 이 파일은 ResourceMappingGenerator에 의해 자동 생성됩니다.");
            sb.AppendLine("// 수동으로 수정하지 마세요! (LoveAlgo > Tools > Generate SD Mapping)");
            sb.AppendLine($"// 생성 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("// ═══════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace LoveAlgo.Data");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// SD 이름 → Resources 경로 매핑 (자동 생성)");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class SdPathMapping");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");
            foreach (var m in mappings.OrderBy(m => m.Key))
                sb.AppendLine($"            {{ \"{m.Key}\", \"{m.Value}\" }},");
            sb.AppendLine("        };");
            sb.AppendLine();

            // SD 별칭 (ID + 한글)
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// SD 별칭 → key_en (ID + 한글, master_resources.csv 기준)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");
            foreach (var a in sdAliases.OrderBy(a => a.Key))
                sb.AppendLine($"            {{ \"{a.Key}\", \"{a.Value}\" }},");
            sb.AppendLine("        };");
            sb.AppendLine();

            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// SD 이름으로 Resources 경로 조회 (ID/한글/영어 모두 지원)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static string GetPath(string sdName)");
            sb.AppendLine("        {");
            sb.AppendLine("            // 별칭(ID/한글) → 정식 이름 변환");
            sb.AppendLine("            if (Aliases.TryGetValue(sdName, out string actual))");
            sb.AppendLine("                sdName = actual;");
            sb.AppendLine("            if (Paths.TryGetValue(sdName, out string path))");
            sb.AppendLine("                return path;");
            sb.AppendLine("            return $\"SD/{sdName}\";");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            Directory.CreateDirectory(Path.GetDirectoryName(SdMappingPath));
            File.WriteAllText(SdMappingPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(SdMappingPath);
            Debug.Log($"[ResourceMappingGenerator] ✅ SD 매핑 {mappings.Count}개 → {SdMappingPath}");
        }

        // ═══════════════════════════════════════════════════════════
        // CSV 스토리 리소스 검증
        // ═══════════════════════════════════════════════════════════

        [MenuItem("LoveAlgo/Tools/Validate Story CSVs", priority = 220)]
        public static void ValidateStoryCSVs()
        {
            var storyPath = "Assets/Resources/Story";
            if (!Directory.Exists(storyPath))
            {
                Debug.LogError("[CSV Validator] Story 폴더를 찾을 수 없습니다.");
                return;
            }

            // 마스터 CSV에서 별칭 로딩
            var master = LoadMasterResources();
            var bgKoreanAliases = BuildBgKoreanAliases(master);
            var bgShortAliases = BuildBgShortAliases(master);
            var bgIdAliases = BuildBgIdAliases(master);
            var emoteFileToCode = BuildEmoteFileToCode(master);
            var emoteKoreanToCode = BuildEmoteKoreanToCode(master);
            var emoteIdToCode = BuildEmoteIdToCode(master);
            var charKoreanToEn = BuildCharKoreanToEn(master);
            var charIdToEn = BuildCharIdToEn(master);
            var cgAliases = BuildCgAliases(master);
            var sdAliases = BuildSdAliases(master);

            // 실제 리소스 목록 수집
            var bgFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cgFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sdFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var charFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var emoteFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            // BG
            if (Directory.Exists("Assets/Resources/Backgrounds"))
            {
                foreach (var f in Directory.GetFiles("Assets/Resources/Backgrounds", "*.png", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    var relPath = f.Replace("Assets/Resources/", "").Replace("\\", "/").Replace(".png", "");
                    bgFiles.Add(name);
                    bgFiles.Add(relPath); // Backgrounds/MyRoom/BG_MyRoom_Interior_Day 형태도 허용
                    // 경로 스타일도 허용: MyRoom/BG_MyRoom_Interior_Day
                    var shortPath = relPath.Replace("Backgrounds/", "");
                    bgFiles.Add(shortPath);
                }
            }
            // BG 별칭도 유효로 추가 (마스터 CSV 기준)
            foreach (var alias in bgShortAliases.Keys) bgFiles.Add(alias);
            foreach (var alias in bgKoreanAliases.Keys) bgFiles.Add(alias);
            foreach (var alias in bgIdAliases.Keys) bgFiles.Add(alias);

            // CG
            if (Directory.Exists("Assets/Resources/CG"))
            {
                foreach (var f in Directory.GetFiles("Assets/Resources/CG", "*.png", SearchOption.AllDirectories))
                {
                    cgFiles.Add(Path.GetFileNameWithoutExtension(f));
                    var relPath = f.Replace("Assets/Resources/", "").Replace("\\", "/").Replace(".png", "");
                    cgFiles.Add(relPath);
                }
            }
            // CG 별칭도 유효로 추가 (마스터 CSV 기준)
            foreach (var alias in cgAliases.Keys) cgFiles.Add(alias);

            // SD
            if (Directory.Exists("Assets/Resources/SD"))
            {
                foreach (var f in Directory.GetFiles("Assets/Resources/SD", "*.png", SearchOption.AllDirectories))
                {
                    sdFiles.Add(Path.GetFileNameWithoutExtension(f));
                }
            }
            // SD 별칭도 유효로 추가 (마스터 CSV 기준)
            foreach (var alias in sdAliases.Keys) sdFiles.Add(alias);

            // Characters
            if (Directory.Exists("Assets/Resources/Characters"))
            {
                foreach (var dir in Directory.GetDirectories("Assets/Resources/Characters"))
                {
                    var charName = Path.GetFileName(dir);
                    if (charName.EndsWith(".meta")) continue;
                    charFolders.Add(charName);

                    var emotes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var f in Directory.GetFiles(dir, "*.png"))
                    {
                        var emoteName = Path.GetFileNameWithoutExtension(f);
                        // 파일명 → 코드명 변환
                        if (emoteFileToCode.TryGetValue(emoteName, out string codeName))
                            emotes.Add(codeName);
                        else
                            emotes.Add(emoteName);
                    }
                    // 한글/ID 별칭도 유효 (마스터 CSV 기준)
                    foreach (var alias in emoteKoreanToCode.Values) emotes.Add(alias);
                    foreach (var alias in emoteKoreanToCode.Keys) emotes.Add(alias);
                    foreach (var alias in emoteIdToCode.Keys) emotes.Add(alias);
                    emoteFiles[charName] = emotes;
                }
            }
            // 캐릭터 한글/ID 별칭도 유효
            foreach (var alias in charKoreanToEn)
                charFolders.Add(alias.Key);
            foreach (var alias in charIdToEn)
                charFolders.Add(alias.Key);

            // CSV 파싱 & 검증
            var csvFiles = Directory.GetFiles(storyPath, "*.csv");
            int totalErrors = 0;
            int totalWarnings = 0;
            var report = new StringBuilder();
            report.AppendLine("=== CSV Story Resource Validation ===");
            report.AppendLine($"검증 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"CSV 파일: {csvFiles.Length}개");
            report.AppendLine();

            foreach (var csvFile in csvFiles)
            {
                var fileName = Path.GetFileName(csvFile);
                var lines = File.ReadAllLines(csvFile);
                var fileErrors = new List<string>();

                for (int i = 1; i < lines.Length; i++) // skip header
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    var cols = ParseCSVLine(line);
                    if (cols.Length < 4) continue;

                    string type = cols[1].Trim();
                    string value = cols[3].Trim();

                    switch (type)
                    {
                        case "BG":
                        {
                            var bgName = value.Split(':')[0];
                            if (!string.IsNullOrEmpty(bgName) && !bgFiles.Contains(bgName))
                            {
                                fileErrors.Add($"  L{i + 1} [BG] 에셋 없음: \"{bgName}\"");
                                totalErrors++;
                            }
                            break;
                        }
                        case "CG":
                        {
                            if (value.StartsWith("Exit", StringComparison.OrdinalIgnoreCase)) break;
                            var cgName = value.Split(':')[0];
                            // CG/ 접두사 제거
                            if (cgName.StartsWith("CG/", StringComparison.OrdinalIgnoreCase))
                                cgName = cgName.Substring(3);
                            if (!string.IsNullOrEmpty(cgName) && !cgFiles.Contains(cgName))
                            {
                                fileErrors.Add($"  L{i + 1} [CG] 에셋 없음: \"{cgName}\"");
                                totalErrors++;
                            }
                            break;
                        }
                        case "SD":
                        {
                            if (value.StartsWith("Exit", StringComparison.OrdinalIgnoreCase)) break;
                            var sdName = value.Split(':')[0];
                            if (!string.IsNullOrEmpty(sdName) && !sdFiles.Contains(sdName))
                            {
                                fileErrors.Add($"  L{i + 1} [SD] 에셋 없음: \"{sdName}\"");
                                totalErrors++;
                            }
                            break;
                        }
                        case "Char":
                        {
                            // C:Enter:CharName[:Emote] or C:Emote:EmoteName
                            var parts = value.Split(':');
                            if (parts.Length >= 3 && parts[0] == "C")
                            {
                                string action = parts[1];
                                if (action.Equals("Enter", StringComparison.OrdinalIgnoreCase))
                                {
                                    string charName = parts[2];
                                    if (!charFolders.Contains(charName))
                                    {
                                        fileErrors.Add($"  L{i + 1} [Char] 캐릭터 없음: \"{charName}\"");
                                        totalErrors++;
                                    }
                                    else if (parts.Length >= 4)
                                    {
                                        string emote = parts[3];
                                        if (emoteFiles.TryGetValue(charName, out var validEmotes) && !validEmotes.Contains(emote))
                                        {
                                            fileErrors.Add($"  L{i + 1} [Char] 표정 없음: {charName}:{emote}");
                                            totalErrors++;
                                        }
                                    }
                                }
                            }
                            break;
                        }
                        case "FX":
                        {
                            // Setup:BG=xxx 체크
                            if (value.StartsWith("Setup:", StringComparison.OrdinalIgnoreCase))
                            {
                                var setupParts = value.Substring(6).Split('|');
                                foreach (var part in setupParts)
                                {
                                    if (part.StartsWith("BG=", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var setupBg = part.Substring(3);
                                        if (!bgFiles.Contains(setupBg))
                                        {
                                            fileErrors.Add($"  L{i + 1} [FX Setup BG] 에셋 없음: \"{setupBg}\"");
                                            totalErrors++;
                                        }
                                    }
                                }
                            }
                            break;
                        }
                        case "Text":
                        {
                            // <emote=xxx/> 인라인 태그 검증
                            var emoteMatches = Regex.Matches(value, @"<emote=(\w+)/>");
                            foreach (Match match in emoteMatches)
                            {
                                string emote = match.Groups[1].Value;
                                // 전체 유효 표정 목록으로 검증 (마스터 CSV 기준)
                                bool valid = emoteFileToCode.Values.Contains(emote) 
                                          || emoteKoreanToCode.Values.Contains(emote)
                                          || emoteKoreanToCode.Keys.Contains(emote)
                                          || emoteIdToCode.Keys.Contains(emote);
                                if (!valid)
                                {
                                    fileErrors.Add($"  L{i + 1} [Text] 알 수 없는 표정 태그: <emote={emote}/>");
                                    totalWarnings++;
                                }
                            }
                            break;
                        }
                    }
                }

                if (fileErrors.Count > 0)
                {
                    report.AppendLine($"❌ {fileName} ({fileErrors.Count}건)");
                    foreach (var err in fileErrors)
                        report.AppendLine(err);
                    report.AppendLine();
                }
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine($"결과: 에러 {totalErrors}개, 경고 {totalWarnings}개");
            report.AppendLine();
            report.AppendLine("=== 등록된 리소스 요약 ===");
            report.AppendLine($"BG: {bgFiles.Count}개 항목 (에셋+별칭 포함)");
            report.AppendLine($"CG: {cgFiles.Count}개");
            report.AppendLine($"SD: {sdFiles.Count}개");
            report.AppendLine($"캐릭터: {charFolders.Count}명");

            var reportPath = "tools/csv_validation_report.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);

            if (totalErrors == 0 && totalWarnings == 0)
                Debug.Log($"[CSV Validator] ✅ 모든 CSV 검증 통과! ({csvFiles.Length}개 파일)");
            else
                Debug.LogWarning($"[CSV Validator] ⚠️ 에러 {totalErrors}개, 경고 {totalWarnings}개 발견 → {reportPath}");

            EditorUtility.RevealInFinder(reportPath);
        }

        // ═══════════════════════════════════════════════════════════
        // Google Sheets → 마스터 CSV 동기화
        // ═══════════════════════════════════════════════════════════

        // ★ Google Sheets 공유 → "웹에 게시" → CSV 형식으로 게시한 URL을 아래에 입력
        // 예: https://docs.google.com/spreadsheets/d/e/XXXX/pub?output=csv
        const string GoogleSheetCsvUrl = "";

        [MenuItem("LoveAlgo/Tools/Download Master Resources (Google Sheets)", priority = 230)]
        public static void DownloadMasterResources()
        {
            if (string.IsNullOrEmpty(GoogleSheetCsvUrl))
            {
                Debug.LogError(
                    "[ResourceMappingGenerator] Google Sheets URL이 설정되지 않았습니다.\n" +
                    "ResourceMappingGenerator.cs의 GoogleSheetCsvUrl 상수에 " +
                    "\"웹에 게시\" CSV URL을 입력해주세요.\n" +
                    "설정 방법: Google Sheets → 파일 → 공유 → 웹에 게시 → CSV 형식 선택 → 게시 → URL 복사");
                return;
            }

            EditorUtility.DisplayProgressBar("마스터 리소스 다운로드", "Google Sheets에서 다운로드 중...", 0.3f);

            try
            {
                using var client = new System.Net.WebClient();
                client.Encoding = Encoding.UTF8;
                var csv = client.DownloadString(GoogleSheetCsvUrl);

                if (string.IsNullOrWhiteSpace(csv) || !csv.Contains("id,type,key_en,name_kr"))
                {
                    Debug.LogError("[ResourceMappingGenerator] 다운로드된 CSV가 올바른 형식이 아닙니다. 헤더 확인: id,type,key_en,name_kr,alias");
                    return;
                }

                // 기존 파일 백업
                if (File.Exists(MasterCsvPath))
                {
                    var backupPath = MasterCsvPath + ".bak";
                    File.Copy(MasterCsvPath, backupPath, true);
                    Debug.Log($"[ResourceMappingGenerator] 기존 CSV 백업: {backupPath}");
                }

                File.WriteAllText(MasterCsvPath, csv, Encoding.UTF8);
                AssetDatabase.ImportAsset(MasterCsvPath);

                // 항목 수 확인
                var entries = LoadMasterResources();
                var typeCounts = entries.GroupBy(e => e.Type)
                    .Select(g => $"{g.Key}:{g.Count()}")
                    .ToList();

                Debug.Log($"[ResourceMappingGenerator] ✅ 마스터 CSV 다운로드 완료! " +
                    $"({entries.Count}개 항목: {string.Join(", ", typeCounts)})\n" +
                    $"→ {MasterCsvPath}\n" +
                    $"이제 'Generate All Mappings'으로 매핑 파일을 재생성하세요.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceMappingGenerator] 다운로드 실패: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// CSV 줄 파싱 (따옴표 안의 콤마 처리)
        /// </summary>
        static string[] ParseCSVLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}
