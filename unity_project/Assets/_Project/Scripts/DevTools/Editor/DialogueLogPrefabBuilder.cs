using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI; // DialogueLogView, DialogueLogEntrySlot

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 대사 로그 팝업 프리팹 조립 도구(Tools ▸ Log ▸ Build Log Popup Prefab) — MessengerPrefabBuilder 패턴 미러.
    /// 씬 대신 프리팹 산출(재실행 = 같은 경로 덮어쓰기, GUID 보존), 씬 배선은 후속(_UI/Popup + Activator).
    /// 슬롯 3종 프리팹(Prefabs/Log/LogEntry*)은 기존 산출물을 참조 바인딩. 위치/크기는 시작값 —
    /// 비주얼 튜닝은 감독 영역(🟢, 목업 = Assets/Art/로그/Mockup).
    /// </summary>
    public static class DialogueLogPrefabBuilder
    {
        const string ArtDir = "Assets/Art/로그/png";
        const string PrefabDir = "Assets/_Project/Prefabs/Log";
        const string BodyFont = "Assets/Fonts/Pretendard-Medium SDF.asset";
        const string NarrationGlowMat = "Assets/_Project/Prefabs/Log/Mat/NarrationGlow.mat";

        [MenuItem("Tools/Log/Build Log Popup Prefab")]
        public static void Build()
        {
            EnsureSpriteImports();
            MessengerPrefabBuilder.EnsureFolder(PrefabDir);

            var charSlot = LoadSlot("LogEntryCharacter");
            var playerSlot = LoadSlot("LogEntryPlayer");
            var narrationSlot = LoadSlot("LogEntryNarration");

            var popup = BuildPopup(charSlot, playerSlot, narrationSlot);
            var prefab = PrefabUtility.SaveAsPrefabAsset(popup, $"{PrefabDir}/LogPopup.prefab");
            Object.DestroyImmediate(popup);

            AssetDatabase.SaveAssets();
            Debug.Log($"[DialogueLogPrefabBuilder] 산출 완료 → {PrefabDir}/LogPopup.prefab (슬롯 3종 바인딩 + 초상 5종). " +
                      "씬 배선: _UI/Popup 하위 인스턴스(inactive) + UiBootActivator targets 등록.");
            _ = prefab;
        }

        /// <summary>임시 조립 잔재 정리: 열린 씬 루트의 LogPopup/LogEntry* 임시 GO + SettingsPopup 패널에
        /// 잘못 들어간 미아 Return Button(MCP 부모 오인 사고) 제거.</summary>
        [MenuItem("Tools/Log/Cleanup Temp Log Objects In Open Scene")]
        public static void CleanupTemp()
        {
            int removed = 0;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "LogPopup" || root.name.StartsWith("LogEntry"))
                {
                    Object.DestroyImmediate(root);
                    removed++;
                }
            }
            // 미아 Return Button — SettingsPopup 하위에 있을 이유가 없는 이름이라 정확 경로 매칭으로 제거
            // (타입 비의존: DevTools.Editor가 Settings asmdef를 참조하지 않음).
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
            {
                if (rt.name != "SettingsPopup") continue;
                var stray = rt.Find("Panel/Return Button");
                if (stray != null) { Object.DestroyImmediate(stray.gameObject); removed++; }
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[DialogueLogPrefabBuilder] 임시 오브젝트 {removed}건 제거.");
        }

        // ───────────────────────── 슬롯 3종 조립 ─────────────────────────

        /// <summary>로그 박스 슬롯 3종(캐릭터/플레이어/나레이션) 조립 — 목업 동결 규칙: 좌 초상+이름박스 열(고정폭)
        /// + 우 와이드 대화박스(높이 = 본문 TMP preferred, 초상열보다 낮으면 세로 가운데 정렬), 주인공 = 초상 없이
        /// 이름박스+전용 박스(흰 본문), 독백 = 박스 없는 흰 텍스트(박스 열과 좌측 정렬 일치). 재실행 = 같은 경로
        /// 덮어쓰기(GUID 보존). 내부 fileID는 바뀌므로 LogPopup.prefab과 열린 씬의 뷰 슬롯 참조를 함께 재바인딩한다
        /// (씬 저장은 호출자 몫). 수치는 시작값 — 비주얼 튜닝은 감독 영역(🟢).</summary>
        [MenuItem("Tools/Log/Build Log Entry Slot Prefabs")]
        public static void BuildSlots()
        {
            EnsureSpriteImports();
            EnsureSlicedBorders();
            MessengerPrefabBuilder.EnsureFolder(PrefabDir);

            SaveSlot(BuildCharacterSlot(), "LogEntryCharacter");
            SaveSlot(BuildPlayerSlot(), "LogEntryPlayer");
            SaveSlot(BuildNarrationSlot(), "LogEntryNarration");
            RebindSlotConsumers();

            AssetDatabase.SaveAssets();
            Debug.Log("[DialogueLogPrefabBuilder] 슬롯 3종 재조립 + LogPopup.prefab/열린 씬 뷰 참조 재바인딩 완료. " +
                      "씬이 더럽혀졌으면 저장 필요.");
        }

        const float LeftColWidth = 150f; // 초상/이름박스 열 폭(목업 비례 시작값)

        static GameObject BuildCharacterSlot()
        {
            var (root, slot) = SlotRoot("LogEntryCharacter");
            var left = LeftColumn(root.transform, TextAnchor.UpperCenter);

            var portrait = MessengerPrefabBuilder.Rect("Portrait", left.transform);
            var portraitImg = portrait.AddComponent<Image>(); // 스프라이트는 런타임 주입(뷰의 id→초상 매핑)
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            Le(portrait, preferredWidth: LeftColWidth, preferredHeight: LeftColWidth); // 아트 230x229 ≈ 정사각

            var nameLabel = NameBox(left.transform, "namebox_character");
            var body = TextBoxColumn(root.transform, "textbox_character", new Color32(26, 26, 26, 255)); // 본문 검정(기획)

            slot.PortraitRoot = portrait;
            slot.PortraitImage = portraitImg;
            slot.NameRoot = nameLabel.transform.parent.gameObject; // NameBox GO — 연속 동일 화자 시 끔
            slot.NameText = nameLabel;
            slot.BodyText = body;
            return root;
        }

        static GameObject BuildPlayerSlot()
        {
            var (root, slot) = SlotRoot("LogEntryPlayer");
            var left = LeftColumn(root.transform, TextAnchor.MiddleCenter);
            var nameLabel = NameBox(left.transform, "namebox_player");
            var body = TextBoxColumn(root.transform, "textbox_player", Color.white); // 주인공 전부 흰색(기획)

            slot.NameRoot = nameLabel.transform.parent.gameObject; // NameBox GO — 연속 동일 화자 시 끔
            slot.NameText = nameLabel;
            slot.BodyText = body;
            return root;
        }

        static GameObject BuildNarrationSlot()
        {
            var (root, slot) = SlotRoot("LogEntryNarration");
            var spacer = MessengerPrefabBuilder.Rect("LeftSpacer", root.transform); // 박스 열과 좌측 정렬 일치용
            Le(spacer, preferredWidth: LeftColWidth, flexibleWidth: 0f);
            var body = Tmp("Body", root.transform, 28f, Color.white, TextAlignmentOptions.TopLeft);
            Le(body.gameObject, flexibleWidth: 1f);
            // 독백 = 박스 없이 글자에만 분홍 번짐(목업: "텍스트 그림자효과"). 캐릭터/플레이어 박스와 달리 배경 없음.
            var glow = EnsureNarrationGlowMaterial();
            if (glow != null) body.fontSharedMaterial = glow;

            slot.BodyText = body;
            return root;
        }

        // 슬롯 공통 루트: 가로 [좌측 열 | 본문]. Content VLG(ctrlH)가 preferred 높이로 행을 쌓는다.
        static (GameObject root, DialogueLogEntrySlot slot) SlotRoot(string name)
        {
            var root = MessengerPrefabBuilder.Rect(name, null);
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft; // 박스가 초상열보다 낮으면 가운데 정렬(목업 규칙)
            hlg.spacing = 24f;
            hlg.padding = new RectOffset(8, 16, 0, 0);
            return (root, root.AddComponent<DialogueLogEntrySlot>());
        }

        static GameObject LeftColumn(Transform parent, TextAnchor align)
        {
            var col = MessengerPrefabBuilder.Rect("Left", parent);
            Le(col, preferredWidth: LeftColWidth, flexibleWidth: 0f);
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = align;
            vlg.spacing = 6f;
            return col;
        }

        // 이름박스(9-슬라이스) + 중앙 라벨. 라벨 자동 축소(긴 이름 대비).
        static TMP_Text NameBox(Transform parent, string spriteName)
        {
            var box = MessengerPrefabBuilder.Rect("NameBox", parent);
            var img = Img(box, spriteName);
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            Le(box, preferredWidth: LeftColWidth, preferredHeight: 52f);

            var label = Tmp("NameLabel", box.transform, 22f, Color.white, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 22f;
            MessengerPrefabBuilder.Stretch(label.gameObject);
            var lr = (RectTransform)label.transform;
            lr.offsetMin = new Vector2(10f, 4f);
            lr.offsetMax = new Vector2(-10f, -4f);
            return label;
        }

        // 대화박스(9-슬라이스, 남은 폭 채움) — 내부 VLG 패딩이 본문 TMP preferred 높이를 박스 높이로 승격.
        static TMP_Text TextBoxColumn(Transform parent, string spriteName, Color bodyColor)
        {
            var box = MessengerPrefabBuilder.Rect("TextBox", parent);
            var img = Img(box, spriteName);
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            Le(box, flexibleWidth: 1f);

            var vlg = box.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(36, 36, 16, 16);

            return Tmp("Body", box.transform, 28f, bodyColor, TextAlignmentOptions.TopLeft);
        }

        static TextMeshProUGUI Tmp(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var go = MessengerPrefabBuilder.Rect(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFont);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false; // 스크롤 레이캐스트는 뷰포트 캐처가 담당
            return t;
        }

        static LayoutElement Le(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth;
            return le;
        }

        static void SaveSlot(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
        }

        /// <summary>기존 캐릭터/플레이어 슬롯 프리팹에 NameRoot(NameBox GO) 참조를 비파괴로 배선 —
        /// 슬롯 전체 재조립 없이(다른 슬롯 수동 튜닝 보존). 연속 동일 화자 2번째+ 박스에서 이름표를 끄기 위함.
        /// 나레이션은 이름박스가 없어 대상 아님.</summary>
        [MenuItem("Tools/Log/Wire Name Roots")]
        public static void WireNameRoots()
        {
            int wired = 0;
            foreach (var name in new[] { "LogEntryCharacter", "LogEntryPlayer" })
            {
                string path = $"{PrefabDir}/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogError($"[DialogueLogPrefabBuilder] 슬롯 프리팹 없음: {path}");
                    continue;
                }
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var slot = contents.GetComponent<DialogueLogEntrySlot>();
                    var nameBox = FindDeep(contents.transform, "NameBox");
                    if (slot == null || nameBox == null)
                    {
                        Debug.LogError($"[DialogueLogPrefabBuilder] {name}: slot/NameBox 못 찾음 — 배선 생략.");
                        continue;
                    }
                    slot.NameRoot = nameBox.gameObject;
                    EditorUtility.SetDirty(slot);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    wired++;
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[DialogueLogPrefabBuilder] NameRoot 배선 {wired}건 완료(연속 동일 화자 이름표 묶음).");
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ───────────────────────── 독백 글로우 ─────────────────────────

        /// <summary>기존 LogEntryNarration 프리팹의 본문 TMP에만 분홍 번짐(글로우) 머티리얼을 입힌다 —
        /// 슬롯 3종 전체 재조립(BuildSlots) 없이 비파괴 적용(다른 슬롯의 감독 수동 튜닝 보존). 머티리얼 미존재면
        /// 생성, 있으면 갱신. 목업 동결: 독백은 배경 박스 없이 글자에만 효과(캐릭터/플레이어 박스와 구분).</summary>
        [MenuItem("Tools/Log/Apply Narration Glow")]
        public static void ApplyNarrationGlow()
        {
            var glow = EnsureNarrationGlowMaterial();
            if (glow == null) return;

            string path = $"{PrefabDir}/LogEntryNarration.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogError($"[DialogueLogPrefabBuilder] 나레이션 슬롯 프리팹 없음: {path}");
                return;
            }
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var slot = contents.GetComponent<DialogueLogEntrySlot>();
                var body = slot != null ? slot.BodyText : null;
                if (body == null) { Debug.LogError("[DialogueLogPrefabBuilder] BodyText 미바인딩 — 글로우 적용 실패."); return; }
                body.fontSharedMaterial = glow;
                EditorUtility.SetDirty(body);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }

            AssetDatabase.SaveAssets();
            Debug.Log($"[DialogueLogPrefabBuilder] 독백 글로우 적용 완료 → {path} (머티리얼 {NarrationGlowMat}). 농도/번짐은 머티리얼 인스펙터에서 감독 튜닝(🟢).");
        }

        /// <summary>독백 본문용 분홍 번짐(Underlay) 머티리얼 보장 — 본문 폰트 머티리얼 복제 + UNDERLAY_ON.
        /// offset 0(글자 둘레 균일 글로우), dilate/softness/alpha는 은은함 시작값(감독 튜닝 영역). 다른 텍스트가 공유하는
        /// 폰트 기본 머티리얼을 건드리지 않도록 전용 인스턴스를 에셋으로 둔다.</summary>
        static Material EnsureNarrationGlowMaterial()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFont);
            if (font == null || font.material == null)
            {
                Debug.LogWarning($"[DialogueLogPrefabBuilder] 본문 폰트/머티리얼 없음: {BodyFont} — 글로우 생략.");
                return null;
            }
            MessengerPrefabBuilder.EnsureFolder($"{PrefabDir}/Mat");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(NarrationGlowMat);
            if (mat == null)
            {
                mat = new Material(font.material) { name = "NarrationGlow" };
                AssetDatabase.CreateAsset(mat, NarrationGlowMat);
            }
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(1f, 0.45f, 0.7f, 0.5f)); // 은은한 분홍(α 0.5 시작값)
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", 0f);
            mat.SetFloat("_UnderlayDilate", 0.3f);   // 번짐 확장(0~1)
            mat.SetFloat("_UnderlaySoftness", 0.5f); // 가장자리 부드러움(0~1)
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>박스 아트 4종에 9-슬라이스 보더 보장(미설정일 때만 — 감독 수동 튜닝 보존).</summary>
        static void EnsureSlicedBorders()
        {
            foreach (var name in new[] { "textbox_character", "textbox_player", "namebox_character", "namebox_player" })
            {
                string path = $"{ArtDir}/{name}.png";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.spriteBorder != Vector4.zero) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                float b = Mathf.Min(30f, tex.height / 2f - 4f, tex.width / 2f - 4f);
                importer.spriteBorder = new Vector4(b, b, b, b);
                importer.SaveAndReimport();
            }
        }

        /// <summary>슬롯 재조립으로 내부 fileID가 바뀐 참조를 복구: LogPopup.prefab + 열린 씬의 DialogueLogView.
        /// 둘 다 갱신해 프리팹 전파/인스턴스 오버라이드/언팩 어느 경우든 안전. + 뷰포트 투명 캐처 보장
        /// (슬롯 그래픽 raycast를 꺼서 휠/드래그 스크롤 히트는 뷰포트가 받는다).</summary>
        static void RebindSlotConsumers()
        {
            var charSlot = LoadSlot("LogEntryCharacter");
            var playerSlot = LoadSlot("LogEntryPlayer");
            var narrationSlot = LoadSlot("LogEntryNarration");

            string popupPath = $"{PrefabDir}/LogPopup.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(popupPath) != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(popupPath);
                try
                {
                    var view = contents.GetComponentInChildren<DialogueLogView>(true);
                    if (view != null)
                    {
                        Rebind(view, charSlot, playerSlot, narrationSlot);
                        EnsureViewportCatcher(view);
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, popupPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }

            foreach (var view in Object.FindObjectsByType<DialogueLogView>(FindObjectsInactive.Include))
            {
                Rebind(view, charSlot, playerSlot, narrationSlot);
                EnsureViewportCatcher(view);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                EditorUtility.SetDirty(view);
            }
        }

        static void Rebind(DialogueLogView view, DialogueLogEntrySlot c, DialogueLogEntrySlot p, DialogueLogEntrySlot n)
        {
            view.CharacterSlotPrefab = c;
            view.PlayerSlotPrefab = p;
            view.NarrationSlotPrefab = n;
        }

        static void EnsureViewportCatcher(DialogueLogView view)
        {
            var viewport = view.Scroll != null ? view.Scroll.viewport : null;
            if (viewport == null || viewport.GetComponent<Graphic>() != null) return;
            var img = viewport.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f); // 투명 — 휠/드래그 스크롤 히트 캐처
            img.raycastTarget = true;
        }

        // ───────────────────────── 조립 ─────────────────────────

        static GameObject BuildPopup(DialogueLogEntrySlot charSlot, DialogueLogEntrySlot playerSlot, DialogueLogEntrySlot narrationSlot)
        {
            var popup = MessengerPrefabBuilder.Rect("LogPopup", null);
            MessengerPrefabBuilder.Stretch(popup);
            ((RectTransform)popup.transform).sizeDelta = new Vector2(1920, 1080); // 캔버스 밖 조립이라 명시
            var group = popup.AddComponent<CanvasGroup>();
            group.alpha = 0f; // 부팅 숨김 정합(뷰 Awake와 동일)
            group.interactable = false;
            group.blocksRaycasts = false;
            var view = popup.AddComponent<DialogueLogView>();

            // 딤(클릭 차단) + 패널
            var dim = MessengerPrefabBuilder.Rect("Dim", popup.transform);
            MessengerPrefabBuilder.Stretch(dim);
            Img(dim, "_dim").raycastTarget = true;

            var panel = MessengerPrefabBuilder.Rect("Panel", popup.transform);
            MessengerPrefabBuilder.Stretch(panel);
            var panelRt = (RectTransform)panel.transform;
            panelRt.offsetMin = new Vector2(80, 50);
            panelRt.offsetMax = new Vector2(-80, -50);
            Img(panel, "_panel").raycastTarget = true;

            // 돌아가기(우상단)
            var ret = MessengerPrefabBuilder.Rect("Return Button", panel.transform);
            var retRt = (RectTransform)ret.transform;
            retRt.anchorMin = retRt.anchorMax = retRt.pivot = new Vector2(1f, 1f);
            retRt.anchoredPosition = new Vector2(-48, -24);
            retRt.sizeDelta = new Vector2(200, 64);
            var retImg = Img(ret, "btn_return");
            retImg.preserveAspect = true;
            var retBtn = ret.AddComponent<Button>();

            // 스크롤 영역
            var scrollGo = MessengerPrefabBuilder.Rect("Scroll View", panel.transform);
            MessengerPrefabBuilder.Stretch(scrollGo);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.offsetMin = new Vector2(60, 60);    // left, bottom
            scrollRt.offsetMax = new Vector2(-120, -120); // right(스크롤바 자리), top(타이틀/버튼 띠)
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewport = MessengerPrefabBuilder.Rect("Viewport", scrollGo.transform);
            MessengerPrefabBuilder.Stretch(viewport);
            viewport.AddComponent<RectMask2D>();

            var content = MessengerPrefabBuilder.Rect("Content", viewport.transform);
            var contentRt = (RectTransform)content.transform;
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20f;
            vlg.padding = new RectOffset(0, 0, 10, 10);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 스크롤바(우측 — scrollbar 트랙 + scroll_btn 핸들)
            var bar = MessengerPrefabBuilder.Rect("Scrollbar", panel.transform);
            var barRt = (RectTransform)bar.transform;
            barRt.anchorMin = new Vector2(1, 0);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.pivot = new Vector2(1, 0.5f);
            barRt.anchoredPosition = new Vector2(-56, -30);
            barRt.sizeDelta = new Vector2(24, -240);
            Img(bar, "scrollbar").raycastTarget = true;
            var scrollbar = bar.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var sliding = MessengerPrefabBuilder.Rect("Sliding Area", bar.transform);
            MessengerPrefabBuilder.Stretch(sliding);
            var handle = MessengerPrefabBuilder.Rect("Handle", sliding.transform);
            MessengerPrefabBuilder.Stretch(handle);
            var handleImg = Img(handle, "scroll_btn");
            handleImg.preserveAspect = true;
            scrollbar.handleRect = (RectTransform)handle.transform;
            scrollbar.targetGraphic = handleImg;

            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRt;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // 뷰 바인딩
            view.Group = group;
            view.Content = content.transform;
            view.Scroll = scroll;
            view.ReturnButton = retBtn;
            view.CharacterSlotPrefab = charSlot;
            view.PlayerSlotPrefab = playerSlot;
            view.NarrationSlotPrefab = narrationSlot;
            view.Portraits.Clear();
            AddPortrait(view, "c01", "roa");
            AddPortrait(view, "c02", "daeun");
            AddPortrait(view, "c03", "yeeun");
            AddPortrait(view, "c04", "heewon");
            AddPortrait(view, "c05", "bom");

            return popup;
        }

        static void AddPortrait(DialogueLogView view, string id, string spriteName)
            => view.Portraits.Add(new DialogueLogView.PortraitPair { speakerId = id, sprite = LoadSprite(spriteName) });

        static DialogueLogEntrySlot LoadSlot(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
            if (go == null)
            {
                Debug.LogError($"[DialogueLogPrefabBuilder] 슬롯 프리팹 없음: {PrefabDir}/{name}.prefab");
                return null;
            }
            return go.GetComponent<DialogueLogEntrySlot>();
        }

        static Image Img(GameObject go, string spriteName)
        {
            var img = go.AddComponent<Image>();
            img.sprite = LoadSprite(spriteName);
            return img;
        }

        static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");
            if (sprite == null) Debug.LogWarning($"[DialogueLogPrefabBuilder] 스프라이트 없음: {ArtDir}/{name}.png");
            return sprite;
        }

        /// <summary>로그 아트 일괄 Sprite 임포트 보장(재실행 안전 — 이미 Sprite면 무변경).</summary>
        static void EnsureSpriteImports()
        {
            if (!Directory.Exists(ArtDir)) { Debug.LogWarning($"[DialogueLogPrefabBuilder] 아트 폴더 없음: {ArtDir}"); return; }
            foreach (var file in Directory.GetFiles(ArtDir, "*.png"))
            {
                var assetPath = file.Replace('\\', '/');
                if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) continue;
                if (importer.textureType == TextureImporterType.Sprite) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
        }
    }
}
