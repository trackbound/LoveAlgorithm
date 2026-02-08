using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.UI;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// LogPopup + LogEntryUI 프리팹 자동 생성 에디터 툴
    /// 1920x1080 Overlay Canvas 위에 채팅 스타일 로그 팝업 구축
    /// </summary>
    public static class LogPopupPrefabBuilder
    {
        // ─── 경로 상수 ────────────────────────────────────
        const string ART_DIR       = "Assets/Art/UI/Log/";
        const string PREFAB_DIR    = "Assets/Prefabs/UI/Popup/";
        const string FONT_PATH     = "Assets/Fonts/Pretendard-Regular.asset";

        // ─── 사이즈 상수 ──────────────────────────────────
        const float POPUP_W = 1755f, POPUP_H = 1089f;
        const float PORTRAIT_SIZE = 110f;
        const float ENTRY_PAD = 20f;
        const float SCROLL_PAD_TOP = 80f, SCROLL_PAD_BOTTOM = 30f;
        const float SCROLL_PAD_LEFT = 60f, SCROLL_PAD_RIGHT = 60f;
        const float SCROLLBAR_W = 18f;
        const float CLOSE_BTN_SIZE = 72f;
        const float NAMEBOX_H = 38f;
        const float ENTRY_SPACING = 16f;
        const float MSG_FONT_SIZE = 26f;
        const float NAME_FONT_SIZE = 24f;
        const float MSG_PAD = 24f;

        // ═══════════════════════════════════════════════════
        //  메인 메뉴
        // ═══════════════════════════════════════════════════
        [MenuItem("LoveAlgo/Tools/Build LogPopup Prefab")]
        static void Build()
        {
            // ── 에셋 로드 ──────────────────────────────────
            var font      = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            var bgWindow  = LoadSprite("bg_log_window");
            var tbChar    = LoadSprite("bg_log_textbox_character");
            var tbUser    = LoadSprite("bg_log_textbox_user");
            var nbChar    = LoadSprite("bg_log_namebox_character");
            var nbUser    = LoadSprite("bg_log_namebox_user");
            var btnReturn = LoadSprite("btn_log_return");
            var btnReturnHover = LoadSprite("btn_log_return_hover");
            var scrollbar = LoadSprite("slider_log_scrollbar");

            var portraits = new (string id, Sprite spr)[]
            {
                ("bom",    LoadSprite("log_portrait_bom")),
                ("daeun",  LoadSprite("log_portrait_daeun")),
                ("heewon", LoadSprite("log_portrait_heewon")),
                ("roa",    LoadSprite("log_portrait_roa")),
                ("yeun",   LoadSprite("log_portrait_yeun")),
            };

            if (font == null)
                Debug.LogWarning($"[LogPopupBuilder] 폰트 없음: {FONT_PATH} — TMP 기본 폰트 사용");

            // ═══════════════════════════════════════════════
            //  1) LogEntryUI 프리팹 제작
            // ═══════════════════════════════════════════════
            var entryPrefab = BuildLogEntryPrefab(font, tbChar, tbUser, nbChar, nbUser);

            // ═══════════════════════════════════════════════
            //  2) 프리뷰 Canvas (Scene 내 배치)
            // ═══════════════════════════════════════════════
            var canvasGO = new GameObject("Canvas_LogPopup_Preview");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ═══════════════════════════════════════════════
            //  3) LogPopup 루트
            // ═══════════════════════════════════════════════
            var popupGO = new GameObject("LogPopup");
            popupGO.layer = LayerMask.NameToLayer("UI");
            popupGO.transform.SetParent(canvasGO.transform, false);

            var popupRect = popupGO.AddComponent<RectTransform>();
            SetCenterAnchors(popupRect, POPUP_W, POPUP_H);

            popupGO.AddComponent<CanvasRenderer>();

            // 배경 이미지
            var popupImg = popupGO.AddComponent<Image>();
            popupImg.sprite = bgWindow;
            popupImg.type = Image.Type.Sliced;
            popupImg.raycastTarget = true;

            // CanvasGroup (팝업 페이드용)
            popupGO.AddComponent<CanvasGroup>();

            // ── LogPopup 컴포넌트 ──────────────────────────
            var logPopup = popupGO.AddComponent<LogPopup>();

            // ═══════════════════════════════════════════════
            //  4) 닫기 버튼 (우상단)
            // ═══════════════════════════════════════════════
            var closeBtnGO = CreateUIObject("CloseButton", popupGO.transform, 5);
            var closeBtnRect = closeBtnGO.GetComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(1, 1);
            closeBtnRect.anchorMax = new Vector2(1, 1);
            closeBtnRect.pivot = new Vector2(1, 1);
            closeBtnRect.anchoredPosition = new Vector2(-20, -16);
            closeBtnRect.sizeDelta = new Vector2(CLOSE_BTN_SIZE, CLOSE_BTN_SIZE);

            var closeBtnImg = closeBtnGO.AddComponent<Image>();
            closeBtnImg.sprite = btnReturn;
            closeBtnImg.type = Image.Type.Simple;
            closeBtnImg.preserveAspect = true;

            var closeBtn = closeBtnGO.AddComponent<Button>();

            // 호버 스프라이트 스왑
            var btnSprState = new SpriteState
            {
                highlightedSprite = btnReturnHover,
                pressedSprite = btnReturnHover
            };
            closeBtn.spriteState = btnSprState;
            closeBtn.transition = Selectable.Transition.SpriteSwap;

            // ═══════════════════════════════════════════════
            //  5) 빈 로그 메시지
            // ═══════════════════════════════════════════════
            var emptyMsgGO = CreateUIObject("EmptyMessage", popupGO.transform, 5);
            StretchFill(emptyMsgGO.GetComponent<RectTransform>());
            var emptyTmp = emptyMsgGO.AddComponent<TextMeshProUGUI>();
            emptyTmp.text = "(로그가 없습니다)";
            emptyTmp.fontSize = 32;
            emptyTmp.alignment = TextAlignmentOptions.Center;
            emptyTmp.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            if (font) emptyTmp.font = font;
            emptyMsgGO.SetActive(false);

            // ═══════════════════════════════════════════════
            //  6) ScrollView
            // ═══════════════════════════════════════════════
            var scrollViewGO = CreateUIObject("ScrollView", popupGO.transform, 5);
            var scrollViewRect = scrollViewGO.GetComponent<RectTransform>();
            StretchFill(scrollViewRect);
            scrollViewRect.offsetMin = new Vector2(SCROLL_PAD_LEFT, SCROLL_PAD_BOTTOM);
            scrollViewRect.offsetMax = new Vector2(-SCROLL_PAD_RIGHT, -SCROLL_PAD_TOP);

            var scrollRectComp = scrollViewGO.AddComponent<ScrollRect>();
            scrollRectComp.horizontal = false;
            scrollRectComp.vertical = true;
            scrollRectComp.movementType = ScrollRect.MovementType.Elastic;
            scrollRectComp.elasticity = 0.1f;
            scrollRectComp.inertia = true;
            scrollRectComp.decelerationRate = 0.135f;
            scrollRectComp.scrollSensitivity = 40f;

            // ── Viewport ──
            var viewportGO = CreateUIObject("Viewport", scrollViewGO.transform, 5);
            var viewportRect = viewportGO.GetComponent<RectTransform>();
            StretchFill(viewportRect);
            viewportRect.offsetMax = new Vector2(-SCROLLBAR_W - 8, 0);  // 스크롤바 공간

            var viewportImg = viewportGO.AddComponent<Image>();
            viewportImg.color = Color.white;
            viewportImg.raycastTarget = true;
            var mask = viewportGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            scrollRectComp.viewport = viewportRect;

            // ── Content ──
            var contentGO = CreateUIObject("Content", viewportGO.transform, 5);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);

            var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(
                (int)ENTRY_PAD, (int)ENTRY_PAD,
                (int)ENTRY_PAD, (int)ENTRY_PAD);
            contentLayout.spacing = ENTRY_SPACING;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRectComp.content = contentRect;

            // ── Scrollbar ──
            var scrollbarGO = CreateUIObject("Scrollbar", scrollViewGO.transform, 5);
            var scrollbarRect = scrollbarGO.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, 0.5f);
            scrollbarRect.anchoredPosition = Vector2.zero;
            scrollbarRect.sizeDelta = new Vector2(SCROLLBAR_W, 0);

            var scrollbarImg = scrollbarGO.AddComponent<Image>();
            scrollbarImg.color = new Color(0, 0, 0, 0);  // 트랙 투명

            var scrollbarComp = scrollbarGO.AddComponent<Scrollbar>();
            scrollbarComp.direction = Scrollbar.Direction.BottomToTop;

            // ─ Sliding Area ─
            var slidingGO = CreateUIObject("SlidingArea", scrollbarGO.transform, 5);
            StretchFill(slidingGO.GetComponent<RectTransform>());

            // ─ Handle ─
            var handleGO = CreateUIObject("Handle", slidingGO.transform, 5);
            var handleRect = handleGO.GetComponent<RectTransform>();
            StretchFill(handleRect);

            var handleImg = handleGO.AddComponent<Image>();
            handleImg.sprite = scrollbar;
            handleImg.type = Image.Type.Sliced;
            handleImg.color = new Color(1, 1, 1, 0.8f);

            scrollbarComp.handleRect = handleRect;
            scrollbarComp.targetGraphic = handleImg;

            scrollRectComp.verticalScrollbar = scrollbarComp;
            scrollRectComp.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRectComp.verticalScrollbarSpacing = 4;

            // ═══════════════════════════════════════════════
            //  7) LogPopup 필드 바인딩 (SerializedObject)
            // ═══════════════════════════════════════════════
            var so = new SerializedObject(logPopup);
            so.FindProperty("scrollRect").objectReferenceValue = scrollRectComp;
            so.FindProperty("contentRoot").objectReferenceValue = contentRect;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("entryPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<LogEntryUI>(PREFAB_DIR + "LogEntryUI.prefab");
            so.FindProperty("characterTextboxSprite").objectReferenceValue = tbChar;
            so.FindProperty("userTextboxSprite").objectReferenceValue = tbUser;
            so.FindProperty("emptyMessage").objectReferenceValue = emptyMsgGO;

            // portraits 리스트
            var portraitsProp = so.FindProperty("portraits");
            portraitsProp.arraySize = portraits.Length;
            for (int i = 0; i < portraits.Length; i++)
            {
                var elem = portraitsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("characterId").stringValue = portraits[i].id;
                elem.FindPropertyRelative("sprite").objectReferenceValue = portraits[i].spr;
            }
            so.ApplyModifiedProperties();

            // ═══════════════════════════════════════════════
            //  8) LogPopup 프리팹 저장
            // ═══════════════════════════════════════════════
            // 기존 파일 백업 알림
            string popupPrefabPath = PREFAB_DIR + "LogPopup.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(popupPrefabPath) != null)
            {
                if (!EditorUtility.DisplayDialog("덮어쓰기 확인",
                    $"기존 {popupPrefabPath} 프리팹을 덮어쓰시겠습니까?",
                    "덮어쓰기", "취소"))
                {
                    Object.DestroyImmediate(canvasGO);
                    return;
                }
            }

            // popupGO를 프리팹으로 저장 (Canvas 없이 LogPopup만)
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                popupGO, popupPrefabPath, InteractionMode.UserAction);

            // ═══════════════════════════════════════════════
            //  완료
            // ═══════════════════════════════════════════════
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build LogPopup Prefab");
            Selection.activeGameObject = popupGO;

            EditorUtility.DisplayDialog("완료",
                "LogPopup 프리팹이 생성되었습니다!\n\n" +
                $"• LogPopup:   {popupPrefabPath}\n" +
                $"• LogEntryUI: {PREFAB_DIR}LogEntryUI.prefab\n" +
                $"• Canvas_LogPopup_Preview가 씬에 배치됨\n" +
                $"• 확인 후 프리뷰 Canvas는 삭제해도 됩니다",
                "확인");

            Debug.Log("[LogPopupBuilder] 프리팹 빌드 완료!");
        }

        // ═══════════════════════════════════════════════════
        //  LogEntryUI 프리팹
        // ═══════════════════════════════════════════════════
        static LogEntryUI BuildLogEntryPrefab(
            TMP_FontAsset font,
            Sprite tbChar, Sprite tbUser,
            Sprite nbChar, Sprite nbUser)
        {
            /*
             * LogEntry (HorizontalLayoutGroup, LogEntryUI)
             * ├── PortraitGroup (VerticalLayoutGroup)
             * │   ├── PortraitImage (Image)
             * │   └── PortraitNameBG (Image) → PortraitNameText
             * ├── ContentColumn (VerticalLayoutGroup)
             * │   ├── UserNameGroup
             * │   │   └── UserNameBG (Image) → UserNameText
             * │   └── TextboxBG (Image)
             * │       └── MessageText (TMP_Text)
             */

            // ── Root ──
            var rootGO = new GameObject("LogEntryUI");
            rootGO.layer = LayerMask.NameToLayer("UI");
            var rootRect = rootGO.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(0, 0);  // layout이 제어

            var rootLayout = rootGO.AddComponent<HorizontalLayoutGroup>();
            rootLayout.padding = new RectOffset(8, 8, 8, 8);
            rootLayout.spacing = 16;
            rootLayout.childAlignment = TextAnchor.UpperLeft;
            rootLayout.childControlWidth = false;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = false;
            rootLayout.childForceExpandHeight = false;

            // ── PortraitGroup ──
            var portraitGroupGO = CreateUIObject("PortraitGroup", rootGO.transform, 5);
            var portraitGroupRect = portraitGroupGO.GetComponent<RectTransform>();

            var portraitGroupLayout = portraitGroupGO.AddComponent<VerticalLayoutGroup>();
            portraitGroupLayout.spacing = 6;
            portraitGroupLayout.childAlignment = TextAnchor.UpperCenter;
            portraitGroupLayout.childControlWidth = false;
            portraitGroupLayout.childControlHeight = false;
            portraitGroupLayout.childForceExpandWidth = false;
            portraitGroupLayout.childForceExpandHeight = false;

            var portraitGroupLE = portraitGroupGO.AddComponent<LayoutElement>();
            portraitGroupLE.preferredWidth = PORTRAIT_SIZE;

            // ── PortraitImage ──
            var portraitImgGO = CreateUIObject("PortraitImage", portraitGroupGO.transform, 5);
            var portraitImgRect = portraitImgGO.GetComponent<RectTransform>();
            portraitImgRect.sizeDelta = new Vector2(PORTRAIT_SIZE, PORTRAIT_SIZE);

            portraitImgGO.AddComponent<CanvasRenderer>();
            var portraitImg = portraitImgGO.AddComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            portraitImg.color = Color.white;

            var portraitImgLE = portraitImgGO.AddComponent<LayoutElement>();
            portraitImgLE.preferredWidth = PORTRAIT_SIZE;
            portraitImgLE.preferredHeight = PORTRAIT_SIZE;

            // ── PortraitNameBG ──
            var pNameBGGO = CreateUIObject("PortraitNameBG", portraitGroupGO.transform, 5);
            var pNameBGRect = pNameBGGO.GetComponent<RectTransform>();
            pNameBGRect.sizeDelta = new Vector2(PORTRAIT_SIZE, NAMEBOX_H);

            pNameBGGO.AddComponent<CanvasRenderer>();
            var pNameBGImg = pNameBGGO.AddComponent<Image>();
            pNameBGImg.sprite = nbChar;
            pNameBGImg.type = nbChar != null ? Image.Type.Sliced : Image.Type.Simple;
            pNameBGImg.raycastTarget = false;

            var pNameBGLE = pNameBGGO.AddComponent<LayoutElement>();
            pNameBGLE.preferredWidth = PORTRAIT_SIZE;
            pNameBGLE.preferredHeight = NAMEBOX_H;

            // ── PortraitNameText ──
            var pNameTextGO = CreateUIObject("PortraitNameText", pNameBGGO.transform, 5);
            var pNameTextRect = pNameTextGO.GetComponent<RectTransform>();
            StretchFill(pNameTextRect);
            pNameTextRect.offsetMin = new Vector2(6, 2);
            pNameTextRect.offsetMax = new Vector2(-6, -2);

            var pNameTmp = pNameTextGO.AddComponent<TextMeshProUGUI>();
            pNameTmp.text = "캐릭터";
            pNameTmp.fontSize = NAME_FONT_SIZE;
            pNameTmp.fontStyle = FontStyles.Bold;
            pNameTmp.alignment = TextAlignmentOptions.Center;
            pNameTmp.color = Color.white;
            pNameTmp.raycastTarget = false;
            pNameTmp.overflowMode = TextOverflowModes.Ellipsis;
            if (font) pNameTmp.font = font;

            // ── ContentColumn ──
            var contentColGO = CreateUIObject("ContentColumn", rootGO.transform, 5);
            var contentColLayout = contentColGO.AddComponent<VerticalLayoutGroup>();
            contentColLayout.spacing = 4;
            contentColLayout.childAlignment = TextAnchor.UpperLeft;
            contentColLayout.childControlWidth = true;
            contentColLayout.childControlHeight = true;
            contentColLayout.childForceExpandWidth = true;
            contentColLayout.childForceExpandHeight = false;

            var contentColLE = contentColGO.AddComponent<LayoutElement>();
            contentColLE.flexibleWidth = 1;

            // ── UserNameGroup ──
            var userNameGroupGO = CreateUIObject("UserNameGroup", contentColGO.transform, 5);

            var userNameGroupLayout = userNameGroupGO.AddComponent<HorizontalLayoutGroup>();
            userNameGroupLayout.childAlignment = TextAnchor.MiddleLeft;
            userNameGroupLayout.childControlWidth = false;
            userNameGroupLayout.childControlHeight = false;
            userNameGroupLayout.childForceExpandWidth = false;
            userNameGroupLayout.childForceExpandHeight = false;

            var userNameGroupLE = userNameGroupGO.AddComponent<LayoutElement>();
            userNameGroupLE.preferredHeight = NAMEBOX_H;

            // ── UserNameBG ──
            var uNameBGGO = CreateUIObject("UserNameBG", userNameGroupGO.transform, 5);
            var uNameBGRect = uNameBGGO.GetComponent<RectTransform>();
            uNameBGRect.sizeDelta = new Vector2(PORTRAIT_SIZE, NAMEBOX_H);

            uNameBGGO.AddComponent<CanvasRenderer>();
            var uNameBGImg = uNameBGGO.AddComponent<Image>();
            uNameBGImg.sprite = nbUser;
            uNameBGImg.type = nbUser != null ? Image.Type.Sliced : Image.Type.Simple;
            uNameBGImg.raycastTarget = false;

            var uNameBGLE = uNameBGGO.AddComponent<LayoutElement>();
            uNameBGLE.preferredWidth = PORTRAIT_SIZE;
            uNameBGLE.preferredHeight = NAMEBOX_H;

            // ── UserNameText ──
            var uNameTextGO = CreateUIObject("UserNameText", uNameBGGO.transform, 5);
            var uNameTextRect = uNameTextGO.GetComponent<RectTransform>();
            StretchFill(uNameTextRect);
            uNameTextRect.offsetMin = new Vector2(6, 2);
            uNameTextRect.offsetMax = new Vector2(-6, -2);

            var uNameTmp = uNameTextGO.AddComponent<TextMeshProUGUI>();
            uNameTmp.text = "주인공";
            uNameTmp.fontSize = NAME_FONT_SIZE;
            uNameTmp.fontStyle = FontStyles.Bold;
            uNameTmp.alignment = TextAlignmentOptions.Center;
            uNameTmp.color = Color.white;
            uNameTmp.raycastTarget = false;
            uNameTmp.overflowMode = TextOverflowModes.Ellipsis;
            if (font) uNameTmp.font = font;

            // ── TextboxBG (말풍선) ──
            var textboxBGGO = CreateUIObject("TextboxBG", contentColGO.transform, 5);
            textboxBGGO.AddComponent<CanvasRenderer>();
            var textboxBGImg = textboxBGGO.AddComponent<Image>();
            textboxBGImg.sprite = tbChar;
            textboxBGImg.type = tbChar != null ? Image.Type.Sliced : Image.Type.Simple;
            textboxBGImg.raycastTarget = false;

            var textboxBGLE = textboxBGGO.AddComponent<LayoutElement>();
            textboxBGLE.minHeight = 60;
            textboxBGLE.flexibleWidth = 1;

            // ── MessageText ──
            var msgTextGO = CreateUIObject("MessageText", textboxBGGO.transform, 5);
            var msgTextRect = msgTextGO.GetComponent<RectTransform>();
            StretchFill(msgTextRect);
            msgTextRect.offsetMin = new Vector2(MSG_PAD, MSG_PAD * 0.6f);
            msgTextRect.offsetMax = new Vector2(-MSG_PAD, -MSG_PAD * 0.6f);

            var msgTmp = msgTextGO.AddComponent<TextMeshProUGUI>();
            msgTmp.text = "대사 텍스트";
            msgTmp.fontSize = MSG_FONT_SIZE;
            msgTmp.alignment = TextAlignmentOptions.TopLeft;
            msgTmp.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            msgTmp.raycastTarget = false;
            msgTmp.textWrappingMode = TextWrappingModes.Normal;
            if (font) msgTmp.font = font;

            // ── LogEntryUI 컴포넌트 바인딩 ──
            var entryComp = rootGO.AddComponent<LogEntryUI>();
            var entrySO = new SerializedObject(entryComp);
            entrySO.FindProperty("messageText").objectReferenceValue = msgTmp;
            entrySO.FindProperty("portraitGroup").objectReferenceValue = portraitGroupGO;
            entrySO.FindProperty("portraitImage").objectReferenceValue = portraitImg;
            entrySO.FindProperty("portraitNameText").objectReferenceValue = pNameTmp;
            entrySO.FindProperty("portraitNameBG").objectReferenceValue = pNameBGImg;
            entrySO.FindProperty("userNameGroup").objectReferenceValue = userNameGroupGO;
            entrySO.FindProperty("userNameText").objectReferenceValue = uNameTmp;
            entrySO.FindProperty("userNameBG").objectReferenceValue = uNameBGImg;
            entrySO.FindProperty("textboxBG").objectReferenceValue = textboxBGImg;
            entrySO.ApplyModifiedProperties();

            // ── 프리팹 저장 ──
            string entryPrefabPath = PREFAB_DIR + "LogEntryUI.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGO, entryPrefabPath);
            Object.DestroyImmediate(rootGO);

            Debug.Log($"[LogPopupBuilder] LogEntryUI 프리팹 저장: {entryPrefabPath}");
            return savedPrefab.GetComponent<LogEntryUI>();
        }

        // ═══════════════════════════════════════════════════
        //  유틸리티
        // ═══════════════════════════════════════════════════

        static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ART_DIR + name + ".png");
            if (sprite == null)
                Debug.LogWarning($"[LogPopupBuilder] 스프라이트 없음: {ART_DIR}{name}.png");
            return sprite;
        }

        static GameObject CreateUIObject(string name, Transform parent, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        static void SetCenterAnchors(RectTransform rt, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }

        static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        [MenuItem("LoveAlgo/Tools/Build LogPopup Prefab", true)]
        static bool Validate() => !Application.isPlaying;
    }
}
