#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoveAlgo.Story;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// StoryMappings 값 ↔ 실제 Resources 파일 정합성 검사.
    /// 메뉴: Tools > LoveAlgo > Validate Resource Mappings
    ///
    /// - missing: dict에 매핑됐는데 실제 파일 없음 (런타임 에러 원인)
    /// - orphan: 폴더에 파일은 있는데 dict에 매핑 없음 (사용 안 되는 자산 또는 신규 추가 누락)
    /// </summary>
    public class ResourceValidatorWindow : EditorWindow
    {
        Vector2 scroll;
        List<string> report = new();

        [MenuItem("Tools/LoveAlgo/Resources/Validate Mappings")]
        public static void Open()
        {
            var w = GetWindow<ResourceValidatorWindow>("Resource Validator");
            w.minSize = new Vector2(560, 520);
            w.Run();
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Resource ↔ StoryMappings Validator", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ Run", GUILayout.Height(26))) Run();
                if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(160), GUILayout.Height(26)))
                    EditorGUIUtility.systemCopyBuffer = string.Join("\n", report);
            }
            EditorGUILayout.Space(6);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var line in report)
                EditorGUILayout.SelectableLabel(line, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndScrollView();
        }

        void Run()
        {
            report.Clear();
            ValidateGroup("BG",         StoryMappings.BG,  "Assets/Resources/BG",         ".png");
            ValidateGroup("CG",         StoryMappings.CG,  "Assets/Resources/CG",         ".png");
            ValidateGroup("SD",         StoryMappings.SD,  "Assets/Resources/SD",         ".png");
            ValidateGroup("BGM",        StoryMappings.BGM, "Assets/Resources/Audio/BGM",  ".mp3", ".wav");
            ValidateGroup("SFX",        StoryMappings.SFX, "Assets/Resources/Audio/SFX",  ".mp3", ".wav");
            ValidateCharacters();
            ValidateOverlays();
            report.Insert(0, $"=== 검사 완료 — {System.DateTime.Now:HH:mm:ss} ===");
            Repaint();
        }

        // ─── 한글→ID dict 그룹 ────────────────────────────
        void ValidateGroup(string label, Dictionary<string, string> dict, string folder, params string[] exts)
        {
            report.Add("");
            report.Add($"━━━ [{label}] ━━━");
            if (!Directory.Exists(folder))
            {
                report.Add($"  ⚠ 폴더 없음: {folder}");
                return;
            }

            var files = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var ext in exts.Length == 0 ? new[] { ".png" } : exts)
            foreach (var f in Directory.GetFiles(folder, "*" + ext))
                files.Add(Path.GetFileNameWithoutExtension(f));

            int missing = 0, ok = 0;
            foreach (var kv in dict)
            {
                if (files.Contains(kv.Value)) { ok++; continue; }
                report.Add($"  ✗ missing: \"{kv.Key}\" → {kv.Value} (파일 없음)");
                missing++;
            }

            var mapped = new HashSet<string>(dict.Values, System.StringComparer.OrdinalIgnoreCase);
            int orphan = 0;
            foreach (var f in files)
            {
                if (mapped.Contains(f)) continue;
                report.Add($"  • orphan: {folder}/{f} (dict에 매핑 없음)");
                orphan++;
            }
            report.Add($"  → ok={ok} · missing={missing} · orphan={orphan}");
        }

        // ─── Characters: c{NN}_{emoteId} 합성 ──────────────
        void ValidateCharacters()
        {
            report.Add("");
            report.Add("━━━ [Characters] ━━━");
            const string folder = "Assets/Resources/Characters";
            if (!Directory.Exists(folder)) { report.Add($"  ⚠ 폴더 없음: {folder}"); return; }

            var files = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.GetFiles(folder, "*.png"))
                files.Add(Path.GetFileNameWithoutExtension(f));

            // 등록된 모든 캐릭터 × 모든 emote 조합 중 매핑되어 있는지가 아니라,
            // 실제 파일이 c{NN}_{emoteId} 패턴인지 + 각 캐릭터가 _00(기본)을 갖고 있는지 검사
            var ids = new List<string>(StoryMappings.Characters.Select(c => c.Id));
            int ok = 0, missDefault = 0;
            foreach (var id in ids)
            {
                var hasDefault = files.Contains(id + "_00");
                if (!hasDefault) { report.Add($"  ✗ {id}: 기본(_00) 표정 파일 없음"); missDefault++; }
                else ok++;
            }
            int orphan = 0;
            foreach (var f in files)
            {
                var idx = f.IndexOf('_');
                if (idx <= 0) { report.Add($"  • orphan: {f} (네이밍 비표준)"); orphan++; continue; }
                var prefix = f.Substring(0, idx);
                if (!ids.Contains(prefix)) { report.Add($"  • orphan: {f} (등록 안 된 캐릭터 ID)"); orphan++; }
            }
            report.Add($"  → 기본표정 OK {ok}/{ids.Count} · 누락 {missDefault} · orphan {orphan} · 총 {files.Count}장");
        }

        // ─── Overlay: {Prefix}_{mode}_{variant} 합성 ─────────
        void ValidateOverlays()
        {
            report.Add("");
            report.Add("━━━ [Overlay] ━━━");
            const string folder = "Assets/Resources/Overlay";
            if (!Directory.Exists(folder)) { report.Add($"  ⚠ 폴더 없음: {folder}"); return; }

            var files = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.GetFiles(folder, "*.png"))
                files.Add(Path.GetFileNameWithoutExtension(f));

            int missing = 0, ok = 0;
            var expected = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var ov in StoryMappings.Overlays)
            {
                var modes = ov.Modes ?? new[] { "" };
                foreach (var mode in modes)
                foreach (var variant in new[] { "default", "positive", "negative" })
                {
                    var n = string.IsNullOrEmpty(mode)
                        ? $"{ov.Prefix}_{variant}"
                        : $"{ov.Prefix}_{mode.ToLowerInvariant()}_{variant}";
                    expected.Add(n);
                    if (files.Contains(n)) ok++;
                    else { report.Add($"  ✗ missing: {n}"); missing++; }
                }
            }
            int orphan = 0;
            foreach (var f in files)
            {
                if (!expected.Contains(f)) { report.Add($"  • orphan: {f}"); orphan++; }
            }
            report.Add($"  → ok={ok} · missing={missing} · orphan={orphan}");
        }

    }
}
#endif
