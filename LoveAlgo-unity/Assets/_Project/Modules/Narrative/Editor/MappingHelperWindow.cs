#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using LoveAlgo.Story;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// 신규 한글↔ID 매핑 entry를 생성해 StoryMappings.cs 에 빠르게 추가.
    /// 메뉴: Tools > LoveAlgo > Story > Mapping Helper
    ///
    /// 흐름:
    ///   1. Type 선택 (BGM/SFX/BG/CG/SD)
    ///   2. 폴더 안 실제 파일 목록 → 매핑 안 된 것만 표시 (orphan)
    ///   3. 각 항목 옆에 한글명 입력 → "Append to StoryMappings.cs" 한 번에
    /// </summary>
    public class MappingHelperWindow : EditorWindow
    {
        const string StoryMappingsPath = "Assets/_Project/Modules/Narrative/Code/StoryMappings.cs";

        enum Kind { BGM, SFX, BG, CG, SD }

        Kind kind = Kind.BGM;
        Vector2 scroll;
        readonly Dictionary<string, string> draft = new(); // engineId → 한글명 (입력 중)

        [MenuItem("Tools/LoveAlgo/Story/Mapping Helper")]
        public static void Open()
        {
            var w = GetWindow<MappingHelperWindow>("Mapping Helper");
            w.minSize = new Vector2(560, 520);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Mapping Helper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("매핑 안 된 실제 파일(orphan)에 한글 키를 부여 → StoryMappings.cs 에 자동 삽입.", MessageType.None);

            kind = (Kind)EditorGUILayout.EnumPopup("Kind", kind);

            EditorGUILayout.Space(6);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            var orphans = FindOrphans(kind);
            if (orphans.Count == 0)
            {
                EditorGUILayout.HelpBox("매핑 안 된 파일이 없습니다 — 모두 등록됨.", MessageType.Info);
            }
            else
            {
                foreach (var id in orphans)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(id, GUILayout.Width(220));
                        if (!draft.TryGetValue(id, out var ko)) ko = "";
                        ko = EditorGUILayout.TextField("한글 키", ko);
                        draft[id] = ko;
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Snippet (Clipboard)", GUILayout.Height(28)))
                    CopySnippet(orphans);

                using (new EditorGUI.DisabledScope(!HasDrafts()))
                {
                    if (GUILayout.Button("Append to StoryMappings.cs", GUILayout.Height(28)))
                        AppendToFile(orphans);
                }
            }
        }

        bool HasDrafts()
        {
            foreach (var kv in draft)
                if (!string.IsNullOrWhiteSpace(kv.Value)) return true;
            return false;
        }

        // ─── orphan 찾기 ────────────────────────────────
        List<string> FindOrphans(Kind k)
        {
            var (folder, exts, dict) = GetTarget(k);
            var orphans = new List<string>();
            if (!Directory.Exists(folder)) return orphans;

            var mapped = new HashSet<string>(dict.Values, System.StringComparer.OrdinalIgnoreCase);
            foreach (var ext in exts)
            foreach (var f in Directory.GetFiles(folder, "*" + ext))
            {
                var name = Path.GetFileNameWithoutExtension(f);
                if (!mapped.Contains(name)) orphans.Add(name);
            }
            orphans.Sort();
            return orphans;
        }

        (string folder, string[] exts, Dictionary<string, string> dict) GetTarget(Kind k) => k switch
        {
            Kind.BGM => ("Assets/Resources/Audio/BGM", new[] { ".mp3", ".wav", ".ogg" }, StoryMappings.BGM),
            Kind.SFX => ("Assets/Resources/Audio/SFX", new[] { ".mp3", ".wav", ".ogg" }, StoryMappings.SFX),
            Kind.BG  => ("Assets/Resources/BG",        new[] { ".png" },                 StoryMappings.BG),
            Kind.CG  => ("Assets/Resources/CG",        new[] { ".png" },                 StoryMappings.CG),
            Kind.SD  => ("Assets/Resources/SD",        new[] { ".png" },                 StoryMappings.SD),
            _ => (null, null, null),
        };

        string DictMarkerLine() => kind switch
        {
            Kind.BGM => "public static readonly Dictionary<string, string> BGM = new(KO)",
            Kind.SFX => "public static readonly Dictionary<string, string> SFX = new(KO)",
            Kind.BG  => "public static readonly Dictionary<string, string> BG = new(KO)",
            Kind.CG  => "public static readonly Dictionary<string, string> CG = new(KO)",
            Kind.SD  => "public static readonly Dictionary<string, string> SD = new(KO)",
            _ => null,
        };

        // ─── 스니펫 ────────────────────────────────────
        string BuildSnippet(List<string> orphans)
        {
            var lines = new List<string>();
            foreach (var id in orphans)
            {
                if (!draft.TryGetValue(id, out var ko) || string.IsNullOrWhiteSpace(ko)) continue;
                lines.Add($"            {{ \"{ko.Trim()}\", \"{id}\" }},");
            }
            return string.Join("\n", lines);
        }

        void CopySnippet(List<string> orphans)
        {
            var snippet = BuildSnippet(orphans);
            if (string.IsNullOrEmpty(snippet))
            {
                EditorUtility.DisplayDialog("비어 있음", "한글 키가 입력된 항목이 없습니다.", "확인");
                return;
            }
            EditorGUIUtility.systemCopyBuffer = snippet;
            Debug.Log("[MappingHelper] 클립보드에 복사됨:\n" + snippet);
        }

        void AppendToFile(List<string> orphans)
        {
            var snippet = BuildSnippet(orphans);
            if (string.IsNullOrEmpty(snippet)) return;
            if (!File.Exists(StoryMappingsPath))
            {
                EditorUtility.DisplayDialog("실패", $"StoryMappings.cs 를 찾지 못함: {StoryMappingsPath}", "확인");
                return;
            }

            var src = File.ReadAllText(StoryMappingsPath);
            var marker = DictMarkerLine();
            int idx = src.IndexOf(marker, System.StringComparison.Ordinal);
            if (idx < 0)
            {
                EditorUtility.DisplayDialog("실패", $"마커 못 찾음: {marker}", "확인");
                return;
            }
            // 마커 다음 첫 '{' → 그 다음 첫 '}' 직전에 삽입
            int braceOpen = src.IndexOf('{', idx);
            int braceClose = src.IndexOf('}', braceOpen + 1);
            if (braceOpen < 0 || braceClose < 0)
            {
                EditorUtility.DisplayDialog("실패", "Dict 본문 구조 못 찾음.", "확인");
                return;
            }

            // 안전: 마지막 line 직전의 '\n' 위치 찾기
            int insertAt = src.LastIndexOf('\n', braceClose);
            if (insertAt < braceOpen) insertAt = braceClose;

            var newSrc = src.Substring(0, insertAt) + "\n" + snippet + src.Substring(insertAt);
            File.WriteAllText(StoryMappingsPath, newSrc, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[MappingHelper] {kind} dict 에 {snippet.Split('\n').Length} 항목 추가됨.");
            draft.Clear();
            EditorUtility.DisplayDialog("완료", $"StoryMappings.cs 에 추가되었습니다.\n\n{snippet}", "확인");
        }
    }
}
#endif
