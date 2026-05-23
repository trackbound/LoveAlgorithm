using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 시나리오 CSV 편집 변경 이력을 JSONL로 기록.
    /// 한 줄 = 하나의 변경 이벤트 — 개발자가 cat/git diff로 쉽게 추적, 향후 도구 파싱도 용이.
    ///
    /// 경로: {StreamingAssets/Story}/.changelog/{scriptName}.jsonl
    ///
    /// 이벤트 형식:
    ///   {"ts":"2026-05-23T14:32:18Z","file":"Prologue","op":"modify",
    ///    "lineIndex":47,"lineId":"L047",
    ///    "before":{"Type":"Text","Speaker":"다은","Value":"안녕!"},
    ///    "after": {"Type":"Text","Speaker":"다은","Value":"안녕 ㅎㅎ"},
    ///    "who":"chris"}
    ///
    /// op: "insert" / "delete" / "modify" / "asset_request"
    /// </summary>
    public static class ChangelogWriter
    {
        const string LogSubdir = ".changelog";

        public static string LogDir => Path.Combine(StoryAssetLoader.StoryDir, LogSubdir);
        public static string GetLogPath(string scriptName)
            => Path.Combine(LogDir, scriptName + ".jsonl");

        /// <summary>편집자 이름 (PlayerPrefs > Environment.UserName).</summary>
        public static string Author
        {
            get
            {
                string pref = PlayerPrefs.GetString("ScenarioEditor.Author", "");
                if (!string.IsNullOrEmpty(pref)) return pref;
                try { return Environment.UserName; } catch { return "unknown"; }
            }
            set
            {
                PlayerPrefs.SetString("ScenarioEditor.Author", value ?? "");
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 두 라인 리스트의 diff를 계산해 changelog에 추가.
        /// 매칭: LineID 우선, 같은 인덱스 폴백.
        /// </summary>
        public static void AppendDiff(string scriptName, IList<ScriptLine> before, IList<ScriptLine> after)
        {
            if (string.IsNullOrEmpty(scriptName)) return;
            if (!StoryAssetLoader.IsWritable) return;

            var events = ComputeDiff(scriptName, before, after, Author);
            if (events.Count == 0) return;

            try
            {
                Directory.CreateDirectory(LogDir);
                string path = GetLogPath(scriptName);
                var sb = new StringBuilder();
                foreach (var ev in events)
                {
                    sb.Append(JsonUtility.ToJson(ev));
                    sb.Append('\n');
                }
                File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
                Debug.Log($"[Changelog] +{events.Count} events → {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Changelog] write fail: {e.Message}");
            }
        }

        /// <summary>에셋 요청 이벤트 (위젯의 📋 에셋 요청 버튼).</summary>
        public static void AppendAssetRequest(string scriptName, string category, string requestedName, string note)
        {
            if (!StoryAssetLoader.IsWritable) return;
            var ev = new ChangeEvent
            {
                ts = DateTime.UtcNow.ToString("o"),
                file = scriptName ?? "",
                op = "asset_request",
                lineIndex = -1,
                lineId = "",
                beforeJson = "",
                afterJson = $"{{\"category\":\"{Esc(category)}\",\"name\":\"{Esc(requestedName)}\",\"note\":\"{Esc(note)}\"}}",
                who = Author,
            };
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(GetLogPath(scriptName), JsonUtility.ToJson(ev) + "\n", new UTF8Encoding(false));
                Debug.Log($"[Changelog] asset request: {category}/{requestedName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Changelog] asset request fail: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════
        //  내부: diff 계산
        // ══════════════════════════════════════════════

        static List<ChangeEvent> ComputeDiff(string scriptName, IList<ScriptLine> before, IList<ScriptLine> after, string who)
        {
            var events = new List<ChangeEvent>();
            string ts = DateTime.UtcNow.ToString("o");

            // 1) 라인 ID로 매칭 — ID 있는 라인은 ID 우선
            var beforeById = new Dictionary<string, (int index, ScriptLine line)>();
            for (int i = 0; i < (before?.Count ?? 0); i++)
            {
                var l = before[i];
                if (!string.IsNullOrEmpty(l.LineID))
                    beforeById[l.LineID] = (i, l);
            }
            var afterById = new Dictionary<string, (int index, ScriptLine line)>();
            for (int i = 0; i < (after?.Count ?? 0); i++)
            {
                var l = after[i];
                if (!string.IsNullOrEmpty(l.LineID))
                    afterById[l.LineID] = (i, l);
            }

            // 2) ID 기반 modify / delete
            foreach (var kv in beforeById)
            {
                if (afterById.TryGetValue(kv.Key, out var afterEntry))
                {
                    if (!LinesEqual(kv.Value.line, afterEntry.line))
                    {
                        events.Add(MakeModifyEvent(scriptName, ts, who, afterEntry.index, kv.Key, kv.Value.line, afterEntry.line));
                    }
                }
                else
                {
                    events.Add(MakeDeleteEvent(scriptName, ts, who, kv.Value.index, kv.Key, kv.Value.line));
                }
            }

            // 3) ID 기반 insert
            foreach (var kv in afterById)
            {
                if (!beforeById.ContainsKey(kv.Key))
                {
                    events.Add(MakeInsertEvent(scriptName, ts, who, kv.Value.index, kv.Key, kv.Value.line));
                }
            }

            // 4) ID 없는 라인은 인덱스 기반 단순 비교 — 보수적으로 길이 차이만 기록
            int beforeNoId = (before?.Count ?? 0) - beforeById.Count;
            int afterNoId  = (after?.Count  ?? 0) - afterById.Count;
            if (beforeNoId != afterNoId)
            {
                int delta = afterNoId - beforeNoId;
                events.Add(new ChangeEvent
                {
                    ts = ts, file = scriptName, op = delta > 0 ? "insert" : "delete",
                    lineIndex = -1, lineId = "(noId)", beforeJson = "", afterJson = $"{{\"countDelta\":{delta}}}", who = who
                });
            }

            return events;
        }

        static bool LinesEqual(ScriptLine a, ScriptLine b)
        {
            if (a == null || b == null) return a == b;
            return a.LineID == b.LineID
                && a.Type == b.Type
                && (a.Speaker ?? "") == (b.Speaker ?? "")
                && (a.Value ?? "") == (b.Value ?? "")
                && a.NextType == b.NextType
                && Mathf.Abs(a.DelaySeconds - b.DelaySeconds) < 0.001f;
        }

        static ChangeEvent MakeModifyEvent(string file, string ts, string who, int index, string lineId, ScriptLine before, ScriptLine after)
            => new ChangeEvent
            {
                ts = ts, file = file, op = "modify",
                lineIndex = index, lineId = lineId,
                beforeJson = LineToJson(before),
                afterJson = LineToJson(after),
                who = who,
            };

        static ChangeEvent MakeInsertEvent(string file, string ts, string who, int index, string lineId, ScriptLine after)
            => new ChangeEvent
            {
                ts = ts, file = file, op = "insert",
                lineIndex = index, lineId = lineId,
                beforeJson = "",
                afterJson = LineToJson(after),
                who = who,
            };

        static ChangeEvent MakeDeleteEvent(string file, string ts, string who, int index, string lineId, ScriptLine before)
            => new ChangeEvent
            {
                ts = ts, file = file, op = "delete",
                lineIndex = index, lineId = lineId,
                beforeJson = LineToJson(before),
                afterJson = "",
                who = who,
            };

        static string LineToJson(ScriptLine l)
        {
            if (l == null) return "{}";
            // 수동 JSON — JsonUtility는 중첩 객체 처리 어색해서 5필드만 직렬화
            var sb = new StringBuilder(80);
            sb.Append('{');
            sb.Append("\"type\":\"").Append(l.Type).Append("\",");
            sb.Append("\"speaker\":\"").Append(Esc(l.Speaker)).Append("\",");
            sb.Append("\"value\":\"").Append(Esc(l.Value)).Append("\",");
            sb.Append("\"next\":\"").Append(l.NextType);
            if (l.NextType == NextType.Delay) sb.Append('(').Append(l.DelaySeconds.ToString("0.##",
                System.Globalization.CultureInfo.InvariantCulture)).Append(')');
            sb.Append("\"}");
            return sb.ToString();
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        // JsonUtility 호환용 직렬화 객체
        [Serializable]
        public class ChangeEvent
        {
            public string ts;
            public string file;
            public string op;
            public int lineIndex;
            public string lineId;
            public string beforeJson;   // raw JSON string (LineToJson 결과)
            public string afterJson;
            public string who;
        }
    }
}
