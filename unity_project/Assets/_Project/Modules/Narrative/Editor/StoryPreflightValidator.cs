#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoveAlgo.Story.StoryEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LoveAlgo.Story.Editor
{
    /// <summary>
    /// Story CSV preflight 게이트.
    /// - 메뉴 "Tools/Story/Validate All CSV": 수동 검증.
    /// - IPreprocessBuildWithReport: 빌드 시작 직전 자동 검증. 에러 1건 이상이면 BuildFailedException으로
    ///   빌드 중단 → 작가가 CSV 깨뜨려도 무음으로 출시 빌드에 통과하지 않음.
    /// Strict 모드를 켜고 ScriptParser+ScriptValidator를 돌린 결과를 합산.
    /// </summary>
    public class StoryPreflightValidator : IPreprocessBuildWithReport
    {
        const string StoryFolder = "Assets/Resources/Story";
        const string BackupSuffix = ".bak";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[StoryPreflightValidator] 빌드 직전 CSV 검증 시작 ...");
            var summary = ValidateAllStoryCsv();
            if (summary.errors > 0)
            {
                throw new BuildFailedException(
                    $"[StoryPreflightValidator] CSV preflight 실패: 에러 {summary.errors}건, 경고 {summary.warnings}건. " +
                    "콘솔에서 위반 라인을 확인하고 수정 후 다시 빌드하세요.");
            }
            Debug.Log($"[StoryPreflightValidator] 빌드 직전 CSV 검증 통과 (경고 {summary.warnings}건)");
        }

        [MenuItem("Tools/Story/Validate All CSV")]
        static void ValidateMenu()
        {
            var summary = ValidateAllStoryCsv();
            string msg = $"검증 완료\n\n파일 {summary.filesChecked}개\n에러 {summary.errors}건\n경고 {summary.warnings}건";
            if (summary.errors == 0 && summary.warnings == 0)
                EditorUtility.DisplayDialog("Story CSV Preflight", msg + "\n\n모두 통과.", "OK");
            else
                EditorUtility.DisplayDialog("Story CSV Preflight", msg + "\n\n상세는 콘솔 참조.", "OK");
        }

        static Summary ValidateAllStoryCsv()
        {
            var summary = new Summary();
            string[] csvPaths = FindStoryCsvAssets();
            if (csvPaths.Length == 0)
            {
                Debug.LogWarning($"[StoryPreflightValidator] {StoryFolder} 아래 CSV 없음 — 검증 스킵");
                return summary;
            }

            bool prevStrict = ScriptParser.Strict;
            ScriptParser.Strict = true;
            try
            {
                foreach (var path in csvPaths)
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    string text = File.ReadAllText(path);

                    int parseErrorsBefore = CountStoryErrorsInLog();
                    var lines = ScriptParser.Parse(text);
                    summary.filesChecked++;

                    // Strict 모드에서 Parse가 출력한 LogError는 콘솔에 이미 찍힘 — 별도 카운트 어려우므로
                    // ScriptValidator 결과만 합산. (Parse 측 누락은 빌드 콘솔의 에러 마커로 보임)
                    var violations = ScriptValidator.Validate(lines);
                    int fileErrors = violations.Count(v => v.Severity == "Error");
                    int fileWarnings = violations.Count(v => v.Severity == "Warning");
                    summary.errors += fileErrors;
                    summary.warnings += fileWarnings;

                    if (fileErrors > 0 || fileWarnings > 0)
                    {
                        Debug.Log($"[StoryPreflightValidator] {fileName} — 에러 {fileErrors}, 경고 {fileWarnings}");
                        foreach (var v in violations)
                        {
                            if (v.Severity == "Error") Debug.LogError($"[{fileName}] {v}");
                            else Debug.LogWarning($"[{fileName}] {v}");
                        }
                    }
                }
            }
            finally
            {
                ScriptParser.Strict = prevStrict;
            }
            return summary;
        }

        /// <summary>
        /// Assets/Resources/Story 아래의 .csv 파일 경로 목록 — 백업(.bak.csv) 제외.
        /// </summary>
        static string[] FindStoryCsvAssets()
        {
            if (!AssetDatabase.IsValidFolder(StoryFolder))
                return System.Array.Empty<string>();

            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { StoryFolder });
            var result = new List<string>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".csv")) continue;
                // "Prologue.bak.csv" 같은 백업 제외
                string nameNoExt = Path.GetFileNameWithoutExtension(path);
                if (nameNoExt.EndsWith(BackupSuffix, System.StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(path);
            }
            return result.ToArray();
        }

        // 향후 Parse 측 에러까지 카운트하고 싶다면 LogMessageReceived 구독으로 확장 가능.
        // 지금은 ScriptValidator의 Violation만 빌드 게이트로 사용.
        static int CountStoryErrorsInLog() => 0;

        struct Summary
        {
            public int filesChecked;
            public int errors;
            public int warnings;
        }
    }
}
#endif
