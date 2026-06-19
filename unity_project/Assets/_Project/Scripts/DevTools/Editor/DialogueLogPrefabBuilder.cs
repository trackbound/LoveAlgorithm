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

            var popup = BuildPopup(
                LoadSlot("LogSpeakerHeader"), LoadSlot("LogPlayerHeader"),
                LoadSlot("LogEntryCharacter"), LoadSlot("LogEntryPlayer"), LoadSlot("LogEntryNarration"));
            var prefab = PrefabUtility.SaveAsPrefabAsset(popup, $"{PrefabDir}/LogPopup.prefab");
            Object.DestroyImmediate(popup);

            AssetDatabase.SaveAssets();
            Debug.Log($"[DialogueLogPrefabBuilder] 산출 완료 → {PrefabDir}/LogPopup.prefab (헤더 2종+버블 3종 바인딩 + 초상 5종). " +
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

        // ───────────────────────── 헤더 2종 + 버블 3종 조립 ─────────────────────────

        /// <summary>로그 요소 프리팹 조립 — run 컨테이너 모델: 좌측 화자 헤더(캐릭터=초상+이름박스 / 플레이어=이름박스)
        /// 고정폭 + 우측 대사 버블(캐릭터 textbox 검정 / 플레이어 textbox 흰 / 독백 분홍 번짐 배경 흰). 컨테이너와
        /// 버블 세로 스택은 DialogueLogView가 런타임 조립(개행 균일·이름표 run당 1회). 재실행 = 같은 경로 덮어쓰기
        /// (GUID 보존), fileID 변경분은 RebindSlotConsumers가 LogPopup/열린 씬 뷰에 재배선. 수치=시작값(감독 튜닝 🟢).</summary>
        [MenuItem("Tools/Log/Build Log Entry Slot Prefabs")]
        public static void BuildSlots()
        {
            EnsureSpriteImports();
            EnsureSlicedBorders();
            MessengerPrefabBuilder.EnsureFolder(PrefabDir);

            SaveSlot(BuildSpeakerHeader(), "LogSpeakerHeader");
            SaveSlot(BuildPlayerHeader(), "LogPlayerHeader");
            SaveSlot(BuildBubble("LogEntryCharacter", "textbox_character", new Color32(26, 26, 26, 255)), "LogEntryCharacter"); // 본문 검정(기획)
            SaveSlot(BuildBubble("LogEntryPlayer", "textbox_player", Color.white), "LogEntryPlayer");                          // 주인공 흰색(기획)
            SaveSlot(BuildNarrationBubble(), "LogEntryNarration");
            RebindSlotConsumers();

            AssetDatabase.SaveAssets();
            Debug.Log("[DialogueLogPrefabBuilder] 헤더 2종+버블 3종 재조립 + LogPopup.prefab/열린 씬 뷰 참조 재바인딩 완료. " +
                      "씬이 더럽혀졌으면 저장 필요.");
        }

        const float LeftColWidth = 150f; // 초상/이름박스 열 폭(목업 비례 시작값)

        // 캐릭터 헤더: 세로 [초상 | 이름박스]. run 좌측에 1회. 초상은 런타임 주입(뷰의 id→초상 매핑, 엑스트라=숨김).
        static GameObject BuildSpeakerHeader()
        {
            var (root, slot) = HeaderRoot("LogSpeakerHeader");
            var portrait = MessengerPrefabBuilder.Rect("Portrait", root.transform);
            var portraitImg = portrait.AddComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            Le(portrait, preferredWidth: LeftColWidth, preferredHeight: LeftColWidth); // 아트 230x229 ≈ 정사각
            var nameLabel = NameBox(root.transform, "namebox_character");

            slot.PortraitRoot = portrait;
            slot.PortraitImage = portraitImg;
            slot.NameText = nameLabel;
            return root;
        }

        // 플레이어 헤더: 이름박스만(초상 없음). 전용 이름박스 아트.
        static GameObject BuildPlayerHeader()
        {
            var (root, slot) = HeaderRoot("LogPlayerHeader");
            slot.NameText = NameBox(root.transform, "namebox_player");
            return root;
        }

        // 헤더 공통 루트: 고정폭 세로 열(상단 정렬).
        static (GameObject root, DialogueLogEntrySlot slot) HeaderRoot(string name)
        {
            var root = MessengerPrefabBuilder.Rect(name, null);
            Le(root, preferredWidth: LeftColWidth, flexibleWidth: 0f);
            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 6f;
            return (root, root.AddComponent<DialogueLogEntrySlot>());
        }

        // 대사 버블(9-슬라이스 박스 + 본문). 루트가 곧 버블 — 폭은 스택이 채운다(flexibleWidth 1).
        static GameObject BuildBubble(string name, string boxSprite, Color bodyColor)
        {
            var root = MessengerPrefabBuilder.Rect(name, null);
            Le(root, flexibleWidth: 1f);
            var slot = root.AddComponent<DialogueLogEntrySlot>();
            var img = Img(root, boxSprite);
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(36, 36, 16, 16);
            slot.BodyText = Tmp("Body", root.transform, 28f, bodyColor, TextAlignmentOptions.TopLeft);
            return root;
        }

        // 독백 버블 = 정의된 박스 대신 (1) 글자 뒤 옅은 분홍 배경 + (2) 글자 윤곽을 따라가는 분홍 언더레이.
        // 둘의 합이 "윤곽을 따라가되 배경느낌"의 중간 톤. 배경 폭은 대사 길이만큼(좌측 정렬) — 루트 HLG가
        // 내용 크기 Box를 좌측에 두고 우측은 빈 여백. 농도는 둘 다 인스펙터에서 감독 튜닝(🟢).
        static GameObject BuildNarrationBubble()
        {
            var root = MessengerPrefabBuilder.Rect("LogEntryNarration", null);
            Le(root, flexibleWidth: 1f);
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; // Box를 본문 폭(preferred)으로 — 가로 전체로 늘리지 않음
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.UpperLeft;
            var slot = root.AddComponent<DialogueLogEntrySlot>();

            var box = MessengerPrefabBuilder.Rect("Box", root.transform);
            ConfigureGlowBubble(box); // 분홍 배경 Image + 본문 패딩 VLG
            var body = Tmp("Body", box.transform, 28f, Color.white, TextAlignmentOptions.TopLeft);
            var glowMat = EnsureNarrationGlowMaterial();
            if (glowMat != null) body.fontSharedMaterial = glowMat; // 글자 윤곽 따라가는 번짐
            if (body.TryGetComponent<CanvasRenderer>(out var cr))
                cr.cullTransparentMesh = false; // 첫 노출(알파0) 프레임에도 그려 SDF 언더레이 셰이더 워밍 → 파란 1프레임 방지

            slot.BodyText = body;
            return root;
        }

        /// <summary>독백 본문용 분홍 언더레이 머티리얼(글자 윤곽을 따라가는 번짐) — 폰트 머티리얼 복제 후 풀 SDF 셰이더로
        /// 교체(Mobile 셰이더는 언더레이 미지원). offset 0(둘레 균일)·dilate/softness로 번짐, 색 알파로 농도. 폰트 공유
        /// 기본 머티리얼을 안 건드리도록 전용 인스턴스 에셋. 값은 중간 톤 시작값(감독 튜닝 🟢).</summary>
        static Material EnsureNarrationGlowMaterial()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFont);
            var fullSdf = Shader.Find("TextMeshPro/Distance Field"); // 언더레이 지원(Mobile 변형은 미지원)
            if (font == null || font.material == null || fullSdf == null)
            {
                Debug.LogWarning($"[DialogueLogPrefabBuilder] 폰트/머티리얼/풀 SDF 셰이더 없음 — 언더레이 생략.");
                return null;
            }
            MessengerPrefabBuilder.EnsureFolder($"{PrefabDir}/Mat");
            // 기존(모바일 셰이더 잔재)이 있으면 지우고 새로 — persisted 자산 셰이더 전환이 안 먹는 케이스 회피.
            if (AssetDatabase.LoadAssetAtPath<Material>(NarrationGlowMat) != null)
                AssetDatabase.DeleteAsset(NarrationGlowMat);

            var mat = new Material(font.material) { name = "NarrationGlow" }; // 아틀라스/face 복제
            mat.shader = fullSdf;                                             // persist 전에 in-memory 전환
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(1f, 0.42f, 0.70f, 0.6f)); // 분홍, 중간 농도 시작값
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", 0f);
            mat.SetFloat("_UnderlayDilate", 1f);    // 최대 확장(패딩 한도 내) — 윤곽 따라 퍼짐
            mat.SetFloat("_UnderlaySoftness", 1f);  // 최대 부드러움 → 배경느낌
            AssetDatabase.CreateAsset(mat, NarrationGlowMat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>독백 버블에 분홍 번짐 배경(소프트 스프라이트 Image) + 본문 패딩 VLG 설정. 신규/기존 공용.</summary>
        static void ConfigureGlowBubble(GameObject bubble)
        {
            var glow = bubble.GetComponent<Image>() ?? bubble.AddComponent<Image>();
            glow.sprite = EnsureGlowSprite();
            glow.type = Image.Type.Sliced;
            glow.color = new Color(1f, 0.45f, 0.72f, 0.30f); // 옅은 분홍 배경(언더레이와 합쳐 중간 톤 — 농도는 감독 튜닝 🟢)
            glow.raycastTarget = false;
            var vlg = bubble.GetComponent<VerticalLayoutGroup>() ?? bubble.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(40, 40, 8, 8); // 가로=번짐 여유, 세로=타이트(독백 줄 간격을 다른 버블과 맞춤)
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

        // ───────────────────────── 독백 글로우(분홍 번짐 배경) ─────────────────────────

        const string GlowSprite = "Assets/_Project/Prefabs/Log/Sprites/narration_glow.png";

        /// <summary>독백 배경용 소프트(깃털 가장자리) 분홍 스프라이트 보장 — 가장자리 알파 0→안쪽 1 스무스스텝.
        /// 9-슬라이스 보더로 늘려도 모서리 번짐 유지. 색/농도는 사용처 Image에서 조절(흰색 텍스처 → 틴트).</summary>
        static Sprite EnsureGlowSprite()
        {
            MessengerPrefabBuilder.EnsureFolder($"{PrefabDir}/Sprites");
            if (!File.Exists(GlowSprite))
            {
                const int S = 128, feather = 56; // 가장자리 깃털 폭(px)
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float d = Mathf.Min(Mathf.Min(x, S - 1 - x), Mathf.Min(y, S - 1 - y)); // 가장자리까지 거리
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / feather));         // 가장자리 0 → 안쪽 1
                        px[y * S + x] = new Color(1f, 1f, 1f, a);
                    }
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(GlowSprite, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(GlowSprite);
            }
            if (AssetImporter.GetAtPath(GlowSprite) is TextureImporter importer)
            {
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
                var b = new Vector4(60, 60, 60, 60); // 9-슬라이스(가운데만 채움, 깃털 보더 보존)
                if (importer.spriteBorder != b) { importer.spriteBorder = b; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(GlowSprite);
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
            string popupPath = $"{PrefabDir}/LogPopup.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(popupPath) != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(popupPath);
                try
                {
                    var view = contents.GetComponentInChildren<DialogueLogView>(true);
                    if (view != null)
                    {
                        Rebind(view);
                        EnsureViewportCatcher(view);
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, popupPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }

            foreach (var view in Object.FindObjectsByType<DialogueLogView>(FindObjectsInactive.Include))
            {
                Rebind(view);
                EnsureViewportCatcher(view);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                EditorUtility.SetDirty(view);
            }
        }

        static void Rebind(DialogueLogView view)
        {
            view.SpeakerHeaderPrefab = LoadSlot("LogSpeakerHeader");
            view.PlayerHeaderPrefab = LoadSlot("LogPlayerHeader");
            view.CharacterBubblePrefab = LoadSlot("LogEntryCharacter");
            view.PlayerBubblePrefab = LoadSlot("LogEntryPlayer");
            view.NarrationBubblePrefab = LoadSlot("LogEntryNarration");
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

        static GameObject BuildPopup(DialogueLogEntrySlot speakerHeader, DialogueLogEntrySlot playerHeader,
            DialogueLogEntrySlot charBubble, DialogueLogEntrySlot playerBubble, DialogueLogEntrySlot narrationBubble)
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
            view.SpeakerHeaderPrefab = speakerHeader;
            view.PlayerHeaderPrefab = playerHeader;
            view.CharacterBubblePrefab = charBubble;
            view.PlayerBubblePrefab = playerBubble;
            view.NarrationBubblePrefab = narrationBubble;
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
