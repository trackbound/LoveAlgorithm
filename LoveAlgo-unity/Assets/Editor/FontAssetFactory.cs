using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 비주얼노벨 최적화 폰트 에셋 팩토리
    /// - 한글 Dynamic SDF 에셋 일괄 생성
    /// - 프리셋 기반 (비주얼노벨 최적 설정)
    /// - 기존 에셋 감지 및 재생성/업데이트 지원
    /// </summary>
    public class FontAssetFactory : EditorWindow
    {
        // ─── 프리셋 (FontTag.FontType 대응) ─────────────────────
        public enum FontPreset
        {
            Header,     // 팝업 제목 — Aggro Bold
            Label,      // 카테고리, 항목, 버튼, 이벤트/장소명 — Aggro Medium
            Caption,    // 설명, 선택지, 대사창 이름, 독백 — Aggro Light
            Dialogue,   // 인게임 대사 — Pretendard SemiBold
            Body,       // 로그 대사, 입력 필드 — Pretendard Medium
        }

        [Serializable]
        public class FontEntry
        {
            public string label;            // 표시 이름
            public Font sourceFont;         // .ttf/.otf 소스
            public string outputName;       // 생성될 에셋 이름
            public FontPreset preset;       // 프리셋
            public bool enabled = true;     // 생성 여부

            // 프리셋에서 자동 계산되지만 오버라이드 가능
            public int samplingPointSize;
            public int atlasWidth;
            public int atlasHeight;
            public int padding;
            public bool overrideSettings;

            [NonSerialized] public TMP_FontAsset existingAsset;    // 기존 에셋 감지
            [NonSerialized] public string status;                  // 상태 표시
        }

        // ─── 상태 ────────────────────────────────────────────
        string fontsFolder = "Assets/Fonts";
        Vector2 scrollPos;
        List<FontEntry> entries = new();
        bool showAdvanced;
        bool initialized;

        // 프리셋별 기본값 (비주얼노벨 최적)
        static readonly Dictionary<FontPreset, (int pointSize, int atlas, int padding)> PresetDefaults = new()
        {
            // Header: 팝업 제목, 큰 텍스트
            { FontPreset.Header,    (90, 1024, 9) },
            // Label: 카테고리/항목/버튼, 중간 크기
            { FontPreset.Label,     (72, 1024, 9) },
            // Caption: 설명/선택지/독백, 얇은 획
            { FontPreset.Caption,   (72, 1024, 9) },
            // Dialogue: 인게임 대사, 한글 글리프 다수 → 큰 아틀라스
            { FontPreset.Dialogue,  (72, 2048, 9) },
            // Body: 로그 대사/입력, 한글 글리프 다수 → 큰 아틀라스
            { FontPreset.Body,      (72, 2048, 9) },
        };

        // ─── 메뉴 ────────────────────────────────────────────

        [MenuItem("LoveAlgo/Font Asset Factory", false, 100)]
        static void OpenWindow()
        {
            var window = GetWindow<FontAssetFactory>("Font Asset Factory");
            window.minSize = new Vector2(680, 500);
            window.Show();
        }

        void OnEnable()
        {
            initialized = false;
        }

        void Initialize()
        {
            entries.Clear();

            // 기본 5개 폰트 엔트리 — FontTag.FontType 1:1 대응
            AddEntry("Aggro Bold",            "Aggro-B",              "Aggro-Bold SDF",             FontPreset.Header);
            AddEntry("Aggro Medium",          "Aggro-Medium",          "Aggro-Medium SDF",           FontPreset.Label);
            AddEntry("Aggro Light",           "Aggro-Light",           "Aggro-Light SDF",            FontPreset.Caption);
            AddEntry("Pretendard SemiBold",   "Pretendard-SemiBold",   "Pretendard-SemiBold SDF",    FontPreset.Dialogue);
            AddEntry("Pretendard Medium",     "Pretendard-Medium",     "Pretendard-Medium SDF",      FontPreset.Body);

            // 기존 에셋 스캔
            RefreshStatus();
            initialized = true;
        }

        void AddEntry(string label, string sourceName, string outputName, FontPreset preset)
        {
            var entry = new FontEntry
            {
                label = label,
                outputName = outputName,
                preset = preset,
                enabled = true,
            };

            // 소스 폰트 자동 탐색 (.ttf, .otf)
            string[] extensions = { ".ttf", ".otf", ".TTF", ".OTF" };
            foreach (var ext in extensions)
            {
                string path = $"{fontsFolder}/{sourceName}{ext}";
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null)
                {
                    entry.sourceFont = font;
                    break;
                }
            }

            // 프리셋 기본값
            ApplyPresetDefaults(entry);

            entries.Add(entry);
        }

        void ApplyPresetDefaults(FontEntry entry)
        {
            if (PresetDefaults.TryGetValue(entry.preset, out var defaults))
            {
                entry.samplingPointSize = defaults.pointSize;
                entry.atlasWidth = defaults.atlas;
                entry.atlasHeight = defaults.atlas;
                entry.padding = defaults.padding;
            }
        }

        void RefreshStatus()
        {
            foreach (var entry in entries)
            {
                // 기존 에셋 탐색
                string assetPath = $"{fontsFolder}/{entry.outputName}.asset";
                entry.existingAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

                if (entry.sourceFont == null)
                    entry.status = "소스 폰트 없음";
                else if (entry.existingAsset != null)
                {
                    bool isDynamic = entry.existingAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic;
                    entry.status = isDynamic ? "Dynamic ✓" : "Static ⚠ (한글 불가)";
                }
                else
                    entry.status = "미생성";
            }
        }

        // ─── GUI ────────────────────────────────────────────

        void OnGUI()
        {
            if (!initialized) Initialize();

            DrawHeader();
            DrawFontList();
            DrawActions();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Font Asset Factory", EditorStyles.largeLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("비주얼노벨 최적 Dynamic SDF 폰트 에셋 생성기", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("폰트 폴더", GUILayout.Width(60));
                fontsFolder = EditorGUILayout.TextField(fontsFolder);
                if (GUILayout.Button("새로고침", GUILayout.Width(65)))
                {
                    Initialize();
                }
            }

            EditorGUILayout.Space(4);
            DrawSeparator();
        }

        void DrawFontList()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < entries.Count; i++)
            {
                DrawFontEntry(entries[i], i);
            }

            EditorGUILayout.Space(8);

            // 커스텀 폰트 추가 버튼
            if (GUILayout.Button("+ 폰트 추가", GUILayout.Height(24)))
            {
                entries.Add(new FontEntry
                {
                    label = "New Font",
                    outputName = "NewFont SDF",
                    preset = FontPreset.Label,
                    enabled = true,
                    samplingPointSize = 72,
                    atlasWidth = 1024,
                    atlasHeight = 1024,
                    padding = 9,
                });
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawFontEntry(FontEntry entry, int index)
        {
            Color bgColor = entry.sourceFont == null ? new Color(1f, 0.9f, 0.9f) :
                           entry.existingAsset != null ? new Color(0.9f, 1f, 0.9f) :
                           new Color(1f, 1f, 0.9f);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                GUI.backgroundColor = prevBg;

                // 1행: 체크박스 + 이름 + 상태
                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(18));
                    EditorGUILayout.LabelField(entry.label, EditorStyles.boldLabel, GUILayout.Width(160));

                    // 상태 뱃지
                    var statusStyle = new GUIStyle(EditorStyles.miniLabel);
                    if (entry.status != null && entry.status.Contains("✓"))
                        statusStyle.normal.textColor = new Color(0.1f, 0.6f, 0.1f);
                    else if (entry.status != null && entry.status.Contains("⚠"))
                        statusStyle.normal.textColor = new Color(0.8f, 0.5f, 0f);
                    else if (entry.status != null && entry.status.Contains("없음") || entry.status == "미생성")
                        statusStyle.normal.textColor = new Color(0.8f, 0.2f, 0.2f);

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(entry.status ?? "", statusStyle, GUILayout.Width(140));

                    // 삭제 (기본 5개 외)
                    if (index >= 5)
                    {
                        if (GUILayout.Button("×", GUILayout.Width(22)))
                        {
                            entries.RemoveAt(index);
                            return;
                        }
                    }
                }

                // 2행: 소스폰트 + 출력 이름
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("소스", GUILayout.Width(30));
                    entry.sourceFont = (Font)EditorGUILayout.ObjectField(entry.sourceFont, typeof(Font), false, GUILayout.Width(200));

                    EditorGUILayout.LabelField("출력", GUILayout.Width(28));
                    entry.outputName = EditorGUILayout.TextField(entry.outputName);
                }

                // 3행: 프리셋 + 설정
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("프리셋", GUILayout.Width(38));
                    var newPreset = (FontPreset)EditorGUILayout.EnumPopup(entry.preset, GUILayout.Width(130));
                    if (newPreset != entry.preset)
                    {
                        entry.preset = newPreset;
                        if (!entry.overrideSettings)
                            ApplyPresetDefaults(entry);
                    }

                    GUILayout.FlexibleSpace();

                    entry.overrideSettings = EditorGUILayout.ToggleLeft("설정 오버라이드", entry.overrideSettings, GUILayout.Width(110));
                }

                // 4행: 상세 설정 (오버라이드 시)
                if (entry.overrideSettings)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Point Size", GUILayout.Width(65));
                        entry.samplingPointSize = EditorGUILayout.IntField(entry.samplingPointSize, GUILayout.Width(50));
                        EditorGUILayout.LabelField("Atlas", GUILayout.Width(35));
                        entry.atlasWidth = EditorGUILayout.IntPopup(entry.atlasWidth,
                            new[] { "512", "1024", "2048", "4096" },
                            new[] { 512, 1024, 2048, 4096 }, GUILayout.Width(60));
                        entry.atlasHeight = entry.atlasWidth;
                        EditorGUILayout.LabelField("Pad", GUILayout.Width(25));
                        entry.padding = EditorGUILayout.IntField(entry.padding, GUILayout.Width(35));
                    }
                }

                // 기존 에셋이 Static인 경우 경고
                if (entry.existingAsset != null &&
                    entry.existingAsset.atlasPopulationMode == AtlasPopulationMode.Static)
                {
                    EditorGUILayout.HelpBox(
                        "현재 Static 모드 — 한글 글리프를 런타임에 로드할 수 없습니다. 재생성하면 Dynamic으로 전환됩니다.",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.Space(2);
        }

        void DrawActions()
        {
            DrawSeparator();
            EditorGUILayout.Space(4);

            // 요약
            int enabledCount = entries.Count(e => e.enabled);
            int missingSource = entries.Count(e => e.enabled && e.sourceFont == null);
            int needsGeneration = entries.Count(e => e.enabled && e.sourceFont != null &&
                (e.existingAsset == null || e.existingAsset.atlasPopulationMode == AtlasPopulationMode.Static));

            EditorGUILayout.LabelField(
                $"선택: {enabledCount}개  |  소스 없음: {missingSource}개  |  생성 필요: {needsGeneration}개",
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                GUI.enabled = needsGeneration > 0;
                if (GUILayout.Button("생성 필요한 것만", GUILayout.Width(130), GUILayout.Height(30)))
                {
                    GenerateAssets(onlyMissing: true);
                }

                GUI.enabled = enabledCount > 0 && missingSource < enabledCount;
                if (GUILayout.Button("선택 항목 모두 생성", GUILayout.Width(140), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("폰트 에셋 재생성",
                        "이미 존재하는 에셋도 덮어씌워집니다.\n계속하시겠습니까?",
                        "생성", "취소"))
                    {
                        GenerateAssets(onlyMissing: false);
                    }
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(8);
        }

        // ─── 생성 로직 ────────────────────────────────────────

        void GenerateAssets(bool onlyMissing)
        {
            int generated = 0;
            int failed = 0;

            var toGenerate = entries.Where(e => e.enabled && e.sourceFont != null).ToList();
            if (onlyMissing)
            {
                toGenerate = toGenerate.Where(e =>
                    e.existingAsset == null ||
                    e.existingAsset.atlasPopulationMode == AtlasPopulationMode.Static
                ).ToList();
            }

            for (int i = 0; i < toGenerate.Count; i++)
            {
                var entry = toGenerate[i];
                EditorUtility.DisplayProgressBar("Font Asset Factory",
                    $"{entry.label} 생성 중... ({i + 1}/{toGenerate.Count})",
                    (float)i / toGenerate.Count);

                try
                {
                    GenerateSingle(entry);
                    generated++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FontAssetFactory] {entry.label} 생성 실패: {ex.Message}");
                    failed++;
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshStatus();

            string msg = $"완료: {generated}개 생성";
            if (failed > 0) msg += $", {failed}개 실패";
            EditorUtility.DisplayDialog("Font Asset Factory", msg, "확인");

            Debug.Log($"[FontAssetFactory] {msg}");
        }

        void GenerateSingle(FontEntry entry)
        {
            string outputPath = $"{fontsFolder}/{entry.outputName}.asset";

            // 기존 에셋 삭제 (서브에셋 포함)
            if (entry.existingAsset != null)
            {
                string existingPath = AssetDatabase.GetAssetPath(entry.existingAsset);
                if (!string.IsNullOrEmpty(existingPath))
                {
                    AssetDatabase.DeleteAsset(existingPath);
                }
            }

            // Dynamic SDF 폰트 에셋 생성
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                entry.sourceFont,
                entry.samplingPointSize,
                entry.padding,
                GlyphRenderMode.SDFAA,
                entry.atlasWidth,
                entry.atlasHeight,
                AtlasPopulationMode.Dynamic
            );

            if (fontAsset == null)
                throw new Exception("TMP_FontAsset.CreateFontAsset() returned null");

            fontAsset.name = entry.outputName;

            // 아틀라스 텍스처 서브에셋
            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = $"{entry.outputName} Atlas";
            }

            // 머티리얼 서브에셋
            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{entry.outputName} Material";
            }

            // 에셋 저장
            AssetDatabase.CreateAsset(fontAsset, outputPath);

            if (fontAsset.atlasTexture != null)
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);

            if (fontAsset.material != null)
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            Debug.Log($"[FontAssetFactory] 생성 완료: {outputPath} " +
                      $"(Dynamic, {entry.samplingPointSize}pt, {entry.atlasWidth}×{entry.atlasHeight}, SDFAA)");
        }

        // ─── 유틸 ────────────────────────────────────────────

        static void DrawSeparator()
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space(2);
        }
    }
}
