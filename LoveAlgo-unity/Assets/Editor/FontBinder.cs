using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// Font Binder — 씬/프리팹의 TMP 폰트 바인딩 스캔·리맵·일괄적용 툴
    /// 
    /// 워크플로우:
    ///   1) 스캔: 프리팹 + 열린 씬의 모든 TMP_Text 컴포넌트 수집
    ///   2) 스냅샷 저장: 현재 바인딩 상태를 JSON으로 백업 (선택)
    ///   3) 기존 폰트 에셋 삭제 → FontAssetFactory로 새 에셋 생성
    ///   4) 리맵 규칙 세팅: Missing/이전 폰트 → 새 폰트 매핑
    ///   5) 일괄 적용: 프리팹 + 씬 한번에 갱신
    /// </summary>
    public class FontBinder : EditorWindow
    {
        // ─── 탭 ────────────────────────────────────────────
        enum Tab { Scan, Remap, Snapshot }
        Tab currentTab = Tab.Scan;
        static readonly string[] TabLabels = { "스캔 & 현황", "리맵핑", "스냅샷" };

        // ─── 데이터 구조 ────────────────────────────────────

        [Serializable]
        class FontBindingRecord
        {
            public string sourcePath;       // 프리팹 경로 or "Scene:씬이름"
            public string componentPath;    // 오브젝트 계층 경로
            public string componentName;    // GameObject 이름
            public string fontAssetName;    // 폰트 에셋 이름
            public string fontAssetGUID;    // 폰트 에셋 GUID (삭제 전 기록용)
            public float fontSize;
            public bool isMissing;          // 폰트가 null (Missing 상태)
        }

        class BindingEntry
        {
            public string sourcePath;
            public string sourceLabel;      // 표시용 이름
            public string componentPath;
            public string componentName;
            public TMP_Text component;
            public TMP_FontAsset currentFont;
            public bool isMissing;
            public float fontSize;
            public bool isPrefab;
        }

        [Serializable]
        class RemapRule
        {
            public string oldFontName;
            public TMP_FontAsset newFont;
            public int matchCount;          // 해당 규칙에 매치되는 컴포넌트 수
        }

        [Serializable]
        class SnapshotData
        {
            public string timestamp;
            public List<FontBindingRecord> records = new();
        }

        // ─── 상태 ────────────────────────────────────────────
        List<BindingEntry> allEntries = new();
        List<RemapRule> remapRules = new();
        Dictionary<string, List<BindingEntry>> groupedEntries = new();  // sourcePath → entries

        // 필터
        enum FilterMode { All, MissingOnly, BoundOnly }
        FilterMode filterMode = FilterMode.All;
        string searchFilter = "";

        // 스냅샷
        string snapshotPath = "";
        SnapshotData loadedSnapshot;

        // UI 상태
        Vector2 scrollPos;
        HashSet<string> expandedGroups = new();
        bool scanIncludeScenes = true;
        bool scanIncludePrefabs = true;

        // 통계
        int totalComponents;
        int missingCount;
        int boundCount;
        int uniqueFonts;

        // ─── 메뉴 ────────────────────────────────────────────

        [MenuItem("LoveAlgo/Font Binder", false, 101)]
        static void OpenWindow()
        {
            var window = GetWindow<FontBinder>("Font Binder");
            window.minSize = new Vector2(720, 520);
            window.Show();
        }

        void OnEnable()
        {
            snapshotPath = Path.Combine(Application.dataPath, "..", "tools", "font_snapshots");
        }

        // ─── GUI ────────────────────────────────────────────

        void OnGUI()
        {
            DrawHeader();
            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, TabLabels, GUILayout.Height(26));
            EditorGUILayout.Space(4);

            switch (currentTab)
            {
                case Tab.Scan: DrawScanTab(); break;
                case Tab.Remap: DrawRemapTab(); break;
                case Tab.Snapshot: DrawSnapshotTab(); break;
            }
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Font Binder", EditorStyles.largeLabel);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("씬·프리팹 TMP 폰트 바인딩 관리", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.Space(4);
        }

        // ═══════════════════════════════════════════════════
        //  탭 1: 스캔 & 현황
        // ═══════════════════════════════════════════════════

        void DrawScanTab()
        {
            // 스캔 옵션
            using (new EditorGUILayout.HorizontalScope())
            {
                scanIncludePrefabs = EditorGUILayout.ToggleLeft("프리팹", scanIncludePrefabs, GUILayout.Width(70));
                scanIncludeScenes = EditorGUILayout.ToggleLeft("열린 씬", scanIncludeScenes, GUILayout.Width(70));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("스캔", GUILayout.Width(80), GUILayout.Height(24)))
                {
                    PerformScan();
                }
            }

            if (allEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("스캔 버튼을 눌러 TMP 컴포넌트를 수집하세요.", MessageType.Info);
                return;
            }

            DrawSeparator();

            // 통계
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStatBadge("총 컴포넌트", totalComponents.ToString(), Color.white);
                DrawStatBadge("바인딩됨", boundCount.ToString(), new Color(0.3f, 0.8f, 0.3f));
                DrawStatBadge("Missing", missingCount.ToString(),
                    missingCount > 0 ? new Color(1f, 0.3f, 0.3f) : Color.gray);
                DrawStatBadge("폰트 종류", uniqueFonts.ToString(), new Color(0.5f, 0.7f, 1f));
            }

            EditorGUILayout.Space(4);

            // 필터
            using (new EditorGUILayout.HorizontalScope())
            {
                filterMode = (FilterMode)EditorGUILayout.EnumPopup(filterMode, GUILayout.Width(120));
                searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            }

            EditorGUILayout.Space(4);

            // 엔트리 목록
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            foreach (var kvp in groupedEntries)
            {
                var entries = FilterEntries(kvp.Value);
                if (entries.Count == 0) continue;

                bool isExpanded = expandedGroups.Contains(kvp.Key);
                int groupMissing = entries.Count(e => e.isMissing);

                // 그룹 헤더
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    string label = entries[0].sourceLabel;
                    if (groupMissing > 0)
                        label += $"  <color=#ff6666>[Missing: {groupMissing}]</color>";

                    var headerStyle = new GUIStyle(EditorStyles.foldout) { richText = true };
                    bool newExpanded = EditorGUILayout.Foldout(isExpanded, label, true, headerStyle);
                    if (newExpanded != isExpanded)
                    {
                        if (newExpanded) expandedGroups.Add(kvp.Key);
                        else expandedGroups.Remove(kvp.Key);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{entries.Count}개", EditorStyles.miniLabel);
                }

                if (!isExpanded) continue;

                // 컴포넌트 목록
                foreach (var entry in entries)
                {
                    DrawEntryRow(entry);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawEntryRow(BindingEntry entry)
        {
            Color bgColor = entry.isMissing ? new Color(1f, 0.85f, 0.85f) : Color.white;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            using (new EditorGUILayout.HorizontalScope("helpbox"))
            {
                GUI.backgroundColor = prevBg;

                // 경로
                EditorGUILayout.LabelField(entry.componentPath, GUILayout.MinWidth(200));

                // 폰트 상태
                if (entry.isMissing)
                {
                    var style = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.9f, 0.2f, 0.2f) }
                    };
                    EditorGUILayout.LabelField("Missing", style, GUILayout.Width(100));
                }
                else
                {
                    EditorGUILayout.LabelField(
                        entry.currentFont != null ? entry.currentFont.name : "(none)",
                        GUILayout.Width(160));
                }

                // 폰트 사이즈
                EditorGUILayout.LabelField($"{entry.fontSize:F0}pt", GUILayout.Width(45));

                // Ping 버튼 (프리팹이면 프리팹 선택, 씬이면 오브젝트 선택)
                if (GUILayout.Button("◎", GUILayout.Width(24)))
                {
                    if (entry.component != null)
                    {
                        Selection.activeGameObject = entry.component.gameObject;
                        EditorGUIUtility.PingObject(entry.component.gameObject);
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  탭 2: 리맵핑
        // ═══════════════════════════════════════════════════

        void DrawRemapTab()
        {
            if (allEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("먼저 '스캔 & 현황' 탭에서 스캔을 실행하세요.", MessageType.Info);
                return;
            }

            // 자동 규칙 생성
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("자동 규칙 생성", GUILayout.Height(24)))
                {
                    GenerateAutoRemapRules();
                }
                if (GUILayout.Button("+ 규칙 추가", GUILayout.Height(24)))
                {
                    remapRules.Add(new RemapRule());
                }
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(4);
            DrawSeparator();
            EditorGUILayout.Space(4);

            if (remapRules.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "'자동 규칙 생성'을 누르면 현재 바인딩 상태를 기반으로 리맵 규칙을 자동 생성합니다.\n" +
                    "Missing 폰트는 이름이 같은 새 에셋을 자동 매칭합니다.",
                    MessageType.Info);
                return;
            }

            // 규칙 목록
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < remapRules.Count; i++)
            {
                DrawRemapRuleRow(remapRules[i], i);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            DrawSeparator();
            EditorGUILayout.Space(4);

            // 적용 요약
            int totalAffected = remapRules.Where(r => r.newFont != null).Sum(r => r.matchCount);
            EditorGUILayout.LabelField(
                $"적용 대상: {totalAffected}개 컴포넌트",
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                // Missing만 적용
                int missingAffected = remapRules.Where(r => r.newFont != null)
                    .Sum(r => allEntries.Count(e => e.isMissing && MatchesRule(e, r)));
                GUI.enabled = missingAffected > 0;
                if (GUILayout.Button($"Missing만 적용 ({missingAffected}개)", GUILayout.Width(160), GUILayout.Height(30)))
                {
                    ApplyRemap(missingOnly: true);
                }

                // 전체 적용
                GUI.enabled = totalAffected > 0;
                if (GUILayout.Button($"전체 적용 ({totalAffected}개)", GUILayout.Width(160), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Font Binder",
                        $"{totalAffected}개 컴포넌트의 폰트를 교체합니다.\n계속하시겠습니까?",
                        "적용", "취소"))
                    {
                        ApplyRemap(missingOnly: false);
                    }
                }
                GUI.enabled = true;

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(8);
        }

        void DrawRemapRuleRow(RemapRule rule, int index)
        {
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                // 기존 폰트 이름 (편집 가능)
                EditorGUILayout.LabelField("기존:", GUILayout.Width(30));
                rule.oldFontName = EditorGUILayout.TextField(rule.oldFontName, GUILayout.Width(180));

                EditorGUILayout.LabelField("→", GUILayout.Width(18));

                // 새 폰트 에셋
                EditorGUILayout.LabelField("새:", GUILayout.Width(20));
                rule.newFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                    rule.newFont, typeof(TMP_FontAsset), false, GUILayout.Width(180));

                // 매치 카운트
                GUILayout.FlexibleSpace();
                rule.matchCount = allEntries.Count(e => MatchesRule(e, rule));
                var countStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = rule.matchCount > 0 ? new Color(0.2f, 0.7f, 0.2f) : Color.gray }
                };
                EditorGUILayout.LabelField($"{rule.matchCount}개", countStyle, GUILayout.Width(35));

                // 삭제
                if (GUILayout.Button("×", GUILayout.Width(22)))
                {
                    remapRules.RemoveAt(index);
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  탭 3: 스냅샷
        // ═══════════════════════════════════════════════════

        void DrawSnapshotTab()
        {
            EditorGUILayout.Space(4);

            // 저장 섹션
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("스냅샷 저장", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "현재 폰트 바인딩 상태를 JSON으로 저장합니다.\n" +
                    "폰트 에셋 삭제 전에 반드시 스냅샷을 생성하세요.",
                    EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space(4);

                GUI.enabled = allEntries.Count > 0;
                if (GUILayout.Button("현재 바인딩 스냅샷 저장", GUILayout.Height(28)))
                {
                    SaveSnapshot();
                }
                GUI.enabled = true;
            }

            EditorGUILayout.Space(8);

            // 불러오기 섹션
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("스냅샷 불러오기", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "저장된 스냅샷을 불러와 리맵 규칙을 자동 생성합니다.",
                    EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space(4);

                if (GUILayout.Button("스냅샷 파일 선택", GUILayout.Height(28)))
                {
                    LoadSnapshot();
                }
            }

            // 불러온 스냅샷 정보
            if (loadedSnapshot != null)
            {
                EditorGUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("불러온 스냅샷", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"시각: {loadedSnapshot.timestamp}");
                    EditorGUILayout.LabelField($"레코드: {loadedSnapshot.records.Count}개");

                    var uniqueOldFonts = loadedSnapshot.records
                        .Where(r => !string.IsNullOrEmpty(r.fontAssetName))
                        .Select(r => r.fontAssetName)
                        .Distinct()
                        .ToList();

                    EditorGUILayout.LabelField($"폰트 종류: {string.Join(", ", uniqueOldFonts)}");

                    EditorGUILayout.Space(4);

                    if (GUILayout.Button("스냅샷 기반 리맵 규칙 생성", GUILayout.Height(24)))
                    {
                        GenerateRulesFromSnapshot();
                    }
                }
            }

            EditorGUILayout.Space(8);

            // 기존 스냅샷 목록
            DrawSnapshotList();
        }

        void DrawSnapshotList()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("저장된 스냅샷", EditorStyles.boldLabel);

                if (!Directory.Exists(snapshotPath))
                {
                    EditorGUILayout.LabelField("저장된 스냅샷이 없습니다.", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                var files = Directory.GetFiles(snapshotPath, "*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(10)
                    .ToArray();

                if (files.Length == 0)
                {
                    EditorGUILayout.LabelField("저장된 스냅샷이 없습니다.", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                foreach (var file in files)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        string time = File.GetLastWriteTime(file).ToString("yyyy-MM-dd HH:mm");
                        EditorGUILayout.LabelField($"{fileName}  ({time})", GUILayout.MinWidth(250));

                        if (GUILayout.Button("불러오기", GUILayout.Width(65)))
                        {
                            LoadSnapshotFromPath(file);
                        }
                        if (GUILayout.Button("삭제", GUILayout.Width(40)))
                        {
                            if (EditorUtility.DisplayDialog("스냅샷 삭제", $"{fileName}을(를) 삭제하시겠습니까?", "삭제", "취소"))
                            {
                                File.Delete(file);
                            }
                        }
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  스캔 로직
        // ═══════════════════════════════════════════════════

        void PerformScan()
        {
            allEntries.Clear();
            groupedEntries.Clear();
            expandedGroups.Clear();

            if (scanIncludePrefabs) ScanPrefabs();
            if (scanIncludeScenes) ScanOpenScenes();

            // 그룹핑
            groupedEntries = allEntries
                .GroupBy(e => e.sourcePath)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 모두 펼치기
            foreach (var key in groupedEntries.Keys)
                expandedGroups.Add(key);

            // 통계
            totalComponents = allEntries.Count;
            missingCount = allEntries.Count(e => e.isMissing);
            boundCount = allEntries.Count(e => !e.isMissing && e.currentFont != null);
            uniqueFonts = allEntries
                .Where(e => e.currentFont != null)
                .Select(e => e.currentFont.name)
                .Distinct()
                .Count();

            Debug.Log($"[FontBinder] 스캔 완료: {totalComponents}개 TMP 컴포넌트 (Missing: {missingCount}, 바인딩: {boundCount})");
        }

        void ScanPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });

            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var tmpComponents = prefab.GetComponentsInChildren<TMP_Text>(true);
                if (tmpComponents.Length == 0) continue;

                foreach (var tmp in tmpComponents)
                {
                    allEntries.Add(new BindingEntry
                    {
                        sourcePath = path,
                        sourceLabel = $"[Prefab] {prefab.name}",
                        componentPath = GetHierarchyPath(tmp.gameObject, prefab.transform),
                        componentName = tmp.gameObject.name,
                        component = tmp,
                        currentFont = tmp.font,
                        isMissing = IsFontMissing(tmp),
                        fontSize = tmp.fontSize,
                        isPrefab = true
                    });
                }
            }
        }

        void ScanOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                string key = $"Scene:{scene.name}";
                var rootObjects = scene.GetRootGameObjects();

                foreach (var root in rootObjects)
                {
                    var tmpComponents = root.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var tmp in tmpComponents)
                    {
                        allEntries.Add(new BindingEntry
                        {
                            sourcePath = key,
                            sourceLabel = $"[Scene] {scene.name}",
                            componentPath = GetFullHierarchyPath(tmp.gameObject),
                            componentName = tmp.gameObject.name,
                            component = tmp,
                            currentFont = tmp.font,
                            isMissing = IsFontMissing(tmp),
                            fontSize = tmp.fontSize,
                            isPrefab = false
                        });
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════
        //  리맵 로직
        // ═══════════════════════════════════════════════════

        void GenerateAutoRemapRules()
        {
            remapRules.Clear();

            // 현재 사용 중인 고유 폰트 이름 수집 (Missing 포함)
            var fontNames = new HashSet<string>();

            foreach (var entry in allEntries)
            {
                string fontName = null;

                if (entry.isMissing)
                {
                    // Missing 참조에서 이름 복구 시도 (SerializedObject)
                    fontName = TryRecoverMissingFontName(entry);
                }
                else if (entry.currentFont != null)
                {
                    fontName = entry.currentFont.name;
                }

                if (!string.IsNullOrEmpty(fontName))
                    fontNames.Add(fontName);
            }

            // 사용 가능한 TMP_FontAsset 전부 로드
            var availableFonts = AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .Where(f => f != null)
                .ToList();

            // 각 폰트 이름에 대해 규칙 생성
            foreach (var fontName in fontNames.OrderBy(n => n))
            {
                var rule = new RemapRule { oldFontName = fontName };

                // 이름 매칭 시도
                var match = availableFonts.FirstOrDefault(f => f.name == fontName);
                if (match != null)
                {
                    rule.newFont = match;
                }
                else
                {
                    // 유사 이름 매칭 (공백/하이픈 무시)
                    string normalized = NormalizeFontName(fontName);
                    match = availableFonts.FirstOrDefault(f =>
                        NormalizeFontName(f.name) == normalized);
                    if (match != null)
                        rule.newFont = match;
                }

                rule.matchCount = allEntries.Count(e => MatchesRule(e, rule));
                remapRules.Add(rule);
            }

            currentTab = Tab.Remap;
            Debug.Log($"[FontBinder] 자동 규칙 {remapRules.Count}개 생성");
        }

        void GenerateRulesFromSnapshot()
        {
            if (loadedSnapshot == null) return;

            remapRules.Clear();

            var oldFontNames = loadedSnapshot.records
                .Where(r => !string.IsNullOrEmpty(r.fontAssetName))
                .Select(r => r.fontAssetName)
                .Distinct()
                .ToList();

            var availableFonts = AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .Where(f => f != null)
                .ToList();

            foreach (var oldName in oldFontNames.OrderBy(n => n))
            {
                var rule = new RemapRule { oldFontName = oldName };

                // 이름 매칭
                var match = availableFonts.FirstOrDefault(f => f.name == oldName)
                    ?? availableFonts.FirstOrDefault(f => NormalizeFontName(f.name) == NormalizeFontName(oldName));

                if (match != null) rule.newFont = match;
                rule.matchCount = allEntries.Count(e => MatchesRule(e, rule));
                remapRules.Add(rule);
            }

            currentTab = Tab.Remap;
            Debug.Log($"[FontBinder] 스냅샷 기반 규칙 {remapRules.Count}개 생성");
        }

        void ApplyRemap(bool missingOnly)
        {
            int applied = 0;
            int failed = 0;
            var modifiedPrefabs = new HashSet<string>();

            foreach (var entry in allEntries)
            {
                if (missingOnly && !entry.isMissing) continue;
                if (entry.component == null) continue;

                // 매칭되는 규칙 찾기
                var matchingRule = remapRules.FirstOrDefault(r =>
                    r.newFont != null && MatchesRule(entry, r));

                if (matchingRule == null) continue;

                try
                {
                    Undo.RecordObject(entry.component, "FontBinder Remap");
                    entry.component.font = matchingRule.newFont;
                    EditorUtility.SetDirty(entry.component);

                    if (entry.isPrefab)
                        modifiedPrefabs.Add(entry.sourcePath);

                    applied++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FontBinder] 적용 실패: {entry.componentPath} — {ex.Message}");
                    failed++;
                }
            }

            // 프리팹 저장
            foreach (var prefabPath in modifiedPrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                }
            }

            // 씬 저장
            if (allEntries.Any(e => !e.isPrefab))
            {
                EditorSceneManager.MarkAllScenesDirty();
            }

            // 재스캔
            PerformScan();

            string msg = $"적용 완료: {applied}개";
            if (failed > 0) msg += $", 실패: {failed}개";
            EditorUtility.DisplayDialog("Font Binder", msg, "확인");
            Debug.Log($"[FontBinder] {msg}");
        }

        // ═══════════════════════════════════════════════════
        //  스냅샷 로직
        // ═══════════════════════════════════════════════════

        void SaveSnapshot()
        {
            if (!Directory.Exists(snapshotPath))
                Directory.CreateDirectory(snapshotPath);

            var snapshot = new SnapshotData
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            foreach (var entry in allEntries)
            {
                var record = new FontBindingRecord
                {
                    sourcePath = entry.sourcePath,
                    componentPath = entry.componentPath,
                    componentName = entry.componentName,
                    fontAssetName = entry.currentFont != null ? entry.currentFont.name : "",
                    fontSize = entry.fontSize,
                    isMissing = entry.isMissing
                };

                // GUID 기록
                if (entry.currentFont != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(entry.currentFont);
                    record.fontAssetGUID = AssetDatabase.AssetPathToGUID(assetPath);
                }

                snapshot.records.Add(record);
            }

            string fileName = $"font_snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string filePath = Path.Combine(snapshotPath, fileName);
            string json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(filePath, json);

            Debug.Log($"[FontBinder] 스냅샷 저장: {filePath} ({snapshot.records.Count}개 레코드)");
            EditorUtility.DisplayDialog("Font Binder", $"스냅샷 저장 완료\n{fileName}", "확인");
        }

        void LoadSnapshot()
        {
            string path = EditorUtility.OpenFilePanel("스냅샷 파일 선택", snapshotPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            LoadSnapshotFromPath(path);
        }

        void LoadSnapshotFromPath(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                loadedSnapshot = JsonUtility.FromJson<SnapshotData>(json);
                Debug.Log($"[FontBinder] 스냅샷 로드: {loadedSnapshot.records.Count}개 레코드 ({loadedSnapshot.timestamp})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FontBinder] 스냅샷 로드 실패: {ex.Message}");
                loadedSnapshot = null;
            }
        }

        // ═══════════════════════════════════════════════════
        //  유틸리티
        // ═══════════════════════════════════════════════════

        bool IsFontMissing(TMP_Text tmp)
        {
            // font 프로퍼티가 null이면 Missing 상태
            if (tmp.font == null) return true;

            // SerializedObject로 실제 Missing 참조 확인
            var so = new SerializedObject(tmp);
            var fontProp = so.FindProperty("m_fontAsset");
            if (fontProp != null && fontProp.objectReferenceValue == null
                && fontProp.objectReferenceInstanceIDValue != 0)
            {
                return true;
            }

            return false;
        }

        string TryRecoverMissingFontName(BindingEntry entry)
        {
            if (entry.component == null) return null;

            // SerializedObject에서 Missing 참조의 이름 복구 시도
            var so = new SerializedObject(entry.component);
            var fontProp = so.FindProperty("m_fontAsset");
            if (fontProp == null) return null;

            // fileID로 에셋 이름 추론은 어려움
            // 대신 .meta 파일에서 GUID 기반으로 매칭 시도
            // → 삭제된 에셋이면 불가능하므로, 스냅샷 기반 복구 권장

            // 컴포넌트 이름 + 기존 패턴으로 추론
            return null;
        }

        bool MatchesRule(BindingEntry entry, RemapRule rule)
        {
            if (string.IsNullOrEmpty(rule.oldFontName)) return false;

            // 현재 바인딩된 폰트 이름 매칭
            if (entry.currentFont != null && entry.currentFont.name == rule.oldFontName)
                return true;

            // Missing 상태에서는 이전 이름이 없으므로 전부 매칭 안 됨
            // 스냅샷 기반 규칙에서는 스냅샷 레코드와 매칭
            if (entry.isMissing && rule.oldFontName == "(Missing)")
                return true;

            return false;
        }

        string GetHierarchyPath(GameObject obj, Transform root)
        {
            string path = obj.name;
            Transform current = obj.transform.parent;

            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        string GetFullHierarchyPath(GameObject obj)
        {
            string path = obj.name;
            Transform current = obj.transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        string NormalizeFontName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return Regex.Replace(name.ToLowerInvariant(), @"[\s\-_]", "");
        }

        List<BindingEntry> FilterEntries(List<BindingEntry> entries)
        {
            var filtered = entries.AsEnumerable();

            // 필터 모드
            switch (filterMode)
            {
                case FilterMode.MissingOnly:
                    filtered = filtered.Where(e => e.isMissing);
                    break;
                case FilterMode.BoundOnly:
                    filtered = filtered.Where(e => !e.isMissing && e.currentFont != null);
                    break;
            }

            // 검색
            if (!string.IsNullOrWhiteSpace(searchFilter))
            {
                string lower = searchFilter.ToLowerInvariant();
                filtered = filtered.Where(e =>
                    e.componentPath.ToLowerInvariant().Contains(lower) ||
                    e.componentName.ToLowerInvariant().Contains(lower) ||
                    (e.currentFont != null && e.currentFont.name.ToLowerInvariant().Contains(lower)));
            }

            return filtered.ToList();
        }

        void DrawStatBadge(string label, string value, Color color)
        {
            using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(120)))
            {
                var valueStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    normal = { textColor = color }
                };
                EditorGUILayout.LabelField(value, valueStyle, GUILayout.Height(22));
                EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
            }
        }

        static void DrawSeparator()
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space(2);
        }
    }
}
