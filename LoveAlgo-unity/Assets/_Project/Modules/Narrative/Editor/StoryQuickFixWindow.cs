#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LoveAlgo.NarrativeEditor.Mappings;
using LoveAlgo.Story;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// 위반 토큰(매핑 누락) Quick-fix 도구.
    /// 위반 리스트 ↑/↓ 이동 → 정전 후보 dropdown → Apply 시 기획 CSV in-place 교체.
    /// 메뉴: Tools > LoveAlgo > Story > Quick Fix Violations
    /// </summary>
    public class StoryQuickFixWindow : EditorWindow
    {
        const string MAP_DIR = "Assets/_Project/Modules/Narrative/Editor/Mappings";
        const string DEFAULT_SRC = "Assets/_Project/Modules/Narrative/Art/Story/프롤로그(기획).csv";

        string sourcePath = DEFAULT_SRC;
        EmoteMap emote; LoveAlgo.Story.CharacterMetaDatabase meta; BgMap bg; CgMap cg; SdMap sd; SoundMap sound;
        List<Violation> violations = new();
        int selectedIdx = -1;
        int selectedCandidateIdx;
        Vector2 listScroll;
        string[] currentCandidates = Array.Empty<string>();

        [MenuItem("Tools/LoveAlgo/Story/Quick Fix Violations")]
        public static void Open()
        {
            var w = GetWindow<StoryQuickFixWindow>("Story Quick Fix");
            w.minSize = new Vector2(600, 500);
            w.Show();
        }

        void OnEnable()
        {
            emote     = AssetDatabase.LoadAssetAtPath<EmoteMap>("Assets/Resources/Data/EmoteMap.asset")
                     ?? AssetDatabase.LoadAssetAtPath<EmoteMap>($"{MAP_DIR}/EmoteMap.asset");
            meta      = AssetDatabase.LoadAssetAtPath<LoveAlgo.Story.CharacterMetaDatabase>("Assets/Resources/Data/CharacterMetaDatabase.asset");
            bg        = AssetDatabase.LoadAssetAtPath<BgMap>($"{MAP_DIR}/BgMap.asset");
            cg        = AssetDatabase.LoadAssetAtPath<CgMap>($"{MAP_DIR}/CgMap.asset");
            sd        = AssetDatabase.LoadAssetAtPath<SdMap>($"{MAP_DIR}/SdMap.asset");
            sound     = AssetDatabase.LoadAssetAtPath<SoundMap>($"{MAP_DIR}/SoundMap.asset");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Story Quick Fix", EditorStyles.boldLabel);
            sourcePath = EditorGUILayout.TextField("Source (기획 CSV)", sourcePath);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Violations", GUILayout.Height(26))) ScanViolations();
                using (new EditorGUI.DisabledScope(violations.Count == 0 || selectedIdx < 0 || currentCandidates.Length == 0))
                {
                    GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
                    if (GUILayout.Button("Apply ▶", GUILayout.Height(26))) ApplyFix();
                    GUI.backgroundColor = Color.white;
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"위반 {violations.Count}건", EditorStyles.miniLabel);

            HandleArrowKeys();

            // 위반 리스트
            using (var scope = new EditorGUILayout.ScrollViewScope(listScroll, GUILayout.MinHeight(200)))
            {
                listScroll = scope.scrollPosition;
                for (int i = 0; i < violations.Count; i++)
                {
                    var v = violations[i];
                    bool sel = i == selectedIdx;
                    var style = sel ? EditorStyles.helpBox : EditorStyles.label;
                    using (new EditorGUILayout.HorizontalScope(style))
                    {
                        EditorGUILayout.LabelField($"[{v.kind}]", GUILayout.Width(70));
                        EditorGUILayout.LabelField(v.lineId, GUILayout.Width(80));
                        EditorGUILayout.LabelField($"\"{v.token}\"");
                        if (GUILayout.Button("선택", GUILayout.Width(50)))
                        {
                            SelectViolation(i);
                        }
                    }
                }
            }

            // 선택된 항목의 후보 dropdown
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("정전 후보", EditorStyles.boldLabel);
            if (selectedIdx >= 0 && selectedIdx < violations.Count)
            {
                var v = violations[selectedIdx];
                EditorGUILayout.LabelField($"선택: [{v.kind}] {v.lineId} \"{v.token}\"");
                if (currentCandidates.Length > 0)
                {
                    selectedCandidateIdx = EditorGUILayout.Popup("교체 대상", selectedCandidateIdx, currentCandidates);
                }
                else
                {
                    EditorGUILayout.HelpBox("정전 후보 없음 (매핑 SO가 비었거나 카테고리 미지원).", MessageType.Warning);
                }
            }
            else EditorGUILayout.HelpBox("Scan 후 위반 항목을 선택하세요. ↑/↓ 키로 이동 가능.", MessageType.Info);
        }

        void HandleArrowKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || violations.Count == 0) return;
            if (e.keyCode == KeyCode.DownArrow)
            {
                SelectViolation(Mathf.Min(violations.Count - 1, selectedIdx + 1));
                e.Use();
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                SelectViolation(Mathf.Max(0, selectedIdx - 1));
                e.Use();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (currentCandidates.Length > 0) { ApplyFix(); e.Use(); }
            }
        }

        void SelectViolation(int idx)
        {
            selectedIdx = idx;
            selectedCandidateIdx = 0;
            currentCandidates = idx >= 0 ? GetCandidates(violations[idx]) : Array.Empty<string>();
            Repaint();
        }

        // ─── Scan ────────────────────────────────────────
        void ScanViolations()
        {
            if (!File.Exists(sourcePath))
            {
                EditorUtility.DisplayDialog("Quick Fix", $"Source 없음: {sourcePath}", "OK");
                return;
            }
            // 기존 컨버터 재사용 — dry-run을 위해 임시 target/patch 경로 사용
            var tmpTarget = Path.GetTempFileName();
            var opt = new StoryConvertOptions
            {
                SourceCsvPath = sourcePath,
                TargetCsvPath = tmpTarget,
                PatchCsvPath  = null,
                AssignLineIdsInPlace = false,  // 스캔만 — 원본 건드리지 않음
                Emote = emote, Meta = meta, Bg = bg, Cg = cg, Sd = sd, Sound = sound,
            };
            var result = StoryCsvConverter.Convert(opt);
            File.Delete(tmpTarget);

            violations = result.Violations
                .OrderBy(v => v.sourceRow)
                .ThenBy(v => v.kind)
                .ToList();
            selectedIdx = violations.Count > 0 ? 0 : -1;
            SelectViolation(selectedIdx);
            Debug.Log($"[QuickFix] {violations.Count} violations scanned.");
        }

        // ─── 후보 (Levenshtein 거리 순) ───────────────────
        string[] GetCandidates(Violation v)
        {
            IEnumerable<string> pool;
            switch (v.kind)
            {
                case ViolationKind.Emote:
                    pool = emote != null ? emote.entries.Select(e => e.ko) : Array.Empty<string>();
                    break;
                case ViolationKind.Bg:
                    pool = bg != null ? bg.entries.Select(e => e.ko) : Array.Empty<string>();
                    break;
                case ViolationKind.Cg:
                    pool = cg != null ? cg.entries.Select(e => e.ko) : Array.Empty<string>();
                    break;
                case ViolationKind.Sd:
                    pool = sd != null ? sd.entries.Select(e => e.ko) : Array.Empty<string>();
                    break;
                default: return Array.Empty<string>();
            }
            return pool
                .Where(s => !string.IsNullOrEmpty(s))
                .OrderBy(s => Levenshtein(v.token ?? "", s))
                .Take(8)
                .ToArray();
        }

        static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            return d[a.Length, b.Length];
        }

        // ─── Apply: 기획 CSV in-place 교체 ────────────────
        void ApplyFix()
        {
            if (selectedIdx < 0 || selectedIdx >= violations.Count) return;
            if (currentCandidates.Length == 0) return;
            var v = violations[selectedIdx];
            string newToken = currentCandidates[selectedCandidateIdx];

            string text = File.ReadAllText(sourcePath);
            var records = CsvUtility.SplitRecords(text);
            if (v.sourceRow < 0 || v.sourceRow >= records.Count) return;

            var cols = CsvUtility.SplitCsv(records[v.sourceRow].Text);
            if (cols.Length < 5) return;

            // Value 컬럼(인덱스 3)에서 토큰 교체
            string before = cols[3];
            string after = ReplaceToken(before, v.token, newToken, v.kind);
            if (before == after)
            {
                EditorUtility.DisplayDialog("Quick Fix", $"교체 실패 — 토큰 '{v.token}'을 Value에서 찾지 못함:\n{before}", "OK");
                return;
            }
            cols[3] = after;

            // 전체 records 재구성 (해당 row만 갱신, 나머지는 원본 그대로)
            var sb = new StringBuilder();
            for (int i = 0; i < records.Count; i++)
            {
                if (i == v.sourceRow)
                {
                    for (int c = 0; c < cols.Length; c++)
                    {
                        if (c > 0) sb.Append(',');
                        sb.Append(EscapeCsvField(cols[c]));
                    }
                }
                else
                {
                    sb.Append(records[i].Text);
                }
                if (i < records.Count - 1) sb.Append('\n');
            }
            File.WriteAllText(sourcePath, sb.ToString(), new UTF8Encoding(true));
            AssetDatabase.Refresh();
            Debug.Log($"[QuickFix] Applied — Row {v.sourceRow + 1} {v.kind}: '{v.token}' → '{newToken}'");

            // 적용한 위반 제거 + 다음 항목 선택
            violations.RemoveAt(selectedIdx);
            if (selectedIdx >= violations.Count) selectedIdx = violations.Count - 1;
            SelectViolation(selectedIdx);
        }

        static string ReplaceToken(string value, string oldToken, string newToken, ViolationKind kind)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(oldToken)) return value;
            if (kind == ViolationKind.Emote)
            {
                // <emote=oldToken/> 형태 우선
                var pat = $"<emote={oldToken}/>";
                if (value.Contains(pat)) return value.Replace(pat, $"<emote={newToken}/>");
            }
            // 일반 substring replace (첫 번째 발견만)
            int idx = value.IndexOf(oldToken, StringComparison.Ordinal);
            if (idx < 0) return value;
            return value.Substring(0, idx) + newToken + value.Substring(idx + oldToken.Length);
        }

        static string EscapeCsvField(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool q = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!q) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
#endif
