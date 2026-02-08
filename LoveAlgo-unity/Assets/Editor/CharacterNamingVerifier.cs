using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 에디터 메뉴: LoveAlgo/Tools/Verify Character Emote Names
    /// - Assets/Resources/Characters/* 폴더를 스캔하여
    ///   canonical 이름이 아닌 파일을 리포트하고 제안합니다.
    /// - 출력 파일: <repo root>/character_naming_report.txt
    /// </summary>
    public static class CharacterNamingVerifier
    {
        static readonly string[] Characters = { "Roa", "Yeun", "Daeun", "Bom", "Heewon" };
        static readonly HashSet<string> Canonical = new(StringComparer.OrdinalIgnoreCase)
        {
            "Default", "EyeSmile", "Bright", "Happy", "Glare", "Tearful", "Surprise", "Happy_Alt"
        };

        static readonly Dictionary<string, string> VariantSuggestions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Smile", "EyeSmile" },
            { "Laugh", "Happy" },
            { "Crying", "Tearful" },
            { "Tear", "Tearful" },
            { "Smile1", "EyeSmile" },
            { "BigSmile", "Happy" },
        };

        [MenuItem("LoveAlgo/Tools/Verify Character Emote Names", priority = 300)]
        public static void VerifyNames()
        {
            string basePath = "Assets/Resources/Characters";
            var reportLines = new List<string>();
            reportLines.Add("Character Naming Verification Report");
            reportLines.Add("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            reportLines.Add("");

            int totalFiles = 0, nonCanonical = 0;

            foreach (var c in Characters)
            {
                string folder = Path.Combine(basePath, c);
                reportLines.Add($"-- {c} --");
                if (!Directory.Exists(folder))
                {
                    reportLines.Add("  (folder missing)");
                    reportLines.Add("");
                    continue;
                }

                var pngs = Directory.GetFiles(folder, "*.png");
                if (pngs.Length == 0)
                {
                    reportLines.Add("  (no png files)");
                    reportLines.Add("");
                    continue;
                }

                foreach (var f in pngs)
                {
                    totalFiles++;
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (Canonical.Contains(name))
                    {
                        // OK
                        reportLines.Add($"  OK: {name}.png");
                    }
                    else if (VariantSuggestions.TryGetValue(name, out var suggest))
                    {
                        nonCanonical++;
                        reportLines.Add($"  Variant: {name}.png  → Suggest: {suggest}.png");
                    }
                    else
                    {
                        nonCanonical++;
                        reportLines.Add($"  Unknown: {name}.png  → No suggestion available");
                    }
                }

                reportLines.Add("");
            }

            reportLines.Add($"Total files scanned: {totalFiles}");
            reportLines.Add($"Non-canonical files: {nonCanonical}");

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "character_naming_report.txt");
            File.WriteAllLines(reportPath, reportLines);
            Debug.Log($"[CharacterNamingVerifier] Report written: {reportPath}");
            Debug.Log(string.Join("\n", reportLines.Take(30)) + (reportLines.Count > 30 ? "\n..." : ""));

            // Show in OS (reveal)
            EditorUtility.RevealInFinder(reportPath);
        }
    }
}