using System.Collections.Generic;
using LoveAlgo.Contracts;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.Story.EditorTools
{
    /// <summary>
    /// 라운드트립 검증: Parse(Serialize(Parse(file))) ≡ Parse(file)
    /// ScriptCsvSerializer가 ScriptLine 5속성(LineID/Type/Speaker/Value/NextType/Delay)을 손실 없이
    /// 복원하는지 보장. 테스트 프레임워크 미설치 환경에서 Editor 메뉴로 실행.
    ///
    /// 사용: Tools/Story/Run CSV Roundtrip Validation
    /// </summary>
    public static class ScriptCsvRoundtripValidator
    {
        const string StoryDir = "Assets/StreamingAssets/Story";

        [MenuItem("Tools/Story/Run CSV Roundtrip Validation")]
        public static void RunAll()
        {
            if (!Directory.Exists(StoryDir))
            {
                Debug.LogError($"[Roundtrip] 폴더 없음: {StoryDir}");
                return;
            }

            var csvFiles = Directory.GetFiles(StoryDir, "*.csv", SearchOption.TopDirectoryOnly);
            if (csvFiles.Length == 0)
            {
                Debug.LogWarning($"[Roundtrip] CSV 파일 없음: {StoryDir}");
                return;
            }

            int passed = 0, failed = 0;
            var failures = new List<string>();

            int skipped = 0;
            foreach (var path in csvFiles)
            {
                string name = Path.GetFileName(path);
                string csv = File.ReadAllText(path, System.Text.Encoding.UTF8);

                // 엔진 포맷이 아닌 CSV는 스킵 — 헤더 `LineID,Type,Speaker,Value,Next` 확인
                if (!IsEngineFormat(csv))
                {
                    Debug.Log($"[Roundtrip] · {name} — 엔진 포맷 아님 (스킵)");
                    skipped++;
                    continue;
                }

                var first = ScriptParser.Parse(csv);
                string serialized = ScriptCsvSerializer.Serialize(first);
                var second = ScriptParser.Parse(serialized);

                string diff = CompareLines(first, second);
                if (diff == null)
                {
                    passed++;
                    Debug.Log($"[Roundtrip] ✓ {name} — {first.Count} lines OK");
                }
                else
                {
                    failed++;
                    failures.Add($"{name}: {diff}");
                    Debug.LogError($"[Roundtrip] ✗ {name}\n{diff}");
                }
            }

            int total = csvFiles.Length;
            if (failed == 0)
                Debug.Log($"[Roundtrip] ALL PASS — {passed}/{total} files (skipped {skipped})");
            else
                Debug.LogError($"[Roundtrip] FAIL — {failed}/{total} files (passed {passed}, skipped {skipped})\n  " + string.Join("\n  ", failures));
        }

        /// <summary>엔진 포맷(5컬럼) CSV인지 헤더로 판정.</summary>
        static bool IsEngineFormat(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return false;
            // 첫 비주석/비공백 라인이 LineID,Type,... 로 시작하는지
            using var reader = new StringReader(csv);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                return trimmed.StartsWith("LineID,", System.StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// ScriptLine 의미적 비교 (SourceLine 무시).
        /// 첫 불일치 위치만 리턴. 모두 일치하면 null.
        /// </summary>
        static string CompareLines(List<ScriptLine> a, List<ScriptLine> b)
        {
            if (a.Count != b.Count)
                return $"line count mismatch: {a.Count} vs {b.Count}";

            for (int i = 0; i < a.Count; i++)
            {
                var x = a[i]; var y = b[i];
                if (x.LineID != y.LineID)            return $"#{i} LineID '{x.LineID}' → '{y.LineID}'";
                if (x.Type != y.Type)                return $"#{i} Type {x.Type} → {y.Type}";
                if ((x.Speaker ?? "") != (y.Speaker ?? "")) return $"#{i} Speaker '{x.Speaker}' → '{y.Speaker}'";
                if ((x.Value ?? "") != (y.Value ?? ""))     return $"#{i} Value '{Trunc(x.Value)}' → '{Trunc(y.Value)}'";
                if (x.NextType != y.NextType)        return $"#{i} NextType {x.NextType} → {y.NextType}";
                if (Mathf.Abs(x.DelaySeconds - y.DelaySeconds) > 0.001f)
                    return $"#{i} Delay {x.DelaySeconds} → {y.DelaySeconds}";
            }
            return null;
        }

        static string Trunc(string s, int max = 60)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
