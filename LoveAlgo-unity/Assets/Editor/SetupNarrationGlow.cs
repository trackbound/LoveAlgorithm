using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// 나레이션 버블 TMP에 핑크 글로우 Material Preset 생성 및 적용
/// 메뉴: Tools > Setup Narration Glow
/// </summary>
public static class SetupNarrationGlow
{
    [MenuItem("Tools/Setup Narration Glow")]
    public static void Execute()
    {
        // 1. 프리팹 로드
        string prefabPath = "Assets/Prefabs/UI/Popup/Log/Bubble/LogNarrationBubble.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"프리팹을 찾을 수 없습니다: {prefabPath}");
            return;
        }

        // 2. MessageText의 TMP 컴포넌트 찾기
        var tmp = prefab.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null)
        {
            Debug.LogError("TMP 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        // 3. 기존 shared material 기반으로 새 Material Preset 생성
        var baseMat = tmp.fontSharedMaterial;
        if (baseMat == null)
        {
            Debug.LogError("기존 fontSharedMaterial이 없습니다.");
            return;
        }

        string matPath = "Assets/Fonts/NarrationGlow.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (mat == null)
        {
            mat = new Material(baseMat);
            mat.name = "NarrationGlow";
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log("새 Material 생성: " + matPath);
        }

        // 4. Underlay 설정 (핑크 글로우)
        // Underlay 활성화
        mat.EnableKeyword("UNDERLAY_ON");
        mat.DisableKeyword("UNDERLAY_INNER");

        // Underlay 색상: 핑크 (목업 기준)
        Color pinkGlow = new Color(1f, 0.4f, 0.62f, 0.85f);
        mat.SetColor("_UnderlayColor", pinkGlow);

        // Underlay 오프셋: (0, 0) → 그림자가 아닌 글로우
        mat.SetFloat("_UnderlayOffsetX", 0f);
        mat.SetFloat("_UnderlayOffsetY", 0f);

        // Underlay 퍼짐 + 부드러움
        mat.SetFloat("_UnderlayDilate", 0.3f);
        mat.SetFloat("_UnderlaySoftness", 0.4f);

        EditorUtility.SetDirty(mat);

        // 5. 프리팹에 적용
        // Prefab 편집 모드로 열기
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(assetPath);
        var tmpInPrefab = root.GetComponentInChildren<TextMeshProUGUI>(true);

        if (tmpInPrefab != null)
        {
            // Material Preset 적용
            tmpInPrefab.fontSharedMaterial = mat;

            // Shadow 컴포넌트 제거 (Underlay로 대체)
            var shadow = tmpInPrefab.GetComponent<UnityEngine.UI.Shadow>();
            if (shadow != null)
            {
                Object.DestroyImmediate(shadow);
                Debug.Log("Shadow 컴포넌트 제거 (Underlay로 대체)");
            }

            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Debug.Log($"✅ 나레이션 글로우 적용 완료: {matPath}");
        }

        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
