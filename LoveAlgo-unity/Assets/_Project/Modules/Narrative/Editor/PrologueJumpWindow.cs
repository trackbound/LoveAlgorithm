#if UNITY_EDITOR
using LoveAlgo.Contracts;
using System.Collections.Generic;
using System.IO;
using LoveAlgo.Common;
using LoveAlgo.Narrative;
using LoveAlgo.Story;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// 플레이 모드에서 스토리 스크립트의 라인을 검색하고 즉시 점프.
    /// 메뉴: Tools > LoveAlgo > Story > Jump Debugger
    ///
    /// - 오프라인(에디터): CSV 파일을 읽어 라인 목록 표시 (참고용)
    /// - 플레이 모드: NarrativeModule.JumpToIndex로 즉시 이동
    /// </summary>
    public class PrologueJumpWindow : EditorWindow
    {
        const string DefaultCsv = "Assets/Resources/Story/Prologue.csv";

        string csvPath = DefaultCsv;
        string search = "";
        Vector2 scroll;
        List<Row> rows = new();

        [MenuItem("Tools/LoveAlgo/Story/Jump Debugger")]
        public static void Open()
        {
            var w = GetWindow<PrologueJumpWindow>("Story Jump");
            w.minSize = new Vector2(640, 520);
            w.Reload();
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Story Jump Debugger", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                csvPath = EditorGUILayout.TextField("CSV", csvPath);
                if (GUILayout.Button("Reload", GUILayout.Width(70))) Reload();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(50));
                search = EditorGUILayout.TextField(search);
            }

            var label = Application.isPlaying ? "▶ 플레이 모드 — 더블클릭으로 즉시 점프" : "■ 에디터 모드 — 플레이 시 점프 가능";
            EditorGUILayout.HelpBox(label, Application.isPlaying ? MessageType.Info : MessageType.None);
            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            int idx = 0;
            foreach (var r in rows)
            {
                if (!string.IsNullOrEmpty(search))
                {
                    var s = search.ToLowerInvariant();
                    if (!r.lineId.ToLowerInvariant().Contains(s)
                     && !r.type.ToLowerInvariant().Contains(s)
                     && !r.value.ToLowerInvariant().Contains(s)
                     && !(r.speaker ?? "").ToLowerInvariant().Contains(s))
                    { idx++; continue; }
                }
                DrawRow(r, idx);
                idx++;
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawRow(Row r, int globalIndex)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"#{globalIndex}", GUILayout.Width(48));
                EditorGUILayout.LabelField(r.lineId, GUILayout.Width(96));
                EditorGUILayout.LabelField(r.type, GUILayout.Width(60));
                if (!string.IsNullOrEmpty(r.speaker))
                    EditorGUILayout.LabelField(r.speaker, GUILayout.Width(80));
                EditorGUILayout.LabelField(Truncate(r.value, 80), EditorStyles.miniLabel);

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Jump", GUILayout.Width(60)))
                        Jump(globalIndex);
                }
            }
        }

        void Jump(int index)
        {
            if (!Application.isPlaying) return;
            var nm = Services.TryGet<INarrative>();
            if (nm == null) { Debug.LogWarning("[StoryJump] INarrative 서비스 없음"); return; }
            nm.JumpToIndex(index);
            Debug.Log($"[StoryJump] #{index} 으로 점프");
        }

        void Reload()
        {
            rows.Clear();
            if (!File.Exists(csvPath)) return;

            // CsvUtility는 namespace LoveAlgo.Story
            var text = File.ReadAllText(csvPath);
            var records = CsvUtility.SplitRecords(text);
            int i = 0;
            foreach (var rec in records)
            {
                var cols = CsvUtility.SplitCsv(rec.Text);
                if (cols.Length < 4) { i++; continue; }
                if (i == 0 && cols[0].Equals("LineID", System.StringComparison.OrdinalIgnoreCase)) { i++; continue; }
                if (cols[0].TrimStart().StartsWith("#")) { i++; continue; }

                rows.Add(new Row
                {
                    lineId = cols[0],
                    type = cols[1],
                    speaker = cols.Length > 2 ? cols[2] : "",
                    value = cols.Length > 3 ? cols[3] : "",
                });
                i++;
            }
            Repaint();
        }

        static string Truncate(string s, int max) =>
            s == null ? "" : (s.Length <= max ? s : s.Substring(0, max) + "…");

        struct Row { public string lineId, type, speaker, value; }
    }
}
#endif
