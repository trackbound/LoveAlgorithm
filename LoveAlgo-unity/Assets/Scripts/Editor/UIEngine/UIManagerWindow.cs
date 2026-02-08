using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using TMPro;
using Object = UnityEngine.Object;

namespace LoveAlgo.Editor.UIEngine
{
    /// <summary>
    /// LoveAlgo UI Manager - 프리팹 내 TMP 컴포넌트 중앙 관리 툴
    /// </summary>
    public class UIManagerWindow : EditorWindow
    {
        [MenuItem("LoveAlgo/UI Manager %#u", priority = 100)]
        static void OpenWindow()
        {
            var window = GetWindow<UIManagerWindow>();
            window.titleContent = new GUIContent("UI Manager", EditorGUIUtility.IconContent("d_Font Icon").image);
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        // 데이터
        List<PrefabData> prefabs = new();
        FontProfile activeProfile;
        string searchFilter = "";
        
        // 선택
        HashSet<TMPComponentData> selectedComponents = new();
        PrefabData selectedPrefab;
        TMPComponentData selectedComponent;

        // UI 상태
        Vector2 leftScrollPos;
        Vector2 rightScrollPos;
        float splitWidth = 300f;
        bool isDraggingSplit;

        // 스타일
        GUIStyle prefabHeaderStyle;
        GUIStyle componentStyle;
        GUIStyle selectedStyle;
        GUIStyle statusStyle;
        bool stylesInitialized;

        // 수정 추적
        HashSet<string> modifiedPrefabPaths = new();

        #region Data Structures

        class PrefabData
        {
            public string path;
            public string name;
            public GameObject prefab;
            public List<TMPComponentData> tmpComponents = new();
            public bool isExpanded = true;
        }

        class TMPComponentData
        {
            public TMP_Text component;
            public string path; // 프리팹 내 경로
            public string name;
            public FontTag fontTag;
            public FontTag.FontType inferredType; // FontTag 없을 때 추론된 타입
            
            // 원본 값 (되돌리기용)
            public TMP_FontAsset originalFont;
            public float originalSize;
            public Color originalColor;
        }

        #endregion

        void OnEnable()
        {
            LoadProfile();
            ScanPrefabs();
        }

        void InitStyles()
        {
            if (stylesInitialized) return;

            prefabHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            componentStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(20, 4, 2, 2)
            };

            selectedStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(20, 4, 2, 2),
                normal = { background = MakeTex(2, 2, new Color(0.24f, 0.49f, 0.91f, 0.5f)) }
            };

            statusStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11
            };

            stylesInitialized = true;
        }

        Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        void OnGUI()
        {
            InitStyles();
            DrawToolbar();
            DrawMainArea();
            DrawStatusBar();
            HandleSplitDrag();
        }

        #region Toolbar

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // 스캔 버튼
            if (GUILayout.Button(new GUIContent(" Scan", EditorGUIUtility.IconContent("d_Refresh").image), 
                EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                ScanPrefabs();
            }

            // 저장 버튼
            GUI.enabled = modifiedPrefabPaths.Count > 0;
            if (GUILayout.Button(new GUIContent(" Save All", EditorGUIUtility.IconContent("d_SaveAs").image), 
                EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                SaveAllModified();
            }
            GUI.enabled = true;

            // 되돌리기 버튼
            if (GUILayout.Button(new GUIContent(" Revert", EditorGUIUtility.IconContent("d_preAudioLoopOff").image), 
                EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RevertAll();
            }

            GUILayout.FlexibleSpace();

            // 검색
            GUILayout.Label("Search:", EditorStyles.toolbarButton, GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));

            GUILayout.Space(10);

            // 프로파일 선택
            GUILayout.Label("Profile:", EditorStyles.toolbarButton, GUILayout.Width(50));
            var newProfile = (FontProfile)EditorGUILayout.ObjectField(activeProfile, typeof(FontProfile), false, GUILayout.Width(150));
            if (newProfile != activeProfile)
            {
                activeProfile = newProfile;
                SaveProfile();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Main Area

        void DrawMainArea()
        {
            EditorGUILayout.BeginHorizontal();
            
            // 왼쪽: 프리팹 트리
            DrawPrefabTree();
            
            // 스플리터
            DrawSplitter();
            
            // 오른쪽: 속성 패널
            DrawPropertiesPanel();
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawPrefabTree()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(splitWidth));
            
            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
            
            leftScrollPos = EditorGUILayout.BeginScrollView(leftScrollPos, EditorStyles.helpBox);

            foreach (var prefabData in prefabs)
            {
                if (!MatchesFilter(prefabData)) continue;
                DrawPrefabItem(prefabData);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        bool MatchesFilter(PrefabData prefab)
        {
            if (string.IsNullOrEmpty(searchFilter)) return true;
            
            string filter = searchFilter.ToLower();
            if (prefab.name.ToLower().Contains(filter)) return true;
            
            return prefab.tmpComponents.Any(c => c.name.ToLower().Contains(filter));
        }

        void DrawPrefabItem(PrefabData prefabData)
        {
            bool isModified = modifiedPrefabPaths.Contains(prefabData.path);
            string displayName = isModified ? $"📦 {prefabData.name} *" : $"📦 {prefabData.name}";
            
            EditorGUILayout.BeginHorizontal();
            
            prefabData.isExpanded = EditorGUILayout.Foldout(prefabData.isExpanded, displayName, true, prefabHeaderStyle);
            
            // 프리팹 선택 버튼
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                Selection.activeObject = prefabData.prefab;
                EditorGUIUtility.PingObject(prefabData.prefab);
            }
            
            EditorGUILayout.EndHorizontal();

            if (prefabData.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var comp in prefabData.tmpComponents)
                {
                    if (!string.IsNullOrEmpty(searchFilter) && 
                        !comp.name.ToLower().Contains(searchFilter.ToLower())) continue;
                    
                    DrawComponentItem(prefabData, comp);
                }
                EditorGUI.indentLevel--;
            }
        }

        void DrawComponentItem(PrefabData prefab, TMPComponentData comp)
        {
            bool isSelected = selectedComponents.Contains(comp);
            var style = isSelected ? selectedStyle : componentStyle;
            
            EditorGUILayout.BeginHorizontal();
            
            // 체크박스
            bool wasSelected = isSelected;
            isSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
            if (isSelected != wasSelected)
            {
                if (isSelected) selectedComponents.Add(comp);
                else selectedComponents.Remove(comp);
            }

            // 아이콘 + 이름
            string icon = comp.fontTag != null ? "📝" : "📄";
            string tagLabel = comp.fontTag != null ? $"[{comp.fontTag.fontType}]" : $"[{comp.inferredType}?]";
            
            var rect = EditorGUILayout.GetControlRect();
            if (GUI.Button(rect, $"{icon} {comp.name} {tagLabel}", style))
            {
                selectedPrefab = prefab;
                selectedComponent = comp;
                
                // Ctrl 클릭이 아니면 기존 선택 해제
                if (!Event.current.control)
                {
                    selectedComponents.Clear();
                }
                selectedComponents.Add(comp);
            }
            
            EditorGUILayout.EndHorizontal();
        }

        void DrawSplitter()
        {
            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(5));
            rect.height = position.height;
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                isDraggingSplit = true;
                Event.current.Use();
            }
            
            GUI.Box(rect, "", EditorStyles.helpBox);
        }

        void HandleSplitDrag()
        {
            if (isDraggingSplit)
            {
                splitWidth = Mathf.Clamp(Event.current.mousePosition.x, 200, position.width - 300);
                Repaint();
                
                if (Event.current.type == EventType.MouseUp)
                {
                    isDraggingSplit = false;
                }
            }
        }

        void DrawPropertiesPanel()
        {
            EditorGUILayout.BeginVertical();
            
            rightScrollPos = EditorGUILayout.BeginScrollView(rightScrollPos, EditorStyles.helpBox);

            if (selectedComponents.Count == 0)
            {
                EditorGUILayout.HelpBox("왼쪽에서 TMP 컴포넌트를 선택하세요.\nCtrl+클릭으로 다중 선택 가능", MessageType.Info);
            }
            else if (selectedComponents.Count == 1 && selectedComponent != null)
            {
                DrawSingleComponentProperties();
            }
            else
            {
                DrawBulkProperties();
            }

            EditorGUILayout.Space(20);
            DrawProfileActions();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawSingleComponentProperties()
        {
            EditorGUILayout.LabelField("Selected Component", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(selectedComponent.path, EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            if (selectedComponent.component == null)
            {
                EditorGUILayout.HelpBox("컴포넌트가 삭제되었습니다.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();

            // Font Asset
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Font Asset", GUILayout.Width(100));
            var newFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                selectedComponent.component.font, typeof(TMP_FontAsset), false);
            if (newFont != selectedComponent.component.font)
            {
                Undo.RecordObject(selectedComponent.component, "Change Font");
                selectedComponent.component.font = newFont;
                MarkModified(selectedPrefab);
            }
            EditorGUILayout.EndHorizontal();

            // Font Size
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Font Size", GUILayout.Width(100));
            float newSize = EditorGUILayout.FloatField(selectedComponent.component.fontSize);
            if (!Mathf.Approximately(newSize, selectedComponent.component.fontSize))
            {
                Undo.RecordObject(selectedComponent.component, "Change Font Size");
                selectedComponent.component.fontSize = newSize;
                MarkModified(selectedPrefab);
            }
            EditorGUILayout.EndHorizontal();

            // Color
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Color", GUILayout.Width(100));
            Color newColor = EditorGUILayout.ColorField(selectedComponent.component.color);
            if (newColor != selectedComponent.component.color)
            {
                Undo.RecordObject(selectedComponent.component, "Change Color");
                selectedComponent.component.color = newColor;
                MarkModified(selectedPrefab);
            }
            EditorGUILayout.EndHorizontal();

            // Alignment
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Alignment", GUILayout.Width(100));
            var newAlign = (TextAlignmentOptions)EditorGUILayout.EnumPopup(selectedComponent.component.alignment);
            if (newAlign != selectedComponent.component.alignment)
            {
                Undo.RecordObject(selectedComponent.component, "Change Alignment");
                selectedComponent.component.alignment = newAlign;
                MarkModified(selectedPrefab);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Font Tag", EditorStyles.boldLabel);

            // FontTag 관리
            if (selectedComponent.fontTag != null)
            {
                var newType = (FontTag.FontType)EditorGUILayout.EnumPopup("Type", selectedComponent.fontTag.fontType);
                if (newType != selectedComponent.fontTag.fontType)
                {
                    Undo.RecordObject(selectedComponent.fontTag, "Change Font Type");
                    selectedComponent.fontTag.fontType = newType;
                    MarkModified(selectedPrefab);
                }
                
                selectedComponent.fontTag.ignoreProfileApplication = 
                    EditorGUILayout.Toggle("Ignore Profile", selectedComponent.fontTag.ignoreProfileApplication);
            }
            else
            {
                EditorGUILayout.HelpBox($"FontTag 없음 (추론: {selectedComponent.inferredType})", MessageType.None);
                if (GUILayout.Button("Add FontTag"))
                {
                    var tag = selectedComponent.component.gameObject.AddComponent<FontTag>();
                    tag.fontType = selectedComponent.inferredType;
                    selectedComponent.fontTag = tag;
                    MarkModified(selectedPrefab);
                }
            }

            EditorGUI.EndChangeCheck();
        }

        void DrawBulkProperties()
        {
            EditorGUILayout.LabelField($"Bulk Edit ({selectedComponents.Count} selected)", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // 일괄 폰트 적용
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Set Font", GUILayout.Width(100));
            var bulkFont = (TMP_FontAsset)EditorGUILayout.ObjectField(null, typeof(TMP_FontAsset), false);
            if (bulkFont != null)
            {
                ApplyToSelected(c => c.font = bulkFont);
            }
            EditorGUILayout.EndHorizontal();

            // 일괄 크기 적용
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Set Size", GUILayout.Width(100));
            float bulkSize = EditorGUILayout.FloatField(0);
            if (bulkSize > 0 && GUILayout.Button("Apply", GUILayout.Width(60)))
            {
                ApplyToSelected(c => c.fontSize = bulkSize);
            }
            EditorGUILayout.EndHorizontal();

            // 일괄 색상 적용
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Set Color", GUILayout.Width(100));
            Color bulkColor = EditorGUILayout.ColorField(Color.white);
            if (GUILayout.Button("Apply", GUILayout.Width(60)))
            {
                ApplyToSelected(c => c.color = bulkColor);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // FontTag 일괄 설정
            EditorGUILayout.LabelField("Set Font Tag", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            var tagType = (FontTag.FontType)EditorGUILayout.EnumPopup(FontTag.FontType.UI);
            if (GUILayout.Button("Apply Tag", GUILayout.Width(80)))
            {
                foreach (var comp in selectedComponents)
                {
                    if (comp.fontTag == null)
                    {
                        comp.fontTag = comp.component.gameObject.AddComponent<FontTag>();
                    }
                    comp.fontTag.fontType = tagType;
                    MarkModifiedByComponent(comp);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawProfileActions()
        {
            if (activeProfile == null)
            {
                EditorGUILayout.HelpBox("FontProfile을 선택하면 프로파일 기반 일괄 적용이 가능합니다.", MessageType.Info);
                if (GUILayout.Button("Create New Profile"))
                {
                    CreateNewProfile();
                }
                return;
            }

            EditorGUILayout.LabelField("Profile Actions", EditorStyles.boldLabel);
            
            // 선택된 컴포넌트에 프로파일 적용
            GUI.enabled = selectedComponents.Count > 0;
            if (GUILayout.Button($"Apply Profile to Selected ({selectedComponents.Count})"))
            {
                ApplyProfileToSelected();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(5);

            // 전체에 프로파일 적용
            if (GUILayout.Button("Apply Profile to ALL (FontTag 기준)"))
            {
                if (EditorUtility.DisplayDialog("Confirm", 
                    "FontTag가 있는 모든 TMP 컴포넌트에 프로파일을 적용합니다.\n계속하시겠습니까?", 
                    "Apply", "Cancel"))
                {
                    ApplyProfileToAll();
                }
            }
        }

        #endregion

        #region Status Bar

        void DrawStatusBar()
        {
            int totalComponents = prefabs.Sum(p => p.tmpComponents.Count);
            string status = $"📦 {prefabs.Count} Prefabs | 📝 {totalComponents} TMP Components | ✏️ {modifiedPrefabPaths.Count} Modified";
            
            EditorGUILayout.LabelField(status, statusStyle);
        }

        #endregion

        #region Actions

        void ScanPrefabs()
        {
            prefabs.Clear();
            selectedComponents.Clear();
            selectedComponent = null;

            string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();

            foreach (var path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var tmpComponents = prefab.GetComponentsInChildren<TMP_Text>(true);
                if (tmpComponents.Length == 0) continue;

                var prefabData = new PrefabData
                {
                    path = path,
                    name = prefab.name,
                    prefab = prefab
                };

                foreach (var tmp in tmpComponents)
                {
                    var compData = new TMPComponentData
                    {
                        component = tmp,
                        path = GetGameObjectPath(tmp.gameObject, prefab.transform),
                        name = tmp.gameObject.name,
                        fontTag = tmp.GetComponent<FontTag>(),
                        originalFont = tmp.font,
                        originalSize = tmp.fontSize,
                        originalColor = tmp.color
                    };
                    
                    // FontTag 없으면 이름으로 추론
                    compData.inferredType = InferFontType(tmp.gameObject.name);
                    
                    prefabData.tmpComponents.Add(compData);
                }

                prefabs.Add(prefabData);
            }

            prefabs = prefabs.OrderBy(p => p.name).ToList();
        }

        string GetGameObjectPath(GameObject obj, Transform root)
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

        FontTag.FontType InferFontType(string name)
        {
            string lower = name.ToLower();
            
            if (lower.Contains("title") || lower.Contains("name") || lower.Contains("header"))
                return FontTag.FontType.Title;
            
            if (lower.Contains("dialogue") || lower.Contains("text") || lower.Contains("content") || lower.Contains("message"))
                return FontTag.FontType.Dialogue;
            
            return FontTag.FontType.UI;
        }

        void ApplyToSelected(Action<TMP_Text> action)
        {
            foreach (var comp in selectedComponents)
            {
                if (comp.component == null) continue;
                Undo.RecordObject(comp.component, "Bulk Edit");
                action(comp.component);
                MarkModifiedByComponent(comp);
            }
        }

        void MarkModified(PrefabData prefab)
        {
            if (prefab != null)
                modifiedPrefabPaths.Add(prefab.path);
        }

        void MarkModifiedByComponent(TMPComponentData comp)
        {
            var prefab = prefabs.FirstOrDefault(p => p.tmpComponents.Contains(comp));
            if (prefab != null)
                modifiedPrefabPaths.Add(prefab.path);
        }

        void SaveAllModified()
        {
            foreach (var path in modifiedPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                }
            }
            
            modifiedPrefabPaths.Clear();
            AssetDatabase.Refresh();
            Debug.Log("[UI Manager] All changes saved.");
        }

        void RevertAll()
        {
            foreach (var prefab in prefabs)
            {
                foreach (var comp in prefab.tmpComponents)
                {
                    if (comp.component == null) continue;
                    comp.component.font = comp.originalFont;
                    comp.component.fontSize = comp.originalSize;
                    comp.component.color = comp.originalColor;
                }
            }
            
            modifiedPrefabPaths.Clear();
            Repaint();
        }

        void ApplyProfileToSelected()
        {
            if (activeProfile == null) return;

            foreach (var comp in selectedComponents)
            {
                ApplyProfileToComponent(comp);
            }
        }

        void ApplyProfileToAll()
        {
            if (activeProfile == null) return;

            foreach (var prefab in prefabs)
            {
                foreach (var comp in prefab.tmpComponents)
                {
                    ApplyProfileToComponent(comp);
                }
            }
        }

        void ApplyProfileToComponent(TMPComponentData comp)
        {
            if (comp.component == null) return;
            if (comp.fontTag != null && comp.fontTag.ignoreProfileApplication) return;

            var fontType = comp.fontTag?.fontType ?? comp.inferredType;
            
            Undo.RecordObject(comp.component, "Apply Profile");
            
            var font = activeProfile.GetFont(fontType);
            if (font != null) comp.component.font = font;
            
            comp.component.fontSize = activeProfile.GetFontSize(fontType);
            comp.component.color = activeProfile.GetColor(fontType);
            
            MarkModifiedByComponent(comp);
        }

        void CreateNewProfile()
        {
            var profile = CreateInstance<FontProfile>();
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Font Profile", "FontProfile", "asset", "Save font profile");
            
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
                activeProfile = profile;
                SaveProfile();
            }
        }

        void LoadProfile()
        {
            string profileGuid = EditorPrefs.GetString("LoveAlgo_UIManager_ProfileGuid", "");
            if (!string.IsNullOrEmpty(profileGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(profileGuid);
                activeProfile = AssetDatabase.LoadAssetAtPath<FontProfile>(path);
            }
        }

        void SaveProfile()
        {
            if (activeProfile != null)
            {
                string path = AssetDatabase.GetAssetPath(activeProfile);
                string guid = AssetDatabase.AssetPathToGUID(path);
                EditorPrefs.SetString("LoveAlgo_UIManager_ProfileGuid", guid);
            }
            else
            {
                EditorPrefs.DeleteKey("LoveAlgo_UIManager_ProfileGuid");
            }
        }

        #endregion
    }
}
