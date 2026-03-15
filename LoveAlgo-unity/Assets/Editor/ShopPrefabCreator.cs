#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

/// <summary>
/// 상점 UI 프리팹 자동 생성/재생성 에디터 유틸리티
/// 메뉴: Tools > Create Shop Prefabs
/// 
/// 생성 프리팹:
///   1. ShopSaleSlot   — 카드형 그리드 아이템 슬롯
///   2. ShopCartSlot   — 장바구니 슬롯 (아이콘 + 수량 조절)
///   3. ShopItemDetail — 호버 설명 팝업
///   4. ShopPanel      — 상점 패널 (ScheduleUI 내 크로스페이드용)
///
/// 임베드 메뉴: Tools > Embed ShopPanel in ScheduleUI
/// </summary>
public static class ShopPrefabCreator
{
    const string PrefabFolder   = "Assets/Prefabs/Shop";
    const string ArtFolder      = "Assets/Art/UI/Shop";

    static TMP_FontAsset defaultFont;

    // Art 리소스 캐시
    static Sprite spr_item_slot;
    static Sprite spr_item_slot_hover;
    static Sprite spr_cart_slot;
    static Sprite spr_cart_panel;
    static Sprite spr_main_panel;
    static Sprite spr_detail_popup;
    static Sprite spr_purchase;
    static Sprite spr_qty_plus;
    static Sprite spr_qty_plus_disabled;
    static Sprite spr_qty_minus;
    static Sprite spr_qty_minus_disabled;
    static Sprite spr_scroll_list_bg;
    static Sprite spr_scroll_list_handle;
    static Sprite spr_scroll_cart_bg;
    static Sprite spr_scroll_cart_handle;

    [MenuItem("Tools/Create Shop Prefabs")]
    public static void CreateAllShopPrefabs()
    {
        defaultFont = FindDefaultTMPFont();
        LoadArtAssets();

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Shop");

        // ShopPanel을 먼저 삭제 (nested prefab 참조 깨짐 방지)
        DeleteIfExists($"{PrefabFolder}/ShopPanel.prefab");
        // 이전 ShopUI.prefab이 있으면 삭제
        DeleteIfExists($"{PrefabFolder}/ShopUI.prefab");

        var saleSlot   = CreateShopSaleSlotPrefab();
        var cartSlot   = CreateShopCartSlotPrefab();
        var detailPopup = CreateShopItemDetailPrefab();
        var shopPanel  = CreateShopPanelPrefab(saleSlot, cartSlot, detailPopup);

        Debug.Log("=== Shop 프리팹 생성/갱신 완료 ===");
        Debug.Log($"  ShopSaleSlot:   {PrefabFolder}/ShopSaleSlot.prefab");
        Debug.Log($"  ShopCartSlot:   {PrefabFolder}/ShopCartSlot.prefab");
        Debug.Log($"  ShopItemDetail: {PrefabFolder}/ShopItemDetail.prefab");
        Debug.Log($"  ShopPanel:      {PrefabFolder}/ShopPanel.prefab");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("완료",
            "Shop 프리팹 4개가 생성/갱신되었습니다.\n" +
            "Tools > Embed ShopPanel in ScheduleUI로\n" +
            "ScheduleUI에 임베드하세요.", "확인");
    }

    // ══════════════════════════════════════════════
    //  Art 리소스 로드
    // ══════════════════════════════════════════════
    static void LoadArtAssets()
    {
        spr_item_slot          = LoadSprite("btn_shop_item_slot");
        spr_item_slot_hover    = LoadSprite("btn_shop_item_slot_hover");
        spr_cart_slot          = LoadSprite("bg_shop_cart_slot");
        spr_cart_panel         = LoadSprite("bg_shop_cart_panel");
        spr_main_panel         = LoadSprite("bg_shop_main_panel");
        spr_detail_popup       = LoadSprite("bg_shop_item_detail_popup");
        spr_purchase           = LoadSprite("btn_shop_purchase");
        spr_qty_plus           = LoadSprite("btn_shop_qty_plus");
        spr_qty_plus_disabled  = LoadSprite("btn_shop_qty_plus_disabled");
        spr_qty_minus          = LoadSprite("btn_shop_qty_minus");
        spr_qty_minus_disabled = LoadSprite("btn_shop_qty_minus_disabled");
        spr_scroll_list_bg     = LoadSprite("slider_shop_list_bg");
        spr_scroll_list_handle = LoadSprite("slider_shop_list_handle");
        spr_scroll_cart_bg     = LoadSprite("slider_shop_cart_bg");
        spr_scroll_cart_handle = LoadSprite("slider_shop_cart_handle");
    }

    static Sprite LoadSprite(string name)
    {
        string path = $"{ArtFolder}/{name}.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"[ShopPrefabCreator] 스프라이트 로드 실패: {path}");
        return sprite;
    }

    // ══════════════════════════════════════════════
    //  1. ShopSaleSlot 프리팹 (카드형)
    // ══════════════════════════════════════════════
    static GameObject CreateShopSaleSlotPrefab()
    {
        string path = $"{PrefabFolder}/ShopSaleSlot.prefab";
        DeleteIfExists(path);

        // 루트: 카드 배경
        var root = CreateUIRoot("ShopSaleSlot", 200, 260);
        var bgImg = root.GetComponent<Image>();
        bgImg.sprite = spr_item_slot;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;

        // 버튼 (카드 전체 클릭)
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        // VerticalLayoutGroup
        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.padding = new RectOffset(12, 12, 16, 12);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // img_icon (아이템 아이콘, 큰 이미지)
        var iconGo = new GameObject("img_icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.color = new Color(1, 1, 1, 0.8f);
        iconImg.preserveAspect = true;
        var iconLe = iconGo.AddComponent<LayoutElement>();
        iconLe.preferredWidth = 120;
        iconLe.preferredHeight = 120;

        // txt_name
        var nameTmp = CreateTMPChild(root.transform, "txt_name", "아이템 이름", 16,
            TextAlignmentOptions.Center, new Vector2(176, 36));
        nameTmp.fontStyle = FontStyles.Bold;
        var nameLe = nameTmp.gameObject.AddComponent<LayoutElement>();
        nameLe.preferredHeight = 36;

        // txt_price
        var priceTmp = CreateTMPChild(root.transform, "txt_price", "0원", 14,
            TextAlignmentOptions.Center, new Vector2(176, 24));
        priceTmp.color = new Color(1f, 0.85f, 0.3f); // 금색
        var priceLe = priceTmp.gameObject.AddComponent<LayoutElement>();
        priceLe.preferredHeight = 24;

        // obj_check (체크마크 — 우측 상단 오버레이)
        var checkGo = new GameObject("obj_check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        checkGo.transform.SetParent(root.transform, false);
        var checkRt = checkGo.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(1, 1);
        checkRt.anchorMax = new Vector2(1, 1);
        checkRt.pivot = new Vector2(1, 1);
        checkRt.anchoredPosition = new Vector2(-8, -8);
        checkRt.sizeDelta = new Vector2(36, 36);
        var checkImg = checkGo.GetComponent<Image>();
        checkImg.color = new Color(0.2f, 0.9f, 0.3f, 1f); // 초록 체크 (아이콘 없을 때 색으로 구분)
        checkGo.SetActive(false); // 기본 비활성

        // ── ShopSaleSlot 컴포넌트 바인딩 ──
        var slot = root.AddComponent<LoveAlgo.Shop.ShopSaleSlot>();
        SetField(slot, "bgImage",       bgImg);
        SetField(slot, "iconImage",     iconImg);
        SetField(slot, "nameText",      nameTmp);
        SetField(slot, "priceText",     priceTmp);
        SetField(slot, "checkMark",     checkGo);
        SetField(slot, "slotButton",    btn);
        SetField(slot, "normalSprite",  spr_item_slot);
        SetField(slot, "hoverSprite",   spr_item_slot_hover);

        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log($"ShopSaleSlot 프리팹 생성: {path}");
        return prefab;
    }

    // ══════════════════════════════════════════════
    //  2. ShopCartSlot 프리팹
    // ══════════════════════════════════════════════
    static GameObject CreateShopCartSlotPrefab()
    {
        string path = $"{PrefabFolder}/ShopCartSlot.prefab";
        DeleteIfExists(path);

        var root = CreateUIRoot("ShopCartSlot", 340, 52);
        var bgImg = root.GetComponent<Image>();
        bgImg.sprite = spr_cart_slot;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;

        // HorizontalLayoutGroup
        var hlg = root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.padding = new RectOffset(8, 8, 4, 4);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var rootLe = root.AddComponent<LayoutElement>();
        rootLe.preferredHeight = 52;

        // img_icon (소형)
        var iconGo = new GameObject("img_icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.preserveAspect = true;
        var iconLe = iconGo.AddComponent<LayoutElement>();
        iconLe.preferredWidth = 36;
        iconLe.preferredHeight = 36;

        // txt_name
        var nameTmp = CreateTMPChild(root.transform, "txt_name", "이름", 13,
            TextAlignmentOptions.Left, new Vector2(100, 40));
        var nameLe = nameTmp.gameObject.AddComponent<LayoutElement>();
        nameLe.preferredWidth = 100;
        nameLe.flexibleWidth = 1;

        // txt_price
        var priceTmp = CreateTMPChild(root.transform, "txt_price", "0원", 12,
            TextAlignmentOptions.Right, new Vector2(60, 40));
        priceTmp.color = new Color(1f, 0.85f, 0.3f);
        var priceLe2 = priceTmp.gameObject.AddComponent<LayoutElement>();
        priceLe2.preferredWidth = 60;

        // btn_minus (disabled 스프라이트 적용)
        var btnMinus = CreateImageButton(root.transform, "btn_minus", spr_qty_minus, 28, 28);
        var minusLe = btnMinus.AddComponent<LayoutElement>();
        minusLe.preferredWidth = 28;
        minusLe.preferredHeight = 28;
        if (spr_qty_minus_disabled != null)
        {
            var minusBtn = btnMinus.GetComponent<Button>();
            minusBtn.transition = Selectable.Transition.SpriteSwap;
            var minusSS = new SpriteState();
            minusSS.disabledSprite = spr_qty_minus_disabled;
            minusBtn.spriteState = minusSS;
        }

        // txt_qty
        var qtyTmp = CreateTMPChild(root.transform, "txt_qty", "1", 14,
            TextAlignmentOptions.Center, new Vector2(28, 40));
        var qtyLe = qtyTmp.gameObject.AddComponent<LayoutElement>();
        qtyLe.preferredWidth = 28;

        // btn_plus (disabled 스프라이트 적용)
        var btnPlus = CreateImageButton(root.transform, "btn_plus", spr_qty_plus, 28, 28);
        var plusLe = btnPlus.AddComponent<LayoutElement>();
        plusLe.preferredWidth = 28;
        plusLe.preferredHeight = 28;
        if (spr_qty_plus_disabled != null)
        {
            var plusBtn = btnPlus.GetComponent<Button>();
            plusBtn.transition = Selectable.Transition.SpriteSwap;
            var plusSS = new SpriteState();
            plusSS.disabledSprite = spr_qty_plus_disabled;
            plusBtn.spriteState = plusSS;
        }

        // ── ShopCartSlot 컴포넌트 바인딩 ──
        var slot = root.AddComponent<LoveAlgo.Shop.ShopCartSlot>();
        SetField(slot, "iconImage",    iconImg);
        SetField(slot, "nameText",     nameTmp);
        SetField(slot, "priceText",    priceTmp);
        SetField(slot, "quantityText", qtyTmp);
        SetField(slot, "plusButton",   btnPlus.GetComponent<Button>());
        SetField(slot, "minusButton",  btnMinus.GetComponent<Button>());

        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log($"ShopCartSlot 프리팹 생성: {path}");
        return prefab;
    }

    // ══════════════════════════════════════════════
    //  3. ShopItemDetail 프리팹 (호버 설명 팝업)
    // ══════════════════════════════════════════════
    static GameObject CreateShopItemDetailPrefab()
    {
        string path = $"{PrefabFolder}/ShopItemDetail.prefab";
        DeleteIfExists(path);

        var root = CreateUIRoot("ShopItemDetail", 280, 360);
        var bgImg = root.GetComponent<Image>();
        bgImg.sprite = spr_detail_popup;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;

        // VerticalLayoutGroup
        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(16, 16, 20, 16);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // img_icon
        var iconGo = new GameObject("img_icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.preserveAspect = true;
        var iconLe = iconGo.AddComponent<LayoutElement>();
        iconLe.preferredWidth = 100;
        iconLe.preferredHeight = 100;

        // txt_name
        var nameTmp = CreateTMPChild(root.transform, "txt_name", "아이템 이름", 18,
            TextAlignmentOptions.Center, new Vector2(248, 30));
        nameTmp.fontStyle = FontStyles.Bold;
        var nameLe = nameTmp.gameObject.AddComponent<LayoutElement>();
        nameLe.preferredHeight = 30;

        // txt_price
        var priceTmp = CreateTMPChild(root.transform, "txt_price", "0원", 15,
            TextAlignmentOptions.Center, new Vector2(248, 24));
        priceTmp.color = new Color(1f, 0.85f, 0.3f);
        var priceLe = priceTmp.gameObject.AddComponent<LayoutElement>();
        priceLe.preferredHeight = 24;

        // txt_desc
        var descTmp = CreateTMPChild(root.transform, "txt_desc", "아이템 설명...", 13,
            TextAlignmentOptions.Left, new Vector2(248, 100));
        descTmp.color = new Color(0.9f, 0.9f, 0.9f);
        descTmp.textWrappingMode = TextWrappingModes.Normal;
        var descLe = descTmp.gameObject.AddComponent<LayoutElement>();
        descLe.preferredHeight = 100;
        descLe.flexibleHeight = 1;

        // ── ShopItemDetailPopup 컴포넌트 바인딩 ──
        var detail = root.AddComponent<LoveAlgo.Shop.ShopItemDetailPopup>();
        SetField(detail, "bgImage",          bgImg);
        SetField(detail, "iconImage",        iconImg);
        SetField(detail, "nameText",         nameTmp);
        SetField(detail, "priceText",        priceTmp);
        SetField(detail, "descriptionText",  descTmp);

        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log($"ShopItemDetail 프리팹 생성: {path}");
        return prefab;
    }

    // ══════════════════════════════════════════════
    //  4. ShopPanel 프리팹 (크로스페이드 패널)
    // ══════════════════════════════════════════════
    static GameObject CreateShopPanelPrefab(
        GameObject saleSlotPrefab,
        GameObject cartSlotPrefab,
        GameObject detailPopupPrefab)
    {
        string path = $"{PrefabFolder}/ShopPanel.prefab";
        DeleteIfExists(path);

        // 루트: Stretch로 부모에 맞춤 (ScheduleUI 내 크로스페이드 패널)
        var root = new GameObject("ShopPanel", typeof(RectTransform), typeof(CanvasRenderer));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // CanvasGroup (크로스페이드용 — ScheduleUI가 alpha 제어)
        var cg = root.AddComponent<CanvasGroup>();

        // ── 메인 패널 (중앙) ──
        var mainPanel = new GameObject("MainPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        mainPanel.transform.SetParent(root.transform, false);
        var mainRt = mainPanel.GetComponent<RectTransform>();
        mainRt.anchorMin = new Vector2(0.5f, 0.5f);
        mainRt.anchorMax = new Vector2(0.5f, 0.5f);
        mainRt.pivot = new Vector2(0.5f, 0.5f);
        mainRt.sizeDelta = new Vector2(960, 640);

        var mainImg = mainPanel.GetComponent<Image>();
        mainImg.sprite = spr_main_panel;
        mainImg.type = Image.Type.Sliced;
        mainImg.color = Color.white;

        // HorizontalLayoutGroup (좌: 카트, 우: 그리드)
        var mainHlg = mainPanel.AddComponent<HorizontalLayoutGroup>();
        mainHlg.spacing = 12;
        mainHlg.padding = new RectOffset(16, 16, 16, 16);
        mainHlg.childAlignment = TextAnchor.MiddleCenter;
        mainHlg.childForceExpandWidth = false;
        mainHlg.childForceExpandHeight = true;

        // ══════ 좌측 패널: 잔액 + 장바구니 + 합계 + 구매 ══════
        var leftPanel = new GameObject("LeftPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        leftPanel.transform.SetParent(mainPanel.transform, false);
        var leftLe = leftPanel.AddComponent<LayoutElement>();
        leftLe.preferredWidth = 360;
        leftLe.flexibleHeight = 1;

        // 좌측 패널 배경: bg_shop_cart_panel
        var leftImg = leftPanel.GetComponent<Image>();
        leftImg.sprite = spr_cart_panel;
        leftImg.type = Image.Type.Sliced;
        leftImg.color = Color.white;

        var leftVlg = leftPanel.AddComponent<VerticalLayoutGroup>();
        leftVlg.spacing = 8;
        leftVlg.padding = new RectOffset(4, 4, 4, 4);
        leftVlg.childAlignment = TextAnchor.UpperCenter;
        leftVlg.childForceExpandWidth = true;
        leftVlg.childForceExpandHeight = false;

        // txt_money (잔액)
        var moneyTmp = CreateTMPChild(leftPanel.transform, "txt_money", "500,000원", 20,
            TextAlignmentOptions.Left, new Vector2(340, 32));
        moneyTmp.fontStyle = FontStyles.Bold;
        var moneyLe = moneyTmp.gameObject.AddComponent<LayoutElement>();
        moneyLe.preferredHeight = 32;

        // 장바구니 헤더
        var cartHeader = CreateTMPChild(leftPanel.transform, "txt_cart_header", "CART", 16,
            TextAlignmentOptions.Left, new Vector2(340, 24));
        cartHeader.color = new Color(0.7f, 0.7f, 0.7f);
        var cartHeaderLe = cartHeader.gameObject.AddComponent<LayoutElement>();
        cartHeaderLe.preferredHeight = 24;

        // 장바구니 스크롤뷰
        var cartScroll = CreateScrollView(leftPanel.transform, "CartScroll", 340, 350,
            spr_scroll_cart_bg, spr_scroll_cart_handle);
        var cartContent = cartScroll.transform.Find("Viewport/Content");

        // 합계 금액
        var totalTmp = CreateTMPChild(leftPanel.transform, "txt_total", "0원", 18,
            TextAlignmentOptions.Right, new Vector2(340, 28));
        totalTmp.fontStyle = FontStyles.Bold;
        totalTmp.color = new Color(1f, 0.85f, 0.3f);
        var totalLe = totalTmp.gameObject.AddComponent<LayoutElement>();
        totalLe.preferredHeight = 28;

        // 구매 버튼
        var btnPurchase = CreateImageButton(leftPanel.transform, "btn_purchase", spr_purchase, 340, 50);
        var purchaseTmp = CreateTMPChild(btnPurchase.transform, "Text", "구매하기", 18,
            TextAlignmentOptions.Center, new Vector2(320, 44));
        purchaseTmp.fontStyle = FontStyles.Bold;
        var purchaseLe = btnPurchase.AddComponent<LayoutElement>();
        purchaseLe.preferredHeight = 50;

        // ══════ 우측 패널: 아이템 그리드 ══════
        var rightPanel = new GameObject("RightPanel", typeof(RectTransform));
        rightPanel.transform.SetParent(mainPanel.transform, false);
        var rightLe = rightPanel.AddComponent<LayoutElement>();
        rightLe.flexibleWidth = 1;
        rightLe.flexibleHeight = 1;

        var rightVlg = rightPanel.AddComponent<VerticalLayoutGroup>();
        rightVlg.spacing = 0;
        rightVlg.padding = new RectOffset(0, 0, 0, 0);
        rightVlg.childForceExpandWidth = true;
        rightVlg.childForceExpandHeight = true;

        // 그리드 스크롤뷰
        var gridScroll = CreateScrollView(rightPanel.transform, "GridScroll", 560, 580,
            spr_scroll_list_bg, spr_scroll_list_handle, useGridLayout: true);
        var gridContent = gridScroll.transform.Find("Viewport/Content");

        // 뒤로가기 버튼 (우측 상단)
        var btnBack = CreateButton(mainPanel.transform, "btn_back", "X", 40, 40);
        var backRt = btnBack.GetComponent<RectTransform>();
        backRt.anchorMin = new Vector2(1, 1);
        backRt.anchorMax = new Vector2(1, 1);
        backRt.pivot = new Vector2(1, 1);
        backRt.anchoredPosition = new Vector2(-8, -8);

        // ── ShopItemDetailPopup 인스턴스 (씬 내 직접 배치) ──
        GameObject detailInstance = null;
        if (detailPopupPrefab != null)
        {
            detailInstance = (GameObject)PrefabUtility.InstantiatePrefab(detailPopupPrefab);
            detailInstance.name = "ShopItemDetailPopup";
            detailInstance.transform.SetParent(root.transform, false);
            var detailRt = detailInstance.GetComponent<RectTransform>();
            detailRt.anchoredPosition = Vector2.zero;
            detailInstance.SetActive(false);
        }

        // ── ShopPopup 컴포넌트 바인딩 (MonoBehaviour 패널) ──
        var shopPopup = root.AddComponent<LoveAlgo.Shop.ShopPopup>();

        SetField(shopPopup, "moneyText",       moneyTmp);
        SetField(shopPopup, "saleContainer",   gridContent);
        SetField(shopPopup, "cartContainer",   cartContent);
        SetField(shopPopup, "totalPriceText",  totalTmp);
        SetField(shopPopup, "purchaseButton",  btnPurchase.GetComponent<Button>());
        SetField(shopPopup, "backButton",      btnBack.GetComponent<Button>());

        // 프리팹 참조 바인딩
        if (saleSlotPrefab != null)
        {
            var saleComp = saleSlotPrefab.GetComponent<LoveAlgo.Shop.ShopSaleSlot>();
            SetField(shopPopup, "saleSlotPrefab", saleComp);
        }
        if (cartSlotPrefab != null)
        {
            var cartComp = cartSlotPrefab.GetComponent<LoveAlgo.Shop.ShopCartSlot>();
            SetField(shopPopup, "cartSlotPrefab", cartComp);
        }
        if (detailInstance != null)
        {
            var detailComp = detailInstance.GetComponent<LoveAlgo.Shop.ShopItemDetailPopup>();
            SetField(shopPopup, "itemDetailPopup", detailComp);
        }

        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log($"ShopPanel 프리팹 생성: {path}");
        return prefab;
    }

    // ══════════════════════════════════════════════
    //  유틸리티
    // ══════════════════════════════════════════════

    static void DeleteIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"기존 프리팹 삭제: {path}");
        }
    }

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
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.richText = true;

        if (defaultFont != null)
            tmp.font = defaultFont;

        return tmp;
    }

    static GameObject CreateImageButton(Transform parent, string name, Sprite sprite, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        return go;
    }

    static GameObject CreateButton(Transform parent, string name, string label, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.4f, 0.9f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };

        CreateTMPChild(go.transform, "Text", label, 16,
            TextAlignmentOptions.Center, new Vector2(w - 4, h - 4));

        return go;
    }

    static GameObject CreateScrollView(Transform parent, string name, float width, float height,
        Sprite scrollBgSprite = null, Sprite scrollHandleSprite = null, bool useGridLayout = false)
    {
        var scroll = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        scroll.transform.SetParent(parent, false);

        var scrollRt = scroll.GetComponent<RectTransform>();
        scrollRt.sizeDelta = new Vector2(width, height);

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
        vpImg.color = new Color(1, 1, 1, 0.01f);

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

        if (useGridLayout)
        {
            // 그리드 레이아웃 (4열)
            var glg = content.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(200, 260);
            glg.spacing = new Vector2(8, 8);
            glg.padding = new RectOffset(8, 8, 8, 8);
            glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = TextAnchor.UpperLeft;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;
        }
        else
        {
            // 세로 리스트
            var contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 4;
            contentVlg.padding = new RectOffset(4, 4, 4, 4);
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;
        }

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRt;

        // 스크롤바
        if (scrollBgSprite != null || scrollHandleSprite != null)
        {
            var scrollbar = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Scrollbar));
            scrollbar.transform.SetParent(scroll.transform, false);

            var sbRt = scrollbar.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1, 0);
            sbRt.anchorMax = new Vector2(1, 1);
            sbRt.pivot = new Vector2(1, 0.5f);
            sbRt.sizeDelta = new Vector2(12, 0);
            sbRt.anchoredPosition = new Vector2(0, 0);

            var sbImg = scrollbar.GetComponent<Image>();
            if (scrollBgSprite != null)
            {
                sbImg.sprite = scrollBgSprite;
                sbImg.type = Image.Type.Sliced;
            }
            else
            {
                sbImg.color = new Color(0.15f, 0.15f, 0.2f, 0.5f);
            }

            // 핸들 영역
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(scrollbar.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.sizeDelta = Vector2.zero;
            haRt.offsetMin = Vector2.zero;
            haRt.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = new Vector2(1, 0.3f);
            handleRt.sizeDelta = Vector2.zero;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            var handleImg = handle.GetComponent<Image>();
            if (scrollHandleSprite != null)
            {
                handleImg.sprite = scrollHandleSprite;
                handleImg.type = Image.Type.Sliced;
            }
            else
            {
                handleImg.color = new Color(0.4f, 0.4f, 0.5f);
            }

            var sb = scrollbar.GetComponent<Scrollbar>();
            sb.handleRect = handleRt;
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.targetGraphic = handleImg;

            scrollRect.verticalScrollbar = sb;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = -2;
        }

        return scroll;
    }

    static TMP_FontAsset FindDefaultTMPFont()
    {
        // 프로젝트의 TMP 폰트 먼저 검색
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts" });
        if (guids.Length > 0)
        {
            var fontPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        }

        // 전체 검색
        guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (var guid in guids)
        {
            var fontPath = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font != null) return font;
        }
        return null;
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
        Debug.LogWarning($"[ShopPrefabCreator] 필드 '{fieldName}'을(를) 찾을 수 없음: {target.GetType().Name}");
    }

    [MenuItem("Tools/Embed ShopPanel in ScheduleUI")]
    public static void EmbedShopPanelInScheduleUI()
    {
        // ── 1. ScheduleUI 찾기 ──
        var scheduleUI = Object.FindFirstObjectByType<LoveAlgo.Schedule.ScheduleUI>();
        if (scheduleUI == null)
        {
            Debug.LogError("ScheduleUI를 씬에서 찾을 수 없습니다.");
            return;
        }

        // ── 2. 프리팹 인스턴스인 경우 언팩 (자식 재배치를 위해) ──
        if (PrefabUtility.IsPartOfPrefabInstance(scheduleUI.gameObject))
        {
            PrefabUtility.UnpackPrefabInstance(
                PrefabUtility.GetOutermostPrefabInstanceRoot(scheduleUI.gameObject),
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            Debug.Log("ScheduleUI 프리팹 인스턴스를 언팩했습니다.");
        }

        var scheduleRt = scheduleUI.GetComponent<RectTransform>();

        // ── 3. 기존 ShopPanel 제거 ──
        var existingShop = scheduleUI.transform.Find("ShopPanel");
        if (existingShop != null)
        {
            Object.DestroyImmediate(existingShop.gameObject);
            Debug.Log("기존 ShopPanel 제거됨");
        }

        // ── 3. ScheduleContent 래퍼 생성/확인 ──
        // 기존 스케줄 콘텐츠를 ScheduleContent 래퍼 안에 넣기
        var scheduleContent = scheduleUI.transform.Find("ScheduleContent");
        CanvasGroup scheduleContentCG;

        if (scheduleContent == null)
        {
            // ScheduleContent 래퍼 생성
            var contentGo = new GameObject("ScheduleContent", typeof(RectTransform), typeof(CanvasGroup));
            contentGo.transform.SetParent(scheduleUI.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.sizeDelta = Vector2.zero;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            // 기존 자식들을 ScheduleContent로 이동
            var children = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < scheduleUI.transform.childCount; i++)
            {
                var child = scheduleUI.transform.GetChild(i);
                if (child.gameObject != contentGo)
                    children.Add(child);
            }

            foreach (var child in children)
            {
                child.SetParent(contentGo.transform, true);
            }

            // ScheduleContent를 맨 앞으로
            contentGo.transform.SetAsFirstSibling();

            scheduleContentCG = contentGo.GetComponent<CanvasGroup>();
            Debug.Log($"ScheduleContent 래퍼 생성 완료 ({children.Count}개 자식 이동)");
        }
        else
        {
            scheduleContentCG = scheduleContent.GetComponent<CanvasGroup>();
            if (scheduleContentCG == null)
                scheduleContentCG = scheduleContent.gameObject.AddComponent<CanvasGroup>();
        }

        // ── 4. ShopPanel 프리팹 인스턴스화 ──
        var shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/ShopPanel.prefab");
        if (shopPrefab == null)
        {
            Debug.LogError("ShopPanel 프리팹이 없습니다. 먼저 Tools > Create Shop Prefabs를 실행하세요.");
            return;
        }

        var shopInstance = (GameObject)PrefabUtility.InstantiatePrefab(shopPrefab);
        shopInstance.name = "ShopPanel";
        shopInstance.transform.SetParent(scheduleUI.transform, false);

        var shopRt = shopInstance.GetComponent<RectTransform>();
        shopRt.anchorMin = Vector2.zero;
        shopRt.anchorMax = Vector2.one;
        shopRt.sizeDelta = Vector2.zero;
        shopRt.offsetMin = Vector2.zero;
        shopRt.offsetMax = Vector2.zero;

        // 초기 비활성
        shopInstance.SetActive(false);

        var shopContentCG = shopInstance.GetComponent<CanvasGroup>();
        if (shopContentCG == null)
            shopContentCG = shopInstance.AddComponent<CanvasGroup>();

        var shopPopup = shopInstance.GetComponent<LoveAlgo.Shop.ShopPopup>();

        // ── 5. ScheduleUI 필드 바인딩 ──
        SetField(scheduleUI, "scheduleContent", scheduleContentCG);
        SetField(scheduleUI, "shopContent",     shopContentCG);
        SetField(scheduleUI, "shopPanel",       shopPopup);

        EditorUtility.SetDirty(scheduleUI);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scheduleUI.gameObject.scene);

        // ── 6. PopupManager에서 ShopUI 제거 (더 이상 Modal이 아님) ──
        RemoveShopFromPopupManager();

        Debug.Log("ShopPanel을 ScheduleUI에 성공적으로 임베드했습니다.");
        Debug.Log("씬을 저장해주세요 (Ctrl+S).");
    }

    /// <summary>PopupManager의 modalPrefabs에서 ShopUI/ShopPopup 제거</summary>
    static void RemoveShopFromPopupManager()
    {
        var pm = Object.FindFirstObjectByType<LoveAlgo.UI.PopupManager>();
        if (pm == null) return;

        var field = typeof(LoveAlgo.UI.PopupManager).GetField("modalPrefabs",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null) return;

        var list = field.GetValue(pm) as System.Collections.Generic.List<GameObject>;
        if (list == null) return;

        int removed = list.RemoveAll(go => go != null &&
            (go.name == "ShopUI" || go.name == "ShopPanel" ||
             go.GetComponent<LoveAlgo.Shop.ShopPopup>() != null));

        if (removed > 0)
        {
            EditorUtility.SetDirty(pm);
            Debug.Log($"PopupManager.modalPrefabs에서 ShopUI 참조 {removed}개 제거됨");
        }
    }
}
#endif
