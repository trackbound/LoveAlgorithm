#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LoveAlgo.UI;

namespace LoveAlgo.EditorTools
{
    /// <summary>
    /// 씬 프리팹화 자동화 — 1단계(Phase A: 인스턴스 유지 모드)
    ///
    /// 동작:
    ///   1) 활성 씬에서 지정한 컴포넌트 타입을 가진 GameObject를 찾는다.
    ///   2) 해당 GameObject 서브트리를 Assets/Prefabs/.../<Name>.prefab 로 저장하고
    ///      씬 인스턴스를 그 프리팹의 인스턴스로 자동 변환한다 (모습/하이어라키 동일).
    ///   3) 씬을 저장한다. PopupManager/UIManager 인스펙터 슬롯은 같은 인스턴스를
    ///      참조하므로 자동 유지된다.
    ///
    /// 사용:
    ///   메뉴: LoveAlgo > Scene > Extract Phase A Prefabs (Title/Toast/Alert/Log)
    ///   CLI:  -executeMethod LoveAlgo.EditorTools.ScenePrefabExtractor.RunPhaseAFromCli
    /// </summary>
    public static class ScenePrefabExtractor
    {
        const string PrefabRoot = "Assets/Prefabs/UI";

        // (컴포넌트 타입, 저장 경로 하위 폴더, 파일명)
        static readonly (System.Type type, string subFolder, string fileName)[] PhaseATargets =
        {
            (typeof(TitleUI),    "",       "TitleUI.prefab"),
            (typeof(ToastPopup), "Popup",  "Toast.prefab"),
            (typeof(AlertPopup), "Popup",  "AlertPopup.prefab"),
            (typeof(LogPopup),   "Popup",  "LogPopup.prefab"),
        };

        [MenuItem("LoveAlgo/Scene/Extract Phase A Prefabs (Title, Toast, Alert, Log)")]
        public static void ExtractPhaseA_Menu()
        {
            var (ok, msg) = ExtractPhaseA();
            if (ok)
                EditorUtility.DisplayDialog("Phase A 완료", msg, "확인");
            else
                EditorUtility.DisplayDialog("Phase A 실패", msg, "확인");
        }

        public static void RunPhaseAFromCli()
        {
            var (ok, msg) = ExtractPhaseA();
            Debug.Log($"[ScenePrefabExtractor] Phase A: ok={ok}\n{msg}");
            if (!ok) EditorApplication.Exit(1);
        }

        static (bool ok, string msg) ExtractPhaseA()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                return (false, "활성 씬이 저장되어 있지 않습니다. Main.unity를 먼저 열어 주세요.");

            EnsureFolder(PrefabRoot);
            var log = new List<string>();
            int created = 0, replaced = 0, skipped = 0;

            foreach (var t in PhaseATargets)
            {
                var folder = string.IsNullOrEmpty(t.subFolder) ? PrefabRoot : $"{PrefabRoot}/{t.subFolder}";
                EnsureFolder(folder);
                var prefabPath = $"{folder}/{t.fileName}";

                var go = FindInActiveScene(t.type, scene);
                if (go == null)
                {
                    log.Add($"  - SKIP [{t.type.Name}] 씬에서 찾지 못함");
                    skipped++;
                    continue;
                }

                // 이미 같은 프리팹 자산의 인스턴스(루트든 nested든)면 SKIP
                var existingAssetPath = PrefabUtility.IsPartOfPrefabInstance(go)
                    ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)
                    : null;
                if (!string.IsNullOrEmpty(existingAssetPath) && existingAssetPath == prefabPath)
                {
                    log.Add($"  - SKIP [{t.type.Name}] 이미 {prefabPath}");
                    skipped++;
                    continue;
                }

                // CASE A: 부모 프리팹(예: UI.prefab) 내부에 중첩되어 있는 경우
                //   → 부모 prefab을 prefab 모드로 열고, 그 안에서 nested prefab으로 분리
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                    if (nearestRoot != go)
                    {
                        var (nestedOk, nestedMsg) = ExtractNestedFromParentPrefab(t.type, prefabPath, existingAssetPath);
                        log.Add($"  - NESTED [{t.type.Name}] {nestedMsg}");
                        if (nestedOk)
                        {
                            if (File.Exists(prefabPath)) created++;
                            continue;
                        }
                        // 실패하면 아래 일반 흐름으로 폴백 (대개 실패)
                        log.Add($"  - FALLBACK [{t.type.Name}] nested 추출 실패, 일반 경로 시도");
                    }
                }

                // CASE B: 단독 GameObject (또는 자기 자신이 prefab 인스턴스 루트)
                bool existed = File.Exists(prefabPath);
                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction, out bool success);
                if (!success || saved == null)
                {
                    log.Add($"  - FAIL [{t.type.Name}] SaveAsPrefabAssetAndConnect 실패: {prefabPath}");
                    continue;
                }

                if (existed) { replaced++; log.Add($"  - REPLACE [{t.type.Name}] {prefabPath}"); }
                else         { created++;  log.Add($"  - CREATE  [{t.type.Name}] {prefabPath}"); }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary =
                $"created={created}, replaced={replaced}, skipped={skipped}\n" +
                string.Join("\n", log) +
                "\n\n인스펙터 슬롯(PopupManager/UIManager)은 같은 GameObject 인스턴스를 가리키므로 그대로 유지됩니다.\n" +
                "이제 각 .prefab을 더블클릭해 편집하면 씬 인스턴스에도 반영됩니다.";

            return (true, summary);
        }

        // ── helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// 부모 프리팹(parentPrefabPath)을 prefab 모드로 로드해서, 그 안에 있는
        /// 컴포넌트 type의 GameObject를 별도 nested prefab(newPrefabPath)으로 분리한다.
        /// 부모 프리팹은 nested 참조를 갖도록 다시 저장된다.
        /// </summary>
        static (bool ok, string msg) ExtractNestedFromParentPrefab(System.Type type, string newPrefabPath, string parentPrefabPath)
        {
            if (string.IsNullOrEmpty(parentPrefabPath))
                return (false, "부모 prefab 경로 비어 있음");

            var parentRoot = PrefabUtility.LoadPrefabContents(parentPrefabPath);
            if (parentRoot == null)
                return (false, $"부모 prefab 로드 실패: {parentPrefabPath}");

            try
            {
                var found = parentRoot.GetComponentsInChildren(type, true);
                if (found == null || found.Length == 0)
                    return (false, $"부모 prefab '{parentPrefabPath}' 내부에서 {type.Name} 못 찾음");

                var targetGo = ((Component)found[0]).gameObject;

                // 부모 prefab 내부에서 nested prefab으로 저장 + 연결
                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(targetGo, newPrefabPath, InteractionMode.AutomatedAction, out bool success);
                if (!success || saved == null)
                    return (false, $"nested SaveAsPrefabAssetAndConnect 실패: {newPrefabPath}");

                // 변경된 부모 prefab 다시 저장
                PrefabUtility.SaveAsPrefabAsset(parentRoot, parentPrefabPath, out bool parentOk);
                if (!parentOk)
                    return (false, $"부모 prefab 재저장 실패: {parentPrefabPath}");

                return (true, $"부모 '{Path.GetFileName(parentPrefabPath)}' 안에서 nested로 분리 → {newPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(parentRoot);
            }
        }

        static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var leaf   = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>활성 씬에서 컴포넌트 타입으로 GameObject(루트) 찾기 — 비활성 포함</summary>
        static GameObject FindInActiveScene(System.Type type, Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentsInChildren(type, true);
                if (found != null && found.Length > 0)
                {
                    // 컴포넌트가 붙은 GameObject 자체를 프리팹 루트로 사용
                    return ((Component)found[0]).gameObject;
                }
            }
            return null;
        }
    }
}
#endif
