using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 프로젝트 내 모든 프리팹에서 Missing Script를 찾아 삭제하는 에디터 도구입니다.
/// </summary>
public class MissingScriptCleaner : EditorWindow
{
    private Vector2 _scrollPosition;
    private List<MissingScriptInfo> _results = new List<MissingScriptInfo>();
    private bool _hasScanned;
    private bool _isScanning;
    
    private class MissingScriptInfo
    {
        public string PrefabPath;
        public GameObject Prefab;
        public int MissingCount;
        public List<string> AffectedObjects = new List<string>();
    }

    [MenuItem("Tools/Missing Script Cleaner", priority = 100)]
    public static void ShowWindow()
    {
        var window = GetWindow<MissingScriptCleaner>("Missing Script Cleaner");
        window.minSize = new Vector2(500, 400);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        
        // 헤더
        EditorGUILayout.LabelField("🔍 Missing Script Cleaner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "프로젝트 내 모든 프리팹을 스캔하여 Missing Script를 찾고 삭제합니다.",
            MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        // 스캔 버튼
        EditorGUI.BeginDisabledGroup(_isScanning);
        if (GUILayout.Button("🔎 프리팹 스캔", GUILayout.Height(30)))
        {
            ScanAllPrefabs();
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(5);
        
        if (_hasScanned)
        {
            DrawResults();
        }
    }

    private void DrawResults()
    {
        // 결과 요약
        int totalMissing = _results.Sum(r => r.MissingCount);
        
        if (totalMissing == 0)
        {
            EditorGUILayout.HelpBox(
                "✅ Missing Script가 없습니다! 모든 프리팹이 깨끗합니다.",
                MessageType.Info);
            return;
        }
        
        EditorGUILayout.LabelField(
            $"⚠️ {_results.Count}개 프리팹에서 총 {totalMissing}개의 Missing Script 발견",
            EditorStyles.boldLabel);
        
        EditorGUILayout.Space(5);
        
        // 전체 삭제 버튼
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button($"🗑️ 모든 Missing Script 삭제 ({totalMissing}개)", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog(
                "Missing Script 삭제 확인",
                $"{_results.Count}개 프리팹에서 {totalMissing}개의 Missing Script를 삭제하시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.",
                "삭제", "취소"))
            {
                RemoveAllMissingScripts();
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(10);
        
        // 결과 리스트
        EditorGUILayout.LabelField("발견된 프리팹:", EditorStyles.boldLabel);
        
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        
        foreach (var info in _results)
        {
            DrawPrefabEntry(info);
        }
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawPrefabEntry(MissingScriptInfo info)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        
        // 프리팹 아이콘 + 이름
        var prefabIcon = EditorGUIUtility.IconContent("Prefab Icon").image;
        GUILayout.Label(prefabIcon, GUILayout.Width(18), GUILayout.Height(18));
        
        if (GUILayout.Button(info.PrefabPath, EditorStyles.linkLabel))
        {
            // 프리팹 선택
            Selection.activeObject = info.Prefab;
            EditorGUIUtility.PingObject(info.Prefab);
        }
        
        GUILayout.FlexibleSpace();
        
        // Missing 개수
        GUILayout.Label($"Missing: {info.MissingCount}", EditorStyles.miniLabel);
        
        // 개별 삭제 버튼
        GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
        if (GUILayout.Button("삭제", GUILayout.Width(50)))
        {
            RemoveMissingScriptsFromPrefab(info);
            _results.Remove(info);
            Repaint();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        // 영향받는 오브젝트 표시
        if (info.AffectedObjects.Count > 0)
        {
            EditorGUI.indentLevel++;
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            
            int displayCount = Mathf.Min(info.AffectedObjects.Count, 5);
            for (int i = 0; i < displayCount; i++)
            {
                EditorGUILayout.LabelField($"└ {info.AffectedObjects[i]}", style);
            }
            
            if (info.AffectedObjects.Count > 5)
            {
                EditorGUILayout.LabelField($"  ... 외 {info.AffectedObjects.Count - 5}개", style);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }

    private void ScanAllPrefabs()
    {
        _isScanning = true;
        _results.Clear();
        
        try
        {
            // 모든 프리팹 GUID 가져오기
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int total = prefabGuids.Length;
            
            for (int i = 0; i < total; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                
                if (EditorUtility.DisplayCancelableProgressBar(
                    "프리팹 스캔 중...",
                    $"({i + 1}/{total}) {path}",
                    (float)i / total))
                {
                    break;
                }
                
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                
                var info = CheckPrefabForMissingScripts(prefab, path);
                if (info != null)
                {
                    _results.Add(info);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            _isScanning = false;
            _hasScanned = true;
        }
        
        // 결과 정렬 (Missing 개수 많은 순)
        _results = _results.OrderByDescending(r => r.MissingCount).ToList();
        
        Debug.Log($"[MissingScriptCleaner] 스캔 완료: {_results.Count}개 프리팹에서 {_results.Sum(r => r.MissingCount)}개 Missing Script 발견");
    }

    private MissingScriptInfo CheckPrefabForMissingScripts(GameObject prefab, string path)
    {
        var allTransforms = prefab.GetComponentsInChildren<Transform>(true);
        int missingCount = 0;
        var affectedObjects = new List<string>();
        
        foreach (var t in allTransforms)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (count > 0)
            {
                missingCount += count;
                affectedObjects.Add(GetGameObjectPath(t, prefab.transform));
            }
        }
        
        if (missingCount == 0) return null;
        
        return new MissingScriptInfo
        {
            PrefabPath = path,
            Prefab = prefab,
            MissingCount = missingCount,
            AffectedObjects = affectedObjects
        };
    }

    private string GetGameObjectPath(Transform target, Transform root)
    {
        var path = target.name;
        var current = target.parent;
        
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        
        return path;
    }

    private void RemoveAllMissingScripts()
    {
        int totalRemoved = 0;
        var prefabsToProcess = _results.ToList();
        
        try
        {
            for (int i = 0; i < prefabsToProcess.Count; i++)
            {
                var info = prefabsToProcess[i];
                
                EditorUtility.DisplayProgressBar(
                    "Missing Script 삭제 중...",
                    $"({i + 1}/{prefabsToProcess.Count}) {info.PrefabPath}",
                    (float)i / prefabsToProcess.Count);
                
                int removed = RemoveMissingScriptsFromPrefab(info);
                totalRemoved += removed;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        
        _results.Clear();
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[MissingScriptCleaner] 삭제 완료: {prefabsToProcess.Count}개 프리팹에서 {totalRemoved}개 Missing Script 삭제됨");
        
        EditorUtility.DisplayDialog(
            "삭제 완료",
            $"{prefabsToProcess.Count}개 프리팹에서 {totalRemoved}개의 Missing Script를 삭제했습니다.",
            "확인");
    }

    private int RemoveMissingScriptsFromPrefab(MissingScriptInfo info)
    {
        string prefabPath = AssetDatabase.GetAssetPath(info.Prefab);
        
        // 프리팹 컨텐츠 로드
        using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var root = editScope.prefabContentsRoot;
            int removed = RemoveMissingScriptsRecursive(root);
            return removed;
        }
    }

    private int RemoveMissingScriptsRecursive(GameObject go)
    {
        int removed = 0;
        
        // 현재 오브젝트의 Missing Script 삭제
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (count > 0)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            removed += count;
        }
        
        // 자식 순회
        foreach (Transform child in go.transform)
        {
            removed += RemoveMissingScriptsRecursive(child.gameObject);
        }
        
        return removed;
    }
}
