using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// Resources 폴더를 스캔하여 리소스 경로 매핑 파일을 자동 생성
    /// </summary>
    public static class ResourceMappingGenerator
    {
        const string BgMappingPath = "Assets/Scripts/Data/BgPathMapping.cs";
        const string CharMappingPath = "Assets/Scripts/Data/CharacterEmoteMapping.cs";

        [MenuItem("LoveAlgo/Tools/Generate All Mappings", priority = 200)]
        public static void GenerateAll()
        {
            GenerateBackgroundMapping();
            GenerateCharacterMapping();
            AssetDatabase.Refresh();
            Debug.Log("[ResourceMappingGenerator] ✅ 모든 매핑 파일 생성 완료!");
        }

        [MenuItem("LoveAlgo/Tools/Generate Background Mapping", priority = 201)]
        public static void GenerateBackgroundMapping()
        {
            var resourcesPath = "Assets/Resources/Backgrounds";
            var mappings = ScanSprites(resourcesPath);

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
            
            // LegacyNames (수동 관리 필요 - 기존 값 유지)
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 레거시 이름 → 신규 이름 매핑 (하위 호환, 수동 관리)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly Dictionary<string, string> LegacyNames = new(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");
            sb.AppendLine("            { \"Black\", \"BG_Black\" },");
            sb.AppendLine("            { \"RoaTheme\", \"BG_RoaTheme\" },");
            sb.AppendLine("            { \"script_select\", \"BG_ScriptSelect\" },");
            sb.AppendLine("            { \"MyRoom\", \"BG_MyRoom_Interior_Day\" },");
            sb.AppendLine("            { \"MyRoom_Night\", \"BG_MyRoom_Interior_Night\" },");
            sb.AppendLine("            { \"Bed\", \"BG_MyRoom_Bed_Day\" },");
            sb.AppendLine("            { \"Bed_Night\", \"BG_MyRoom_Bed_Night\" },");
            sb.AppendLine("            { \"Desk\", \"BG_MyRoom_Desk_Day\" },");
            sb.AppendLine("            { \"StudentCenter\", \"BG_StudentCenter_Front_Day\" },");
            sb.AppendLine("            { \"StudentCenter_Night\", \"BG_StudentCenter_Front_Night\" },");
            sb.AppendLine("            { \"Cafe\", \"BG_StudentCenter_Cafe_Day\" },");
            sb.AppendLine("            { \"ClubRoom\", \"BG_ClubRoom_Corridor_Day\" },");
            sb.AppendLine("            { \"Engineering\", \"BG_Engineering_Front_Day\" },");
            sb.AppendLine("            { \"MajorClass\", \"BG_Engineering_Classroom_Day\" },");
            sb.AppendLine("        };");
            sb.AppendLine();
            
            // GetPath 메서드
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 배경 이름으로 Resources 경로 조회");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static string GetPath(string bgName)");
            sb.AppendLine("        {");
            sb.AppendLine("            // 레거시 이름 변환");
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
                    sb.AppendLine($"                    {{ \"{emote.Key}\", \"{emote.Value}\" }},");
                    totalEmotes++;
                }

                sb.AppendLine("                }");
                sb.AppendLine("            },");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            
            // 헬퍼 메서드들
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 캐릭터의 표정 경로 조회");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static string GetPath(string character, string emote)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Characters.TryGetValue(character, out var emotes))");
            sb.AppendLine("            {");
            sb.AppendLine("                if (emotes.TryGetValue(emote, out string path))");
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
            sb.AppendLine("            return $\"Characters/{character}/{emote}\";");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 캐릭터가 특정 표정을 가지고 있는지 확인");
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
    }
}
