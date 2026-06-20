using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 프롤로그 CSV의 배경 변경을 한눈에 보고 편집하는 편의 에디터 창 (Tools/Story/배경 전환 헬퍼).
    /// 문서 순서대로 두 종류를 함께 나열한다:
    ///   · <c>Type=BG</c> 전환 라인 — 전환할 배경 + 전환 종류(Cut/Fade/Cross) 편집.
    ///   · <c>Type=FX</c>의 <c>Setup:BG=…</c> 초기 배경(씬 시작, 즉시) — 배경키만 편집.
    ///
    /// BG Value 문법은 런타임 파서(<c>StageParser.ParseBackground</c>)와 동일: <c>배경키[:Cut|Fade|Cross
    /// [:duration]]</c>(전환 생략 시 Cross). Setup은 <c>SetupMacroParser</c> 규칙(파이프 구분 BG=값)을 따른다.
    /// 카탈로그는 <c>Resources/BG/*.png</c>(컨벤션 로딩 경로)에서 수집해 드롭다운으로 제시하며, 카탈로그에
    /// 없는 키(직접 입력값)도 보존한다. 변경된 라인만 다시 써 저장하므로 — BG는 Value 재구성(암시적 Cross·
    /// duration 보존), Setup은 BG=토큰 값만 교체(BGM·Char 등 나머지 토큰 보존) — 손대지 않은 라인은 원본
    /// 그대로 남아 diff 노이즈가 없다. 저장 전 .backups에 타임스탬프 백업을 남긴다. 에디터 전용(빌드 무관).
    /// </summary>
    public class BgTransitionWindow : EditorWindow
    {
        const string CsvPath = "Assets/StreamingAssets/Story/Prologue.csv";
        const string BgDir = "Assets/Resources/BG";

        // 전환 종류(런타임 BgTransition 1:1 미러 — 이 도구는 런타임 asmdef 비의존으로 자립).
        enum Transition { Cut, Fade, Cross }

        // 라인 종류: BG=명시적 배경 전환 / Setup=FX 매크로의 초기 배경(즉시, 전환 없음).
        enum RowKind { Bg, Setup }

        // 배경 변경 한 건(BG 전환 라인 또는 FX Setup 초기 배경).
        class BgRow
        {
            public RowKind kind;          // BG 전환인가, Setup 초기 배경인가
            public int rawIndex;          // _rawLines 내 인덱스(편집 대상)
            public string lineId;         // LineID 컬럼(빈 문자열 가능)
            public string scene;          // 직전 Mark:scene 이름(맥락)
            public string nextLine;       // 다음 Text 라인 미리보기(맥락)
            public string origValue;      // 원본 Value 필드(Setup 재기록 시 나머지 토큰 보존용)

            public string origName;       // 파일에 적힌 원본 배경키
            public Transition origTransition; // 파일 기준 전환(토큰 없으면 Cross; Setup은 무의미)
            public bool hadTransitionToken;   // 원본에 전환 토큰이 명시돼 있었나(BG 전용)
            public string durationToken;  // 보존할 duration 토큰("" = 없음; BG 전용)

            public string name;           // 현재 선택 배경키
            public Transition transition; // 현재 선택 전환(BG 전용)

            // Setup은 전환이 없으므로 배경키 변경만 dirty로 본다.
            public bool Changed => name != origName
                || (kind == RowKind.Bg && transition != origTransition);
        }

        List<string> _rawLines = new();
        string _eol = "\n";
        bool _trailingNewline;
        readonly List<BgRow> _items = new();
        List<string> _catalog = new();                // Resources/BG 배경키 목록(정렬)
        readonly Dictionary<string, Texture2D> _thumbs = new();
        Vector2 _scroll;
        bool _dirty;
        string _status = "";

        [MenuItem("Tools/Story/배경 전환 헬퍼")]
        public static void Open()
        {
            var win = GetWindow<BgTransitionWindow>(false, "배경 전환", true);
            win.minSize = new Vector2(620, 480);
            win.Reload();
            win.Show();
        }

        void OnEnable() => Reload();

        // ── CSV 로드 & BG 전환 추출 ──
        void Reload()
        {
            _items.Clear();
            _thumbs.Clear();
            _dirty = false;
            _status = "";

            LoadCatalog();

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

                if (type.Equals("BG", StringComparison.OrdinalIgnoreCase))
                {
                    ParseBg(value, out string name, out Transition tr, out bool hadToken, out string durTok);
                    if (string.IsNullOrEmpty(name)) continue; // 빈 배경키는 건너뜀
                    _items.Add(new BgRow
                    {
                        kind = RowKind.Bg,
                        rawIndex = i,
                        lineId = lineId,
                        scene = currentScene,
                        nextLine = FindNextDialogue(i),
                        origValue = value,
                        origName = name,
                        origTransition = tr,
                        hadTransitionToken = hadToken,
                        durationToken = durTok,
                        name = name,
                        transition = tr,
                    });
                }
                else if (type.Equals("FX", StringComparison.OrdinalIgnoreCase)
                         && TryParseSetupBg(value, out string setupBg))
                {
                    _items.Add(new BgRow
                    {
                        kind = RowKind.Setup,
                        rawIndex = i,
                        lineId = lineId,
                        scene = currentScene,
                        nextLine = FindNextDialogue(i),
                        origValue = value,
                        origName = setupBg,
                        origTransition = Transition.Cut, // 즉시 — 표시상 의미만
                        hadTransitionToken = false,
                        durationToken = "",
                        name = setupBg,
                        transition = Transition.Cut,
                    });
                }
            }

            int bgN = 0, setupN = 0;
            foreach (var r in _items) { if (r.kind == RowKind.Setup) setupN++; else bgN++; }
            _status = $"배경 전환 {bgN}건 · 초기 배경 {setupN}건 로드됨.";
        }

        // Resources/BG/*.png → 배경키 카탈로그(확장자 제거, 정렬).
        void LoadCatalog()
        {
            _catalog = new List<string>();
            var dir = Path.GetFullPath(BgDir);
            if (!Directory.Exists(dir)) return;
            foreach (var png in Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly))
                _catalog.Add(Path.GetFileNameWithoutExtension(png));
            _catalog.Sort(StringComparer.OrdinalIgnoreCase);
        }

        // BG Value 파싱(StageParser.ParseBackground 규칙 미러). 전환 토큰 없으면 Cross.
        static void ParseBg(string value, out string name, out Transition tr, out bool hadToken, out string durationToken)
        {
            name = ""; tr = Transition.Cross; hadToken = false; durationToken = "";
            if (string.IsNullOrEmpty(value)) return;
            var parts = value.Split(':');
            name = parts[0].Trim();
            if (parts.Length >= 2)
            {
                hadToken = true;
                tr = ParseTransition(parts[1]);
            }
            if (parts.Length >= 3) durationToken = parts[2].Trim();
        }

        // FX Value가 Setup이고 BG=토큰을 가지면 그 배경키 반환(SetupMacroParser 규칙 미러).
        static bool TryParseSetupBg(string value, out string name)
        {
            name = "";
            if (string.IsNullOrEmpty(value)) return false;
            int ci = value.IndexOf(':');
            string head = (ci >= 0 ? value.Substring(0, ci) : value).Trim();
            if (!head.Equals("Setup", StringComparison.OrdinalIgnoreCase)) return false;
            string body = ci >= 0 ? value.Substring(ci + 1) : "";
            foreach (var seg in body.Split('|'))
            {
                int eq = seg.IndexOf('=');
                if (eq < 0) continue;
                if (!seg.Substring(0, eq).Trim().Equals("BG", StringComparison.OrdinalIgnoreCase)) continue;
                string val = seg.Substring(eq + 1).Trim();
                if (val.Length == 0) return false; // 빈 BG는 편집 대상 아님
                name = val;
                return true;
            }
            return false;
        }

        // Setup Value의 BG=토큰 값만 newName으로 교체(나머지 토큰·순서·헤더 보존).
        static string RewriteSetupBg(string value, string newName)
        {
            int ci = value.IndexOf(':');
            if (ci < 0) return value;
            string head = value.Substring(0, ci);
            var segs = new List<string>(value.Substring(ci + 1).Split('|'));
            for (int i = 0; i < segs.Count; i++)
            {
                int eq = segs[i].IndexOf('=');
                if (eq < 0) continue;
                if (!segs[i].Substring(0, eq).Trim().Equals("BG", StringComparison.OrdinalIgnoreCase)) continue;
                segs[i] = segs[i].Substring(0, eq + 1) + newName; // 키 텍스트("BG=") 보존, 값만 교체
                break;
            }
            return head + ":" + string.Join("|", segs);
        }

        static Transition ParseTransition(string s)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "cut": return Transition.Cut;
                case "fade": return Transition.Fade;
                case "cross":
                case "crossfade": return Transition.Cross;
                default: return Transition.Cross; // 미지정/오타 → Cross(런타임 기본과 동일)
            }
        }

        static string TransitionToken(Transition t)
        {
            switch (t)
            {
                case Transition.Cut: return "Cut";
                case Transition.Fade: return "Fade";
                default: return "Cross";
            }
        }

        static string ExtractScene(string value)
        {
            const string key = "Mark:scene:";
            int idx = value.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            return value.Substring(idx + key.Length).Trim();
        }

        // 전환 라인 다음의 첫 Text 라인을 '화자: 내용' 미리보기로.
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

        Texture2D Thumb(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_thumbs.TryGetValue(name, out var t)) return t;
            t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{BgDir}/{name}.png");
            _thumbs[name] = t;
            return t;
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
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(_status) ? "BG 전환 라인이 없습니다." : _status, MessageType.Info);
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
                EditorGUILayout.LabelField("Prologue.csv — 배경 전환", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("새로고침", GUILayout.Width(70))) Reload();
            }

            int cut = 0, fade = 0, cross = 0, setup = 0;
            foreach (var r in _items)
            {
                if (r.kind == RowKind.Setup) { setup++; continue; }
                if (r.transition == Transition.Cut) cut++;
                else if (r.transition == Transition.Fade) fade++;
                else cross++;
            }
            EditorGUILayout.LabelField(
                $"전환 ✂ Cut {cut} · 🌑 Fade {fade} · 🔀 Cross {cross}   |   ⚡ 초기 {setup}",
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
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
            var r = _items[index];
            var bg = r.kind == RowKind.Setup
                ? new Color(0.85f, 0.70f, 0.30f, r.Changed ? 0.26f : 0.12f)
                : TransitionTint(r.transition, r.Changed);
            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rect, bg);

            using (new EditorGUILayout.HorizontalScope())
            {
                // 배경 썸네일
                var thumb = Thumb(r.name);
                var trect = GUILayoutUtility.GetRect(64, 36, GUILayout.Width(64), GUILayout.Height(36));
                if (thumb != null) GUI.DrawTexture(trect, thumb, ScaleMode.ScaleAndCrop);
                else EditorGUI.DrawRect(trect, new Color(0f, 0f, 0f, 0.25f));

                // 맥락 + 배경 드롭다운
                using (new EditorGUILayout.VerticalScope())
                {
                    string id = string.IsNullOrEmpty(r.lineId) ? "(무번호)" : r.lineId;
                    string mark = r.Changed ? "  *" : "";
                    string tag = r.kind == RowKind.Setup ? "⚡초기 " : "";
                    EditorGUILayout.LabelField($"#{index + 1}  {tag}[{id}]  ⟪{r.scene}⟫{mark}", EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(r.nextLine))
                        EditorGUILayout.LabelField("   ↳ " + r.nextLine, EditorStyles.miniLabel);
                    DrawBgDropdown(r);
                }

                GUILayout.FlexibleSpace();

                // 전환 토글 버튼 (Setup 초기 배경은 전환이 없음)
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(204)))
                {
                    if (r.kind == RowKind.Setup)
                    {
                        EditorGUILayout.LabelField("⚡ 초기 배경 (즉시)", EditorStyles.miniBoldLabel);
                    }
                    else
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            TransitionButton(r, Transition.Cut, "✂ Cut");
                            TransitionButton(r, Transition.Fade, "🌑 Fade");
                            TransitionButton(r, Transition.Cross, "🔀 Cross");
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
            DrawSeparator();
        }

        // 배경 드롭다운: 카탈로그 + (카탈로그에 없는) 현재값 보존.
        void DrawBgDropdown(BgRow r)
        {
            var choices = new List<string>(_catalog);
            int sel = choices.FindIndex(c => string.Equals(c, r.name, StringComparison.OrdinalIgnoreCase));
            if (sel < 0)
            {
                choices.Insert(0, r.name + "  (카탈로그 외)");
                sel = 0;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("배경", GUILayout.Width(34));
                int next = EditorGUILayout.Popup(sel, choices.ToArray(), GUILayout.Width(220));
                if (next != sel)
                {
                    // '(카탈로그 외)' 가짜 항목은 무시하고 실제 카탈로그 배경키만 반영.
                    string resolved = ResolvePick(choices, next);
                    if (resolved != null && resolved != r.name)
                    {
                        r.name = resolved;
                        RecomputeDirty();
                    }
                }
            }
        }

        // 드롭다운 선택 인덱스를 실제 배경키로 해석('(카탈로그 외)' 가짜 항목은 무시).
        static string ResolvePick(List<string> choices, int idx)
        {
            if (idx < 0 || idx >= choices.Count) return null;
            string c = choices[idx];
            if (c.EndsWith("(카탈로그 외)")) return null;
            return c;
        }

        void TransitionButton(BgRow r, Transition t, string label)
        {
            bool active = r.transition == t;
            GUI.backgroundColor = active ? TransitionColor(t) : Color.white;
            if (GUILayout.Button(label, GUILayout.Width(64), GUILayout.Height(28)))
            {
                if (r.transition != t) { r.transition = t; RecomputeDirty(); }
            }
            GUI.backgroundColor = Color.white;
        }

        static Color TransitionColor(Transition t)
        {
            switch (t)
            {
                case Transition.Cut: return new Color(0.85f, 0.85f, 0.85f);
                case Transition.Fade: return new Color(0.55f, 0.55f, 0.70f);
                default: return new Color(0.45f, 0.80f, 0.55f);
            }
        }

        static Color TransitionTint(Transition t, bool changed)
        {
            float a = changed ? 0.26f : 0.12f;
            switch (t)
            {
                case Transition.Cut: return new Color(0.7f, 0.7f, 0.7f, a);
                case Transition.Fade: return new Color(0.45f, 0.45f, 0.80f, a);
                default: return new Color(0.40f, 0.80f, 0.50f, a);
            }
        }

        void DrawSeparator()
        {
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.15f));
        }

        void RecomputeDirty()
        {
            _dirty = false;
            foreach (var r in _items) if (r.Changed) { _dirty = true; break; }
        }

        // ── 저장: 변경된 BG 라인만 Value를 다시 써서 기록 ──
        void Save()
        {
            if (!_dirty) return;
            try
            {
                int changed = 0;
                foreach (var r in _items)
                {
                    if (!r.Changed) continue;
                    string newValue = r.kind == RowKind.Setup
                        ? RewriteSetupBg(r.origValue, r.name)
                        : BuildValue(r);
                    _rawLines[r.rawIndex] = RewriteValue(_rawLines[r.rawIndex], newValue);
                    changed++;
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
                _status = $"저장 완료 — {changed}건 변경 기록(백업 생성).";
                ShowNotification(new GUIContent("저장 완료"));
            }
            catch (Exception e)
            {
                _status = "저장 실패: " + e.Message;
                Debug.LogError($"[BgTransitionWindow] 저장 실패: {e}");
            }
        }

        // 현재 선택값으로 BG Value 문자열 구성. 암시적 Cross(원본 토큰 없고 Cross 유지)는 토큰을 붙이지 않아
        // 원본 스타일을 보존. duration 토큰이 있으면 전환 토큰과 함께 보존한다.
        static string BuildValue(BgRow r)
        {
            string v = r.name;
            bool hasDuration = !string.IsNullOrEmpty(r.durationToken);
            bool needTransition = r.transition != Transition.Cross || r.hadTransitionToken || hasDuration;
            if (needTransition) v += ":" + TransitionToken(r.transition);
            if (hasDuration) v += ":" + r.durationToken;
            return v;
        }

        // 한 raw 라인의 Value(4번째 필드)를 newValue로 교체.
        static string RewriteValue(string rawLine, string newValue)
        {
            var fields = ParseCsvLine(rawLine);
            if (fields.Count < 4) return rawLine;
            fields[3] = newValue;

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
                Debug.LogWarning($"[BgTransitionWindow] 백업 실패(저장은 진행): {e.Message}");
            }
        }
    }
}
