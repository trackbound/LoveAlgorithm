using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 기존 프리팹의 Scrollbar에 ScrollbarEX 자동 추가
    /// - Scrollbar.Transition → None
    /// - 핸들 호버 스프라이트 바인딩 (있으면)
    /// </summary>
    public static class ScrollbarEXBinder
    {
        [MenuItem("LoveAlgo/Tools/Bind ScrollbarEX to Prefabs")]
        public static void Bind()
        {
            // Log 호버 스프라이트
            var logHoverSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Log/btn_log_scroll_hover.png");

            var prefabPaths = new[]
            {
                "Assets/Prefabs/UI/Popup/Log/LogPopup.prefab",
                "Assets/Prefabs/Shop/ShopPanel.prefab",
                "Assets/Prefabs/Phone/PhonePanel.prefab",
            };

            int count = 0;

            foreach (var path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[ScrollbarEXBinder] 프리팹 없음: {path}");
                    continue;
                }

                using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    var root = scope.prefabContentsRoot;
                    var scrollbars = root.GetComponentsInChildren<Scrollbar>(true);

                    foreach (var sb in scrollbars)
                    {
                        // 이미 있으면 스킵
                        if (sb.GetComponent<ScrollbarEX>() != null) continue;

                        var ex = sb.gameObject.AddComponent<ScrollbarEX>();
                        var so = new SerializedObject(ex);
                        so.Update();

                        // LogPopup 핸들에 호버 스프라이트 바인딩
                        if (path.Contains("LogPopup") && logHoverSprite != null)
                        {
                            so.FindProperty("handleHoverSprite").objectReferenceValue = logHoverSprite;
                        }

                        so.ApplyModifiedPropertiesWithoutUndo();

                        // Scrollbar Transition → None
                        sb.transition = Selectable.Transition.None;

                        count++;
                        Debug.Log($"[ScrollbarEXBinder] ScrollbarEX 추가: {path} / {sb.gameObject.name}");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ScrollbarEXBinder 완료] {count}개 Scrollbar에 ScrollbarEX 추가됨");
        }
    }
}
