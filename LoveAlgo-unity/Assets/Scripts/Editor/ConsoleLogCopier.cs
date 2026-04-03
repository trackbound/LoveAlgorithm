using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 콘솔 로그를 AI 친화적 포맷으로 클립보드에 복사
    /// 단축키: Ctrl+Shift+L  |  메뉴: Tools → LoveAlgo → Copy Console Logs for AI
    /// </summary>
    public static class ConsoleLogCopier
    {
        // Unity 내부 LogMessageFlags 비트마스크
        const int ErrorMask =
            1 | 2 | 16 | 64 | 256 | 2048 | 8192 | 131072
            | (1 << 20) | (1 << 21) | (1 << 22);

        const int WarningMask = 128 | 512 | 4096;

        const int MaxEntries = 500;

        [MenuItem("Tools/LoveAlgo/Copy Console Logs for AI %#l")]
        static void CopyConsoleLogs()
        {
            var asm = Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var tEntries = asm.GetType("UnityEditor.LogEntries");
            var tEntry   = asm.GetType("UnityEditor.LogEntry");

            if (tEntries == null || tEntry == null)
            {
                Debug.LogWarning("[ConsoleLogCopier] LogEntries API에 접근할 수 없습니다.");
                return;
            }

            var mGetCount = tEntries.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
            var mStart    = tEntries.GetMethod("StartGettingEntries", BindingFlags.Static | BindingFlags.Public);
            var mGetEntry = tEntries.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
            var mEnd      = tEntries.GetMethod("EndGettingEntries", BindingFlags.Static | BindingFlags.Public);

            if (mGetCount == null || mStart == null || mGetEntry == null || mEnd == null)
            {
                Debug.LogWarning("[ConsoleLogCopier] LogEntries 메서드를 찾을 수 없습니다. " +
                    $"GetCount={mGetCount != null} Start={mStart != null} GetEntry={mGetEntry != null} End={mEnd != null}");
                return;
            }

            int count = (int)mGetCount.Invoke(null, null);
            if (count == 0)
            {
                Debug.Log("[ConsoleLogCopier] 콘솔이 비어 있습니다.");
                return;
            }

            mStart.Invoke(null, null);

            var entry = Activator.CreateInstance(tEntry);
            var allFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fCond = tEntry.GetField("condition", allFlags) ?? tEntry.GetField("message", allFlags);
            var fMode = tEntry.GetField("mode", allFlags);
            var fFile = tEntry.GetField("file", allFlags);
            var fLine = tEntry.GetField("line", allFlags);

            if (fCond == null || fMode == null)
            {
                mEnd.Invoke(null, null);
                // 디버그 출력: 사용 가능한 필드 목록
                var fields = tEntry.GetFields(allFlags);
                var names = new List<string>();
                foreach (var f in fields) names.Add(f.Name);
                Debug.LogWarning($"[ConsoleLogCopier] LogEntry 필드를 찾을 수 없습니다. 사용 가능: {string.Join(", ", names)}");
                return;
            }

            var errors   = new List<(string, int)>();
            var warnings = new List<(string, int)>();
            var logs     = new List<(string, int)>();

            // key → (list ref, index in list)
            var errorSeen   = new Dictionary<string, int>();
            var warningSeen = new Dictionary<string, int>();
            var logSeen     = new Dictionary<string, int>();

            int totalE = 0, totalW = 0, totalL = 0;
            int limit = Math.Min(count, MaxEntries);

            for (int i = 0; i < limit; i++)
            {
                mGetEntry.Invoke(null, new object[] { i, entry });

                string condition = (string)fCond.GetValue(entry);
                int mode         = (int)fMode.GetValue(entry);
                string file      = fFile != null ? (string)fFile.GetValue(entry) : "";
                int line         = fLine != null ? (int)fLine.GetValue(entry) : 0;

                // 첫 줄만 추출
                int nl = condition.IndexOf('\n');
                string firstLine = nl >= 0 ? condition.Substring(0, nl) : condition;

                // 파일:라인
                string loc = "";
                if (!string.IsNullOrEmpty(file))
                    loc = $" ({Path.GetFileName(file)}:{line})";

                string type = Classify(mode);

                List<(string, int)> targetList;
                Dictionary<string, int> seen;
                string prefix;

                switch (type)
                {
                    case "Error":
                        totalE++; targetList = errors; seen = errorSeen; prefix = "[E]"; break;
                    case "Warning":
                        totalW++; targetList = warnings; seen = warningSeen; prefix = "[W]"; break;
                    default:
                        totalL++; targetList = logs; seen = logSeen; prefix = "[L]"; break;
                }

                string formatted = $"{prefix} {firstLine}{loc}";

                if (seen.TryGetValue(formatted, out int idx))
                {
                    var item = targetList[idx];
                    targetList[idx] = (item.Item1, item.Item2 + 1);
                }
                else
                {
                    seen[formatted] = targetList.Count;
                    targetList.Add((formatted, 1));
                }
            }

            mEnd.Invoke(null, null);

            // 출력 조립
            var sb = new StringBuilder();
            sb.AppendLine($"=== Unity Console ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            sb.AppendLine($"Total E:{totalE} W:{totalW} L:{totalL}");
            if (count > MaxEntries)
                sb.AppendLine($"(showing first {MaxEntries} of {count})");

            if (errors.Count > 0) { sb.AppendLine(); Append(sb, errors); }
            if (warnings.Count > 0) { sb.AppendLine(); Append(sb, warnings); }
            if (logs.Count > 0) { sb.AppendLine(); Append(sb, logs); }

            string result = sb.ToString().TrimEnd();
            GUIUtility.systemCopyBuffer = result;
            Debug.Log($"[ConsoleLogCopier] E:{errors.Count} W:{warnings.Count} L:{logs.Count} → 클립보드 복사 완료");
        }

        static void Append(StringBuilder sb, List<(string, int)> items)
        {
            foreach (var item in items)
            {
                sb.Append(item.Item1);
                if (item.Item2 > 1) sb.Append($" x{item.Item2}");
                sb.AppendLine();
            }
        }

        static string Classify(int mode)
        {
            if ((mode & ErrorMask) != 0) return "Error";
            if ((mode & WarningMask) != 0) return "Warning";
            return "Log";
        }
    }
}
