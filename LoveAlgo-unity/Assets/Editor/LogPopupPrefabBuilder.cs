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
        const string FONT_PATH     = "Assets/Fonts/Pretendard-Medium SDF.asset";
        const string NARRATION_FONT_PATH = "Assets/Fonts/Aggro-Light SDF.asset";

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
            var narrationFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NARRATION_FONT_PATH);
            var bgWindow     = LoadSprite("bg_log_window");
            var tbChar       = LoadSprite("bg_log_textbox_character");
            var tbUser       = LoadSprite("bg_log_textbox_user");
            var nbChar       = LoadSprite("bg_log_namebox_character");
            var nbUser       = LoadSprite("bg_log_namebox_user");
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
            if (narrationFont == null)
                Debug.LogWarning($"[LogPopupBuilder] 독백 폰트 없음: {NARRATION_FONT_PATH}");

            // ═══════════════════════════════════════════════
            //  1) LogEntryUI 프리팹 제작
            // ═══════════════════════════════════════════════
            var entryPrefab = BuildLogEntryPrefab(font, narrationFont, tbChar, tbUser, nbChar, nbUser);

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
            so.FindProperty("narrationFont").objectReferenceValue = narrationFont;
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
                $"• 헤더 프리팹: LogHeader_Character / Extra / User\n" +
                $"• Canvas_LogPopup_Preview가 씬에 배치됨\n" +
                $"• 확인 후 프리뷰 Canvas는 삭제해도 됩니다",
                "확인");

            Debug.Log("[LogPopupBuilder] 프리팹 빌드 완료!");
        }

        // ═══════════════════════════════════════════════════
        //  LogEntryUI + 헤더 프리팹 3종
        // ═══════════════════════════════════════════════════

        const float HEADER_CHAR_H = 154f;  // portrait(110) + gap(6) + name(38)
        const float PORTRAIT_GAP = 6f;

        /// <summary>헤더 3종 + LogEntryUI 프리팹 빌드</summary>
        static LogEntryUI BuildLogEntryPrefab(
            TMP_FontAsset font, TMP_FontAsset narrationFont,
            Sprite tbChar, Sprite tbUser,
            Sprite nbChar, Sprite nbUser)
        {
            // 1) 헤더 프리팹 3종
            var headerCharPath  = BuildHeaderCharacterPrefab(font, nbChar);
            var headerExtraPath = BuildHeaderExtraPrefab(font, nbChar);
            var headerUserPath  = BuildHeaderUserPrefab(font, nbUser);

            // 2) LogEntryUI 본체
            return BuildLogEntryPrefabCore(
                font, tbChar, tbUser,
                headerCharPath, headerExtraPath, headerUserPath);
        }

        // ─────────────────────────────────────────
        //  케이스1: 프로필 + 네임박스 (110×154, 고정 배치)
        // ─────────────────────────────────────────
        static string BuildHeaderCharacterPrefab(TMP_FontAsset font, Sprite nbChar)
        {
            var rootGO = new GameObject("LogHeader_Character");
            rootGO.layer = LayerMask.NameToLayer("UI");
            var rootRect = rootGO.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(PORTRAIT_SIZE, HEADER_CHAR_H);

            var rootLE = rootGO.AddComponent<LayoutElement>();
            rootLE.preferredWidth = PORTRAIT_SIZE;
            rootLE.preferredHeight = HEADER_CHAR_H;
            rootLE.flexibleWidth = 0;

            // PortraitImage — 상단 고정 110×110
            var portraitGO = CreateUIObject("PortraitImage", rootGO.transform, 5);
            var portraitRect = portraitGO.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 1);
            portraitRect.anchorMax = new Vector2(0.5f, 1);
            portraitRect.pivot = new Vector2(0.5f, 1);
            portraitRect.anchoredPosition = Vector2.zero;
            portraitRect.sizeDelta = new Vector2(PORTRAIT_SIZE, PORTRAIT_SIZE);

            portraitGO.AddComponent<CanvasRenderer>();
            var portraitImg = portraitGO.AddComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;

            // NameBG — 초상화 아래 (y = -(110+6))
            float nameY = -(PORTRAIT_SIZE + PORTRAIT_GAP);
            var nameTmp = BuildNameBoxAnchored(
                "NameBG", "NameText", rootGO.transform,
                nbChar, font, "캐릭터",
                new Vector2(0.5f, 1), new Vector2(0, nameY));

            // LogHeaderUI 컴포넌트
            var headerComp = rootGO.AddComponent<LogHeaderUI>();
            var headerSO = new SerializedObject(headerComp);
            headerSO.FindProperty("portraitImage").objectReferenceValue = portraitImg;
            headerSO.FindProperty("nameText").objectReferenceValue = nameTmp;
            headerSO.ApplyModifiedProperties();

            string path = PREFAB_DIR + "LogHeader_Character.prefab";
            PrefabUtility.SaveAsPrefabAsset(rootGO, path);
            Object.DestroyImmediate(rootGO);
            Debug.Log($"[LogPopupBuilder] 헤더 프리팹 저장: {path}");
            return path;
        }

        // ─────────────────────────────────────────
        //  케이스2: 엑스트라 네임박스만 (110×38, 고정 배치)
        // ─────────────────────────────────────────
        static string BuildHeaderExtraPrefab(TMP_FontAsset font, Sprite nbSprite)
        {
            var rootGO = new GameObject("LogHeader_Extra");
            rootGO.layer = LayerMask.NameToLayer("UI");
            var rootRect = rootGO.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(PORTRAIT_SIZE, NAMEBOX_H);

            var rootLE = rootGO.AddComponent<LayoutElement>();
            rootLE.preferredWidth = PORTRAIT_SIZE;
            rootLE.preferredHeight = NAMEBOX_H;
            rootLE.flexibleWidth = 0;

            var nameTmp = BuildNameBoxAnchored(
                "NameBG", "NameText", rootGO.transform,
                nbSprite, font, "엑스트라",
                new Vector2(0.5f, 0.5f), Vector2.zero);

            var headerComp = rootGO.AddComponent<LogHeaderUI>();
            var headerSO = new SerializedObject(headerComp);
            headerSO.FindProperty("nameText").objectReferenceValue = nameTmp;
            headerSO.ApplyModifiedProperties();

            string path = PREFAB_DIR + "LogHeader_Extra.prefab";
            PrefabUtility.SaveAsPrefabAsset(rootGO, path);
            Object.DestroyImmediate(rootGO);
            Debug.Log($"[LogPopupBuilder] 헤더 프리팹 저장: {path}");
            return path;
        }

        // ─────────────────────────────────────────
        //  케이스3: 주인공 네임박스 (110×38, 고정 배치)
        // ─────────────────────────────────────────
        static string BuildHeaderUserPrefab(TMP_FontAsset font, Sprite nbSprite)
        {
            var rootGO = new GameObject("LogHeader_User");
            rootGO.layer = LayerMask.NameToLayer("UI");
            var rootRect = rootGO.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(PORTRAIT_SIZE, NAMEBOX_H);

            var rootLE = rootGO.AddComponent<LayoutElement>();
            rootLE.preferredWidth = PORTRAIT_SIZE;
            rootLE.preferredHeight = NAMEBOX_H;
            rootLE.flexibleWidth = 0;

            var nameTmp = BuildNameBoxAnchored(
                "NameBG", "NameText", rootGO.transform,
                nbSprite, font, "주인공",
                new Vector2(0.5f, 0.5f), Vector2.zero);

            var headerComp = rootGO.AddComponent<LogHeaderUI>();
            var headerSO = new SerializedObject(headerComp);
            headerSO.FindProperty("nameText").objectReferenceValue = nameTmp;
            headerSO.ApplyModifiedProperties();

            string path = PREFAB_DIR + "LogHeader_User.prefab";
            PrefabUtility.SaveAsPrefabAsset(rootGO, path);
            Object.DestroyImmediate(rootGO);
            Debug.Log($"[LogPopupBuilder] 헤더 프리팹 저장: {path}");
            return path;
        }

        // ─────────────────────────────────────────
        //  LogEntryUI 본체 프리팹
        // ─────────────────────────────────────────
        static LogEntryUI BuildLogEntryPrefabCore(
            TMP_FontAsset font, Sprite tbChar, Sprite tbUser,
            string headerCharPath, string headerExtraPath, string headerUserPath)
        {
            /*
             * LogEntry (HLG, LogEntryUI)
             * ├── [Header prefab instance]         ← 런타임 케이스별 Instantiate
             * ├── DialogueColumn (VLG, spacing=8)  ← 버블 부모
             * └── BubbleTemplate (비활성, 클론 원본)
             */

            const float BUBBLE_SPACING = 8f;
            const float BUBBLE_MIN_H = 48f;
            const float BUBBLE_PAD_H = 14f;
            const float BUBBLE_PAD_W = 20f;

            // ── Root ──
            var rootGO = new GameObject("LogEntryUI");
            rootGO.layer = LayerMask.NameToLayer("UI");
            var rootRect = rootGO.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(0, 0);

            var rootLayout = rootGO.AddComponent<HorizontalLayoutGroup>();
            rootLayout.padding = new RectOffset(8, 8, 8, 8);
            rootLayout.spacing = 16;
            rootLayout.childAlignment = TextAnchor.UpperLeft;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = false;
            rootLayout.childForceExpandHeight = false;

            // ── DialogueColumn (버블 부모) ──
            var dialogueColGO = CreateUIObject("DialogueColumn", rootGO.transform, 5);

            var dialogueColLayout = dialogueColGO.AddComponent<VerticalLayoutGroup>();
            dialogueColLayout.spacing = BUBBLE_SPACING;
            dialogueColLayout.childAlignment = TextAnchor.MiddleLeft;
            dialogueColLayout.childControlWidth = true;
            dialogueColLayout.childControlHeight = true;
            dialogueColLayout.childForceExpandWidth = true;
            dialogueColLayout.childForceExpandHeight = false;

            var dialogueColLE = dialogueColGO.AddComponent<LayoutElement>();
            dialogueColLE.flexibleWidth = 1;
            dialogueColLE.flexibleHeight = 1;  // 헤더 높이에 맞춰 확장 → 버블 세로 중앙

            // ── BubbleTemplate (비활성, 클론 원본) ──
            var bubbleGO = CreateUIObject("BubbleTemplate", rootGO.transform, 5);
            bubbleGO.SetActive(false);

            var bubbleLayout = bubbleGO.AddComponent<VerticalLayoutGroup>();
            bubbleLayout.padding = new RectOffset(
                (int)BUBBLE_PAD_W, (int)BUBBLE_PAD_W,
                (int)BUBBLE_PAD_H, (int)BUBBLE_PAD_H);
            bubbleLayout.childAlignment = TextAnchor.UpperLeft;
            bubbleLayout.childControlWidth = true;
            bubbleLayout.childControlHeight = true;
            bubbleLayout.childForceExpandWidth = true;
            bubbleLayout.childForceExpandHeight = false;

            bubbleGO.AddComponent<CanvasRenderer>();
            var bubbleBGImg = bubbleGO.AddComponent<Image>();
            bubbleBGImg.sprite = tbChar;
            bubbleBGImg.type = tbChar != null ? Image.Type.Sliced : Image.Type.Simple;
            bubbleBGImg.raycastTarget = false;

            var bubbleLE = bubbleGO.AddComponent<LayoutElement>();
            bubbleLE.minHeight = BUBBLE_MIN_H;

            // MessageText (버블 내부)
            var msgTextGO = CreateUIObject("MessageText", bubbleGO.transform, 5);
            var msgTmp = msgTextGO.AddComponent<TextMeshProUGUI>();
            msgTmp.text = "대사 텍스트";
            msgTmp.fontSize = MSG_FONT_SIZE;
            msgTmp.alignment = TextAlignmentOptions.TopLeft;
            msgTmp.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            msgTmp.raycastTarget = false;
            msgTmp.textWrappingMode = TextWrappingModes.Normal;
            if (font) msgTmp.font = font;

            // Shadow (독백 전용, 기본 OFF)
            var msgShadow = msgTextGO.AddComponent<Shadow>();
            msgShadow.effectColor = new Color(1f, 0.4f, 0.616f, 0.7f);  // #FF669D
            msgShadow.effectDistance = new Vector2(1.5f, -1.5f);
            msgShadow.enabled = false;

            // ── LogEntryUI 컴포넌트 바인딩 ──
            var entryComp = rootGO.AddComponent<LogEntryUI>();
            var entrySO = new SerializedObject(entryComp);

            entrySO.FindProperty("headerCharacterPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(headerCharPath);
            entrySO.FindProperty("headerExtraPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(headerExtraPath);
            entrySO.FindProperty("headerUserPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(headerUserPath);
            entrySO.FindProperty("dialogueColumn").objectReferenceValue =
                dialogueColGO.GetComponent<RectTransform>();
            entrySO.FindProperty("bubbleTemplate").objectReferenceValue = bubbleGO;
            entrySO.ApplyModifiedProperties();

            // ── 프리팹 저장 ──
            string entryPrefabPath = PREFAB_DIR + "LogEntryUI.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGO, entryPrefabPath);
            Object.DestroyImmediate(rootGO);

            Debug.Log($"[LogPopupBuilder] LogEntryUI 프리팹 저장: {entryPrefabPath}");
            return savedPrefab.GetComponent<LogEntryUI>();
        }

        /// <summary>앵커 기반 네임박스 (VLG 불필요, 고정 배치)</summary>
        static TMP_Text BuildNameBoxAnchored(
            string bgName, string textName,
            Transform parent, Sprite nbSprite, TMP_FontAsset font,
            string defaultText,
            Vector2 anchor, Vector2 anchoredPos)
        {
            var bgGO = CreateUIObject(bgName, parent, 5);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = anchor;
            bgRect.anchorMax = anchor;
            bgRect.pivot = new Vector2(0.5f, 1);
            bgRect.anchoredPosition = anchoredPos;
            bgRect.sizeDelta = new Vector2(PORTRAIT_SIZE, NAMEBOX_H);

            bgGO.AddComponent<CanvasRenderer>();
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = nbSprite;
            bgImg.type = nbSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            bgImg.raycastTarget = false;

            var textGO = CreateUIObject(textName, bgGO.transform, 5);
            var textRect = textGO.GetComponent<RectTransform>();
            StretchFill(textRect);
            textRect.offsetMin = new Vector2(6, 2);
            textRect.offsetMax = new Vector2(-6, -2);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = NAME_FONT_SIZE;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            if (font) tmp.font = font;

            return tmp;
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

        // ═══════════════════════════════════════════════════
        //  씬에 LogPopup 인스턴스 배치 + PopupManager 바인딩
        // ═══════════════════════════════════════════════════
        [MenuItem("LoveAlgo/Tools/Place LogPopup In Scene")]
        static void PlaceInScene()
        {
            // 1) 프리팹 로드
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_DIR + "LogPopup.prefab");
            if (prefab == null)
            {
                Debug.LogError("[LogPopupBuilder] LogPopup.prefab 없음. 먼저 Build LogPopup Prefab 실행하세요.");
                return;
            }

            // 2) PopupManager 찾기
            var popupMgr = Object.FindFirstObjectByType<PopupManager>();
            if (popupMgr == null)
            {
                Debug.LogError("[LogPopupBuilder] 씬에 PopupManager가 없습니다.");
                return;
            }

            // 3) Top 레이어 찾기 (layerTop 필드)
            var layerTopField = typeof(PopupManager)
                .GetField("layerTop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var layerTop = layerTopField?.GetValue(popupMgr) as Transform;
            if (layerTop == null)
            {
                Debug.LogError("[LogPopupBuilder] PopupManager.layerTop 바인딩이 없습니다.");
                return;
            }

            // 4) 기존 LogPopup 인스턴스 제거
            var existing = layerTop.Find("LogPopup");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                Debug.Log("[LogPopupBuilder] 기존 LogPopup 인스턴스 제거됨");
            }

            // 5) 프리팹 인스턴스 생성
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, layerTop);
            instance.name = "LogPopup";
            Undo.RegisterCreatedObjectUndo(instance, "Place LogPopup In Scene");

            // RectTransform을 센터 앵커로 설정 (프리팹 원본대로)
            var rt = instance.GetComponent<RectTransform>();
            SetCenterAnchors(rt, POPUP_W, POPUP_H);

            // 비활성 상태로 시작
            instance.SetActive(false);

            // 6) PopupManager.logPopup 바인딩
            var logPopupField = typeof(PopupManager)
                .GetField("logPopup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (logPopupField != null)
            {
                Undo.RecordObject(popupMgr, "Bind LogPopup to PopupManager");
                logPopupField.SetValue(popupMgr, instance.GetComponent<LogPopup>());
                EditorUtility.SetDirty(popupMgr);
            }

            // 7) 씬 저장 (dirty)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[LogPopupBuilder] ✅ LogPopup 씬 배치 + PopupManager 바인딩 완료");
        }

        [MenuItem("LoveAlgo/Tools/Place LogPopup In Scene", true)]
        static bool ValidatePlace() => !Application.isPlaying;
    }
}
