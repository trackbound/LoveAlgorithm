using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 기존 프리팹의 Scrollbar를 비주얼 자식 패턴으로 변환
    ///
    /// 변환 전:
    ///   Scrollbar (Image: 트랙 스프라이트)
    ///     └─ Handle Slide Area
    ///          └─ Handle (Image: 핸들 스프라이트)  ← Unity가 크기/위치 덮어씀
    ///
    /// 변환 후:
    ///   Scrollbar (Image: 투명, Transition: None)
    ///     ├─ TrackVisual  (Image: 트랙 스프라이트, 스트레치)   ← 고정 비주얼
    ///     └─ Handle Slide Area
    ///          └─ Handle (Image: 투명)                       ← Unity 자유 제어
    ///               └─ HandleVisual (Image: 핸들, 고정 크기)  ← 고정 비주얼
    /// </summary>
    public static class ScrollbarEXBinder
    {
        [MenuItem("LoveAlgo/Tools/Bind ScrollbarEX to Prefabs")]
        public static void Bind()
        {
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
                        if (ConvertScrollbar(sb, path))
                            count++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ScrollbarEXBinder 완료] {count}개 Scrollbar 변환됨");
        }

        static bool ConvertScrollbar(Scrollbar sb, string prefabPath)
        {
            var handle = sb.handleRect;
            if (handle == null)
            {
                Debug.LogWarning($"[ScrollbarEXBinder] handleRect 없음: {sb.gameObject.name}");
                return false;
            }

            // 이미 변환됨?
            if (handle.Find("HandleVisual") != null)
            {
                Debug.Log($"[ScrollbarEXBinder] 이미 변환됨 스킵: {sb.gameObject.name}");
                return false;
            }

            // ── 기존 스프라이트 읽기 ──
            var sbImage = sb.GetComponent<Image>();
            var handleImage = handle.GetComponent<Image>();
            Sprite trackSprite = sbImage != null ? sbImage.sprite : null;
            Sprite handleSprite = handleImage != null ? handleImage.sprite : null;

            // ── TrackVisual 생성 (Scrollbar 자식, 첫 번째 → 뒤에 렌더링) ──
            if (trackSprite != null)
            {
                var trackGo = new GameObject("TrackVisual");
                trackGo.transform.SetParent(sb.transform, false);
                trackGo.transform.SetAsFirstSibling();

                var trackRT = trackGo.AddComponent<RectTransform>();
                trackRT.anchorMin = Vector2.zero;
                trackRT.anchorMax = Vector2.one;
                trackRT.offsetMin = Vector2.zero;
                trackRT.offsetMax = Vector2.zero;

                var trackImg = trackGo.AddComponent<Image>();
                trackImg.sprite = trackSprite;
                trackImg.type = trackSprite.border != Vector4.zero
                    ? Image.Type.Sliced : Image.Type.Simple;
                trackImg.raycastTarget = false;
            }

            // ── HandleVisual 생성 (Handle 자식, 중앙 고정 크기) ──
            Image handleVisualImg = null;
            if (handleSprite != null)
            {
                var hvGo = new GameObject("HandleVisual");
                hvGo.transform.SetParent(handle, false);

                var hvRT = hvGo.AddComponent<RectTransform>();
                hvRT.anchorMin = new Vector2(0.5f, 0.5f);
                hvRT.anchorMax = new Vector2(0.5f, 0.5f);
                hvRT.pivot = new Vector2(0.5f, 0.5f);
                hvRT.sizeDelta = handleSprite.rect.size;

                handleVisualImg = hvGo.AddComponent<Image>();
                handleVisualImg.sprite = handleSprite;
                handleVisualImg.raycastTarget = false;
            }

            // ── 기존 Image 투명 처리 (raycastTarget 유지 → 클릭/드래그 영역) ──
            if (sbImage != null)
            {
                sbImage.sprite = null;
                sbImage.color = Color.clear;
            }
            if (handleImage != null)
            {
                handleImage.sprite = null;
                handleImage.color = Color.clear;
            }

            // ── Scrollbar 설정 ──
            sb.transition = Selectable.Transition.None;
            sb.direction = Scrollbar.Direction.BottomToTop;

            // ── ScrollbarEX 추가 + 바인딩 ──
            var ex = sb.GetComponent<ScrollbarEX>();
            if (ex == null)
                ex = sb.gameObject.AddComponent<ScrollbarEX>();

            var so = new SerializedObject(ex);
            so.Update();

            if (handleVisualImg != null)
                so.FindProperty("handleVisual").objectReferenceValue = handleVisualImg;

            // 호버 스프라이트 자동 검색 (foo.png → foo_hover.png)
            if (handleSprite != null)
            {
                var hoverSprite = FindHoverSprite(handleSprite);
                if (hoverSprite != null)
                    so.FindProperty("handleHoverSprite").objectReferenceValue = hoverSprite;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[ScrollbarEXBinder] 변환 완료: {prefabPath} / {sb.gameObject.name}");
            return true;
        }

        /// <summary>
        /// 호버 스프라이트 자동 검색 (foo.png → foo_hover.png)
        /// </summary>
        static Sprite FindHoverSprite(Sprite handleSprite)
        {
            string path = AssetDatabase.GetAssetPath(handleSprite);
            if (string.IsNullOrEmpty(path)) return null;

            string ext = System.IO.Path.GetExtension(path);
            string withoutExt = path.Substring(0, path.Length - ext.Length);
            return AssetDatabase.LoadAssetAtPath<Sprite>(withoutExt + "_hover" + ext);
        }
    }
}
