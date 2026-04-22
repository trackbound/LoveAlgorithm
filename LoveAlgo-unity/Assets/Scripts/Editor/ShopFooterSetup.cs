#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.Shop;

namespace LoveAlgo.EditorTools
{
    /// <summary>
    /// Shop.prefab의 Sale ScrollRect Content("Sale Content") 끝에
    /// "ComingSoonFooter" GameObject(Image + TMP_Text)를 추가하고,
    /// ShopPopup.saleListFooter 인스펙터 슬롯에 자동 바인딩.
    ///
    /// 사용:
    ///   Editor 메뉴: LoveAlgo > Shop > Setup ComingSoon Footer
    ///   CLI:
    ///     "C:\Program Files\Unity\Hub\Editor\<버전>\Editor\Unity.exe" ^
    ///       -batchmode -nographics -quit -projectPath "<프로젝트 경로>" ^
    ///       -executeMethod LoveAlgo.EditorTools.ShopFooterSetup.RunFromCli
    /// </summary>
    public static class ShopFooterSetup
    {
        const string PrefabPath     = "Assets/Prefabs/Shop/Shop.prefab";
        const string FooterName     = "ComingSoonFooter";
        const string FooterText     = "COMING SOON…\n더 많은 상품 입고 대기 중…";
        // 기존 분홍 그라디언트 스프라이트 재사용 (LogRowBg_Pink, 9-slice)
        const string SpritePath     = "Assets/Art/UI/Log/LogRowBg_Pink.png";
        const float  FooterHeight   = 120f;
        const int    FooterGap      = 20; // 그리드 마지막 줄과 푸터 사이 간격(px)

        [MenuItem("LoveAlgo/Shop/Setup ComingSoon Footer")]
        public static void Run()
        {
            var (ok, msg) = Setup();
            if (ok) EditorUtility.DisplayDialog("Shop Footer", msg, "OK");
            else    EditorUtility.DisplayDialog("Shop Footer 실패", msg, "OK");
        }

        /// <summary>CLI batchmode 진입점 — 실패 시 exit code 1</summary>
        public static void RunFromCli()
        {
            var (ok, msg) = Setup();
            Debug.Log($"[ShopFooterSetup] {(ok ? "OK" : "FAIL")}: {msg}");
            if (!ok) EditorApplication.Exit(1);
        }

        static (bool ok, string msg) Setup()
        {
            if (!File.Exists(PrefabPath))
                return (false, $"프리팹 없음: {PrefabPath}");

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return (false, "LoadPrefabContents 실패");

            try
            {
                // 1) ShopPopup → saleContainer (GridLayoutGroup가 붙은 ScrollRect.content) 찾기
                var shopPopup = root.GetComponentInChildren<ShopPopup>(true);
                if (shopPopup == null)
                    return (false, "ShopPopup 컴포넌트를 찾지 못함");

                var soShop = new SerializedObject(shopPopup);
                var saleContainerProp = soShop.FindProperty("saleContainer");
                var saleContainerTr = saleContainerProp?.objectReferenceValue as Transform;
                if (saleContainerTr == null)
                    return (false, "ShopPopup.saleContainer 미설정");

                var grid = saleContainerTr.GetComponent<GridLayoutGroup>();
                if (grid == null)
                    return (false, "saleContainer에 GridLayoutGroup이 없음 (구조 불일치)");

                // 2) GridLayoutGroup 바닥 패딩 확보 → 마지막 행 아래에 푸터 자리 마련
                var pad = grid.padding;
                int desiredBottom = Mathf.RoundToInt(FooterHeight + FooterGap);
                if (pad.bottom != desiredBottom)
                {
                    pad.bottom = desiredBottom;
                    grid.padding = pad;
                }

                // 3) 기존 푸터 제거 후 재생성 (ignoreLayout이라 Grid 셀로 잡히지 않음)
                var existing = saleContainerTr.Find(FooterName);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);

                var footer = new GameObject(FooterName,
                    typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                footer.transform.SetParent(saleContainerTr, false);
                footer.transform.SetAsLastSibling();

                // Content 하단에 가로로 길게 깔리도록 anchor 설정
                var rt = (RectTransform)footer.transform;
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot     = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(0, FooterHeight);
                rt.anchoredPosition = new Vector2(0, FooterGap * 0.5f);

                var le = footer.GetComponent<LayoutElement>();
                le.ignoreLayout = true; // GridLayoutGroup이 셀로 잡지 않도록

                var img = footer.GetComponent<Image>();
                img.color = new Color(1f, 0.78f, 0.88f, 0.9f);
                img.raycastTarget = false;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.type   = Image.Type.Sliced;
                }

                // 4) TMP_Text 자식 추가
                var label = new GameObject("Label", typeof(RectTransform));
                label.transform.SetParent(footer.transform, false);
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;

                var tmp = label.AddComponent<TextMeshProUGUI>();
                tmp.text = FooterText;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 36f;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.Normal;

                // 5) ShopPopup.saleListFooter 바인딩
                var prop = soShop.FindProperty("saleListFooter");
                if (prop == null)
                    return (false, "ShopPopup에 saleListFooter 필드 없음 (스크립트 컴파일 확인)");
                prop.objectReferenceValue = footer;
                soShop.ApplyModifiedPropertiesWithoutUndo();

                // 6) 프리팹 저장
                EditorUtility.SetDirty(grid);
                EditorUtility.SetDirty(shopPopup);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();

                return (true, $"\"{FooterName}\" 설치 완료 (GLG bottom padding={desiredBottom}, ignoreLayout=true)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform FindDescendant(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindDescendant(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
