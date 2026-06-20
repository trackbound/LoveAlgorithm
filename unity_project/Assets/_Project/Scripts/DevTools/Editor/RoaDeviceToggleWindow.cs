using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 프롤로그 CSV의 로아(ROA) 등장 라인을 한눈에 보고, 등장마다 모바일/PC(오버레이 디바이스)를
    /// 편하게 토글하는 편의 에디터 창 (Tools/Story/ROA 디바이스 전환기).
    ///
    /// 로아 등장 = <c>Char</c> 타입 + <c>Enter</c> 액션 + 캐릭터 토큰이 로아인 라인이며,
    /// 디바이스는 Value의 <c>...:Enter:로아:표정:[디바이스]</c> 마지막 토큰으로 표현된다.
    /// 런타임 파서(<c>RoaDeviceParse</c>)는 <c>pc</c>/<c>모바일</c>/<c>mobile</c>만 인식하므로,
    /// 저장 시 항상 런타임 유효 토큰(<c>모바일</c>/<c>pc</c>)으로 정규화한다 — 과거 미인식 토큰
    /// (<c>Mob</c> 등, 런타임에서 모바일이 안 뜨던 버그)도 저장하면 자동 교정된다.
    /// 저장 전 .backups에 타임스탬프 백업을 남긴다. 에디터 전용(빌드 런타임 무관).
    /// </summary>
    public class RoaDeviceToggleWindow : EditorWindow
    {
        const string CsvPath = "Assets/StreamingAssets/Story/Prologue.csv";

        // 로아 캐릭터 식별 토큰(작가 한글명 + 코드 id). 대소문자 무시.
        static readonly HashSet<string> RoaTokens = new(StringComparer.OrdinalIgnoreCase) { "로아", "roa" };
        // 모바일로 간주하는 토큰(런타임 유효 + 과거 약어). PC는 "pc"만.
        static readonly HashSet<string> MobileTokens = new(StringComparer.OrdinalIgnoreCase) { "모바일", "mobile", "mob" };

        enum Device { Pc, Mobile }

        // 로아 등장 한 건.
        class Appearance
        {
            public int rawIndex;        // _rawLines 내 인덱스(편집 대상)
            public string lineId;       // LineID 컬럼(빈 문자열 가능)
            public string scene;        // 직전 Mark:scene 이름(맥락)
            public string nextLine;     // 다음 Text 라인 미리보기(맥락)
            public string emote;        // 표정 토큰(표시용)
            public string rawDeviceToken; // 파일에 적힌 원본 디바이스 토큰("" = 없음)
            public Device device;       // 현재 선택값
            public bool unrecognized;   // 원본 토큰이 런타임 미인식(저장 시 교정 대상)
        }

        List<string> _rawLines = new();
        string _eol = "\n";
        bool _trailingNewline;
        readonly List<Appearance> _items = new();
        Vector2 _scroll;
        bool _dirty;
        string _status = "";

        [MenuItem("Tools/Story/ROA 디바이스 전환기 (모바일/PC)")]
        public static void Open()
        {
            var win = GetWindow<RoaDeviceToggleWindow>(false, "ROA 디바이스", true);
            win.minSize = new Vector2(560, 460);
            win.Reload();
            win.Show();
        }

        void OnEnable() => Reload();

        // ── CSV 로드 & 로아 등장 추출 ──
        void Reload()
        {
            _items.Clear();
            _dirty = false;
            _status = "";

            var full = Path.GetFullPath(CsvPath);
            if (!File.Exists(full))
            {
                _status = $"CSV를 찾을 수 없음: {CsvPath}";
                _rawLines.Clear();
                return;
            }

            string text = File.ReadAllText(full);
            _eol = text.Contains("\r\n") ? "\r\n" : "\n";
            _trailingNewline = text.EndsWith("\n");
            _rawLines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            if (_trailingNewline && _rawLines.Count > 0 && _rawLines[_rawLines.Count - 1].Length == 0)
                _rawLines.RemoveAt(_rawLines.Count - 1); // split 말미 빈 항목 제거(저장 시 재부착)

            string currentScene = "(시작)";
            for (int i = 0; i < _rawLines.Count; i++)
            {
                var fields = ParseCsvLine(_rawLines[i]);
                if (fields.Count < 4) continue;
                string lineId = fields[0].Trim();
                string type = fields[1].Trim();
                string value = fields[3];

                if (type.Equals("Flow", StringComparison.OrdinalIgnoreCase))
                {
                    string scene = ExtractScene(value);
                    if (scene != null) currentScene = scene;
                    continue;
                }
                if (!type.Equals("Char", StringComparison.OrdinalIgnoreCase)) continue;

                if (!TryParseRoaEnter(value, out string emote, out string deviceToken)) continue;

                var ap = new Appearance
                {
                    rawIndex = i,
                    lineId = lineId,
                    scene = currentScene,
                    emote = emote,
                    rawDeviceToken = deviceToken,
                    nextLine = FindNextDialogue(i),
                };
                bool isMobile = MobileTokens.Contains(deviceToken);
                bool isPc = string.Equals(deviceToken, "pc", StringComparison.OrdinalIgnoreCase);
                ap.device = isMobile ? Device.Mobile : Device.Pc;
                ap.unrecognized = !string.IsNullOrEmpty(deviceToken)
                                  && !isPc
                                  && !string.Equals(deviceToken, "모바일", StringComparison.OrdinalIgnoreCase)
                                  && !string.Equals(deviceToken, "mobile", StringComparison.OrdinalIgnoreCase);
                _items.Add(ap);
            }

            _status = $"로아 등장 {_items.Count}건 로드됨.";
        }

        // ── Value가 '로아 Enter'면 표정/디바이스 토큰 반환 ──
        static bool TryParseRoaEnter(string value, out string emote, out string device)
        {
            emote = ""; device = "";
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split(':');

            // Enter 토큰 위치 탐색(슬롯 L/C/R 선행 허용).
            int ei = -1;
            for (int i = 0; i < parts.Length && i < 2; i++)
                if (parts[i].Trim().Equals("Enter", StringComparison.OrdinalIgnoreCase)) { ei = i; break; }
            if (ei < 0) return false;

            int charIdx = ei + 1;
            if (charIdx >= parts.Length) return false;
            if (!RoaTokens.Contains(parts[charIdx].Trim())) return false;

            if (ei + 2 < parts.Length) emote = parts[ei + 2].Trim();
            if (ei + 3 < parts.Length) device = parts[ei + 3].Trim();
            return true;
        }

        static string ExtractScene(string value)
        {
            // Flow Value 예: "Mark:scene:로아 첫만남 (CG 인트로)"
            const string key = "Mark:scene:";
            int idx = value.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            return value.Substring(idx + key.Length).Trim();
        }

        // 등장 라인 다음의 첫 Text 라인을 '화자: 내용' 미리보기로.
        string FindNextDialogue(int fromRaw)
        {
            for (int i = fromRaw + 1; i < _rawLines.Count && i < fromRaw + 12; i++)
            {
                var f = ParseCsvLine(_rawLines[i]);
                if (f.Count < 4) continue;
                if (!f[1].Trim().Equals("Text", StringComparison.OrdinalIgnoreCase)) continue;
                string speaker = f[2].Trim();
                string body = StripEmotes(f[3].Trim());
                if (body.Length > 42) body = body.Substring(0, 42) + "…";
                return string.IsNullOrEmpty(speaker) ? body : $"{speaker}: {body}";
            }
            return "";
        }

        static string StripEmotes(string s)
        {
            // <emote=.../> 인라인 태그 제거(미리보기 가독성).
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '<')
                {
                    int close = s.IndexOf('>', i);
                    if (close >= 0) { i = close; continue; }
                }
                sb.Append(s[i]);
            }
            return sb.ToString().Replace("\\n", " ").Trim();
        }

        // ── 최소 CSV 파서(따옴표·이스케이프 처리) ──
        static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null) { result.Add(""); return result; }
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        static string QuoteField(string field)
        {
            if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }

        // ── GUI ──
        void OnGUI()
        {
            DrawHeader();
            if (_items.Count == 0)
            {
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(_status) ? "로아 등장 라인이 없습니다." : _status, MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _items.Count; i++) DrawRow(i);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Prologue.csv — 로아 등장 디바이스", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("새로고침", GUILayout.Width(70))) Reload();
            }

            int mob = 0, pc = 0, fix = 0;
            foreach (var a in _items)
            {
                if (a.device == Device.Mobile) mob++; else pc++;
                if (a.unrecognized) fix++;
            }
            EditorGUILayout.LabelField($"총 {_items.Count}건  ·  📱 모바일 {mob}  ·  🖥 PC {pc}", EditorStyles.miniLabel);
            if (fix > 0)
                EditorGUILayout.HelpBox($"런타임 미인식 토큰 {fix}건(예: \"Mob\") — 저장 시 유효 토큰으로 교정됩니다.", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("모두 📱 모바일", GUILayout.Width(110))) SetAll(Device.Mobile);
                if (GUILayout.Button("모두 🖥 PC", GUILayout.Width(110))) SetAll(Device.Pc);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!_dirty))
                {
                    GUI.backgroundColor = _dirty ? new Color(0.6f, 0.9f, 0.6f) : Color.white;
                    if (GUILayout.Button(_dirty ? "저장 *" : "저장", GUILayout.Width(90))) Save();
                    GUI.backgroundColor = Color.white;
                }
            }
            EditorGUILayout.Space(2);
            DrawSeparator();
        }

        void DrawRow(int index)
        {
            var a = _items[index];
            var bg = a.device == Device.Mobile
                ? new Color(0.45f, 0.70f, 0.95f, 0.18f)
                : new Color(0.95f, 0.75f, 0.40f, 0.18f);
            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rect, bg);

            using (new EditorGUILayout.HorizontalScope())
            {
                // 맥락 텍스트
                using (new EditorGUILayout.VerticalScope())
                {
                    string id = string.IsNullOrEmpty(a.lineId) ? "(무번호)" : a.lineId;
                    string emote = string.IsNullOrEmpty(a.emote) ? "" : $"  표정:{a.emote}";
                    EditorGUILayout.LabelField($"#{index + 1}  [{id}]  ⟪{a.scene}⟫{emote}", EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(a.nextLine))
                        EditorGUILayout.LabelField("   ↳ " + a.nextLine, EditorStyles.miniLabel);
                    if (a.unrecognized)
                        EditorGUILayout.LabelField($"   ⚠ 원본 토큰 \"{a.rawDeviceToken}\" — 저장 시 교정", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                // 디바이스 토글 버튼
                DeviceButton(a, Device.Mobile, "📱 모바일");
                DeviceButton(a, Device.Pc, "🖥 PC");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
            DrawSeparator();
        }

        void DeviceButton(Appearance a, Device d, string label)
        {
            bool active = a.device == d;
            GUI.backgroundColor = active
                ? (d == Device.Mobile ? new Color(0.40f, 0.65f, 1f) : new Color(1f, 0.70f, 0.30f))
                : Color.white;
            if (GUILayout.Button(label, GUILayout.Width(90), GUILayout.Height(30)))
            {
                if (a.device != d) { a.device = d; _dirty = true; }
            }
            GUI.backgroundColor = Color.white;
        }

        void DrawSeparator()
        {
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.15f));
        }

        void SetAll(Device d)
        {
            foreach (var a in _items)
                if (a.device != d) { a.device = d; _dirty = true; }
        }

        // ── 저장: 디바이스 토큰을 런타임 유효 토큰으로 정규화해 기록 ──
        void Save()
        {
            if (!_dirty) return;
            try
            {
                foreach (var a in _items)
                {
                    string token = a.device == Device.Mobile ? "모바일" : "pc";
                    _rawLines[a.rawIndex] = ReplaceDeviceToken(_rawLines[a.rawIndex], token);
                }

                var full = Path.GetFullPath(CsvPath);
                WriteBackup(full);

                var sb = new StringBuilder();
                for (int i = 0; i < _rawLines.Count; i++)
                {
                    sb.Append(_rawLines[i]);
                    if (i < _rawLines.Count - 1 || _trailingNewline) sb.Append(_eol);
                }
                File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(CsvPath);

                _dirty = false;
                Reload();
                _status = $"저장 완료 — {_items.Count}건 기록(백업 생성).";
                ShowNotification(new GUIContent("저장 완료"));
            }
            catch (Exception e)
            {
                _status = "저장 실패: " + e.Message;
                Debug.LogError($"[RoaDeviceToggleWindow] 저장 실패: {e}");
            }
        }

        // 한 raw 라인의 로아 Enter Value에서 디바이스 토큰을 newToken으로 교체(없으면 추가).
        static string ReplaceDeviceToken(string rawLine, string newToken)
        {
            var fields = ParseCsvLine(rawLine);
            if (fields.Count < 4) return rawLine;
            string value = fields[3];
            var parts = new List<string>(value.Split(':'));

            int ei = -1;
            for (int i = 0; i < parts.Count && i < 2; i++)
                if (parts[i].Trim().Equals("Enter", StringComparison.OrdinalIgnoreCase)) { ei = i; break; }
            if (ei < 0) return rawLine;

            int emoteIdx = ei + 2;
            int devIdx = ei + 3;
            // 디바이스는 표정 뒤 위치 — 표정이 없으면 기본 표정을 채워 자리를 맞춘다.
            while (parts.Count <= emoteIdx) parts.Add("기본");
            if (parts.Count <= devIdx) parts.Add(newToken);
            else parts[devIdx] = newToken;

            fields[3] = string.Join(":", parts);

            var rebuilt = new StringBuilder();
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) rebuilt.Append(',');
                rebuilt.Append(QuoteField(fields[i]));
            }
            return rebuilt.ToString();
        }

        static void WriteBackup(string fullCsvPath)
        {
            try
            {
                string dir = Path.Combine(Path.GetDirectoryName(fullCsvPath), ".backups");
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string dest = Path.Combine(dir, $"Prologue.{stamp}.csv");
                File.Copy(fullCsvPath, dest, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RoaDeviceToggleWindow] 백업 실패(저장은 진행): {e.Message}");
            }
        }
    }
}
