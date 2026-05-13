#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// 로그 UI 엔트리/버블 프리팹을 LogUIDesignConfig 기반으로 재생성.
    /// 메뉴: Tools > LoveAlgo > Rebuild Log Entry & Bubble Prefabs
    /// </summary>
    public static class LogUIRebuilder
    {
        const string PREFAB_DIR = "Assets/_Project/Modules/Narrative/Prefabs/Log";
        const string BUBBLE_DIR = PREFAB_DIR + "/Bubbles";
        const string CONFIG_PATH = "Assets/_Project/Modules/Narrative/Editor/LogUIDesignConfig.asset";
        const string FONT_DIR = "Assets/Fonts";

        const string GUID_BG_TEXTBOX_CHAR = "400ca891e3e7b77458772788a3d14ecd";
        const string GUID_BG_TEXTBOX_USER = "2fe32a63c937d594587d65c775eb5704";
        const string GUID_BG_NAMEBOX_CHAR = "3e6129db69bf8ef408ccdfd98e5e4ec4";
        const string GUID_BG_NAMEBOX_USER = "9c1cf79fd98fed04784d4b2f9cc99f97";
        const string FONT_NAME_BODY = "Pretendard-Medium SDF";
        const string FONT_NAME_HEADER = "Pretendard-SemiBold SDF";
        const string FONT_NAME_NARR = "Aggro-Light SDF";

        const int UI_LAYER = 5;

        [MenuItem("Tools/LoveAlgo/Rebuild Log Entry & Bubble Prefabs")]
        public static void Rebuild()
        {
            var cfg = LoadOrCreateConfig();
            ResolveAutoRefs(cfg);
            Rebuild(cfg);
        }

        public static void Rebuild(LogUIDesignConfig cfg)
        {
            if (cfg == null) { Debug.LogError("[LogUIRebuilder] cfg가 null"); return; }
            EnsureDir(PREFAB_DIR);
            EnsureDir(BUBBLE_DIR);

            var bubbleChar = BuildBubblePrefab("LogBubbleCharacter", cfg.bgTextboxCharacter, cfg.bodyFont, cfg.charTextColor, false, cfg);
            var bubbleUser = BuildBubblePrefab("LogBubbleUser",      cfg.bgTextboxUser,      cfg.bodyFont, cfg.userTextColor, false, cfg);
            var bubbleNarr = BuildBubblePrefab("LogBubbleNarration", null,                   cfg.narrationFont ?? cfg.bodyFont, cfg.narrTextColor, true, cfg);

            BuildDialogueEntryPrefab(cfg, bubbleChar, bubbleUser);
            BuildNarrationEntryPrefab(cfg, bubbleNarr);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var dialogueEntry = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/LogDialogueEntry.prefab");
            var narrationEntry = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/LogNarrationEntry.prefab");

            WireLogPopup(dialogueEntry, narrationEntry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LogUIRebuilder] 완료 — 버블 3종 + 엔트리 2종 재생성, LogPopup 와이어링 완료.");
        }

        public static LogUIDesignConfig LoadOrCreateConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<LogUIDesignConfig>(CONFIG_PATH);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<LogUIDesignConfig>();
                Directory.CreateDirectory(Path.GetDirectoryName(CONFIG_PATH));
                AssetDatabase.CreateAsset(cfg, CONFIG_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[LogUIRebuilder] LogUIDesignConfig 신규 생성: {CONFIG_PATH}");
            }
            return cfg;
        }

        /// <summary>None인 sprite/font 슬롯을 알려진 GUID/경로로 자동 채움.</summary>
        public static void ResolveAutoRefs(LogUIDesignConfig cfg)
        {
            if (cfg.bgTextboxCharacter == null) cfg.bgTextboxCharacter = LoadSprite(GUID_BG_TEXTBOX_CHAR);
            if (cfg.bgTextboxUser      == null) cfg.bgTextboxUser      = LoadSprite(GUID_BG_TEXTBOX_USER);
            if (cfg.bgNameBoxCharacter == null) cfg.bgNameBoxCharacter = LoadSprite(GUID_BG_NAMEBOX_CHAR);
            if (cfg.bgNameBoxUser      == null) cfg.bgNameBoxUser      = LoadSprite(GUID_BG_NAMEBOX_USER);
            if (cfg.bodyFont      == null) cfg.bodyFont      = LoadFont(FONT_NAME_BODY);
            if (cfg.nameFont      == null) cfg.nameFont      = LoadFont(FONT_NAME_HEADER);
            if (cfg.narrationFont == null) cfg.narrationFont = LoadFont(FONT_NAME_NARR);
            EditorUtility.SetDirty(cfg);
        }

        // ── 버블 ─────────────────────────────────────────
        static GameObject BuildBubblePrefab(string name, Sprite bg, TMP_FontAsset font, Color textColor, bool withShadow, LogUIDesignConfig cfg)
        {
            var go = NewUI(name, out var rt);
            rt.sizeDelta = new Vector2(1200, cfg.bubbleMinHeight);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(cfg.bubblePadLeft, cfg.bubblePadRight, cfg.bubblePadTop, cfg.bubblePadBottom);
            vlg.spacing = 0;
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var csf = go.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = cfg.bubbleMinHeight;
            le.flexibleWidth = 1;

            if (bg != null)
            {
                var img = go.AddComponent<Image>();
                img.sprite = bg;
                img.type = Image.Type.Sliced;
                img.raycastTarget = false;
            }

            var textGo = NewUIChild(go, "MessageText", out _);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = withShadow ? cfg.narrationFontSize : cfg.bodyFontSize;
            tmp.color = textColor;
            tmp.text = withShadow ? "(narration sample)" : "(sample line)";
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.lineSpacing = withShadow ? cfg.narrationLineSpacing : cfg.bodyLineSpacing;
            tmp.characterSpacing = cfg.bodyCharacterSpacing;
            tmp.raycastTarget = false;

            if (withShadow)
            {
                var shadow = textGo.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.6f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }

            go.SetActive(false);
            return SaveAsPrefab(go, $"{BUBBLE_DIR}/{name}.prefab");
        }

        // ── 다이얼로그 엔트리 ────────────────────────────
        static GameObject BuildDialogueEntryPrefab(LogUIDesignConfig cfg, GameObject bubbleCharPrefab, GameObject bubbleUserPrefab)
        {
            var root = NewUI("LogDialogueEntry", out var rt);
            rt.sizeDelta = new Vector2(0, 0);

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(cfg.entryPadLeft, cfg.entryPadRight, cfg.entryPadTop, cfg.entryPadBottom);
            hlg.spacing = cfg.entrySpacing;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var rootCsf = root.AddComponent<ContentSizeFitter>();
            rootCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rootCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = NewUIChild(root, "Header", out var headerRt);
            headerRt.sizeDelta = new Vector2(cfg.headerWidth, 0);

            var headerVlg = header.AddComponent<VerticalLayoutGroup>();
            headerVlg.padding = new RectOffset(0, 0, 0, 0);
            headerVlg.spacing = cfg.headerPortraitToNameGap;
            headerVlg.childAlignment = TextAnchor.MiddleCenter;
            headerVlg.childForceExpandWidth = false;
            headerVlg.childForceExpandHeight = false;
            headerVlg.childControlWidth = true;
            headerVlg.childControlHeight = true;

            var headerLe = header.AddComponent<LayoutElement>();
            headerLe.preferredWidth = cfg.headerWidth;
            headerLe.minWidth = cfg.headerWidth;
            headerLe.flexibleWidth = 0;

            var portraitGo = NewUIChild(header, "Portrait", out var portraitRt);
            portraitRt.sizeDelta = cfg.portraitSize;
            var portraitImg = portraitGo.AddComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            var portraitLe = portraitGo.AddComponent<LayoutElement>();
            portraitLe.preferredWidth = cfg.portraitSize.x;
            portraitLe.preferredHeight = cfg.portraitSize.y;

            // NamePlate — 기본은 캐릭터 사이즈, Init()에서 isUser에 따라 swap
            var namePlate = NewUIChild(header, "NamePlate", out var nameRt);
            nameRt.sizeDelta = cfg.characterNameBoxSize;
            var nameBg = namePlate.AddComponent<Image>();
            nameBg.sprite = cfg.bgNameBoxCharacter;
            nameBg.type = Image.Type.Sliced;
            nameBg.raycastTarget = false;
            var namePlateLe = namePlate.AddComponent<LayoutElement>();
            namePlateLe.preferredWidth = cfg.characterNameBoxSize.x;
            namePlateLe.preferredHeight = cfg.characterNameBoxSize.y;

            var nameTextGo = NewUIChild(namePlate, "NameText", out var nameTextRt);
            nameTextRt.anchorMin = Vector2.zero;
            nameTextRt.anchorMax = Vector2.one;
            nameTextRt.offsetMin = Vector2.zero;
            nameTextRt.offsetMax = Vector2.zero;
            var nameText = nameTextGo.AddComponent<TextMeshProUGUI>();
            nameText.font = cfg.nameFont ?? cfg.bodyFont;
            nameText.fontSize = cfg.nameFontSize;
            nameText.color = cfg.characterNameColor;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.text = "이름";
            nameText.raycastTarget = false;

            var dialogueColumn = NewUIChild(root, "DialogueColumn", out var dcRt);
            var dcVlg = dialogueColumn.AddComponent<VerticalLayoutGroup>();
            dcVlg.padding = new RectOffset(cfg.dialogueColumnPadLeft, cfg.dialogueColumnPadRight, cfg.dialogueColumnPadTop, cfg.dialogueColumnPadBottom);
            dcVlg.spacing = cfg.dialogueColumnSpacing;
            dcVlg.childAlignment = TextAnchor.UpperLeft;
            dcVlg.childForceExpandWidth = true;
            dcVlg.childForceExpandHeight = false;
            dcVlg.childControlWidth = true;
            dcVlg.childControlHeight = true;
            var dcLe = dialogueColumn.AddComponent<LayoutElement>();
            dcLe.flexibleWidth = 1;

            var ghostBubble = (GameObject)PrefabUtility.InstantiatePrefab(bubbleCharPrefab, dialogueColumn.transform);
            ghostBubble.name = bubbleCharPrefab.name;

            var script = root.AddComponent<LogDialogueEntry>();
            using (var so = new SerializedObject(script))
            {
                so.FindProperty("dialogueColumn").objectReferenceValue = dcRt;
                so.FindProperty("bubbleTemplate").objectReferenceValue = bubbleCharPrefab;
                so.FindProperty("portraitContainer").objectReferenceValue = portraitGo;
                so.FindProperty("portraitImage").objectReferenceValue = portraitImg;
                so.FindProperty("nameBoxRect").objectReferenceValue = nameRt;
                so.FindProperty("nameBoxLayout").objectReferenceValue = namePlateLe;
                so.FindProperty("nameBoxImage").objectReferenceValue = nameBg;
                so.FindProperty("nameText").objectReferenceValue = nameText;
                so.FindProperty("characterNameBoxSprite").objectReferenceValue = cfg.bgNameBoxCharacter;
                so.FindProperty("userNameBoxSprite").objectReferenceValue = cfg.bgNameBoxUser;
                so.FindProperty("characterNameBoxSize").vector2Value = cfg.characterNameBoxSize;
                so.FindProperty("userNameBoxSize").vector2Value = cfg.userNameBoxSize;
                so.FindProperty("characterNameColor").colorValue = cfg.characterNameColor;
                so.FindProperty("userNameColor").colorValue = cfg.userNameColor;
                so.FindProperty("userBubbleTemplate").objectReferenceValue = bubbleUserPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return SaveAsPrefab(root, $"{PREFAB_DIR}/LogDialogueEntry.prefab");
        }

        // ── 독백 엔트리 ──────────────────────────────────
        static GameObject BuildNarrationEntryPrefab(LogUIDesignConfig cfg, GameObject bubbleNarrPrefab)
        {
            var root = NewUI("LogNarrationEntry", out _);

            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(
                cfg.entryPadLeft + (int)cfg.headerWidth + (int)cfg.entrySpacing,
                cfg.entryPadRight, cfg.entryPadTop, cfg.entryPadBottom);
            vlg.spacing = cfg.dialogueColumnSpacing;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var rootCsf = root.AddComponent<ContentSizeFitter>();
            rootCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rootCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var dialogueColumn = NewUIChild(root, "DialogueColumn", out var dcRt);
            var dcVlg = dialogueColumn.AddComponent<VerticalLayoutGroup>();
            dcVlg.padding = new RectOffset(cfg.dialogueColumnPadLeft, cfg.dialogueColumnPadRight, cfg.dialogueColumnPadTop, cfg.dialogueColumnPadBottom);
            dcVlg.spacing = cfg.dialogueColumnSpacing;
            dcVlg.childAlignment = TextAnchor.UpperLeft;
            dcVlg.childForceExpandWidth = true;
            dcVlg.childForceExpandHeight = false;
            dcVlg.childControlWidth = true;
            dcVlg.childControlHeight = true;

            var ghostBubble = (GameObject)PrefabUtility.InstantiatePrefab(bubbleNarrPrefab, dialogueColumn.transform);
            ghostBubble.name = bubbleNarrPrefab.name;

            var script = root.AddComponent<LogNarrationEntry>();
            using (var so = new SerializedObject(script))
            {
                so.FindProperty("dialogueColumn").objectReferenceValue = dcRt;
                so.FindProperty("bubbleTemplate").objectReferenceValue = bubbleNarrPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return SaveAsPrefab(root, $"{PREFAB_DIR}/LogNarrationEntry.prefab");
        }

        // ── LogPopup 와이어링 + dead children 정리 ──────
        static void WireLogPopup(GameObject dialogueEntry, GameObject narrationEntry)
        {
            const string popupPath = PREFAB_DIR + "/LogPopup.prefab";
            var root = PrefabUtility.LoadPrefabContents(popupPath);
            try
            {
                CleanupDeadChildren(root);
                var popup = root.GetComponentInChildren<LogPopup>(true);
                if (popup == null) { Debug.LogError("[LogUIRebuilder] LogPopup 컴포넌트 없음"); return; }

                using (var so = new SerializedObject(popup))
                {
                    so.FindProperty("dialogueEntryPrefab").objectReferenceValue = dialogueEntry?.GetComponent<LogDialogueEntry>();
                    so.FindProperty("narrationEntryPrefab").objectReferenceValue = narrationEntry?.GetComponent<LogNarrationEntry>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, popupPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void CleanupDeadChildren(GameObject root)
        {
            var deadNames = new System.Collections.Generic.HashSet<string>
            {
                "LogEntry_Character", "LogEntry_Extra", "LogEntry_User", "LogEntry_Narration",
                "LogHeader_Character", "LogHeader_Extra", "LogHeader_User", "Blank",
            };
            var toDelete = new System.Collections.Generic.List<GameObject>();
            void Visit(Transform t)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    var c = t.GetChild(i);
                    if (deadNames.Contains(c.gameObject.name)) toDelete.Add(c.gameObject);
                    else Visit(c);
                }
            }
            Visit(root.transform);
            foreach (var go in toDelete) Object.DestroyImmediate(go, false);
        }

        // ── 유틸 ─────────────────────────────────────────
        static GameObject NewUI(string name, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UI_LAYER;
            rt = (RectTransform)go.transform;
            return go;
        }

        static GameObject NewUIChild(GameObject parent, string name, out RectTransform rt)
        {
            var go = NewUI(name, out rt);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static Sprite LoadSprite(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static TMP_FontAsset LoadFont(string fileName)
            => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FONT_DIR}/{fileName}.asset");

        static GameObject SaveAsPrefab(GameObject go, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        static void EnsureDir(string assetPath)
        {
            var sys = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(sys)) Directory.CreateDirectory(sys);
            AssetDatabase.Refresh();
        }
    }
}
#endif
