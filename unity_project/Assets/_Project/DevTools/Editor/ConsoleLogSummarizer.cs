using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.DevTools.EditorTools
{
    /// <summary>
    /// Unity Console 로그를 템플릿화·중복제거해서 요약 → 클립보드 복사.
    /// 동일 패턴 메시지가 수십~수백 개 쏟아질 때 LLM/리뷰어에게 전달하기 좋은 형식.
    ///
    /// 사용: Tools > Logs > Copy Console Summary
    ///
    /// 요약 형식:
    ///   [Error × 18] ScriptParser Line ?: 컬럼 부족 (3/5) - "..."
    ///     첫 발생 (line 1): "dim,roa,text"
    ///     스택: ScriptParser.ParseLine → ScriptParser.Parse → ScriptCsvRoundtripValidator.RunAll
    ///
    /// 구현 노트:
    ///   - Unity LogEntries (internal API)를 reflection으로 접근
    ///   - 메시지 템플릿화: 숫자(`Line 12` → `Line ?`), 따옴표 내부(`"..."` → `"…"`), 경로(`/Assets/...` → `/.../`)
    /// </summary>
    public static class ConsoleLogSummarizer
    {
        [MenuItem("Tools/Logs/Copy Console Summary %#&l")]  // Ctrl+Alt+Shift+L
        public static void CopySummary()
        {
            var groups = CollectGrouped();
            if (groups.Count == 0)
            {
                Debug.Log("[LogSummary] 콘솔 로그 없음");
                EditorGUIUtility.systemCopyBuffer = "(콘솔 로그 없음)";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Unity Console Summary  ({groups.Count} unique patterns, {TotalCount(groups)} total entries)");
            sb.AppendLine();

            foreach (var g in groups)
            {
                string sev = SeverityLabel(g.Mode);
                sb.AppendLine($"## [{sev} × {g.Count}] {g.Template}");
                sb.AppendLine();
                sb.AppendLine("**첫 발생:**");
                sb.AppendLine("```");
                sb.AppendLine(g.FirstFullMessage);
                sb.AppendLine("```");
                if (!string.IsNullOrEmpty(g.FirstStack))
                {
                    sb.AppendLine("**스택 (첫 발생):**");
                    sb.AppendLine("```");
                    sb.AppendLine(CompactStack(g.FirstStack));
                    sb.AppendLine("```");
                }
                if (g.Count > 1 && g.SampleVariants.Count > 0)
                {
                    sb.AppendLine($"**다른 발생 사례 ({Math.Min(g.SampleVariants.Count, 3)}개):**");
                    foreach (var v in g.SampleVariants)
                        sb.AppendLine($"- {Trunc(v, 200)}");
                }
                sb.AppendLine();
            }

            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"[LogSummary] 클립보드 복사 완료 — {groups.Count}개 패턴, {TotalCount(groups)}개 항목, {sb.Length} chars");
        }

        [MenuItem("Tools/Logs/Clear Console")]
        public static void ClearConsole()
        {
            var assembly = Assembly.GetAssembly(typeof(SceneView));
            var logEntries = assembly.GetType("UnityEditor.LogEntries");
            var clear = logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
            clear?.Invoke(null, null);
        }

        // ── 내부 ──

        class LogGroup
        {
            public string Template;
            public int Mode;               // Unity LogEntry mode 비트 (Error/Warning/Log)
            public int Count;
            public string FirstFullMessage;
            public string FirstStack;
            public List<string> SampleVariants = new List<string>(); // 첫 발생 제외, 최대 3개
        }

        static List<LogGroup> CollectGrouped()
        {
            var entries = ReadAllLogEntries();
            var byKey = new Dictionary<string, LogGroup>();
            var order = new List<string>();

            foreach (var e in entries)
            {
                string template = MakeTemplate(e.message);
                string key = template + "|" + (e.mode & 0xFFFF); // mode 일부만 (Error/Warning 구분)
                if (!byKey.TryGetValue(key, out var g))
                {
                    g = new LogGroup
                    {
                        Template = template,
                        Mode = e.mode,
                        FirstFullMessage = e.message,
                        FirstStack = e.stack,
                    };
                    byKey[key] = g;
                    order.Add(key);
                }
                else if (g.SampleVariants.Count < 3 && e.message != g.FirstFullMessage)
                {
                    g.SampleVariants.Add(e.message);
                }
                g.Count++;
            }

            var result = new List<LogGroup>(order.Count);
            foreach (var key in order) result.Add(byKey[key]);
            // Error 우선, 그 다음 Warning, 그 다음 Log. 같은 severity 내에서는 count 내림차순
            result.Sort((a, b) =>
            {
                int sa = SeverityOrder(a.Mode), sb = SeverityOrder(b.Mode);
                if (sa != sb) return sa - sb;
                return b.Count - a.Count;
            });
            return result;
        }

        struct LogEntry { public string message; public string stack; public int mode; }

        /// <summary>UnityEditor.LogEntries (internal)를 reflection으로 읽기.</summary>
        static List<LogEntry> ReadAllLogEntries()
        {
            var result = new List<LogEntry>();
            var assembly = Assembly.GetAssembly(typeof(SceneView));
            var typeLogEntries = assembly.GetType("UnityEditor.LogEntries");
            var typeLogEntry = assembly.GetType("UnityEditor.LogEntry");
            if (typeLogEntries == null || typeLogEntry == null) return result;

            var startGetting = typeLogEntries.GetMethod("StartGettingEntries", BindingFlags.Static | BindingFlags.Public);
            var endGetting   = typeLogEntries.GetMethod("EndGettingEntries",   BindingFlags.Static | BindingFlags.Public);
            var getEntry     = typeLogEntries.GetMethod("GetEntryInternal",    BindingFlags.Static | BindingFlags.Public);
            var fldMessage   = typeLogEntry.GetField("message", BindingFlags.Public | BindingFlags.Instance);
            var fldMode      = typeLogEntry.GetField("mode",    BindingFlags.Public | BindingFlags.Instance);
            // Unity 2023+ : 스택 별도 메서드. 2022- : message에 포함. 일단 message에 들어있다고 가정 (보편적)
            int count = (int)startGetting.Invoke(null, null);
            try
            {
                var entry = Activator.CreateInstance(typeLogEntry);
                for (int i = 0; i < count; i++)
                {
                    getEntry.Invoke(null, new object[] { i, entry });
                    string raw = (string)fldMessage.GetValue(entry);
                    int mode = (int)fldMode.GetValue(entry);
                    // message는 보통 "ErrorText\nStackLine1\nStackLine2..." 형식
                    var split = SplitMessageAndStack(raw);
                    result.Add(new LogEntry { message = split.message, stack = split.stack, mode = mode });
                }
            }
            finally
            {
                endGetting.Invoke(null, null);
            }
            return result;
        }

        static (string message, string stack) SplitMessageAndStack(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return ("", "");
            int firstNewline = raw.IndexOf('\n');
            if (firstNewline < 0) return (raw, "");
            // 다음 줄들 중 스택트레이스로 보이는 패턴 시작 위치 찾기 (UnityEngine. / 메서드 이름 (param) 등)
            // 단순화: 첫 줄을 message로, 나머지를 stack으로
            return (raw.Substring(0, firstNewline).TrimEnd(),
                    raw.Substring(firstNewline + 1).TrimEnd());
        }

        // ── 템플릿화 ──

        static readonly Regex RxLineNum   = new Regex(@"\b[Ll]ine\s+\d+\b", RegexOptions.Compiled);
        static readonly Regex RxAtLine    = new Regex(@":\d+\)?\s*$|\.cs:\d+", RegexOptions.Compiled);
        static readonly Regex RxQuoted    = new Regex(@"""[^""]{0,200}""", RegexOptions.Compiled);
        static readonly Regex RxPath      = new Regex(@"(?:[A-Za-z]:)?[\\/](?:Assets|Library|Users)[\\/][^\s""',)]+", RegexOptions.Compiled);
        static readonly Regex RxParens    = new Regex(@"\(\d+(?:[,/]\d+)?\)", RegexOptions.Compiled);
        static readonly Regex RxNumbers   = new Regex(@"\b\d{2,}\b", RegexOptions.Compiled);
        static readonly Regex RxWhitespace= new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>메시지에서 가변 요소를 치환해 그룹화 키 생성.</summary>
        static string MakeTemplate(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return "";
            string s = msg;
            s = RxLineNum.Replace(s, "Line ?");
            s = RxQuoted.Replace(s, "\"…\"");
            s = RxPath.Replace(s, "/…");
            s = RxParens.Replace(s, "(?)");
            s = RxNumbers.Replace(s, "?");
            s = RxAtLine.Replace(s, "");
            s = RxWhitespace.Replace(s, " ").Trim();
            return Trunc(s, 180);
        }

        // ── 스택 단순화 ──
        static string CompactStack(string stack)
        {
            // 한 줄에 하나씩, 핵심만 (메서드 + 파일:라인)
            if (string.IsNullOrEmpty(stack)) return "";
            var lines = stack.Split('\n');
            var sb = new StringBuilder();
            int kept = 0;
            foreach (var raw in lines)
            {
                var t = raw.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                // Unity 내부 스택 노이즈 제거
                if (t.StartsWith("UnityEditor.") || t.StartsWith("UnityEngine.Debug:")) continue;
                sb.AppendLine("  " + Trunc(t, 200));
                if (++kept >= 6) { sb.AppendLine("  ..."); break; }
            }
            return sb.ToString().TrimEnd();
        }

        static int SeverityOrder(int mode)
        {
            // Unity LogEntry mode flags (간이) — bit 0x100=Error, 0x200=Warning. 정확하진 않지만 정렬에 충분.
            if ((mode & (1 << 17)) != 0) return 0; // Error
            if ((mode & (1 << 9)) != 0) return 0;  // Error alt
            if ((mode & (1 << 1)) != 0) return 0;  // Error alt2
            if ((mode & (1 << 8)) != 0) return 1;  // Warning
            if ((mode & (1 << 0)) != 0) return 1;  // Warning alt
            return 2; // Log
        }

        static string SeverityLabel(int mode)
        {
            int o = SeverityOrder(mode);
            return o == 0 ? "Error" : o == 1 ? "Warning" : "Log";
        }

        static int TotalCount(List<LogGroup> gs)
        {
            int t = 0;
            foreach (var g in gs) t += g.Count;
            return t;
        }

        static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
