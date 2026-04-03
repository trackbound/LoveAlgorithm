using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class FolderReorganizer
{
    [MenuItem("Tools/Reorganize Folders (Dry Run)", false, 200)]
    static void DryRun()
    {
        Run(dryRun: true);
    }

    [MenuItem("Tools/Reorganize Folders (Execute)", false, 201)]
    static void Execute()
    {
        if (!EditorUtility.DisplayDialog(
                "폴더 재구성",
                "폴더 구조를 재구성합니다.\n\n" +
                "1) Feel, Plugins/Demigiant, TextMesh Pro → _ThirdParty/\n" +
                "2) 루트 loose 파일들 → Settings/\n" +
                "3) 빈 폴더 정리\n\n" +
                "먼저 Dry Run으로 확인하셨나요?",
                "실행", "취소"))
            return;

        Run(dryRun: false);
    }

    static void Run(bool dryRun)
    {
        string mode = dryRun ? "[DRY RUN]" : "[EXECUTE]";
        Debug.Log($"=== {mode} 폴더 재구성 시작 ===");

        var moves = new List<(string from, string to)>();
        var foldersToCreate = new List<string>();
        var foldersToDelete = new List<string>();

        // --- 1. _ThirdParty 폴더 생성 ---
        foldersToCreate.Add("Assets/_ThirdParty");

        // --- 2. 서드파티 이동 ---
        // Feel → _ThirdParty/Feel
        if (AssetDatabase.IsValidFolder("Assets/Feel"))
            moves.Add(("Assets/Feel", "Assets/_ThirdParty/Feel"));

        // Plugins/Demigiant → _ThirdParty/DOTween
        if (AssetDatabase.IsValidFolder("Assets/Plugins/Demigiant"))
            moves.Add(("Assets/Plugins/Demigiant", "Assets/_ThirdParty/DOTween"));

        // TextMesh Pro → _ThirdParty/TextMeshPro
        if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            moves.Add(("Assets/TextMesh Pro", "Assets/_ThirdParty/TextMeshPro"));

        // --- 3. Loose 파일 → Settings/ ---
        string[] looseFiles = new[]
        {
            "Assets/DefaultVolumeProfile.asset",
            "Assets/UniversalRenderPipelineGlobalSettings.asset",
            "Assets/PythonTools.asset",
            "Assets/InputSystem_Actions.cs",
            "Assets/InputSystem_Actions.inputactions",
        };

        foreach (string file in looseFiles)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(file)) && File.Exists(file))
            {
                string fileName = Path.GetFileName(file);
                moves.Add((file, $"Assets/Settings/{fileName}"));
            }
        }

        // --- 4. 빈 Plugins 폴더 정리 ---
        if (AssetDatabase.IsValidFolder("Assets/Plugins"))
        {
            // Demigiant 이동 후 Plugins가 비게 되면 삭제
            string[] remaining = AssetDatabase.FindAssets("", new[] { "Assets/Plugins" });
            // Demigiant 내부 에셋만 있으면 이동 후 비게 됨
            bool onlyDemigiant = true;
            foreach (string guid in remaining)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/Plugins/Demigiant"))
                {
                    onlyDemigiant = false;
                    break;
                }
            }
            if (onlyDemigiant)
                foldersToDelete.Add("Assets/Plugins");
        }

        // --- 로그 출력 ---
        Debug.Log($"{mode} 생성할 폴더: {foldersToCreate.Count}개");
        foreach (string f in foldersToCreate)
            Debug.Log($"  CREATE: {f}");

        Debug.Log($"{mode} 이동할 항목: {moves.Count}개");
        foreach (var (from, to) in moves)
            Debug.Log($"  MOVE: {from} → {to}");

        Debug.Log($"{mode} 삭제할 빈 폴더: {foldersToDelete.Count}개");
        foreach (string f in foldersToDelete)
            Debug.Log($"  DELETE: {f}");

        if (dryRun)
        {
            Debug.Log($"=== {mode} 완료. 위 내용을 확인 후 'Tools > Reorganize Folders (Execute)' 실행 ===");
            return;
        }

        // --- 실행 ---
        int success = 0, fail = 0;

        // 폴더 생성
        foreach (string folder in foldersToCreate)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                string name = Path.GetFileName(folder);
                string guid = AssetDatabase.CreateFolder(parent, name);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogError($"폴더 생성 실패: {folder}");
                    fail++;
                }
                else
                {
                    Debug.Log($"폴더 생성 완료: {folder}");
                    success++;
                }
            }
        }

        // 이동
        foreach (var (from, to) in moves)
        {
            string result = AssetDatabase.MoveAsset(from, to);
            if (string.IsNullOrEmpty(result))
            {
                Debug.Log($"이동 완료: {from} → {to}");
                success++;
            }
            else
            {
                Debug.LogError($"이동 실패: {from} → {to} | 사유: {result}");
                fail++;
            }
        }

        // 빈 폴더 삭제
        foreach (string folder in foldersToDelete)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                if (AssetDatabase.DeleteAsset(folder))
                {
                    Debug.Log($"빈 폴더 삭제 완료: {folder}");
                    success++;
                }
                else
                {
                    Debug.LogWarning($"빈 폴더 삭제 실패: {folder}");
                    fail++;
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"=== 폴더 재구성 완료: 성공 {success}, 실패 {fail} ===");
    }
}
