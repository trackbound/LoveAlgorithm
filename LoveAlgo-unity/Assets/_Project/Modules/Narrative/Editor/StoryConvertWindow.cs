#if UNITY_EDITOR
using System.IO;
using LoveAlgo.NarrativeEditor.Mappings;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// 시나리오 CSV 변환 디자이너 (기획 CSV → 엔진 CSV + 패치 머지 + diff 리포트).
    /// 메뉴: Tools > LoveAlgo > Story > Convert 기획 CSV
    /// </summary>
    public class StoryConvertWindow : EditorWindow
    {
        const string MAP_DIR = "Assets/_Project/Modules/Narrative/Editor/Mappings";
        const string DEFAULT_SRC = "Assets/_Project/Modules/Narrative/Art/Story/프롤로그(기획).csv";
        const string DEFAULT_DST = "Assets/Resources/Story/Prologue.csv";
        const string DEFAULT_PATCH = "Assets/Resources/Story/Prologue.patch.csv";

        string sourcePath = DEFAULT_SRC;
        string targetPath = DEFAULT_DST;
        string patchPath = DEFAULT_PATCH;
        string lineIdPrefix = "pro_";
        bool inPlaceLineIds = true;

        EmoteMap emote;
        CharacterMap character;
        BgMap bg;
        CgMap cg;
        SdMap sd;
        SoundMap sound;

        StoryConvertResult lastResult;
        Vector2 reportScroll;

        [MenuItem("Tools/LoveAlgo/Story/Convert 기획 CSV")]
        public static void Open()
        {
            var w = GetWindow<StoryConvertWindow>("Story Convert");
            w.minSize = new Vector2(520, 600);
            w.Show();
        }

        void OnEnable() => ReloadMaps();

        void ReloadMaps()
        {
            emote     = AssetDatabase.LoadAssetAtPath<EmoteMap>($"{MAP_DIR}/EmoteMap.asset");
            character = AssetDatabase.LoadAssetAtPath<CharacterMap>($"{MAP_DIR}/CharacterMap.asset");
            bg        = AssetDatabase.LoadAssetAtPath<BgMap>($"{MAP_DIR}/BgMap.asset");
            cg        = AssetDatabase.LoadAssetAtPath<CgMap>($"{MAP_DIR}/CgMap.asset");
            sd        = AssetDatabase.LoadAssetAtPath<SdMap>($"{MAP_DIR}/SdMap.asset");
            sound     = AssetDatabase.LoadAssetAtPath<SoundMap>($"{MAP_DIR}/SoundMap.asset");
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

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Mappings", EditorStyles.boldLabel);
            emote     = (EmoteMap)    EditorGUILayout.ObjectField("Emote",     emote,     typeof(EmoteMap),     false);
            character = (CharacterMap)EditorGUILayout.ObjectField("Character", character, typeof(CharacterMap), false);
            bg        = (BgMap)       EditorGUILayout.ObjectField("BG",        bg,        typeof(BgMap),        false);
            cg        = (CgMap)       EditorGUILayout.ObjectField("CG",        cg,        typeof(CgMap),        false);
            sd        = (SdMap)       EditorGUILayout.ObjectField("SD",        sd,        typeof(SdMap),        false);
            sound     = (SoundMap)    EditorGUILayout.ObjectField("Sound",     sound,     typeof(SoundMap),     false);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Import Mappings from xlsx", GUILayout.Height(26)))
                { StoryMappingImporter.ImportAll(); ReloadMaps(); }

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
                Emote = emote, Character = character, Bg = bg, Cg = cg, Sd = sd, Sound = sound,
            };
            lastResult = StoryCsvConverter.Convert(opt);
            AssetDatabase.Refresh();
            Debug.Log($"[StoryConvert] 완료 — {lastResult.Rows.Count} rows. Missing: emote={lastResult.MissingEmote.Count}, bg={lastResult.MissingBg.Count}, cg={lastResult.MissingCg.Count}, sd={lastResult.MissingSd.Count}, orphan patch={lastResult.OrphanPatches}");
        }

        void DrawReport()
        {
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            if (lastResult == null) { EditorGUILayout.HelpBox("아직 변환 실행 안 됨.", MessageType.Info); return; }

            EditorGUILayout.LabelField($"행 수: {lastResult.Rows.Count}");
            EditorGUILayout.LabelField($"Orphan patches: {lastResult.OrphanPatches}");

            reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.MinHeight(200));

            DrawMissingSection("Missing Emote", lastResult.MissingEmote);
            DrawMissingSection("Missing BG", lastResult.MissingBg);
            DrawMissingSection("Missing CG", lastResult.MissingCg);
            DrawMissingSection("Missing SD", lastResult.MissingSd);
            DrawMissingSection("Missing Character", lastResult.MissingCharacter);
            DrawMissingSection("Warnings", lastResult.Warnings);

            EditorGUILayout.EndScrollView();
        }

        static void DrawMissingSection(string title, System.Collections.Generic.List<string> list)
        {
            if (list == null || list.Count == 0) return;
            EditorGUILayout.LabelField($"▼ {title} ({list.Count})", EditorStyles.boldLabel);
            foreach (var s in list) EditorGUILayout.LabelField("  " + s);
            EditorGUILayout.Space(4);
        }
    }
}
#endif
