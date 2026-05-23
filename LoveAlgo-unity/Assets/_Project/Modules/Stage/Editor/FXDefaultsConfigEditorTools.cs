using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Core.EditorTools
{
    /// <summary>
    /// FXDefaultsConfig SO 편집 헬퍼.
    ///
    /// 메뉴:
    ///   Tools > FX Defaults > Open Editor Window   — 카테고리별 한눈에 편집
    ///   Tools > FX Defaults > Create Default Config — Resources/Data/FXDefaultsConfig.asset 자동 생성 (없을 때)
    ///   Tools > FX Defaults > Select Asset          — Project Window에서 ping
    ///   Tools > FX Defaults > Reset All to Defaults — 모든 값을 코드 디폴트로 리셋 (Undo 가능)
    ///
    /// SO 자체 inspector도 정상 동작 — 이 윈도우는 카테고리 강조 + 단축 메뉴 + Reset 편의.
    /// </summary>
    public static class FXDefaultsConfigCreator
    {
        public const string AssetDir  = "Assets/Resources/Data";
        public const string AssetPath = "Assets/Resources/Data/FXDefaultsConfig.asset";

        [MenuItem("Tools/FX Defaults/Open Editor Window")]
        public static void OpenWindow() => FXDefaultsConfigWindow.Open();

        [MenuItem("Tools/FX Defaults/Create Default Config")]
        public static void CreateDefault()
        {
            var existing = AssetDatabase.LoadAssetAtPath<FXDefaultsConfig>(AssetPath);
            if (existing != null)
            {
                Debug.LogWarning($"[FXDefaults] 이미 존재: {AssetPath}");
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }
            if (!Directory.Exists(AssetDir)) Directory.CreateDirectory(AssetDir);
            var so = ScriptableObject.CreateInstance<FXDefaultsConfig>();
            AssetDatabase.CreateAsset(so, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = so;
            EditorGUIUtility.PingObject(so);
            Debug.Log($"[FXDefaults] 생성: {AssetPath}");
        }

        [MenuItem("Tools/FX Defaults/Select Asset")]
        public static void SelectAsset()
        {
            var so = AssetDatabase.LoadAssetAtPath<FXDefaultsConfig>(AssetPath);
            if (so == null)
            {
                if (EditorUtility.DisplayDialog("FX Defaults",
                    $"{AssetPath} 가 없습니다. 지금 생성할까요?", "생성", "취소"))
                    CreateDefault();
                return;
            }
            Selection.activeObject = so;
            EditorGUIUtility.PingObject(so);
        }

        [MenuItem("Tools/FX Defaults/Reset All to Defaults")]
        public static void ResetAll()
        {
            var so = AssetDatabase.LoadAssetAtPath<FXDefaultsConfig>(AssetPath);
            if (so == null)
            {
                Debug.LogError($"[FXDefaults] {AssetPath} 없음 — 'Create Default Config' 먼저");
                return;
            }
            if (!EditorUtility.DisplayDialog("FX Defaults Reset",
                "모든 timing 값을 코드 디폴트로 되돌립니다.\n계속할까요? (Undo 가능)", "Reset", "취소"))
                return;

            Undo.RecordObject(so, "Reset FXDefaultsConfig");
            var fresh = ScriptableObject.CreateInstance<FXDefaultsConfig>();
            EditorUtility.CopySerialized(fresh, so);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            Debug.Log("[FXDefaults] 모든 값 리셋 완료");
        }
    }

    /// <summary>
    /// FXDefaultsConfig 편집 윈도우.
    /// [Header] attribute 단위로 카테고리 그룹 + Foldout. 검색 + Reset.
    /// </summary>
    public class FXDefaultsConfigWindow : EditorWindow
    {
        FXDefaultsConfig so;
        SerializedObject ser;
        Vector2 scroll;
        string filter = "";
        static readonly System.Collections.Generic.Dictionary<string, bool> foldouts = new();

        public static void Open()
        {
            var w = GetWindow<FXDefaultsConfigWindow>("FX Defaults");
            w.minSize = new Vector2(520, 600);
            w.Reload();
        }

        void OnEnable() => Reload();

        void Reload()
        {
            so = AssetDatabase.LoadAssetAtPath<FXDefaultsConfig>(FXDefaultsConfigCreator.AssetPath);
            ser = so != null ? new SerializedObject(so) : null;
        }

        void OnGUI()
        {
            if (so == null || ser == null)
            {
                EditorGUILayout.HelpBox(
                    $"FXDefaultsConfig.asset이 없습니다.\n경로: {FXDefaultsConfigCreator.AssetPath}",
                    MessageType.Warning);
                if (GUILayout.Button("지금 생성", GUILayout.Height(40)))
                {
                    FXDefaultsConfigCreator.CreateDefault();
                    Reload();
                }
                return;
            }

            ser.Update();

            // ── 툴바 ──
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("검색", GUILayout.Width(40));
            filter = GUILayout.TextField(filter, EditorStyles.toolbarTextField, GUILayout.MinWidth(120));
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(24))) filter = "";
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60))) Reload();
            if (GUILayout.Button("Reset All", EditorStyles.toolbarButton, GUILayout.Width(70)))
                FXDefaultsConfigCreator.ResetAll();
            if (GUILayout.Button("Ping Asset", EditorStyles.toolbarButton, GUILayout.Width(80)))
                { Selection.activeObject = so; EditorGUIUtility.PingObject(so); }
            if (GUILayout.Button("Expand All", EditorStyles.toolbarButton, GUILayout.Width(80)))
                SetAllFoldouts(true);
            if (GUILayout.Button("Collapse All", EditorStyles.toolbarButton, GUILayout.Width(90)))
                SetAllFoldouts(false);
            EditorGUILayout.EndHorizontal();

            // ── 안내 박스 ──
            EditorGUILayout.HelpBox(
                "이 SO가 모든 timing의 단일 정전(SoT)입니다.\n" +
                "각 컴포넌트의 SerializedField는 제거되어, 여기서만 값 조정 가능합니다.\n" +
                "CSV에 명시한 duration은 그것을 우선합니다 (per-line override).",
                MessageType.Info);

            // ── 본문 ──
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawGroupedFields();
            EditorGUILayout.EndScrollView();

            ser.ApplyModifiedProperties();
        }

        /// <summary>
        /// [Header] attribute로 그룹화해서 Foldout 렌더링.
        /// 필드 순서는 코드 선언 순서 → Header가 카테고리 경계.
        /// </summary>
        void DrawGroupedFields()
        {
            var type = typeof(FXDefaultsConfig);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            string currentGroup = "기타";
            string filterLower = filter?.ToLowerInvariant() ?? "";

            // 그룹별로 prop 모으기 (선언 순서 유지)
            var groups = new System.Collections.Generic.List<(string name, System.Collections.Generic.List<FieldInfo> fields)>();
            System.Collections.Generic.List<FieldInfo> bucket = null;

            foreach (var f in fields)
            {
                var header = f.GetCustomAttribute<HeaderAttribute>();
                if (header != null)
                {
                    currentGroup = header.header;
                    bucket = new System.Collections.Generic.List<FieldInfo>();
                    groups.Add((currentGroup, bucket));
                }
                if (bucket == null)
                {
                    bucket = new System.Collections.Generic.List<FieldInfo>();
                    groups.Add((currentGroup, bucket));
                }
                bucket.Add(f);
            }

            // 그리기
            foreach (var (groupName, groupFields) in groups)
            {
                // 필터 적용 시 그룹 내 매칭 필드 없으면 그룹 자체 스킵
                if (!string.IsNullOrEmpty(filterLower))
                {
                    bool anyMatch = false;
                    foreach (var f in groupFields)
                    {
                        if (f.Name.ToLowerInvariant().Contains(filterLower)
                         || groupName.ToLowerInvariant().Contains(filterLower))
                        { anyMatch = true; break; }
                    }
                    if (!anyMatch) continue;
                }

                if (!foldouts.ContainsKey(groupName)) foldouts[groupName] = true;

                EditorGUILayout.Space(4);
                var headerStyle = new GUIStyle(EditorStyles.foldoutHeader)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                };
                foldouts[groupName] = EditorGUILayout.Foldout(
                    foldouts[groupName], $"  {groupName}  ({groupFields.Count} 필드)",
                    true, headerStyle);

                if (!foldouts[groupName]) continue;

                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var f in groupFields)
                {
                    if (!string.IsNullOrEmpty(filterLower)
                     && !f.Name.ToLowerInvariant().Contains(filterLower)
                     && !groupName.ToLowerInvariant().Contains(filterLower))
                        continue;

                    var prop = ser.FindProperty(f.Name);
                    if (prop == null) continue;
                    EditorGUILayout.PropertyField(prop, true);
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        static void SetAllFoldouts(bool open)
        {
            var keys = new System.Collections.Generic.List<string>(foldouts.Keys);
            foreach (var k in keys) foldouts[k] = open;
        }
    }
}
