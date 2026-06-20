using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Events; // CharIntent, CharAction, CharSlot, EmoteTarget, InlineEmote
using LoveAlgo.Story;  // StageParser, InlineTagParser, ParsedDialogue, ResourceAliasCatalogSO

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 스토리 CSV가 참조하는 캐릭터 표정 중, 실제 스프라이트 파일이 없는 것을 한눈에 잡는 검사 창
    /// (Tools/Story/표정 누락 검사). 런타임은 누락 표정을 '기본'으로 폴백하고 dev 토스트만 띄우므로
    /// 플레이로 그 라인을 지나가기 전엔 작가가 모를 수 있다 — 이 창은 모든 CSV를 편집 시점에 통째 스캔해
    /// 누락을 캐릭터별로 모아 보여준다(구 emote_audit.txt의 에디터 툴 승격).
    ///
    /// 재사용(재발명 금지): 캐릭터/표정 파싱은 런타임과 동일한 순수 파서 <see cref="StageParser"/>·
    /// <see cref="InlineTagParser"/>로, 한글명/약어→코드 id 해석은 <see cref="ResourceAliasCatalogSO"/>로.
    /// 존재 판정은 Resources/Characters/{id}/ 폴더의 실제 파일 목록과 대조. 에디터 전용.
    /// </summary>
    public class EmoteAuditWindow : EditorWindow
    {
        const string CharRoot = "Assets/Resources/Characters";
        const string CatalogResourcePath = "Data/ResourceAliasCatalog";
        static readonly string[] StoryRoots = { "Assets/StreamingAssets/Story", "Assets/Resources/Story" };
        static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".psd", ".asset", ".tga" };

        // 한 누락 사용처(어느 CSV·어느 라인).
        struct Usage
        {
            public string csv;     // 파일명
            public int line;       // 파일 내 라인 번호(1-base)
            public string lineId;  // LineID 컬럼
            public string rawEmote; // CSV 원문 표정 토큰(약어일 수 있음)
            public string source;  // "Enter" / "Emote" / "<emote>"
        }

        // 캐릭터+표정 단위 누락 묶음.
        class Miss
        {
            public string charId;
            public string emoteId;
            public readonly List<Usage> usages = new();
        }

        ResourceAliasCatalogSO _catalog;
        string _defaultEmote = "기본";
        Dictionary<string, HashSet<string>> _folders = new(StringComparer.OrdinalIgnoreCase); // charId → 보유 표정 id
        List<Miss> _misses = new();
        int _csvCount, _usageCount;
        string _status = "";
        Vector2 _scroll;
        readonly HashSet<string> _expanded = new();

        [MenuItem("Tools/Story/표정 누락 검사 (Emote Audit)")]
        public static void Open()
        {
            var win = GetWindow<EmoteAuditWindow>(false, "표정 누락", true);
            win.minSize = new Vector2(560, 480);
            win.Scan();
            win.Show();
        }

        void OnEnable() => Scan();

        // ── 스캔: 폴더 표정 목록 + CSV 참조 대조 ──
        void Scan()
        {
            _misses.Clear();
            _expanded.Clear();
            _csvCount = 0;
            _usageCount = 0;

            _catalog = Resources.Load<ResourceAliasCatalogSO>(CatalogResourcePath);
            if (_catalog != null && !string.IsNullOrEmpty(_catalog.DefaultEmote)) _defaultEmote = _catalog.DefaultEmote;

            LoadFolders();
            if (_folders.Count == 0)
            {
                _status = $"캐릭터 폴더를 찾지 못함: {CharRoot}";
                return;
            }

            var byKey = new Dictionary<string, Miss>();
            foreach (var csv in EnumerateStoryCsvs())
            {
                _csvCount++;
                ScanCsv(csv, byKey);
            }

            _misses = byKey.Values
                .OrderBy(m => m.charId, StringComparer.Ordinal)
                .ThenBy(m => m.emoteId, StringComparer.Ordinal)
                .ToList();
            _usageCount = _misses.Sum(m => m.usages.Count);
            _status = _misses.Count == 0
                ? $"누락 없음 ✓  (CSV {_csvCount}개 스캔)"
                : $"누락 {_misses.Count}종 · 사용 {_usageCount}회  (CSV {_csvCount}개 스캔)";
        }

        void LoadFolders()
        {
            _folders.Clear();
            var root = Path.GetFullPath(CharRoot);
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root))
            {
                string id = Path.GetFileName(dir);
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var file in Directory.GetFiles(dir))
                {
                    string ext = Path.GetExtension(file);
                    if (!ImageExt.Contains(ext)) continue;
                    set.Add(Path.GetFileNameWithoutExtension(file));
                }
                _folders[id] = set;
            }
        }

        static IEnumerable<string> EnumerateStoryCsvs()
        {
            foreach (var rootRel in StoryRoots)
            {
                var root = Path.GetFullPath(rootRel);
                if (!Directory.Exists(root)) continue;
                foreach (var f in Directory.GetFiles(root, "*.csv", SearchOption.AllDirectories))
                {
                    string norm = f.Replace('\\', '/');
                    if (norm.Contains("/.backups/") || norm.Contains("/.changelog/")) continue;
                    yield return f;
                }
            }
        }

        // ── 한 CSV 스캔: 슬롯/화자 추적하며 표정 참조를 모은다 ──
        void ScanCsv(string fullPath, Dictionary<string, Miss> byKey)
        {
            string csvName = Path.GetFileName(fullPath);
            string[] lines;
            try { lines = File.ReadAllLines(fullPath); }
            catch { return; }

            var slotChar = new string[3]; // L/C/R 현재 캐릭터 id(해석됨)
            string lastSpeaker = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var f = ParseCsvLine(lines[i]);
                if (f.Count < 4) continue;
                string lineId = f[0].Trim();
                string type = f[1].Trim();
                string speaker = f[2].Trim();
                string value = f[3];
                int lineNo = i + 1;

                if (type.Equals("Char", StringComparison.OrdinalIgnoreCase))
                {
                    var intent = StageParser.ParseCharacter(value);
                    int slot = (int)intent.Slot;
                    switch (intent.Action)
                    {
                        case CharAction.Enter:
                            if (string.IsNullOrEmpty(intent.Character)) break;
                            string entId = ResolveChar(intent.Character);
                            slotChar[slot] = entId;
                            string entEmote = string.IsNullOrEmpty(intent.Emote) ? _defaultEmote : intent.Emote;
                            Consider(byKey, entId, intent.Character, entEmote, csvName, lineNo, lineId, "Enter");
                            break;
                        case CharAction.Emote:
                            string tgtRaw = null, tgtId = null;
                            switch (intent.Target)
                            {
                                case EmoteTarget.Character: tgtRaw = intent.Character; tgtId = ResolveChar(intent.Character); break;
                                case EmoteTarget.Slot: tgtId = slotChar[slot]; tgtRaw = tgtId; break;
                                case EmoteTarget.LastSpeaker: tgtRaw = lastSpeaker; tgtId = ResolveChar(lastSpeaker); break;
                            }
                            Consider(byKey, tgtId, tgtRaw, intent.Emote, csvName, lineNo, lineId, "Emote");
                            break;
                        case CharAction.Exit:
                        case CharAction.Clear:
                            slotChar[slot] = null;
                            break;
                    }
                    continue;
                }

                if (type.Equals("Text", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(speaker)) lastSpeaker = speaker;
                    ParsedDialogue parsed;
                    try { parsed = InlineTagParser.Parse(value); }
                    catch { continue; }
                    if (parsed.Emotes == null) continue;
                    foreach (var e in parsed.Emotes)
                    {
                        string tgtRaw = string.IsNullOrEmpty(e.Target) ? speaker : e.Target;
                        if (string.IsNullOrEmpty(tgtRaw)) continue; // 나레이션 등 화자 없는 인라인 표정 — 귀속 불가, 건너뜀
                        Consider(byKey, ResolveChar(tgtRaw), tgtRaw, e.Emote, csvName, lineNo, lineId, "<emote>");
                    }
                }
            }
        }

        // 참조 한 건을 검사해 누락이면 byKey에 적재.
        void Consider(Dictionary<string, Miss> byKey, string charId, string rawChar, string rawEmote,
                      string csv, int lineNo, string lineId, string source)
        {
            if (string.IsNullOrEmpty(charId) || string.IsNullOrEmpty(rawEmote)) return;
            if (!_folders.TryGetValue(charId, out var owned)) return; // {{Player}}/나레이션/미등록 캐릭터 — 스프라이트 대상 아님

            string emoteId = ResolveEmote(rawEmote);
            if (string.IsNullOrEmpty(emoteId) || owned.Contains(emoteId)) return; // 존재 → 정상

            string key = charId + " " + emoteId;
            if (!byKey.TryGetValue(key, out var miss))
            {
                miss = new Miss { charId = charId, emoteId = emoteId };
                byKey[key] = miss;
            }
            miss.usages.Add(new Usage { csv = csv, line = lineNo, lineId = lineId, rawEmote = rawEmote, source = source });
        }

        string ResolveChar(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string n = name.Trim();
            return _catalog != null ? _catalog.ResolveCharacter(n) : n;
        }

        string ResolveEmote(string name)
        {
            if (string.IsNullOrEmpty(name)) return _defaultEmote;
            string n = name.Trim();
            return _catalog != null ? _catalog.ResolveEmote(n) : n;
        }

        // ── 최소 CSV 파서(따옴표·이스케이프) ──
        static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null) { result.Add(""); return result; }
            var sb = new StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (q)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else q = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') q = true;
                    else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        // ── GUI ──
        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("스토리 CSV 표정 누락 검사", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("새로고침", GUILayout.Width(70))) Scan();
                using (new EditorGUI.DisabledScope(_misses.Count == 0))
                    if (GUILayout.Button("리포트 복사", GUILayout.Width(84))) CopyReport();
            }
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("누락 = 그 캐릭터 폴더에 해당 표정 파일이 없음(런타임은 기본으로 폴백). 별칭은 코드 id로 해석해 대조.", EditorStyles.wordWrappedMiniLabel);
            DrawSeparator();

            if (_misses.Count == 0)
            {
                EditorGUILayout.HelpBox(_csvCount == 0 ? "스캔된 CSV가 없습니다." : "표정 누락이 없습니다 ✓", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string curChar = null;
            foreach (var m in _misses)
            {
                if (m.charId != curChar)
                {
                    curChar = m.charId;
                    EditorGUILayout.Space(4);
                    string have = _folders.TryGetValue(curChar, out var owned)
                        ? string.Join(", ", owned.OrderBy(x => x, StringComparer.Ordinal))
                        : "(폴더 없음)";
                    EditorGUILayout.LabelField($"▌ {curChar}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"   보유 표정: {have}", EditorStyles.miniLabel);
                }
                DrawMiss(m);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawMiss(Miss m)
        {
            string key = m.charId + "/" + m.emoteId;
            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rect, new Color(0.95f, 0.4f, 0.35f, 0.16f));

            using (new EditorGUILayout.HorizontalScope())
            {
                bool exp = _expanded.Contains(key);
                bool now = EditorGUILayout.Foldout(exp, $"표정 없음: {m.charId} / {m.emoteId}   ({m.usages.Count}회)", true);
                if (now != exp) { if (now) _expanded.Add(key); else _expanded.Remove(key); }
            }
            if (_expanded.Contains(key))
            {
                foreach (var u in m.usages)
                {
                    string id = string.IsNullOrEmpty(u.lineId) ? "(무번호)" : u.lineId;
                    string raw = string.Equals(u.rawEmote, m.emoteId, StringComparison.Ordinal) ? "" : $"  원문:{u.rawEmote}";
                    EditorGUILayout.LabelField($"      {u.csv}:{u.line}  [{id}]  {u.source}{raw}", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(1);
        }

        void DrawSeparator()
        {
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.15f));
        }

        void CopyReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== 표정 누락 ({_misses.Count}종 / {_usageCount}회, CSV {_csvCount}개) ===");
            string curChar = null;
            foreach (var m in _misses)
            {
                if (m.charId != curChar)
                {
                    curChar = m.charId;
                    sb.AppendLine();
                    sb.AppendLine($"[{curChar}]  보유: {(_folders.TryGetValue(curChar, out var o) ? string.Join(", ", o.OrderBy(x => x, StringComparer.Ordinal)) : "-")}");
                }
                var refs = string.Join(", ", m.usages.Select(u => string.IsNullOrEmpty(u.lineId) ? $"{u.csv}:{u.line}" : u.lineId));
                sb.AppendLine($"  {m.emoteId}  →  {refs}");
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            ShowNotification(new GUIContent("리포트를 클립보드에 복사"));
        }
    }
}
