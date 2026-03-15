#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

/// <summary>
/// GiftPopup 관련 프리팹 자동 생성 에디터 유틸리티
/// 메뉴: Tools > Create Gift Prefabs
/// </summary>
public static class GiftPrefabCreator
{
    const string PrefabFolder = "Assets/Prefabs/Shop";
    static TMP_FontAsset defaultFont;

    [MenuItem("Tools/Create Gift Prefabs")]
    public static void CreateAllGiftPrefabs()
    {
        // TMP 기본 폰트 자동 탐색
        defaultFont = FindDefaultTMPFont();

        // 폴더 확인
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Shop");

        var itemSlotPrefab = CreateGiftItemSlotPrefab();
        var heroineSlotPrefab = CreateGiftHeroineSlotPrefab();
        var giftPopupPrefab = CreateGiftPopupPrefab(itemSlotPrefab, heroineSlotPrefab);

        // PopupManager에 등록 안내
        Debug.Log("=== Gift 프리팹 생성 완료 ===");
        Debug.Log($"  GiftItemSlot:    {PrefabFolder}/GiftItemSlot.prefab");
        Debug.Log($"  GiftHeroineSlot: {PrefabFolder}/GiftHeroineSlot.prefab");
        Debug.Log($"  GiftPopup:       {PrefabFolder}/GiftPopup.prefab");
        Debug.Log("PopupManager의 modalPrefabs에 GiftPopup 프리팹을 추가하세요.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("완료",
            "Gift 관련 프리팹 3개가 생성되었습니다.\n" +
            "PopupManager 인스펙터에서 ModalPrefabs에\n" +
            "GiftPopup 프리팹을 추가해주세요.", "확인");
    }

    // ──────────────────────────────────────────────
    //  GiftItemSlot 프리팹
    // ──────────────────────────────────────────────
    static GameObject CreateGiftItemSlotPrefab()
    {
        string path = $"{PrefabFolder}/GiftItemSlot.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"GiftItemSlot 프리팹 이미 존재: {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        var root = CreateUIRoot("GiftItemSlot", 400, 60);

        // 배경 이미지
        var bg = root.GetComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        // HorizontalLayoutGroup
        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.padding = new RectOffset(12, 12, 8, 8);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // LayoutElement
        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 60;
        le.preferredWidth = 400;

        // txt_name
        var txtName = CreateTMPChild(root.transform, "txt_name", "아이템 이름", 18,
            TextAlignmentOptions.Left, new Vector2(240, 40));
        var nameLe = txtName.gameObject.AddComponent<LayoutElement>();
        nameLe.preferredWidth = 240;
        nameLe.flexibleWidth = 1;

        // txt_count
        var txtCount = CreateTMPChild(root.transform, "txt_count", "x1", 16,
            TextAlignmentOptions.Right, new Vector2(50, 40));
        var cntLe = txtCount.gameObject.AddComponent<LayoutElement>();
        cntLe.preferredWidth = 50;

        // btn_select (전체를 버튼으로)
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        // GiftItemSlot 컴포넌트 추가 & 바인딩
        var slot = root.AddComponent<LoveAlgo.Shop.GiftItemSlot>();
        SetField(slot, "nameText", txtName);
        SetField(slot, "countText", txtCount);
        SetField(slot, "selectButton", btn);

        // 프리팹 저장
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log($"GiftItemSlot 프리팹 생성: {path}");
        return prefab;
    }

    // ──────────────────────────────────────────────
    //  GiftHeroineSlot 프리팹
    // ──────────────────────────────────────────────
    static GameObject CreateGiftHeroineSlotPrefab()
    {
        string path = $"{PrefabFolder}/GiftHeroineSlot.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"GiftHeroineSlot 프리팹 이미 존재: {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        var root = CreateUIRoot("GiftHeroineSlot", 380, 70);

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0.2f, 0.15f, 0.25f, 0.9f);

        // VerticalLayoutGroup
        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.padding = new RectOffset(12, 12, 6, 6);
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 70;
        le.preferredWidth = 380;

        // txt_name (히로인 이름)
        var txtName = CreateTMPChild(root.transform, "txt_name", "히로인 이름", 20,
            TextAlignmentOptions.Center, new Vector2(350, 30));

        // txt_remaining (남은 포인트)
        var txtRemaining = CreateTMPChild(root.transform, "txt_remaining", "남은 선물 포인트: 8", 14,
            TextAlignmentOptions.Center, new Vector2(350, 22));
        txtRemaining.color = new Color(1f, 0.8f, 0.5f);

        // 버튼
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        // GiftHeroineSlot 컴포넌트 & 바인딩
        var slot = root.AddComponent<LoveAlgo.Shop.GiftHeroineSlot>();
        SetField(slot, "nameText", txtName);
        SetField(slot, "remainingText", txtRemaining);
        SetField(slot, "selectButton", btn);

        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log($"GiftHeroineSlot 프리팹 생성: {path}");
        return prefab;
    }

    // ──────────────────────────────────────────────
    //  GiftPopup 프리팹
    // ──────────────────────────────────────────────
    static GameObject CreateGiftPopupPrefab(GameObject itemSlotPrefab, GameObject heroineSlotPrefab)
    {
        string path = $"{PrefabFolder}/GiftPopup.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"GiftPopup 프리팹 이미 존재: {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        var root = CreateUIRoot("GiftPopup", 500, 600);

        var rootBg = root.GetComponent<Image>();
        rootBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // VerticalLayoutGroup (루트)
        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // ── Header ──
        var header = CreateTMPChild(root.transform, "txt_header", "선물 주기", 24,
            TextAlignmentOptions.Center, new Vector2(460, 40));
        header.fontStyle = FontStyles.Bold;
        var headerLe = header.gameObject.AddComponent<LayoutElement>();
        headerLe.preferredHeight = 40;

        // ── Message ──
        var msgText = CreateTMPChild(root.transform, "txt_message", "선물할 아이템을 선택하세요.", 16,
            TextAlignmentOptions.Center, new Vector2(460, 30));
        var msgLe = msgText.gameObject.AddComponent<LayoutElement>();
        msgLe.preferredHeight = 30;

        // ── Item List (ScrollView) ──
        var itemScroll = CreateScrollView(root.transform, "ItemListScroll", 460, 200);
        var itemContainer = itemScroll.transform.Find("Viewport/Content");

        // ── Heroine List (ScrollView) ──
        var heroineScroll = CreateScrollView(root.transform, "HeroineListScroll", 460, 250);
        var heroineContainer = heroineScroll.transform.Find("Viewport/Content");

        // ── Back Button ──
        var btnBack = CreateButton(root.transform, "btn_back", "닫기", 120, 40);
        var btnLe = btnBack.GetComponent<LayoutElement>();
        if (btnLe == null) btnLe = btnBack.AddComponent<LayoutElement>();
        btnLe.preferredHeight = 40;
        btnLe.preferredWidth = 120;

        // ── GiftPopup 컴포넌트 & 바인딩 ──
        var giftPopup = root.AddComponent<LoveAlgo.Shop.GiftPopup>();
        SetField(giftPopup, "itemListContainer", itemContainer);
        SetField(giftPopup, "heroineListContainer", heroineContainer);
        SetField(giftPopup, "messageText", msgText);
        SetField(giftPopup, "backButton", btnBack.GetComponent<Button>());

        // 프리팹 참조 바인딩
        var itemSlotComp = itemSlotPrefab.GetComponent<LoveAlgo.Shop.GiftItemSlot>();
        var heroineSlotComp = heroineSlotPrefab.GetComponent<LoveAlgo.Shop.GiftHeroineSlot>();
        SetField(giftPopup, "itemSlotPrefab", itemSlotComp);
        SetField(giftPopup, "heroineSlotPrefab", heroineSlotComp);

        // CanvasGroup (팝업 표시/숨김용)
        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 1;

        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log($"GiftPopup 프리팹 생성: {path}");
        return prefab;
    }

    // ──────────────────────────────────────────────
    //  유틸리티
    // ──────────────────────────────────────────────

    static GameObject CreateUIRoot(string name, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static TMP_Text CreateTMPChild(Transform parent, string name, string text, float fontSize,
        TextAlignmentOptions alignment, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.richText = true;

        if (defaultFont != null)
            tmp.font = defaultFont;

        return tmp;
    }

    static GameObject CreateScrollView(Transform parent, string name, float width, float height)
    {
        var scroll = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        scroll.transform.SetParent(parent, false);

        var scrollRt = scroll.GetComponent<RectTransform>();
        scrollRt.sizeDelta = new Vector2(width, height);

        var scrollImg = scroll.GetComponent<Image>();
        scrollImg.color = new Color(0.08f, 0.08f, 0.12f, 0.5f);

        var scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var scrollLe = scroll.AddComponent<LayoutElement>();
        scrollLe.preferredHeight = height;
        scrollLe.flexibleHeight = 1;

        // Viewport
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scroll.transform, false);

        var vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.sizeDelta = Vector2.zero;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;

        var vpImg = viewport.GetComponent<Image>();
        vpImg.color = new Color(1, 1, 1, 0.01f); // 거의 투명 (Mask 필요)

        var vpMask = viewport.GetComponent<Mask>();
        vpMask.showMaskGraphic = false;

        scrollRect.viewport = vpRt;

        // Content
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);

        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.sizeDelta = new Vector2(0, 0);

        var contentVlg = content.AddComponent<VerticalLayoutGroup>();
        contentVlg.spacing = 4;
        contentVlg.padding = new RectOffset(4, 4, 4, 4);
        contentVlg.childAlignment = TextAnchor.UpperCenter;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRt;

        return scroll;
    }

    static GameObject CreateButton(Transform parent, string name, string label, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.4f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        // 버튼 텍스트
        var txt = CreateTMPChild(go.transform, "Text", label, 16,
            TextAlignmentOptions.Center, new Vector2(width - 10, height - 6));

        return go;
    }

    static TMP_FontAsset FindDefaultTMPFont()
    {
        // 프로젝트의 TMP 폰트 찾기
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts" });
        if (guids.Length > 0)
        {
            var fontPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        }

        // 기본 TMP 폰트
        guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (var guid in guids)
        {
            var fontPath = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font != null) return font;
        }
        return null;
    }

    [MenuItem("Tools/Register GiftPopup in PopupManager")]
    public static void RegisterGiftPopupInPopupManager()
    {
        // 씬에서 PopupManager 찾기
        var pm = Object.FindFirstObjectByType<LoveAlgo.UI.PopupManager>();
        if (pm == null)
        {
            Debug.LogError("PopupManager를 씬에서 찾을 수 없습니다.");
            return;
        }

        // GiftPopup 프리팹 로드
        var giftPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/GiftPopup.prefab");
        if (giftPrefab == null)
        {
            Debug.LogError("GiftPopup 프리팹이 없습니다. 먼저 Tools > Create Gift Prefabs를 실행하세요.");
            return;
        }

        // modalPrefabs 리스트에 추가 (이미 있으면 스킵)
        var field = typeof(LoveAlgo.UI.PopupManager).GetField("modalPrefabs",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
        {
            Debug.LogError("modalPrefabs 필드를 찾을 수 없습니다.");
            return;
        }

        var list = field.GetValue(pm) as System.Collections.Generic.List<GameObject>;
        if (list == null)
        {
            list = new System.Collections.Generic.List<GameObject>();
            field.SetValue(pm, list);
        }

        // 중복 체크
        bool alreadyExists = false;
        foreach (var go in list)
        {
            if (go != null && go.name == "GiftPopup")
            {
                alreadyExists = true;
                break;
            }
        }

        if (alreadyExists)
        {
            Debug.Log("GiftPopup이 이미 modalPrefabs에 등록되어 있습니다.");
        }
        else
        {
            list.Add(giftPrefab);
            EditorUtility.SetDirty(pm);
            Debug.Log("GiftPopup을 PopupManager.modalPrefabs에 등록했습니다.");
        }

        // 씬 저장 마크
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
        Debug.Log("씬을 저장해주세요 (Ctrl+S).");
    }

    /// <summary>리플렉션으로 SerializeField 설정</summary>
    static void SetField(object target, string fieldName, object value)
    {
        var type = target.GetType();
        while (type != null)
        {
            var field = type.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        Debug.LogWarning($"[GiftPrefabCreator] 필드 '{fieldName}'을(를) 찾을 수 없음: {target.GetType().Name}");
    }
}
#endif
