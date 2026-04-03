using UnityEditor;
using UnityEngine;
using TMPro;
using System.Reflection;

public static class TMPFontCleaner
{
    [MenuItem("Tools/TMP Clear Dynamic Data (All Aggro Fonts)")]
    static void ClearAllAggroFonts()
    {
        string[] paths = new[]
        {
            "Assets/Fonts/Aggro-Bold SDF.asset",
            "Assets/Fonts/Aggro-Medium SDF.asset",
            "Assets/Fonts/Aggro-Light SDF.asset",
        };

        int cleared = 0;
        foreach (string path in paths)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                Debug.LogWarning($"[TMPCleaner] 폰트를 찾을 수 없음: {path}");
                continue;
            }

            font.ClearFontAssetData();
            EditorUtility.SetDirty(font);
            Debug.Log($"[TMPCleaner] Dynamic 데이터 클리어 완료: {font.name} (Atlas: {font.atlasWidth}x{font.atlasHeight})");
            cleared++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TMPCleaner] 완료: {cleared}개 폰트 클리어됨. 플레이 시 글리프가 재생성됩니다.");
    }
}
