#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Story;
using LoveAlgo.Story.StoryEngine;

namespace LoveAlgo.StoryEditor
{
    /// <summary>
    /// Tools/Story 메뉴 — CSV preflight validator 실행.
    /// 콘솔에 Violation 출력 (Error는 LogError로 — 빨강색, 작가가 즉시 식별 가능).
    /// </summary>
    public static class StoryValidatorMenu
    {
        [MenuItem("Tools/Story/Validate All Story CSV")]
        public static void ValidateAll()
        {
            string storyDir = Path.Combine(Application.dataPath, "Resources", "Story");
            if (!Directory.Exists(storyDir))
            {
                Debug.LogWarning($"[StoryValidator] {storyDir} 폴더가 없습니다.");
                return;
            }

            var files = Directory.GetFiles(storyDir, "*.csv", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                Debug.Log("[StoryValidator] CSV 파일이 없습니다.");
                return;
            }

            int totalErr = 0, totalWarn = 0;
            foreach (var path in files)
            {
                string csv = File.ReadAllText(path);
                var lines = ScriptParser.Parse(csv);
                var violations = ScriptValidator.Validate(lines);

                if (violations.Count == 0)
                {
                    Debug.Log($"[StoryValidator] ✓ {Path.GetFileName(path)} — OK ({lines.Count} 라인)");
                    continue;
                }

                int errors = 0, warnings = 0;
                foreach (var v in violations)
                {
                    if (v.Severity == "Error") errors++;
                    else warnings++;
                }
                totalErr += errors;
                totalWarn += warnings;

                string header = $"[StoryValidator] {Path.GetFileName(path)} — Error {errors} / Warning {warnings}";
                string body = ScriptValidator.FormatReport(violations);
                if (errors > 0) Debug.LogError($"{header}\n{body}");
                else            Debug.LogWarning($"{header}\n{body}");
            }

            Debug.Log($"[StoryValidator] 완료 — {files.Length} 파일, 총 Error {totalErr} / Warning {totalWarn}");
        }

        [MenuItem("Tools/Story/Validate Selected CSV")]
        public static void ValidateSelected()
        {
            var asset = Selection.activeObject as TextAsset;
            if (asset == null)
            {
                Debug.LogWarning("[StoryValidator] CSV TextAsset을 Project 창에서 선택하세요.");
                return;
            }

            var lines = ScriptParser.Parse(asset);
            var violations = ScriptValidator.Validate(lines);
            string report = ScriptValidator.FormatReport(violations);

            if (violations.Count == 0)
                Debug.Log($"[StoryValidator] ✓ {asset.name} — OK ({lines.Count} 라인)");
            else
                Debug.LogWarning($"[StoryValidator] {asset.name}\n{report}");
        }
    }
}
#endif
