#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using LoveAlgo.Story;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// 시나리오 CSV 변환 디자이너 (기획 CSV → 엔진 CSV + 패치 머지 + diff 리포트).
    /// 매핑 데이터는 StoryMappings.cs 정전 — 별도 SO 슬롯 없음.
    /// 메뉴: Tools > LoveAlgo > Story > Convert 기획 CSV
    /// </summary>
    public class StoryConvertWindow : EditorWindow
    {
        const string DEFAULT_SRC = "Assets/_Project/Modules/Narrative/Art/Story/프롤로그(기획).csv";
        const string DEFAULT_DST = "Assets/Resources/Story/Prologue.csv";
        const string DEFAULT_PATCH = "Assets/Resources/Story/Prologue.patch.csv";

        string sourcePath = DEFAULT_SRC;
        string targetPath = DEFAULT_DST;
        string patchPath = DEFAULT_PATCH;
        string lineIdPrefix = "pro_";
        bool inPlaceLineIds = true;
        bool strictMode = true;

        StoryConvertResult lastResult;
        List<CsvUtility.CsvRecord> lastSourceRecords;
        Vector2 reportScroll;

        [MenuItem("Tools/LoveAlgo/Story/Convert 기획 CSV")]
        public static void Open()
        {
            var w = GetWindow<StoryConvertWindow>("Story Convert");
            w.minSize = new Vector2(520, 600);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Story CSV Convert", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            PathField("Source (기획 CSV)", ref sourcePath, "csv");
            PathField("Target (엔진 CSV)", ref targetPath, "csv");
            PathField("Patch (윤문)",       ref patchPath,  "csv");
            lineIdPrefix = EditorGUILayout.TextField("LineID Prefix", lineIdPrefix);
            inPlaceLineIds = EditorGUILayout.ToggleLeft("LineID 자동 발급 (원본 CSV 갱신)", inPlaceLineIds);
            strictMode = EditorGUILayout.ToggleLeft("Strict 모드 (Violations 발생 시 출력 중단)", strictMode);

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox("매핑(한글↔ID)은 StoryMappings.cs 한 파일에서 관리합니다.", MessageType.Info);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
                if (GUILayout.Button("Convert ▶", GUILayout.Height(30))) RunConvert();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(10);
            DrawReport();
        }

        void PathField(string label, ref string path, string ext)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                path = EditorGUILayout.TextField(label, path);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    var sel = EditorUtility.OpenFilePanel(label, "Assets", ext);
                    if (!string.IsNullOrEmpty(sel))
                    {
                        if (sel.StartsWith(Application.dataPath))
                            sel = "Assets" + sel.Substring(Application.dataPath.Length);
                        path = sel;
                    }
                }
            }
        }

        void RunConvert()
        {
            var opt = new StoryConvertOptions
            {
                SourceCsvPath = sourcePath,
                TargetCsvPath = targetPath,
                PatchCsvPath  = patchPath,
                LineIdPrefix  = lineIdPrefix,
                AssignLineIdsInPlace = inPlaceLineIds,
                Strict = strictMode,
            };
            lastResult = StoryCsvConverter.Convert(opt);
            lastSourceRecords = File.Exists(sourcePath)
                ? CsvUtility.SplitRecords(File.ReadAllText(sourcePath))
                : null;
            AssetDatabase.Refresh();
            Debug.Log($"[StoryConvert] 완료 — {lastResult.Rows.Count} rows. Violations={lastResult.Violations.Count}, orphan patch={lastResult.OrphanPatches}");
        }

        void OpenSourceCsv()
        {
            if (!File.Exists(sourcePath)) { EditorUtility.DisplayDialog("Open", $"파일 없음:\n{sourcePath}", "OK"); return; }
            EditorUtility.RevealInFinder(sourcePath);
        }

        void DrawReport()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
                if (GUILayout.Button("Open Source CSV", GUILayout.Width(140))) OpenSourceCsv();
            }

            if (lastResult == null) { EditorGUILayout.HelpBox("아직 변환 실행 안 됨.", MessageType.Info); return; }

            EditorGUILayout.LabelField($"행 수: {lastResult.Rows.Count}");
            EditorGUILayout.LabelField($"Orphan patches: {lastResult.OrphanPatches}");

            if (lastResult.Violations.Count == 0 && (lastResult.Warnings == null || lastResult.Warnings.Count == 0))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.6f, 0.95f, 0.6f);
                EditorGUILayout.HelpBox("✓ 누락 없음 — 변환 결과 깨끗함.", MessageType.Info);
                GUI.backgroundColor = prev;
                return;
            }

            reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.MinHeight(200));

            DrawViolationsSection("Missing Emote",     ViolationKind.Emote);
            DrawViolationsSection("Missing BG",        ViolationKind.Bg);
            DrawViolationsSection("Missing CG",        ViolationKind.Cg);
            DrawViolationsSection("Missing SD",        ViolationKind.Sd);
            DrawViolationsSection("Missing Character", ViolationKind.Character);

            if (lastResult.Warnings != null && lastResult.Warnings.Count > 0)
            {
                EditorGUILayout.LabelField($"▼ Warnings ({lastResult.Warnings.Count})", EditorStyles.boldLabel);
                foreach (var s in lastResult.Warnings) EditorGUILayout.SelectableLabel("  " + s, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawViolationsSection(string title, ViolationKind kind)
        {
            int count = 0;
            foreach (var v in lastResult.Violations) if (v.kind == kind) count++;
            if (count == 0) return;

            EditorGUILayout.LabelField($"▼ {title} ({count})", EditorStyles.boldLabel);
            foreach (var v in lastResult.Violations)
            {
                if (v.kind != kind) continue;
                string excerpt = LookupExcerpt(v.sourceRow);
                int fileLine = (lastSourceRecords != null && v.sourceRow >= 0 && v.sourceRow < lastSourceRecords.Count)
                    ? lastSourceRecords[v.sourceRow].StartLine
                    : v.sourceRow + 1;
                string lineLabel = string.IsNullOrEmpty(v.lineId) ? $"행 {fileLine}" : $"행 {fileLine} ({v.lineId})";
                EditorGUILayout.SelectableLabel(
                    $"  [{lineLabel}] {v.token}    — {excerpt}",
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            EditorGUILayout.Space(4);
        }

        string LookupExcerpt(int sourceRow)
        {
            if (lastSourceRecords == null || sourceRow < 0 || sourceRow >= lastSourceRecords.Count) return "";
            var text = lastSourceRecords[sourceRow].Text ?? "";
            text = text.Replace("\r", " ").Replace("\n", " ⏎ ");
            const int max = 80;
            if (text.Length > max) text = text.Substring(0, max) + "…";
            return "\"" + text + "\"";
        }
    }
}
#endif
