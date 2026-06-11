using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core;      // GameStateSO
using LoveAlgo.Messenger; // 뷰/슬롯/카탈로그

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 메신저 프리팹 일괄 조립 도구(Tools ▸ Messenger ▸ Build Messenger Prefabs).
    /// Game.unity가 병렬 작업 중이라 씬 대신 프리팹으로 산출(Modal.prefab 선례, ADR-013 씬별 인스턴스) —
    /// 씬 배선은 후속. 재실행 안전(같은 경로 덮어쓰기 = GUID 보존). 위치/크기 수치는 시작값일 뿐
    /// 비주얼 튜닝은 감독 영역(🟢) — 구조·바인딩·아트 연결까지만 책임진다.
    /// </summary>
    public static class MessengerPrefabBuilder
    {
        const string ArtDir = "Assets/Art/메신저/png";
        const string PrefabDir = "Assets/_Project/Prefabs/Messenger";
        const string DataDir = "Assets/Resources/Data";
        const string AggroFont = "Assets/Fonts/Aggro-Medium SDF.asset";
        const string BodyFont = "Assets/Fonts/Pretendard-SemiBold SDF.asset";

        // 기획서 폰트 정보의 컬러 스펙
        static readonly Color TitlePink = Hex("#ff99b6");
        static readonly Color DarkText = Hex("#4a4a4a");

        [MenuItem("Tools/Messenger/Build Messenger Prefabs")]
        public static void Build()
        {
            EnsureSpriteImports();
            EnsureFolder(PrefabDir);

            var friendCatalog = EnsureAsset<FriendCatalogSO>($"{DataDir}/FriendCatalog.asset");
            var scriptCatalog = EnsureAsset<MessengerScriptCatalogSO>($"{DataDir}/MessengerScriptCatalog.asset");
            var state = FindGameState();

            // ── 서브 프리팹 ──
            var friendSlot = SavePrefab(BuildFriendSlot(), $"{PrefabDir}/FriendSlot.prefab").GetComponent<FriendSlot>();
            var chatRoomSlot = SavePrefab(BuildChatRoomSlot(), $"{PrefabDir}/ChatRoomSlot.prefab").GetComponent<ChatRoomSlot>();
            var bubbleIn = SavePrefab(BuildBubble("BubbleIn", "chat_in", withSender: true), $"{PrefabDir}/BubbleIn.prefab").GetComponent<MessengerBubble>();
            var bubbleOut = SavePrefab(BuildBubble("BubbleOut", "chat_out", withSender: false), $"{PrefabDir}/BubbleOut.prefab").GetComponent<MessengerBubble>();
            var optionSlot = SavePrefab(BuildOptionSlot(), $"{PrefabDir}/MessengerOptionSlot.prefab").GetComponent<MessengerOptionSlot>();
            var profileChoice = SavePrefab(BuildProfileChoiceSlot(), $"{PrefabDir}/ProfileChoiceSlot.prefab").GetComponent<ProfileChoiceSlot>();

            // 프로필 후보 카탈로그 — 비어 있을 때만 현재 가용 아트로 기본 채움(감독 편집 보존, 후보 아트 도착 시 추가)
            var profileCatalog = EnsureAsset<MessengerProfileCatalogSO>($"{DataDir}/MessengerProfileCatalog.asset");
            if (profileCatalog.ProfileImages.Count == 0 && profileCatalog.Backgrounds.Count == 0)
            {
                profileCatalog.SetData(
                    new System.Collections.Generic.List<Sprite> { LoadSprite("profile_pic_basic_l"), LoadSprite("profile_test") },
                    new System.Collections.Generic.List<Sprite> { LoadSprite("profile_bg_basic"), LoadSprite("profile_bg_test") });
                EditorUtility.SetDirty(profileCatalog);
            }

            // ── 메인 프리팹 ──
            var messenger = BuildMessenger(state, friendCatalog, scriptCatalog, profileCatalog,
                friendSlot, chatRoomSlot, bubbleIn, bubbleOut, optionSlot, profileChoice);
            SavePrefab(messenger, $"{PrefabDir}/Messenger.prefab");

            // ── 폰 버튼(메신저 진입점 1) ──
            var tuning = EnsureAsset<MessengerTuningSO>($"{DataDir}/MessengerTuning.asset");
            SavePrefab(BuildPhoneButton(state, tuning), $"{PrefabDir}/PhoneButton.prefab");

            AssetDatabase.SaveAssets();
            Debug.Log($"[MessengerPrefabBuilder] 산출 완료 → {PrefabDir} (Messenger + PhoneButton + 슬롯/말풍선 5종). " +
                      "FriendCatalog/MessengerScriptCatalog는 없으면 빈 생성 — 엔트리는 채움 메뉴/기획 입력.");
        }

        /// <summary>
        /// FriendCatalog를 별칭 카탈로그(정본)의 캐릭터 매핑에서 자동 채움 — 한글명↔c0X를 한 곳에서만
        /// 관리해 오타/이중 정의를 차단. 표시 순서는 가나다(기획 목업 친구 목록 순). 재실행 = 전체 갱신.
        /// </summary>
        [MenuItem("Tools/Messenger/Fill Friend Catalog From Aliases")]
        public static void FillFriendCatalog()
        {
            var aliasCatalog = AssetDatabase.LoadAssetAtPath<LoveAlgo.Story.ResourceAliasCatalogSO>(
                $"{DataDir}/ResourceAliasCatalog.asset");
            if (aliasCatalog == null)
            {
                Debug.LogError($"[MessengerPrefabBuilder] {DataDir}/ResourceAliasCatalog.asset 없음 — 채움 중단.");
                return;
            }

            var friends = EnsureAsset<FriendCatalogSO>($"{DataDir}/FriendCatalog.asset");
            var entries = new System.Collections.Generic.List<FriendCatalogSO.Entry>();
            foreach (var ch in aliasCatalog.Characters)
            {
                if (ch == null || string.IsNullOrEmpty(ch.id)) continue;
                if (!System.Text.RegularExpressions.Regex.IsMatch(ch.id, "^c0[1-5]$")) continue; // 히로인만
                string display = ch.aliases != null && ch.aliases.Length > 0 ? ch.aliases[0] : ch.id;
                entries.Add(new FriendCatalogSO.Entry
                {
                    id = ch.id,
                    displayName = display,
                    defaultStatus = "상태 메세지입니다." // 목업 기본 문구 — 히로인별 상메는 기획 입력
                });
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName)); // 가나다(목업 순)
            friends.SetEntries(entries);
            EditorUtility.SetDirty(friends);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MessengerPrefabBuilder] FriendCatalog {entries.Count}명 채움: " +
                      string.Join(", ", entries.ConvertAll(e => $"{e.displayName}={e.id}")));
        }

        // ───────────────────────── 메인 ─────────────────────────

        static GameObject BuildMessenger(GameStateSO state, FriendCatalogSO friends, MessengerScriptCatalogSO catalog,
            MessengerProfileCatalogSO profileCatalog, FriendSlot friendSlot, ChatRoomSlot chatRoomSlot,
            MessengerBubble bubbleIn, MessengerBubble bubbleOut, MessengerOptionSlot optionSlot, ProfileChoiceSlot profileChoice)
        {
            var messengerGo = Rect("Messenger", null);
            Stretch(messengerGo);
            var view = messengerGo.AddComponent<MessengerView>();

            // Root — 부팅 시 닫힘(Modal 패턴: 컴포넌트 GO는 active, Root만 inactive)
            var root = Rect("Root", messengerGo.transform);
            Stretch(root);
            view.Root = root;

            var dim = Img(Rect("Dim", root.transform), null, new Color(0f, 0f, 0f, 0.35f));
            Stretch(dim.gameObject); // 뒤 클릭 차단(raycast)

            var window = Rect("Window", root.transform);
            Size(window, 1300, 760);

            // 좌측 탭 칼럼
            var tabColumn = Img(Rect("TabColumn", window.transform), "menu_bg");
            var tabRt = tabColumn.GetComponent<RectTransform>();
            tabRt.anchorMin = new Vector2(0, 0); tabRt.anchorMax = new Vector2(0, 1);
            tabRt.pivot = new Vector2(0, 0.5f);
            tabRt.offsetMin = Vector2.zero; tabRt.sizeDelta = new Vector2(92, 0);

            view.FriendTabButton = IconButton(tabColumn.transform, "FriendTab", "btn_friendlist", new Vector2(46, -90), 72);
            view.ChatTabButton = IconButton(tabColumn.transform, "ChatTab", "btn_chatlist", new Vector2(46, -190), 72);
            view.CloseButton = IconButton(window.transform, "Close", "btn_close", Vector2.zero, 48);
            var closeRt = view.CloseButton.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1, 1); closeRt.anchoredPosition = new Vector2(-34, -34);

            // ── 친구 탭 패널 ──
            var friendPanel = Img(Rect("FriendPanel", window.transform), "friendlist_bg_none");
            StretchWithLeft(friendPanel.gameObject, 92);
            view.FriendPanel = friendPanel.gameObject;

            var friendList = friendPanel.gameObject.AddComponent<FriendListView>();
            friendList.Container = ScrollList(friendPanel.transform, "FriendScroll", new Vector2(0.02f, 0.03f), new Vector2(0.46f, 0.97f));
            friendList.SlotPrefab = friendSlot;
            friendList.Friends = friends;
            friendList.State = state;
            view.FriendList = friendList;

            // 우측 프로필 영역(기획서: 접근 시 빈 화면 → 행 클릭 시 프로필, 플레이어만 편집)
            view.ProfilePanel = BuildProfilePanel(friendPanel.transform, window.transform, state, friends, profileCatalog);
            view.ProfileEdit = BuildProfileEdit(friendPanel.transform, state, profileCatalog, profileChoice);

            // ── 채팅 탭 패널 ──
            var chatPanel = Img(Rect("ChatPanel", window.transform), "chatlist_bg_none");
            StretchWithLeft(chatPanel.gameObject, 92);
            view.ChatPanel = chatPanel.gameObject;

            var chatList = chatPanel.gameObject.AddComponent<ChatListView>();
            chatList.Container = ScrollList(chatPanel.transform, "ChatListScroll", new Vector2(0.02f, 0.03f), new Vector2(0.46f, 0.97f));
            chatList.SlotPrefab = chatRoomSlot;
            chatList.Friends = friends;
            chatList.Catalog = catalog;
            chatList.State = state;
            view.ChatList = chatList;

            // 채팅창(우측) — Root만 토글되는 ChatRoomView
            var chatRoomRoot = Img(Rect("ChatRoom", chatPanel.transform), "chat_bg");
            var crRt = chatRoomRoot.GetComponent<RectTransform>();
            crRt.anchorMin = new Vector2(0.48f, 0.03f); crRt.anchorMax = new Vector2(0.98f, 0.97f);
            crRt.offsetMin = crRt.offsetMax = Vector2.zero;

            var chatRoom = chatRoomRoot.gameObject.AddComponent<ChatRoomView>();
            chatRoom.Root = chatRoomRoot.gameObject;

            var header = Img(Rect("Header", chatRoomRoot.transform), "profie text_chat header");
            var hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0, 1); hRt.anchorMax = new Vector2(1, 1);
            hRt.pivot = new Vector2(0.5f, 1); hRt.sizeDelta = new Vector2(-24, 64); hRt.anchoredPosition = new Vector2(0, -10);

            chatRoom.BubbleContainer = ScrollList(chatRoomRoot.transform, "BubbleScroll", new Vector2(0.02f, 0.22f), new Vector2(0.98f, 0.88f));
            var optionArea = Rect("OptionArea", chatRoomRoot.transform);
            var oRt = optionArea.GetComponent<RectTransform>();
            oRt.anchorMin = new Vector2(0.05f, 0.02f); oRt.anchorMax = new Vector2(0.95f, 0.20f);
            oRt.offsetMin = oRt.offsetMax = Vector2.zero;
            var oLayout = optionArea.AddComponent<VerticalLayoutGroup>();
            oLayout.spacing = 10; oLayout.childForceExpandHeight = false; oLayout.childControlHeight = false;
            chatRoom.OptionContainer = optionArea.transform;

            chatRoom.BubbleInPrefab = bubbleIn;
            chatRoom.BubbleOutPrefab = bubbleOut;
            chatRoom.OptionPrefab = optionSlot;
            chatRoom.State = state;
            chatRoom.Catalog = catalog;
            chatRoom.Friends = friends;
            view.ChatRoom = chatRoom;

            // 부팅 상태: 친구 탭 기본, 채팅창/채팅패널/루트 닫힘
            chatRoomRoot.gameObject.SetActive(false);
            chatPanel.gameObject.SetActive(false);
            root.SetActive(false);
            return messengerGo;
        }

        /// <summary>
        /// 폰 버튼 — 우측 가장자리에 살짝 보이는 말풍선 박스(+MESSAGE 라벨+배지). 전용 아트(btn_phone*)가
        /// 감독 정리로 삭제된 상태라 메신저 말풍선 아트로 구성 — 아트 도착 시 스프라이트만 교체.
        /// 화면 밖으로 나간 만큼이 호버 슬라이드로 드러난다(노출 폭/위치는 감독 튜닝).
        /// </summary>
        static GameObject BuildPhoneButton(GameStateSO state, MessengerTuningSO tuning)
        {
            var go = Rect("PhoneButton", null);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.8f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(210, 64);
            rt.anchoredPosition = new Vector2(150, 0); // 150px 화면 밖 = 60px만 노출(슬라이드로 전체 공개)

            var img = go.AddComponent<Image>();
            img.sprite = Sprite9("chat_out"); img.type = Image.Type.Sliced;

            var view = go.AddComponent<PhoneButtonView>();
            view.Group = go.AddComponent<CanvasGroup>();
            view.Button = go.AddComponent<Button>();
            view.State = state;
            view.Tuning = tuning;

            var label = Label(go.transform, "Label", AggroFont, 18, TitlePink, new Vector2(24, 0), new Vector2(160, 30), TextAlignmentOptions.MidlineLeft);
            label.text = "MESSAGE";

            var badge = Img(Rect("Badge", go.transform), "new_badge");
            var bRt = badge.GetComponent<RectTransform>();
            bRt.anchorMin = bRt.anchorMax = new Vector2(0, 1);
            bRt.sizeDelta = new Vector2(26, 26); bRt.anchoredPosition = new Vector2(8, -4);
            badge.gameObject.SetActive(false); // 부팅: 미읽음 없음
            view.Badge = badge.gameObject;
            return go;
        }

        /// <summary>친구 탭 우측 프로필 영역 + 사진 확대 오버레이(기획서 p7~12). 줌은 창 전체 위라 window에 단다.</summary>
        static ProfilePanelView BuildProfilePanel(Transform friendPanel, Transform window,
            GameStateSO state, FriendCatalogSO friends, MessengerProfileCatalogSO profileCatalog)
        {
            var area = Rect("ProfileArea", friendPanel);
            var aRt = (RectTransform)area.transform;
            aRt.anchorMin = new Vector2(0.48f, 0.03f); aRt.anchorMax = new Vector2(0.98f, 0.97f);
            aRt.offsetMin = aRt.offsetMax = Vector2.zero;
            var panel = area.AddComponent<ProfilePanelView>();
            panel.State = state;
            panel.Friends = friends;
            panel.ProfileCatalog = profileCatalog;

            // 빈 화면(기본) — 흐린 기본 실루엣만
            var empty = Rect("Empty", area.transform);
            Stretch(empty);
            var emptyIcon = Img(Circle(empty.transform, "Silhouette", 120, Vector2.zero), "profile_pic_basic_l");
            var eRt = emptyIcon.GetComponent<RectTransform>();
            eRt.anchorMin = eRt.anchorMax = new Vector2(0.5f, 0.5f);
            emptyIcon.color = new Color(1f, 1f, 1f, 0.35f);
            panel.EmptyRoot = empty;

            // 프로필 콘텐츠
            var content = Rect("Content", area.transform);
            Stretch(content);
            panel.BgImage = Img(Rect("Bg", content.transform), "profile_bg_basic");
            Stretch(panel.BgImage.gameObject);

            var dim = Img(Rect("BottomDim", content.transform), "profile_bg_dim"); // 기획: 배경 사진 하단 Dim
            var dRt = dim.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0, 0); dRt.anchorMax = new Vector2(1, 0);
            dRt.pivot = new Vector2(0.5f, 0); dRt.sizeDelta = new Vector2(0, 200); dRt.anchoredPosition = Vector2.zero;

            var portrait = Img(Rect("Portrait", content.transform), "profile_pic_basic_l");
            var pRt = portrait.GetComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.32f);
            pRt.sizeDelta = new Vector2(140, 140);
            panel.PortraitImage = portrait;
            panel.PortraitButton = portrait.gameObject.AddComponent<Button>();

            panel.NameText = CenterLabel(content.transform, "Name", AggroFont, 25, Color.white, new Vector2(0.5f, 0.17f), new Vector2(360, 36));
            panel.StatusText = CenterLabel(content.transform, "Status", BodyFont, 16, new Color(1f, 1f, 1f, 0.9f), new Vector2(0.5f, 0.09f), new Vector2(380, 30));
            panel.EditButton = IconButton(content.transform, "Edit", "btn_edit", new Vector2(-34, -34), 44);
            var ebRt = panel.EditButton.GetComponent<RectTransform>();
            ebRt.anchorMin = ebRt.anchorMax = new Vector2(1, 1);
            content.SetActive(false);

            // 사진 확대(줌) — 창 전체 오버레이, 아무 곳 클릭 닫기
            var zoom = Rect("ProfileZoom", window);
            Stretch(zoom);
            var zoomBg = Img(Rect("ZoomBg", zoom.transform), "profiepic_zoom_bg", new Color(1f, 1f, 1f, 0.97f));
            Stretch(zoomBg.gameObject);
            panel.ZoomCloseButton = zoomBg.gameObject.AddComponent<Button>();
            var zoomImg = Img(Rect("ZoomImage", zoom.transform), "profile_pic_zoom");
            var zRt = zoomImg.GetComponent<RectTransform>();
            zRt.anchorMin = zRt.anchorMax = new Vector2(0.5f, 0.55f);
            zRt.sizeDelta = new Vector2(420, 420);
            panel.ZoomImage = zoomImg;
            var zoomName = Img(Rect("ZoomNameBox", zoom.transform), "profile_text_zoom");
            var znRt = zoomName.GetComponent<RectTransform>();
            znRt.anchorMin = znRt.anchorMax = new Vector2(0.5f, 0.22f);
            znRt.sizeDelta = new Vector2(320, 56);
            panel.ZoomNameText = CenterLabel(zoomName.transform, "Name", AggroFont, 23, TitlePink, new Vector2(0.5f, 0.5f), new Vector2(300, 36));
            zoom.SetActive(false);
            panel.ZoomRoot = zoom;

            return panel;
        }

        /// <summary>플레이어 프로필 편집 화면(기획서 p9~10) — 친구 패널을 덮는 오버레이.</summary>
        static ProfileEditView BuildProfileEdit(Transform friendPanel, GameStateSO state,
            MessengerProfileCatalogSO profileCatalog, ProfileChoiceSlot profileChoice)
        {
            var root = Rect("ProfileEdit", friendPanel);
            Stretch(root);
            var edit = root.AddComponent<ProfileEditView>();
            edit.Root = root;
            edit.State = state;
            edit.ProfileCatalog = profileCatalog;
            edit.SlotPrefab = profileChoice;

            var bg = Img(Rect("Bg", root.transform), "profile edit_bg");
            Stretch(bg.gameObject);

            var title = Img(Rect("TitleBox", root.transform), "profile edit_title box");
            var tRt = title.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.04f, 1); tRt.anchorMax = new Vector2(0.4f, 1);
            tRt.pivot = new Vector2(0.5f, 1); tRt.sizeDelta = new Vector2(0, 48); tRt.anchoredPosition = new Vector2(0, -18);

            edit.ImageContainer = ChoiceRow(root.transform, "ImageRow", 0.70f);
            edit.BgContainer = ChoiceRow(root.transform, "BgRow", 0.46f);

            // 상태메시지 입력(TMP 기본 구조 재사용)
            var inputGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            inputGo.name = "StatusInput";
            inputGo.transform.SetParent(root.transform, false);
            var iRt = (RectTransform)inputGo.transform;
            iRt.anchorMin = new Vector2(0.06f, 0.24f); iRt.anchorMax = new Vector2(0.56f, 0.32f);
            iRt.offsetMin = iRt.offsetMax = Vector2.zero;
            edit.StatusInput = inputGo.GetComponent<TMP_InputField>();
            var placeholder = edit.StatusInput.placeholder as TextMeshProUGUI;
            if (placeholder != null) placeholder.text = "기본 상태메시지";

            edit.SaveButton = IconButton(root.transform, "Save", "btn_save edit", new Vector2(-110, 40), 44);
            var sRt = edit.SaveButton.GetComponent<RectTransform>();
            sRt.anchorMin = sRt.anchorMax = new Vector2(1, 0); sRt.sizeDelta = new Vector2(120, 44);
            edit.CloseButton = IconButton(root.transform, "Close", "btn_close edit", new Vector2(-34, -28), 36);
            var cRt = edit.CloseButton.GetComponent<RectTransform>();
            cRt.anchorMin = cRt.anchorMax = new Vector2(1, 1);

            // 우측 미리보기(기획: 동일하게 dim 적용)
            var preview = Rect("Preview", root.transform);
            var pvRt = (RectTransform)preview.transform;
            pvRt.anchorMin = new Vector2(0.62f, 0.08f); pvRt.anchorMax = new Vector2(0.96f, 0.92f);
            pvRt.offsetMin = pvRt.offsetMax = Vector2.zero;
            edit.PreviewBg = Img(Rect("PreviewBg", preview.transform), "profile_bg_basic");
            Stretch(edit.PreviewBg.gameObject);
            var pvDim = Img(Rect("PreviewDim", preview.transform), "profile_bg_dim");
            Stretch(pvDim.gameObject);
            var pvPortrait = Img(Circle(preview.transform, "PreviewPortrait", 110, Vector2.zero), "profile_pic_basic_l");
            var ppRt = pvPortrait.GetComponent<RectTransform>();
            ppRt.anchorMin = ppRt.anchorMax = new Vector2(0.5f, 0.45f);
            edit.PreviewPortrait = pvPortrait;

            root.SetActive(false);
            return edit;
        }

        /// <summary>후보 선택 가로 행(편집 화면) — HorizontalLayoutGroup 컨테이너.</summary>
        static Transform ChoiceRow(Transform parent, string name, float anchorY)
        {
            var row = Rect(name, parent);
            var rt = (RectTransform)row.transform;
            rt.anchorMin = new Vector2(0.06f, anchorY); rt.anchorMax = new Vector2(0.56f, anchorY + 0.18f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return row.transform;
        }

        static GameObject BuildProfileChoiceSlot()
        {
            var go = Rect("ProfileChoiceSlot", null);
            Size(go, 96, 96);
            var bg = go.AddComponent<Image>();
            bg.sprite = Sprite9("list_bg"); bg.type = Image.Type.Sliced;
            var slot = go.AddComponent<ProfileChoiceSlot>();
            slot.Button = go.AddComponent<Button>();
            go.AddComponent<LayoutElement>().preferredWidth = 96;

            var candidate = Img(Rect("Candidate", go.transform), null);
            var cRt = candidate.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero; cRt.anchorMax = Vector2.one;
            cRt.offsetMin = new Vector2(6, 6); cRt.offsetMax = new Vector2(-6, -6);
            candidate.preserveAspect = true;
            slot.Image = candidate;

            var frame = Img(Rect("SelectedFrame", go.transform), "profile pic_select");
            Stretch(frame.gameObject);
            frame.gameObject.SetActive(false);
            slot.SelectedFrame = frame.gameObject;
            return go;
        }

        /// <summary>가운데 정렬 라벨(앵커 비율 위치).</summary>
        static TextMeshProUGUI CenterLabel(Transform parent, string name, string fontPath, float size, Color color,
            Vector2 anchor, Vector2 dim)
        {
            var tmp = Label(parent, name, fontPath, size, color, Vector2.zero, dim, TextAlignmentOptions.Center);
            var rt = (RectTransform)tmp.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return tmp;
        }

        // ───────────────────────── 서브 프리팹 ─────────────────────────

        static GameObject BuildFriendSlot()
        {
            var go = Rect("FriendSlot", null);
            Size(go, 520, 96);
            var bg = go.AddComponent<Image>();
            bg.sprite = Sprite9("list_bg"); bg.type = Image.Type.Sliced; bg.color = Color.white;
            var slot = go.AddComponent<FriendSlot>();
            slot.Button = go.AddComponent<Button>();
            go.AddComponent<LayoutElement>().preferredHeight = 96;

            slot.Portrait = Img(Circle(go.transform, "Portrait", 64, new Vector2(56, 0)), "profile_pic_basic_s");
            slot.NameText = Label(go.transform, "Name", AggroFont, 23, TitlePink, new Vector2(110, 14), new Vector2(380, 32), TextAlignmentOptions.MidlineLeft);
            slot.StatusText = Label(go.transform, "Status", BodyFont, 16, DarkText, new Vector2(110, -20), new Vector2(380, 26), TextAlignmentOptions.MidlineLeft);
            return go;
        }

        static GameObject BuildChatRoomSlot()
        {
            var go = BuildFriendSlot();
            go.name = "ChatRoomSlot";
            var friend = go.GetComponent<FriendSlot>();
            var slot = go.AddComponent<ChatRoomSlot>();
            slot.Button = friend.Button;
            slot.Portrait = friend.Portrait;
            slot.NameText = friend.NameText;
            slot.PreviewText = friend.StatusText; // 같은 자리 = 마지막 메시지 미리보기
            Object.DestroyImmediate(friend); // FriendSlot 컴포넌트만 제거(자식 유지)

            var badge = Img(Rect("NewBadge", go.transform), "new_badge");
            var bRt = badge.GetComponent<RectTransform>();
            bRt.anchorMin = bRt.anchorMax = new Vector2(1, 0.5f);
            bRt.sizeDelta = new Vector2(28, 28); bRt.anchoredPosition = new Vector2(-26, 0);
            slot.NewBadge = badge.gameObject;
            return go;
        }

        static GameObject BuildBubble(string name, string boxSprite, bool withSender)
        {
            var go = Rect(name, null);
            Size(go, 560, 90);
            var bubble = go.AddComponent<MessengerBubble>();
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4; layout.childControlHeight = true; layout.childControlWidth = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.childAlignment = withSender ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            layout.padding = new RectOffset(14, 14, 4, 4);

            if (withSender)
            {
                bubble.SenderText = Label(go.transform, "Sender", AggroFont, 18, TitlePink, Vector2.zero, new Vector2(200, 24), TextAlignmentOptions.MidlineLeft);
            }

            var box = Img(Rect("Box", go.transform), null);
            box.sprite = Sprite9(boxSprite); box.type = Image.Type.Sliced; box.color = Color.white;
            var boxLayout = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            boxLayout.padding = new RectOffset(20, 20, 12, 12);
            boxLayout.childControlWidth = true; boxLayout.childControlHeight = true;
            boxLayout.childForceExpandWidth = false; boxLayout.childForceExpandHeight = false;
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            box.gameObject.AddComponent<LayoutElement>().preferredWidth = 440;

            bubble.MessageText = Label(box.transform, "Msg", BodyFont, 22, DarkText, Vector2.zero, new Vector2(400, 32), TextAlignmentOptions.TopLeft);
            bubble.MessageText.textWrappingMode = TextWrappingModes.Normal;
            return go;
        }

        static GameObject BuildOptionSlot()
        {
            var go = Rect("MessengerOptionSlot", null);
            Size(go, 620, 58);
            var img = go.AddComponent<Image>();
            img.sprite = Sprite9("chat_select_box"); img.type = Image.Type.Sliced;
            var slot = go.AddComponent<MessengerOptionSlot>();
            var button = go.AddComponent<Button>();
            slot.Button = button;
            go.AddComponent<LayoutElement>().preferredHeight = 58;

            // 기획서: 선택지 마우스 호버 강조 — hover 스프라이트 스왑
            var hover = Sprite9("chat_select_box_hover");
            if (hover != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                var ss = button.spriteState; ss.highlightedSprite = hover; ss.pressedSprite = hover;
                button.spriteState = ss;
            }

            slot.LabelText = Label(go.transform, "Label", BodyFont, 21, DarkText, Vector2.zero, new Vector2(560, 40), TextAlignmentOptions.Center);
            return go;
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        static GameObject Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void StretchWithLeft(GameObject go, float left)
        {
            Stretch(go);
            var rt = (RectTransform)go.transform;
            rt.offsetMin = new Vector2(left, 0);
        }

        static void Size(GameObject go, float w, float h)
        {
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(w, h);
        }

        static Image Img(GameObject go, string spriteName) => Img(go, spriteName, Color.white);

        static Image Img(GameObject go, string spriteName, Color color)
        {
            var img = go.AddComponent<Image>();
            if (!string.IsNullOrEmpty(spriteName)) img.sprite = LoadSprite(spriteName);
            img.color = color;
            return img;
        }

        static GameObject Circle(Transform parent, string name, float size, Vector2 pos)
        {
            var go = Rect(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = pos;
            return go;
        }

        static Button IconButton(Transform parent, string name, string spriteName, Vector2 pos, float size)
        {
            var go = Rect(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.sprite = LoadSprite(spriteName);
            img.preserveAspect = true;
            return go.AddComponent<Button>();
        }

        static TextMeshProUGUI Label(Transform parent, string name, string fontPath, float size, Color color,
            Vector2 pos, Vector2 dim, TextAlignmentOptions align)
        {
            var go = Rect(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = dim; rt.anchoredPosition = pos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font != null) tmp.font = font;
            tmp.fontSize = size; tmp.color = color; tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>리스트용 ScrollRect 조립(Viewport+Content VLG) — Content Transform 반환.</summary>
        static Transform ScrollList(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = Rect(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = Rect("Viewport", go.transform);
            Stretch(viewport);
            viewport.AddComponent<RectMask2D>();

            var content = Rect("Content", viewport.transform);
            var cRt = (RectTransform)content.transform;
            cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1);
            cRt.pivot = new Vector2(0.5f, 1); cRt.offsetMin = Vector2.zero; cRt.offsetMax = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8; layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = false; layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = cRt;
            return content.transform;
        }

        static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
            if (sprite == null) Debug.LogWarning($"[MessengerPrefabBuilder] 스프라이트 없음: {ArtDir}/{name}.png");
            return sprite;
        }

        static Sprite Sprite9(string name) => LoadSprite(name);

        static GameObject SavePrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static GameStateSO FindGameState()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameStateSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<GameStateSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null) return so;
            }
            Debug.LogWarning("[MessengerPrefabBuilder] GameStateSO 에셋을 못 찾음 — State 바인딩 비움(씬 배선 시 주입).");
            return null;
        }

        /// <summary>png 폴더 일괄 Sprite 임포트 + 늘려 쓰는 박스류 9-슬라이스 보더 지정(재실행 안전).</summary>
        static void EnsureSpriteImports()
        {
            if (!Directory.Exists(ArtDir)) { Debug.LogWarning($"[MessengerPrefabBuilder] 아트 폴더 없음: {ArtDir}"); return; }

            string[] nineSlice = { "chat_in", "chat_out", "chat_select_box", "chat_select_box_hover", "list_bg", "menu_bg", "chat_select_bg" };
            foreach (var file in Directory.GetFiles(ArtDir, "*.png"))
            {
                var assetPath = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                var baseName = Path.GetFileNameWithoutExtension(assetPath);
                if (System.Array.IndexOf(nineSlice, baseName) >= 0 && importer.spriteBorder == Vector4.zero)
                {
                    importer.spriteBorder = new Vector4(12, 12, 12, 12);
                    dirty = true;
                }
                if (dirty) importer.SaveAndReimport();
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
