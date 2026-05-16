#if UNITY_EDITOR
using System.IO;
using LoveAlgo.Phone;
using LoveAlgo.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.PhoneEditor
{
    /// <summary>
    /// PhonePopup 전체 prefab 자동 재생성 (PhoneUIDesignConfig 기반).
    /// 골격(레이아웃 + 컴포넌트 와이어링)만 생성 — 아트 sprite는 사용자가 수동 매핑.
    /// 메뉴: Tools > LoveAlgo > Phone > Rebuild PhonePopup
    /// </summary>
    public static class PhonePopupBuilder
    {
        const string PREFAB_PATH = "Assets/_Project/Modules/Phone/Prefabs/PhonePopup.prefab";
        const string BACKUP_PATH = "Assets/_Project/Modules/Phone/Prefabs/PhonePopup.bak.prefab";
        const int UI_LAYER = 5;

        [MenuItem("Tools/LoveAlgo/Phone/Rebuild PhonePopup")]
        public static void Rebuild()
        {
            var cfg = ChatBubbleBuilder.LoadOrCreateConfig();

            // 백업
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) != null)
            {
                AssetDatabase.DeleteAsset(BACKUP_PATH);
                AssetDatabase.CopyAsset(PREFAB_PATH, BACKUP_PATH);
                Debug.Log($"[PhonePopupBuilder] 백업: {BACKUP_PATH}");
            }

            var root = Build(cfg);
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PhonePopupBuilder] 완료: {PREFAB_PATH}");
        }

        // ═══════════════════════════════════════════════
        static GameObject Build(PhoneUIDesignConfig cfg)
        {
            // ─── Root (PhonePopup, 전체 화면) ───────────────
            // 컨벤션: 각 popup prefab이 자체 Dim 자식을 가짐 → 사용자가 빌더 후 수동 추가
            var root = NewUI("PhonePopup", out var rootRt);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // MainPanel (중앙, fixed size)
            var main = NewUIChild(root, "MainPanel", out var mainRt);
            mainRt.anchorMin = mainRt.anchorMax = new Vector2(0.5f, 0.5f);
            mainRt.sizeDelta = cfg.mainPanelSize;
            mainRt.anchoredPosition = Vector2.zero;
            var mainBg = main.AddComponent<Image>();
            mainBg.color = cfg.listBgColor;

            var mainHlg = main.AddComponent<HorizontalLayoutGroup>();
            mainHlg.padding = new RectOffset(0, 0, 0, 0);
            mainHlg.spacing = 0;
            mainHlg.childAlignment = TextAnchor.UpperLeft;
            mainHlg.childForceExpandWidth = false;
            mainHlg.childForceExpandHeight = true;
            mainHlg.childControlWidth = true;
            mainHlg.childControlHeight = true;

            // ─── Sidebar ───────────────────────────────────
            var sidebar = NewUIChild(main, "Sidebar", out _);
            var sidebarBg = sidebar.AddComponent<Image>();
            sidebarBg.color = cfg.sidebarColor;
            var sidebarLe = sidebar.AddComponent<LayoutElement>();
            sidebarLe.preferredWidth = cfg.sidebarWidth;
            sidebarLe.flexibleWidth = 0;
            var sidebarVlg = sidebar.AddComponent<VerticalLayoutGroup>();
            sidebarVlg.padding = new RectOffset(0, 0, 30, 30);
            sidebarVlg.spacing = 20;
            sidebarVlg.childAlignment = TextAnchor.UpperCenter;
            sidebarVlg.childForceExpandWidth = true;
            sidebarVlg.childForceExpandHeight = false;
            sidebarVlg.childControlWidth = true;
            sidebarVlg.childControlHeight = true;

            var tabFriend = BuildTab(sidebar, "Tab_Friend", "친구", cfg);
            var tabChat   = BuildTab(sidebar, "Tab_Chat",   "채팅", cfg);
            var tabTheme  = BuildTab(sidebar, "Tab_Theme",  "테마", cfg);
            tabTheme.SetActive(false);  // 데모 제외

            var tabGroup = sidebar.AddComponent<TabGroup>();
            using (var so = new SerializedObject(tabGroup))
            {
                var arr = so.FindProperty("tabs");
                arr.arraySize = 2;
                arr.GetArrayElementAtIndex(0).objectReferenceValue = tabFriend.GetComponent<ButtonEX>();
                arr.GetArrayElementAtIndex(1).objectReferenceValue = tabChat.GetComponent<ButtonEX>();
                so.FindProperty("defaultTab").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ─── List (Friend/Chat 스택) ───────────────────
            var list = NewUIChild(main, "List", out _);
            var listLe = list.AddComponent<LayoutElement>();
            listLe.preferredWidth = cfg.listColumnWidth;
            listLe.flexibleWidth = 0;
            var listBg = list.AddComponent<Image>();
            listBg.color = cfg.listBgColor;

            var friendList = BuildFriendListPanel(list, cfg);
            var chatList   = BuildChatListPanel(list, cfg);
            // 초기: 친구 탭 활성
            friendList.SetActive(true);
            chatList.SetActive(false);

            // ─── 우측 영역 (ProfilePanel + ChatRoomPanel 스택) ─
            var right = NewUIChild(main, "RightPanel", out var rightRt);
            var rightLe = right.AddComponent<LayoutElement>();
            rightLe.flexibleWidth = 1;
            var rightBg = right.AddComponent<Image>();
            rightBg.color = cfg.rightPanelTopColor;

            var profilePanel = BuildProfilePanel(right, cfg);
            var chatRoomPanel = BuildChatRoomPanel(right, cfg);
            profilePanel.SetActive(false);
            chatRoomPanel.SetActive(false);

            // ─── PhonePopup 컴포넌트 + 와이어링 ─────────────
            var popup = root.AddComponent<PhonePopup>();
            using (var so = new SerializedObject(popup))
            {
                so.FindProperty("tabGroup").objectReferenceValue = tabGroup;
                so.FindProperty("friendListPanel").objectReferenceValue = friendList;
                so.FindProperty("chatListPanel").objectReferenceValue = chatList;
                so.FindProperty("chatRoomPanel").objectReferenceValue = chatRoomPanel;
                so.FindProperty("profilePanel").objectReferenceValue = profilePanel;
                so.FindProperty("friendListContent").objectReferenceValue = friendList.transform.Find("ScrollView/Viewport/Content");
                so.FindProperty("chatListContent").objectReferenceValue = chatList.transform.Find("ScrollView/Viewport/Content");
                so.FindProperty("chatRoom").objectReferenceValue = chatRoomPanel.GetComponent<PhoneChatRoom>();
                so.FindProperty("profileImage").objectReferenceValue = profilePanel.transform.Find("LargeProfile").GetComponent<Image>();
                so.FindProperty("profileNameText").objectReferenceValue = profilePanel.transform.Find("Name").GetComponent<TMP_Text>();
                so.FindProperty("profileStatusText").objectReferenceValue = profilePanel.transform.Find("StatusMessage/Text").GetComponent<TMP_Text>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return root;
        }

        // ═══════════════════════════════════════════════
        static GameObject BuildTab(GameObject parent, string name, string label, PhoneUIDesignConfig cfg)
        {
            var go = NewUIChild(parent, name, out var rt);
            rt.sizeDelta = new Vector2(cfg.sidebarWidth, cfg.sidebarWidth);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = cfg.sidebarWidth;
            go.AddComponent<Image>().color = new Color(1, 1, 1, 0); // 투명 (호버 시 변경)
            go.AddComponent<Button>();
            var btnEx = go.AddComponent<ButtonEX>();

            // 라벨
            var textGo = NewUIChild(go, "Label", out var textRt);
            Stretch(textRt);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = cfg.tabFontSize;
            tmp.color = cfg.tabOffColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            // 드롭쉐도우
            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = cfg.tabShadowColor;
            shadow.effectDistance = new Vector2(0, -2);

            return go;
        }

        static GameObject BuildFriendListPanel(GameObject parent, PhoneUIDesignConfig cfg)
        {
            var panel = NewUIChild(parent, "FriendList", out var rt);
            Stretch(rt);
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 20, 20);
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // CharPlayerSlot (placeholder GameObject)
            var playerSlot = NewUIChild(panel, "CharPlayerSlot", out _);
            playerSlot.AddComponent<Image>().color = new Color(1, 0.9f, 0.93f);
            var playerLe = playerSlot.AddComponent<LayoutElement>();
            playerLe.preferredHeight = cfg.listItemHeight + 10;

            // Header
            var header = NewUIChild(panel, "Header", out _);
            var headerLe = header.AddComponent<LayoutElement>();
            headerLe.preferredHeight = 40;
            var htmp = NewUIChild(header, "Title", out var htRt);
            Stretch(htRt);
            var htmpText = htmp.AddComponent<TextMeshProUGUI>();
            htmpText.text = "친구";
            htmpText.fontSize = cfg.titleFontSize;
            htmpText.color = cfg.titleColor;
            htmpText.alignment = TextAlignmentOptions.MidlineLeft;
            htmpText.margin = new Vector4(20, 0, 0, 0);

            // Scroll View > Viewport > Content
            BuildScrollView(panel, out _);

            return panel;
        }

        static GameObject BuildChatListPanel(GameObject parent, PhoneUIDesignConfig cfg)
        {
            var panel = NewUIChild(parent, "ChatList", out var rt);
            Stretch(rt);
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 20, 20);
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var header = NewUIChild(panel, "Header", out _);
            var headerLe = header.AddComponent<LayoutElement>();
            headerLe.preferredHeight = 40;
            var htmp = NewUIChild(header, "Title", out var htRt);
            Stretch(htRt);
            var htmpText = htmp.AddComponent<TextMeshProUGUI>();
            htmpText.text = "채팅";
            htmpText.fontSize = cfg.titleFontSize;
            htmpText.color = cfg.titleColor;
            htmpText.alignment = TextAlignmentOptions.MidlineLeft;
            htmpText.margin = new Vector4(20, 0, 0, 0);

            BuildScrollView(panel, out _);

            return panel;
        }

        static GameObject BuildScrollView(GameObject parent, out GameObject content)
        {
            var sv = NewUIChild(parent, "ScrollView", out var svRt);
            var svLe = sv.AddComponent<LayoutElement>();
            svLe.flexibleHeight = 1;

            var scrollRect = sv.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var viewport = NewUIChild(sv, "Viewport", out var vpRt);
            Stretch(vpRt);
            viewport.AddComponent<Image>().color = new Color(1, 1, 1, 0);
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            content = NewUIChild(viewport, "Content", out var contentRt);
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = new Vector2(0, 0);
            var cvlg = content.AddComponent<VerticalLayoutGroup>();
            cvlg.padding = new RectOffset(0, 0, 0, 0);
            cvlg.spacing = 8;
            cvlg.childAlignment = TextAnchor.UpperLeft;
            cvlg.childForceExpandWidth = true;
            cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth = true;
            cvlg.childControlHeight = true;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = vpRt;
            scrollRect.content = contentRt;

            return sv;
        }

        static GameObject BuildProfilePanel(GameObject parent, PhoneUIDesignConfig cfg)
        {
            var panel = NewUIChild(parent, "ProfilePanel", out var rt);
            Stretch(rt);

            // 큰 프로필 이미지
            var profile = NewUIChild(panel, "LargeProfile", out var pRt);
            pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.sizeDelta = new Vector2(180, 180);
            pRt.anchoredPosition = new Vector2(0, 40);
            profile.AddComponent<Image>().color = new Color(1, 1, 1, 0.6f);

            // Name
            var nameGo = NewUIChild(panel, "Name", out var nameRt);
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 0.5f);
            nameRt.sizeDelta = new Vector2(300, 40);
            nameRt.anchoredPosition = new Vector2(0, -80);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = "이름";
            nameTmp.fontSize = 28;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.fontStyle = FontStyles.Bold;

            // StatusMessage > Text
            var status = NewUIChild(panel, "StatusMessage", out var stRt);
            stRt.anchorMin = stRt.anchorMax = new Vector2(0.5f, 0.5f);
            stRt.sizeDelta = new Vector2(280, 30);
            stRt.anchoredPosition = new Vector2(0, -130);
            status.AddComponent<Image>().color = new Color(1, 1, 1, 0.6f);
            var statusText = NewUIChild(status, "Text", out var stxtRt);
            Stretch(stxtRt);
            var stTmp = statusText.AddComponent<TextMeshProUGUI>();
            stTmp.text = "상태 메시지입니다.";
            stTmp.fontSize = 14;
            stTmp.color = new Color(0.4f, 0.4f, 0.4f);
            stTmp.alignment = TextAlignmentOptions.Center;

            return panel;
        }

        static GameObject BuildChatRoomPanel(GameObject parent, PhoneUIDesignConfig cfg)
        {
            var panel = NewUIChild(parent, "ChatRoomPanel", out var rt);
            Stretch(rt);
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(cfg.chatRoomPadLeft, cfg.chatRoomPadRight, cfg.chatRoomPadTop, cfg.chatRoomPadBottom);
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // Header
            var header = NewUIChild(panel, "Header", out _);
            var headerLe = header.AddComponent<LayoutElement>();
            headerLe.preferredHeight = 50;
            var headerHlg = header.AddComponent<HorizontalLayoutGroup>();
            headerHlg.spacing = 10;
            headerHlg.childAlignment = TextAnchor.MiddleLeft;
            headerHlg.childForceExpandWidth = false;
            headerHlg.childForceExpandHeight = false;
            headerHlg.childControlWidth = true;
            headerHlg.childControlHeight = true;

            var profile = NewUIChild(header, "Profile", out var profRt);
            profRt.sizeDelta = new Vector2(36, 36);
            profile.AddComponent<Image>().color = Color.white;
            var profLe = profile.AddComponent<LayoutElement>();
            profLe.preferredWidth = 36; profLe.preferredHeight = 36;

            var nameGo = NewUIChild(header, "Name", out _);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = "이름";
            nameTmp.fontSize = 20;
            nameTmp.color = Color.white;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var statusBox = NewUIChild(header, "Status", out _);
            statusBox.AddComponent<Image>().color = new Color(1, 1, 1, 0.7f);
            var statusBoxLe = statusBox.AddComponent<LayoutElement>();
            statusBoxLe.preferredWidth = 160;
            var statusText = NewUIChild(statusBox, "Text", out var stRt);
            Stretch(stRt);
            var stTmp = statusText.AddComponent<TextMeshProUGUI>();
            stTmp.text = "상태 메시지입니다.";
            stTmp.fontSize = 12;
            stTmp.color = new Color(0.4f, 0.4f, 0.4f);
            stTmp.alignment = TextAlignmentOptions.Center;

            // MessageScroll
            BuildScrollView(panel, out var msgContent);
            msgContent.transform.parent.parent.name = "MessageScroll";

            // BottomBar (선택지 영역 placeholder)
            var bottom = NewUIChild(panel, "BottomBar", out _);
            var bottomLe = bottom.AddComponent<LayoutElement>();
            bottomLe.preferredHeight = 80;
            bottom.AddComponent<Image>().color = new Color(1, 1, 1, 0.3f);
            var bvlg = bottom.AddComponent<VerticalLayoutGroup>();
            bvlg.spacing = 4;
            bvlg.childForceExpandWidth = true;
            bvlg.childControlWidth = true;
            bvlg.childControlHeight = true;

            // ChoiceArea (선택지)
            var choiceArea = NewUIChild(bottom, "ChoiceArea", out _);
            choiceArea.SetActive(false); // 기본 비활성

            // PhoneChatRoom 컴포넌트 + 와이어링
            var chatRoom = panel.AddComponent<PhoneChatRoom>();
            using (var so = new SerializedObject(chatRoom))
            {
                so.FindProperty("headerNameText").objectReferenceValue = nameTmp;
                so.FindProperty("headerStatusText").objectReferenceValue = stTmp;
                so.FindProperty("headerProfileImage").objectReferenceValue = profile.GetComponent<Image>();
                so.FindProperty("scrollRect").objectReferenceValue = msgContent.transform.parent.parent.GetComponent<ScrollRect>();
                so.FindProperty("messageContainer").objectReferenceValue = msgContent.transform;
                // selfBubblePrefab/otherBubblePrefab — 자동 검색
                var selfBubble = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Modules/Phone/Prefabs/ChatBubbleSelf.prefab");
                var otherBubble = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Modules/Phone/Prefabs/ChatBubbleOther.prefab");
                if (selfBubble != null) so.FindProperty("selfBubblePrefab").objectReferenceValue = selfBubble.GetComponent<ChatBubble>();
                if (otherBubble != null) so.FindProperty("otherBubblePrefab").objectReferenceValue = otherBubble.GetComponent<ChatBubble>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return panel;
        }

        // ─── 유틸 ───────────────────────────────────────
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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
