#if UNITY_EDITOR
using System.IO;
using LoveAlgo.Phone;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.PhoneEditor
{
    /// <summary>
    /// ChatBubble Self/Other 프리팹 자동 생성 (PhoneUIDesignConfig 기반).
    /// 메뉴: Tools > LoveAlgo > Phone > Rebuild ChatBubble Prefabs
    /// </summary>
    public static class ChatBubbleBuilder
    {
        const string PREFAB_DIR = "Assets/_Project/Modules/Phone/Prefabs";
        const string CONFIG_PATH = "Assets/_Project/Modules/Phone/Editor/PhoneUIDesignConfig.asset";
        const string GUID_BG_OTHER = "d08e9997a7dade94fb36a480b8641840";
        const string GUID_BG_SELF  = "1197b44bf9f46dc4fb0fe872ce949831";
        const int UI_LAYER = 5;

        [MenuItem("Tools/LoveAlgo/Phone/Rebuild ChatBubble Prefabs")]
        public static void RebuildDefault()
        {
            var cfg = LoadOrCreateConfig();
            Rebuild(cfg);
        }

        public static void Rebuild(PhoneUIDesignConfig cfg)
        {
            var spriteOther = LoadSprite(GUID_BG_OTHER);
            var spriteSelf  = LoadSprite(GUID_BG_SELF);

            BuildPrefab(cfg, "ChatBubbleOther", spriteOther, alignRight: false, textColor: cfg.otherTextColor);
            BuildPrefab(cfg, "ChatBubbleSelf",  spriteSelf,  alignRight: true,  textColor: cfg.selfTextColor);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ChatBubbleBuilder] 완료 — ChatBubbleSelf/Other.prefab 재생성.");
        }

        public static PhoneUIDesignConfig LoadOrCreateConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<PhoneUIDesignConfig>(CONFIG_PATH);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<PhoneUIDesignConfig>();
                Directory.CreateDirectory(Path.GetDirectoryName(CONFIG_PATH));
                AssetDatabase.CreateAsset(cfg, CONFIG_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ChatBubbleBuilder] PhoneUIDesignConfig 신규 생성: {CONFIG_PATH}");
            }
            return cfg;
        }

        static GameObject BuildPrefab(PhoneUIDesignConfig cfg, string name, Sprite bg, bool alignRight, Color textColor)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.layer = UI_LAYER;

            var rootHlg = root.AddComponent<HorizontalLayoutGroup>();
            rootHlg.padding = new RectOffset(0, 0, 0, 0);
            rootHlg.childAlignment = alignRight ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            rootHlg.childForceExpandWidth = false;
            rootHlg.childForceExpandHeight = false;
            rootHlg.childControlWidth = true;
            rootHlg.childControlHeight = true;

            var rootCsf = root.AddComponent<ContentSizeFitter>();
            rootCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rootCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // BG
            var bgGo = new GameObject("BG", typeof(RectTransform));
            bgGo.layer = UI_LAYER;
            bgGo.transform.SetParent(root.transform, false);

            var img = bgGo.AddComponent<Image>();
            img.sprite = bg;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;

            var bgVlg = bgGo.AddComponent<VerticalLayoutGroup>();
            bgVlg.padding = new RectOffset(cfg.bubblePadLeft, cfg.bubblePadRight, cfg.bubblePadTop, cfg.bubblePadBottom);
            bgVlg.childAlignment = TextAnchor.MiddleLeft;
            bgVlg.childForceExpandWidth = true;
            bgVlg.childForceExpandHeight = false;
            bgVlg.childControlWidth = true;
            bgVlg.childControlHeight = true;

            var bgCsf = bgGo.AddComponent<ContentSizeFitter>();
            bgCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bgCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // MessageText
            var textGo = new GameObject("MessageText", typeof(RectTransform));
            textGo.layer = UI_LAYER;
            textGo.transform.SetParent(bgGo.transform, false);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "샘플 메시지";
            tmp.fontSize = cfg.textFontSize;
            tmp.characterSpacing = cfg.characterSpacing;
            tmp.lineSpacing = cfg.lineSpacing;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            var textLe = textGo.AddComponent<LayoutElement>();
            textLe.preferredWidth = cfg.maxBubbleWidth - cfg.bubblePadLeft - cfg.bubblePadRight;
            textLe.flexibleWidth = 0;

            // ChatBubble 컴포넌트
            var bubble = root.AddComponent<ChatBubble>();
            using (var so = new SerializedObject(bubble))
            {
                so.FindProperty("messageText").objectReferenceValue = tmp;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EnsureDir(PREFAB_DIR);
            var path = $"{PREFAB_DIR}/{name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        static Sprite LoadSprite(string guid)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(p);
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
