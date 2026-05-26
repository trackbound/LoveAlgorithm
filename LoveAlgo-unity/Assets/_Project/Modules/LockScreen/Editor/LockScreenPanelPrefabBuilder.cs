using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.LockScreen.UI;

namespace LoveAlgo.LockScreen.EditorTools
{
    /// <summary>
    /// LockScreenPanel prefab 자동 빌드 — 기획서 + 목업 + PNG 14개 통합.
    ///
    /// 구조 (1920×1080 기준):
    ///   LockScreenPanel (CanvasGroup, fullscreen)
    ///   ├ Background (bg_normal)
    ///   ├ Clock (TimeText "23:58", 중앙 상단)
    ///   ├ LeftWidgets (좌측 그룹 — slideOut 대상)
    ///   │ ├ WarnWidget   (warn + WarningShakeWidget)
    ///   │ ├ AudioWidget  (audio)
    ///   │ └ ToDoWidget   (todo + ToDoEntry × 3)
    ///   ├ RoaMessage (4 슬롯, 위에서 아래)
    ///   ├ LoginDim (bg_overlay, CanvasGroup α=0)  ← LoginStage 밖
    ///   ├ LoginStage (CanvasGroup, 비활성)
    ///   │ ├ HeaderText
    ///   │ ├ PasswordInput (InputBox + InputField + Key-left + Reveal)
    ///   │ ├ LoginButton (active/deact swap)
    ///   │ └ KeyIcon (password_key, 우하단, 분실 시만 활성)
    ///   ├ InputCatcher (전체 클릭, 메시지 4개 후 활성)
    ///   └ BlackOverlay (검정 Outro, CanvasGroup α=0)
    ///
    /// 메뉴: Tools > LockScreen > Build LockScreenPanel Prefab
    /// </summary>
    public static class LockScreenPanelPrefabBuilder
    {
        const string PrefabPath = "Assets/_Project/Modules/LockScreen/Prefabs/LockScreenPanel.prefab";
        const string PrefabDir  = "Assets/_Project/Modules/LockScreen/Prefabs";
        const string ArtDir     = "Assets/_Project/Modules/LockScreen/Art/LockScreen";

        // ── 디자인 const (목업 기준, 1920×1080) ──
        const float CanvasW = 1920, CanvasH = 1080;
        // 시계
        const float ClockY = -120f;  // 상단에서 아래로
        const int ClockFontSize = 180;
        // 입력칸
        const float InputBoxY = 0f;       // 화면 중앙
        const float InputBoxW = 600f, InputBoxH = 80f;
        // 로그인 버튼
        const float LoginBtnY = -130f;    // 입력칸 아래
        const float LoginBtnW = 220f, LoginBtnH = 80f;
        // 좌측 위젯 가로 정렬 (x = -660 = 화면 좌측에서 ~300px)
        const float LeftWidgetX = -660f;
        const float WarnY  = 280f;        // 상
        const float AudioY = 0f;          // 중
        const float ToDoY  = -260f;       // 하
        const float WidgetW = 320f;
        const float WarnH  = 220f;
        const float AudioH = 100f;
        const float ToDoH  = 220f;
        // 메시지 (4슬롯, 중앙 하단)
        const float MessageX = 0f;
        const float MessageBaseY = -340f;     // 가장 아래(최신) Y
        const float MessageSpacingY = 38f;    // 슬롯 간격(위로 갈수록 살짝 위)
        const float MessageW = 1100f;
        const float MessageH = 180f;

        [MenuItem("Tools/LockScreen/Build LockScreenPanel Prefab")]
        public static void Build()
        {
            // 1. 스프라이트 로드
            var sprites = new SpriteSet();
            if (!sprites.LoadAll())
            {
                EditorUtility.DisplayDialog("PNG 누락",
                    $"필수 스프라이트 누락. 경로 확인:\n{ArtDir}\n\n누락:\n{sprites.MissingReport()}",
                    "OK");
                return;
            }

            // 2. 기존 prefab 확인
            bool existed = File.Exists(PrefabPath);
            if (existed)
            {
                bool ok = EditorUtility.DisplayDialog("Prefab 재구성",
                    $"기존 prefab 덮어씁니다:\n{PrefabPath}\n\n계속할까요?", "재구성", "취소");
                if (!ok) return;
            }

            // 3. 루트
            var root = new GameObject("LockScreenPanel", typeof(RectTransform), typeof(CanvasGroup));
            var rootRT = SetupFullscreen(root.GetComponent<RectTransform>());
            rootRT.sizeDelta = new Vector2(CanvasW, CanvasH);

            // 4. 자식들 빌드
            var bg          = BuildBackground(root, sprites);
            var clock       = BuildClock(root);
            var leftRoot    = BuildLeftWidgetsContainer(root);
            var warn        = BuildWarnWidget(leftRoot, sprites);
            var audio       = BuildAudioWidget(leftRoot, sprites);
            var todo        = BuildToDoWidget(leftRoot, sprites);
            var roaMessage  = BuildRoaMessageWidget(root, sprites);
            var loginDim    = BuildLoginDim(root, sprites);                // LoginStage 밖
            var loginStage  = BuildLoginStage(root, sprites, out var hint, out var pwd, out var loginBtn, out var keyIcon);
            var catcher     = BuildInputCatcher(root);
            var blackOver   = BuildBlackOverlay(root);

            // 5. PhoneNotificationButton 컴포넌트 추가 + 필드 바인딩
            var panel = root.AddComponent<LockScreenPanel>();
            var so = new SerializedObject(panel);
            SetRef(so, "rootCanvasGroup",  root.GetComponent<CanvasGroup>());
            SetRef(so, "clock",            clock);
            SetRef(so, "toDo",             todo);
            SetRef(so, "roaMessage",       roaMessage);
            SetRef(so, "passwordInput",    pwd);
            SetRef(so, "headerText",       hint);
            SetRef(so, "loginStage",       loginStage);
            SetRef(so, "loginDim",         loginDim.GetComponent<CanvasGroup>());
            SetRef(so, "inputCatcher",     catcher);
            SetRef(so, "blackOverlay",     blackOver.GetComponent<CanvasGroup>());

            // leftWidgets (RectTransform 리스트) — Warn/Audio/ToDo 순
            var leftList = so.FindProperty("leftWidgets");
            if (leftList != null)
            {
                leftList.arraySize = 3;
                leftList.GetArrayElementAtIndex(0).objectReferenceValue = warn.GetComponent<RectTransform>();
                leftList.GetArrayElementAtIndex(1).objectReferenceValue = audio.GetComponent<RectTransform>();
                leftList.GetArrayElementAtIndex(2).objectReferenceValue = todo.GetComponent<RectTransform>();
            }
            so.ApplyModifiedProperties();

            // 6. 초기 상태 — LoginStage 비활성, LoginDim α=0, InputCatcher 비활성, BlackOverlay α=0
            loginStage.SetActive(false);
            loginDim.GetComponent<CanvasGroup>().alpha = 0f;
            catcher.gameObject.SetActive(false);
            blackOver.GetComponent<CanvasGroup>().alpha = 0f;
            keyIcon.SetActive(false);

            // 7. Prefab 저장
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            Debug.Log($"[LockScreenPrefabBuilder] {(existed ? "재구성" : "신규 생성")} 완료: {PrefabPath}");
        }

        // ══════════════════════════════════════════════
        //  스프라이트 로더
        // ══════════════════════════════════════════════
        class SpriteSet
        {
            public Sprite bgNormal, bgOverlay;
            public Sprite warn, audio, todo, todoCheck;
            public Sprite messageBox, messageHeader;
            public Sprite passwordBox, passwordHidden, passwordView, passwordKey;
            public Sprite btnLoginActive, btnLoginDeact;

            public bool LoadAll()
            {
                bgNormal       = Load("bg_normal");
                bgOverlay      = Load("bg_overlay");
                warn           = Load("warn widget");
                audio          = Load("audio widget");
                todo           = Load("todo widget");
                todoCheck      = Load("todo checkbox");
                messageBox     = Load("message_box");
                messageHeader  = Load("message_header");
                passwordBox    = Load("password_box");
                passwordHidden = Load("password_hidden");
                passwordView   = Load("password_view");
                passwordKey    = Load("password_key");
                btnLoginActive = Load("btn_login_active");
                btnLoginDeact  = Load("btn_login_deact");
                return bgNormal && bgOverlay && warn && audio && todo && todoCheck
                    && messageBox && messageHeader && passwordBox && passwordHidden
                    && passwordView && passwordKey && btnLoginActive && btnLoginDeact;
            }

            public string MissingReport()
            {
                var sb = new System.Text.StringBuilder();
                Add(sb, "bg_normal", bgNormal); Add(sb, "bg_overlay", bgOverlay);
                Add(sb, "warn widget", warn); Add(sb, "audio widget", audio);
                Add(sb, "todo widget", todo); Add(sb, "todo checkbox", todoCheck);
                Add(sb, "message_box", messageBox); Add(sb, "message_header", messageHeader);
                Add(sb, "password_box", passwordBox); Add(sb, "password_hidden", passwordHidden);
                Add(sb, "password_view", passwordView); Add(sb, "password_key", passwordKey);
                Add(sb, "btn_login_active", btnLoginActive); Add(sb, "btn_login_deact", btnLoginDeact);
                return sb.ToString();
            }
            static void Add(System.Text.StringBuilder sb, string name, Sprite s)
            { if (s == null) sb.AppendLine("  • " + name + ".png"); }
            static Sprite Load(string name)
                => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
        }

        // ══════════════════════════════════════════════
        //  하위 빌더
        // ══════════════════════════════════════════════

        static GameObject BuildBackground(GameObject parent, SpriteSet s)
        {
            var go = MakeImage("Background", parent.transform, s.bgNormal, fill: true);
            go.GetComponent<Image>().preserveAspect = false;
            return go;
        }

        static ClockWidget BuildClock(GameObject parent)
        {
            var go = new GameObject("Clock", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(800, 220);
            rt.anchoredPosition = new Vector2(0, ClockY);

            var timeGO = new GameObject("TimeText",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            timeGO.transform.SetParent(go.transform, false);
            var timeRT = timeGO.GetComponent<RectTransform>();
            timeRT.anchorMin = Vector2.zero; timeRT.anchorMax = Vector2.one;
            timeRT.offsetMin = Vector2.zero; timeRT.offsetMax = Vector2.zero;
            var time = timeGO.GetComponent<TextMeshProUGUI>();
            time.text = "23:58";
            time.fontSize = ClockFontSize;
            time.fontStyle = FontStyles.Bold;
            time.alignment = TextAlignmentOptions.Center;
            time.color = new Color(1, 1, 1, 0.92f);

            var cw = go.AddComponent<ClockWidget>();
            var so = new SerializedObject(cw);
            SetRef(so, "timeText", time);
            so.ApplyModifiedProperties();
            return cw;
        }

        static GameObject BuildLeftWidgetsContainer(GameObject parent)
        {
            // 단순 그룹 RT — leftWidgets 리스트는 자식 3개 RT 참조
            var go = new GameObject("LeftWidgets", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            SetupFullscreen(go.GetComponent<RectTransform>());
            return go;
        }

        static GameObject BuildWarnWidget(GameObject parent, SpriteSet s)
        {
            var go = MakeImage("WarnWidget", parent.transform, s.warn, fill: false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(WidgetW, WarnH);
            rt.anchoredPosition = new Vector2(LeftWidgetX, WarnY);

            var shake = go.AddComponent<WarningShakeWidget>();
            var so = new SerializedObject(shake);
            SetRef(so, "target", rt);
            so.ApplyModifiedProperties();
            return go;
        }

        static GameObject BuildAudioWidget(GameObject parent, SpriteSet s)
        {
            var go = MakeImage("AudioWidget", parent.transform, s.audio, fill: false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(WidgetW, AudioH);
            rt.anchoredPosition = new Vector2(LeftWidgetX, AudioY);
            return go;
        }

        static ToDoWidget BuildToDoWidget(GameObject parent, SpriteSet s)
        {
            var go = MakeImage("ToDoWidget", parent.transform, s.todo, fill: false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(WidgetW, ToDoH);
            rt.anchoredPosition = new Vector2(LeftWidgetX, ToDoY);

            // 3개 ToDoEntry — Toggle (checkbox + label)
            var entries = new List<ToDoEntry>(3);
            const float entryYStart = 30f;
            const float entryDY = -42f;
            for (int i = 0; i < 3; i++)
            {
                var entry = BuildToDoEntry(go, s, $"Entry_{i + 1}", new Vector2(20, entryYStart + entryDY * i));
                entries.Add(entry);
            }

            var widget = go.AddComponent<ToDoWidget>();
            var so = new SerializedObject(widget);
            var prop = so.FindProperty("entrySlots");
            if (prop != null)
            {
                prop.arraySize = entries.Count;
                for (int i = 0; i < entries.Count; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            }
            so.ApplyModifiedProperties();
            return widget;
        }

        static ToDoEntry BuildToDoEntry(GameObject parent, SpriteSet s, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot     = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(280, 32);
            rt.anchoredPosition = anchoredPos;

            // 체크박스 (Toggle)
            var togGO = new GameObject("Checkbox",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
            togGO.transform.SetParent(go.transform, false);
            var togRT = togGO.GetComponent<RectTransform>();
            togRT.anchorMin = new Vector2(0, 0.5f);
            togRT.anchorMax = new Vector2(0, 0.5f);
            togRT.pivot     = new Vector2(0, 0.5f);
            togRT.sizeDelta = new Vector2(24, 24);
            togRT.anchoredPosition = new Vector2(0, 0);
            var togImg = togGO.GetComponent<Image>();
            togImg.sprite = s.todoCheck;
            togImg.preserveAspect = true;
            var tog = togGO.GetComponent<Toggle>();
            tog.targetGraphic = togImg;
            tog.isOn = false;

            // 라벨 (TMP)
            var labGO = new GameObject("Label",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labGO.transform.SetParent(go.transform, false);
            var labRT = labGO.GetComponent<RectTransform>();
            labRT.anchorMin = new Vector2(0, 0.5f);
            labRT.anchorMax = new Vector2(1, 0.5f);
            labRT.pivot     = new Vector2(0, 0.5f);
            labRT.sizeDelta = new Vector2(0, 28);
            labRT.anchoredPosition = new Vector2(36, 0);
            labRT.offsetMax = new Vector2(0, labRT.offsetMax.y);
            var lab = labGO.GetComponent<TextMeshProUGUI>();
            lab.text = "할 일";
            lab.fontSize = 18;
            lab.color = new Color(1, 1, 1, 0.95f);
            lab.alignment = TextAlignmentOptions.MidlineLeft;

            var entry = go.AddComponent<ToDoEntry>();
            var so = new SerializedObject(entry);
            SetRef(so, "checkbox", tog);
            SetRef(so, "label",    lab);
            so.ApplyModifiedProperties();
            return entry;
        }

        static RoaMessageWidget BuildRoaMessageWidget(GameObject parent, SpriteSet s)
        {
            var go = new GameObject("RoaMessage", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(MessageW, 4 * (MessageH + MessageSpacingY));
            rt.anchoredPosition = new Vector2(MessageX, MessageBaseY);

            // 4 슬롯 — 인덱스 0이 가장 위(오래된), 3이 가장 아래(최신)
            var widget = go.AddComponent<RoaMessageWidget>();
            var so = new SerializedObject(widget);
            var slotProp = so.FindProperty("slots");
            slotProp.arraySize = 4;

            for (int i = 0; i < 4; i++)
            {
                // 슬롯 부모
                var slotGO = new GameObject($"Slot_{i}",
                    typeof(RectTransform), typeof(CanvasGroup));
                slotGO.transform.SetParent(go.transform, false);
                var slotRT = slotGO.GetComponent<RectTransform>();
                slotRT.anchorMin = slotRT.anchorMax = slotRT.pivot = new Vector2(0.5f, 0.5f);
                slotRT.sizeDelta = new Vector2(MessageW, MessageH);
                // 아래(최신)일수록 Y 낮음. i=3 → y=0(최하단), i=0 → y=가장 위
                float y = (3 - i) * (MessageH + MessageSpacingY) - (3 * (MessageH + MessageSpacingY)) / 2f;
                slotRT.anchoredPosition = new Vector2(0, -y);

                var slotCG = slotGO.GetComponent<CanvasGroup>();
                slotCG.alpha = 0f;

                // 박스 이미지
                var box = MakeImage("Box", slotGO.transform, s.messageBox, fill: true);
                box.GetComponent<Image>().preserveAspect = false;

                // 헤더 (좌상단)
                var hdr = MakeImage("Header", slotGO.transform, s.messageHeader, fill: false);
                var hdrRT = hdr.GetComponent<RectTransform>();
                hdrRT.anchorMin = new Vector2(0, 1);
                hdrRT.anchorMax = new Vector2(0, 1);
                hdrRT.pivot     = new Vector2(0, 1);
                hdrRT.sizeDelta = new Vector2(280, 36);
                hdrRT.anchoredPosition = new Vector2(28, 0);

                // 본문 텍스트
                var bodyGO = new GameObject("Body",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                bodyGO.transform.SetParent(slotGO.transform, false);
                var bodyRT = bodyGO.GetComponent<RectTransform>();
                bodyRT.anchorMin = new Vector2(0, 0);
                bodyRT.anchorMax = new Vector2(1, 1);
                bodyRT.pivot     = new Vector2(0.5f, 0.5f);
                bodyRT.offsetMin = new Vector2(40, 20);
                bodyRT.offsetMax = new Vector2(-60, -56);
                var body = bodyGO.GetComponent<TextMeshProUGUI>();
                body.text = "Message from ROA";
                body.fontSize = 24;
                body.fontStyle = FontStyles.Bold;
                body.color = Color.white;
                body.alignment = TextAlignmentOptions.TopLeft;

                // SerializedObject로 슬롯 데이터 바인딩 (MessageSlot 내부 클래스)
                var slotElem = slotProp.GetArrayElementAtIndex(i);
                slotElem.FindPropertyRelative("group").objectReferenceValue = slotCG;
                slotElem.FindPropertyRelative("rect").objectReferenceValue  = slotRT;
                slotElem.FindPropertyRelative("text").objectReferenceValue  = body;
            }
            so.ApplyModifiedProperties();
            return widget;
        }

        static GameObject BuildLoginDim(GameObject parent, SpriteSet s)
        {
            var go = new GameObject("LoginDim",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent.transform, false);
            SetupFullscreen(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.sprite = s.bgOverlay;
            img.preserveAspect = false;
            img.raycastTarget = false;
            go.GetComponent<CanvasGroup>().alpha = 0f;
            return go;
        }

        static GameObject BuildLoginStage(GameObject parent, SpriteSet s,
            out TMP_Text headerText, out PasswordInputWidget passwordInput,
            out Button loginButton, out GameObject keyIcon)
        {
            var go = new GameObject("LoginStage",
                typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent.transform, false);
            SetupFullscreen(go.GetComponent<RectTransform>());

            // 안내 텍스트 (입력칸 위)
            var hintGO = new GameObject("HeaderText",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            hintGO.transform.SetParent(go.transform, false);
            var hintRT = hintGO.GetComponent<RectTransform>();
            hintRT.anchorMin = hintRT.anchorMax = hintRT.pivot = new Vector2(0.5f, 0.5f);
            hintRT.sizeDelta = new Vector2(900, 60);
            hintRT.anchoredPosition = new Vector2(0, InputBoxY + 70);
            headerText = hintGO.GetComponent<TextMeshProUGUI>();
            headerText.text = "앞으로 사용할 비밀번호를 입력해주세요. 최대 7자까지 입력 가능합니다.";
            headerText.fontSize = 22;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.color = Color.white;

            // PasswordInput root
            var pwdGO = new GameObject("PasswordInput", typeof(RectTransform));
            pwdGO.transform.SetParent(go.transform, false);
            var pwdRT = pwdGO.GetComponent<RectTransform>();
            pwdRT.anchorMin = pwdRT.anchorMax = pwdRT.pivot = new Vector2(0.5f, 0.5f);
            pwdRT.sizeDelta = new Vector2(InputBoxW, InputBoxH);
            pwdRT.anchoredPosition = new Vector2(0, InputBoxY);

            // 입력칸 박스
            var inputBoxImg = MakeImage("InputBox", pwdGO.transform, s.passwordBox, fill: true);
            inputBoxImg.GetComponent<Image>().preserveAspect = false;
            inputBoxImg.GetComponent<Image>().raycastTarget = false;

            // 좌측 데코 키 아이콘 (입력칸 안)
            var keyLeftGO = MakeImage("KeyLeftDecor", pwdGO.transform, s.passwordKey, fill: false);
            var keyLeftRT = keyLeftGO.GetComponent<RectTransform>();
            keyLeftRT.anchorMin = new Vector2(0, 0.5f);
            keyLeftRT.anchorMax = new Vector2(0, 0.5f);
            keyLeftRT.pivot     = new Vector2(0, 0.5f);
            keyLeftRT.sizeDelta = new Vector2(40, 40);
            keyLeftRT.anchoredPosition = new Vector2(20, 0);
            keyLeftGO.GetComponent<Image>().raycastTarget = false;

            // TMP_InputField — 박스 내부
            var inputGO = new GameObject("InputField",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
            inputGO.transform.SetParent(pwdGO.transform, false);
            var inputRT = inputGO.GetComponent<RectTransform>();
            inputRT.anchorMin = new Vector2(0, 0); inputRT.anchorMax = new Vector2(1, 1);
            inputRT.offsetMin = new Vector2(80, 10); inputRT.offsetMax = new Vector2(-80, -10);
            var inputImg = inputGO.GetComponent<Image>();
            inputImg.color = new Color(0, 0, 0, 0);   // 투명 — passwordBox가 시각
            var input = inputGO.GetComponent<TMP_InputField>();

            // TextArea (TMP_InputField 필수 자식)
            var taGO = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            taGO.transform.SetParent(inputGO.transform, false);
            var taRT = taGO.GetComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
            taRT.offsetMin = Vector2.zero; taRT.offsetMax = Vector2.zero;

            // Placeholder
            var phGO = new GameObject("Placeholder",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            phGO.transform.SetParent(taGO.transform, false);
            var phRT = phGO.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
            var ph = phGO.GetComponent<TextMeshProUGUI>();
            ph.text = "";
            ph.fontSize = 28;
            ph.color = new Color(1, 1, 1, 0.4f);
            ph.alignment = TextAlignmentOptions.Center;

            // Text (실제 입력 표시)
            var txtGO = new GameObject("Text",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(taGO.transform, false);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.fontSize = 32;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;

            // TMP_InputField 필드 바인딩
            input.textViewport = taRT;
            input.textComponent = txt;
            input.placeholder = ph;
            input.contentType = TMP_InputField.ContentType.Password;
            input.characterLimit = 7;
            input.asteriskChar = '*';

            // Reveal Toggle (우측 눈 아이콘)
            var revGO = new GameObject("RevealToggle",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
            revGO.transform.SetParent(pwdGO.transform, false);
            var revRT = revGO.GetComponent<RectTransform>();
            revRT.anchorMin = new Vector2(1, 0.5f);
            revRT.anchorMax = new Vector2(1, 0.5f);
            revRT.pivot     = new Vector2(1, 0.5f);
            revRT.sizeDelta = new Vector2(40, 40);
            revRT.anchoredPosition = new Vector2(-20, 0);
            var revImg = revGO.GetComponent<Image>();
            revImg.sprite = s.passwordHidden;     // 기본 감은 눈
            revImg.preserveAspect = true;
            var rev = revGO.GetComponent<Toggle>();
            rev.targetGraphic = revImg;
            rev.isOn = false;

            // LOGIN 버튼 — 입력칸 아래
            var btnGO = new GameObject("LoginButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(go.transform, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = btnRT.anchorMax = btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(LoginBtnW, LoginBtnH);
            btnRT.anchoredPosition = new Vector2(0, InputBoxY + LoginBtnY);
            var btnImg = btnGO.GetComponent<Image>();
            btnImg.sprite = s.btnLoginActive;
            btnImg.preserveAspect = true;
            loginButton = btnGO.GetComponent<Button>();
            loginButton.targetGraphic = btnImg;
            // SpriteState — 비활성 상태 sprite 사용
            var ss = new SpriteState
            {
                disabledSprite = s.btnLoginDeact,
                pressedSprite  = s.btnLoginActive,
                highlightedSprite = s.btnLoginActive,
                selectedSprite = s.btnLoginActive,
            };
            loginButton.spriteState = ss;
            loginButton.transition = Selectable.Transition.SpriteSwap;

            // 우하단 키 아이콘 (분실 시 활성)
            keyIcon = MakeImage("KeyIcon", go.transform, s.passwordKey, fill: false);
            var keyRT = keyIcon.GetComponent<RectTransform>();
            keyRT.anchorMin = new Vector2(1, 0);
            keyRT.anchorMax = new Vector2(1, 0);
            keyRT.pivot     = new Vector2(1, 0);
            keyRT.sizeDelta = new Vector2(80, 80);
            keyRT.anchoredPosition = new Vector2(-60, 60);
            var keyBtnComp = keyIcon.AddComponent<Button>();
            keyBtnComp.targetGraphic = keyIcon.GetComponent<Image>();

            // PasswordInputWidget 컴포넌트 + 필드 바인딩
            passwordInput = pwdGO.AddComponent<PasswordInputWidget>();
            var so = new SerializedObject(passwordInput);
            SetRef(so, "inputField",    input);
            SetRef(so, "confirmButton", loginButton);
            SetRef(so, "revealToggle",  rev);
            SetRef(so, "keyIcon",       keyIcon);
            SetRef(so, "keyButton",     keyBtnComp);
            SetRef(so, "shakeTarget",   pwdRT);
            so.ApplyModifiedProperties();

            return go;
        }

        static Button BuildInputCatcher(GameObject parent)
        {
            var go = new GameObject("InputCatcher",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            SetupFullscreen(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0);   // 투명 — raycast만
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            go.SetActive(false);
            return btn;
        }

        static GameObject BuildBlackOverlay(GameObject parent)
        {
            var go = new GameObject("BlackOverlay",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent.transform, false);
            SetupFullscreen(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            go.GetComponent<CanvasGroup>().alpha = 0f;
            return go;
        }

        // ══════════════════════════════════════════════
        //  공통 헬퍼
        // ══════════════════════════════════════════════
        static RectTransform SetupFullscreen(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static GameObject MakeImage(string name, Transform parent, Sprite sprite, bool fill)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (fill)
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            }
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return go;
        }

        static void SetRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[LockScreenPrefabBuilder] '{fieldName}' 필드 못 찾음 — 스킵");
                return;
            }
            prop.objectReferenceValue = value;
        }
    }
}
