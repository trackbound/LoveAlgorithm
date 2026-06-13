using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Tutorial;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 스탯/자유행동 첫 진입 튜토리얼 시퀀스 시드(Tools ▸ Tutorial) — 기획서(내부 콘텐츠 p8~34)
    /// 27스텝 대사·표정·하이라이트·클릭 제한을 에셋으로 생성. 이후 대사 수정은 에셋에서(기획 영역) —
    /// 재실행 시 기존 스텝이 있으면 덮어쓰기 확인을 묻는다(기획 수정 보호).
    /// 앵커 id 계약(씬 부착 시 TutorialAnchor.id와 일치해야 함):
    /// LeftPanel/RightPanel/InfoPanel/StatPanel/ActionArea/PartTimeTab/WorkStudyArea/
    /// ShopButton/ShopBalance/ShopCart/ShopItems/ShopBack.
    /// 딤 텍스처(Art/스케줄 튜토리얼/png/dim_N — 디자이너 베이크)는 앵커별 자동 배선:
    /// 풀딤0/좌측창1/우측창2/인포3/스탯4/행동탭5/알바탭6/운동·공부7/아이템구매8/상점좌측9(잔액·장바구니 공용)/
    /// 상점아이템10/돌아가기11 (투명 구멍 bbox 픽셀 분석으로 확정, 2026-06-13).
    /// </summary>
    public static class TutorialSequenceBuilder
    {
        const string AssetPath = "Assets/Resources/Data/Tutorial_ScheduleIntro.asset";
        const string ArtDir = "Assets/Art/스케줄 튜토리얼/png";

        // 로아 그룹 시작 위치(중앙 기준, 목업 근사 — 감독 에셋 튜닝 영역): 풀딤=좌중앙, 구멍 반대편 하단.
        static readonly Vector2 PosCenter = new(-330f, 0f);
        static readonly Vector2 PosWhenLeftHole = new(-150f, -250f);
        static readonly Vector2 PosWhenRightHole = new(-560f, -250f);

        [MenuItem("Tools/Tutorial/Seed Schedule Intro Sequence")]
        public static void Seed()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TutorialSequenceSO>(AssetPath);
            if (existing != null && existing.Steps.Count > 0 &&
                !EditorUtility.DisplayDialog("튜토리얼 시드",
                    $"기존 스텝 {existing.Steps.Count}개를 기획서 시드 27개로 덮어씁니다(에셋 수정분 소실). 진행할까요?",
                    "덮어쓰기", "취소"))
                return;

            SeedForce();
        }

        /// <summary>확인 없이 시드(원격/자동 실행용 — 다이얼로그는 비포커스 에디터에서 hang).</summary>
        [MenuItem("Tools/Tutorial/Seed Schedule Intro Sequence (Force)")]
        public static void SeedForce()
        {
            EnsureSpriteImports(); // 딤 배선 전 Sprite 임포트 보장
            var so = MessengerPrefabBuilder.EnsureAsset<TutorialSequenceSO>(AssetPath);
            so.prefsKey = "Tutorial_ScheduleIntro";
            so.SetSteps(BuildSteps());
            WireDims(so);
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TutorialSequenceBuilder] 시드 완료 → {AssetPath} ({so.Steps.Count}스텝, 강제 클릭 2곳: ShopButton/ShopBack, 딤 자동 배선)");
        }

        /// <summary>앵커 id → 디자이너 딤 텍스처/로아 위치 배선(시드·재배선 공용).</summary>
        static void WireDims(TutorialSequenceSO so)
        {
            // 상점 진입 직후 일반 설명(하이라이트 없음)은 풀딤. 잔액/장바구니는 상점 좌측 딤(9) 공용.
            var dimByAnchor = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
            {
                [""] = 0, ["LeftPanel"] = 1, ["RightPanel"] = 2, ["InfoPanel"] = 3, ["StatPanel"] = 4,
                ["ActionArea"] = 5, ["PartTimeTab"] = 6, ["WorkStudyArea"] = 7, ["ShopButton"] = 8,
                ["ShopBalance"] = 9, ["ShopCart"] = 9, ["ShopItems"] = 10, ["ShopBack"] = 11
            };
            // 구멍 중심 x(픽셀 분석): 좌측계(<40%) vs 우측계 — 로아는 구멍 반대편 하단.
            var leftHole = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
                { "LeftPanel", "InfoPanel", "StatPanel", "ShopBalance", "ShopCart" };

            foreach (var step in so.Steps)
            {
                string anchor = step.highlightAnchor ?? "";
                int dimIndex = dimByAnchor.TryGetValue(anchor, out int idx) ? idx : 0;
                step.dim = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/dim_{dimIndex}.png");
                if (step.dim == null)
                    Debug.LogWarning($"[TutorialSequenceBuilder] 딤 스프라이트 없음: dim_{dimIndex} (Sprite 임포트 확인)");

                step.roaPosition = string.IsNullOrEmpty(anchor) ? PosCenter
                    : leftHole.Contains(anchor) ? PosWhenLeftHole
                    : PosWhenRightHole;
            }
        }

        [MenuItem("Tools/Tutorial/Reset Schedule Intro Done Flag")]
        public static void ResetFlag()
        {
            TutorialFlag.Reset("Tutorial_ScheduleIntro");
            Debug.Log("[TutorialSequenceBuilder] Tutorial_ScheduleIntro 완료 기록 삭제 — 다음 진입 시 재생.");
        }

        // ───────────────────────── 프리팹 + 데브 씬 ─────────────────────────

        const string PrefabDir = "Assets/_Project/Prefabs/Tutorial";
        const string DevDir = "Assets/_Dev/Tutorial";
        const string DevScenePath = DevDir + "/TutorialDev.unity";
        const string BodyFont = "Assets/Fonts/Pretendard-SemiBold SDF.asset";

        [MenuItem("Tools/Tutorial/Build Tutorial Prefab")]
        public static void BuildPrefab()
        {
            EnsureSpriteImports();
            MessengerPrefabBuilder.EnsureFolder(PrefabDir);

            var sequence = AssetDatabase.LoadAssetAtPath<TutorialSequenceSO>(AssetPath);
            var state = MessengerPrefabBuilder.FindGameState();

            var tutorialGo = MessengerPrefabBuilder.Rect("Tutorial", null);
            MessengerPrefabBuilder.Stretch(tutorialGo);
            var view = tutorialGo.AddComponent<TutorialView>();
            view.State = state;
            view.Sequence = sequence;

            var root = MessengerPrefabBuilder.Rect("Root", tutorialGo.transform);
            MessengerPrefabBuilder.Stretch(root);
            view.Root = root;

            // 딤(클릭 캐처 겸 — 디자이너 베이크 텍스처 스왑)
            var dim = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("Dim", root.transform), null);
            MessengerPrefabBuilder.Stretch(dim.gameObject);
            dim.sprite = LoadDim(0);
            var catcher = dim.gameObject.AddComponent<TutorialClickCatcher>();
            catcher.View = view;
            view.DimImage = dim;
            view.FallbackDim = LoadDim(0);

            // 로아 아이콘 + 말풍선(스텝별 위치 이동 그룹)
            var group = MessengerPrefabBuilder.Rect("RoaGroup", root.transform);
            var gRt = (RectTransform)group.transform;
            gRt.anchorMin = gRt.anchorMax = gRt.pivot = new Vector2(0.5f, 0.5f);
            view.RoaGroup = gRt;

            var roa = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("Roa", group.transform), null);
            var rRt = (RectTransform)roa.transform;
            rRt.sizeDelta = new Vector2(330, 290);
            roa.preserveAspect = true;
            roa.raycastTarget = false;
            roa.sprite = LoadRoa(1);
            view.RoaImage = roa;
            view.RoaBasic = LoadRoa(1);
            view.RoaSmile = LoadRoa(2);
            view.RoaBeam = LoadRoa(3);

            // 말풍선 — 대사 길이에 따라 가로+세로 가변(목업 사양, 런타임 ResizeBubble이 측정·적용).
            // pivot 좌측 = 로아 오른쪽에서 자라남.
            var bubble = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("Bubble", group.transform), null);
            var bbRt = (RectTransform)bubble.transform;
            bbRt.pivot = new Vector2(0f, 0.5f);
            bbRt.anchoredPosition = new Vector2(170, 25);
            bbRt.sizeDelta = new Vector2(420, 150); // 시작값 — 스텝마다 코드가 갱신
            bubble.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/textbox.png");
            bubble.type = Image.Type.Sliced;
            bubble.raycastTarget = false;
            view.BubbleRect = bbRt;

            var text = MessengerPrefabBuilder.Label(bubble.transform, "Text", BodyFont, 21, Color.white,
                Vector2.zero, new Vector2(430, 140), TMPro.TextAlignmentOptions.Midline);
            var txRt = (RectTransform)text.transform;
            txRt.anchorMin = txRt.anchorMax = txRt.pivot = new Vector2(0.5f, 0.5f);
            txRt.anchoredPosition = Vector2.zero;
            text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            view.BubbleText = text;

            root.SetActive(false); // 부팅 닫힘(StartTutorialCommand로 열림)
            var prefab = PrefabUtility.SaveAsPrefabAsset(tutorialGo, $"{PrefabDir}/Tutorial.prefab");
            Object.DestroyImmediate(tutorialGo);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TutorialSequenceBuilder] Tutorial.prefab 산출 완료 → {PrefabDir} (아트: 딤 12종/로아 3종/말풍선)");
        }

        [MenuItem("Tools/Tutorial/Build Dev Scene")]
        public static void BuildDevScene()
        {
            MessengerPrefabBuilder.EnsureFolder(DevDir);
            var sequence = AssetDatabase.LoadAssetAtPath<TutorialSequenceSO>(AssetPath);

            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path == DevScenePath)
            {
                foreach (var rootGo in active.GetRootGameObjects())
                    Object.DestroyImmediate(rootGo);
                BuildDevContents(sequence);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(active);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(active);
                Debug.Log($"[TutorialSequenceBuilder] 열린 데브 씬 재구성 완료 → {DevScenePath}");
            }
            else
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                try
                {
                    BuildDevContents(sequence);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, DevScenePath);
                    Debug.Log($"[TutorialSequenceBuilder] 데브 씬 산출 완료 → {DevScenePath} (열고 Play로 검수)");
                }
                finally
                {
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static void BuildDevContents(TutorialSequenceSO sequence)
        {
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.75f, 0.74f, 0.78f); // 밝은 배경 — 딤 구멍 확인용
            camGo.tag = "MainCamera";

            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 가짜 스케줄 화면 배경(밝은 판) — 딤 구멍으로 비치는 영역 확인
            var bg = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("FakeScreen", canvasGo.transform), null,
                new Color(0.93f, 0.90f, 0.94f));
            MessengerPrefabBuilder.Stretch(bg.gameObject);

            // 강제 클릭 검수용 가짜 앵커 2개 — 딤 구멍 좌표(픽셀 분석)와 일치 배치
            FakeAnchor(canvasGo.transform, "ShopButton", "아이템 구매", new Vector2(653, 378), new Vector2(211, 54));
            FakeAnchor(canvasGo.transform, "ShopBack", "돌아가기", new Vector2(883, -421), new Vector2(115, 108));

            // 튜토리얼 프리팹 인스턴스
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Tutorial.prefab");
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(canvasGo.transform, false);
            }
            else Debug.LogError($"[TutorialSequenceBuilder] {PrefabDir}/Tutorial.prefab 없음 — 먼저 Build Tutorial Prefab 실행.");

            var boot = new GameObject("_Bootstrap");
            var controller = boot.AddComponent<TutorialController>();
            controller.Sequence = sequence;
            var trigger = boot.AddComponent<TutorialDevTrigger>();
            trigger.Sequence = sequence;

            var panel = MessengerPrefabBuilder.Rect("DevPanel", canvasGo.transform);
            var pRt = (RectTransform)panel.transform;
            pRt.anchorMin = pRt.anchorMax = new Vector2(0, 1);
            pRt.pivot = new Vector2(0, 1);
            pRt.anchoredPosition = new Vector2(16, -16);
            pRt.sizeDelta = new Vector2(240, 130);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlHeight = false; layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            trigger.PlayButton = MessengerDevSceneBuilder.DevButton(panel.transform, "튜토리얼 재생");
            trigger.ResetFlagButton = MessengerDevSceneBuilder.DevButton(panel.transform, "완료 기록 리셋");
        }

        /// <summary>강제 클릭 검수용 가짜 앵커 버튼(딤 구멍 좌표에 배치, 리스너 없음 — 패스스루 호출 확인용).</summary>
        static void FakeAnchor(Transform parent, string anchorId, string label, Vector2 pos, Vector2 size)
        {
            var go = MessengerPrefabBuilder.Rect($"Fake_{anchorId}", parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 0.72f, 0.82f);
            var button = go.AddComponent<Button>();
            var anchor = go.AddComponent<TutorialAnchor>();
            anchor.Id = anchorId;
            anchor.Button = button;
            var text = MessengerPrefabBuilder.Label(go.transform, "Label", BodyFont, 15,
                new Color(0.35f, 0.2f, 0.28f), Vector2.zero, new Vector2(size.x - 8, 30), TMPro.TextAlignmentOptions.Center);
            var lRt = (RectTransform)text.transform;
            lRt.anchorMin = lRt.anchorMax = lRt.pivot = new Vector2(0.5f, 0.5f);
            lRt.anchoredPosition = Vector2.zero;
            text.text = label;
        }

        static Sprite LoadDim(int index) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/dim_{index}.png");
        static Sprite LoadRoa(int index) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/roa_{index}.png");

        /// <summary>아트 폴더 일괄 Sprite 임포트 + 말풍선 9-슬라이스 보더(재실행 안전).</summary>
        static void EnsureSpriteImports()
        {
            if (!System.IO.Directory.Exists(ArtDir)) { Debug.LogWarning($"[TutorialSequenceBuilder] 아트 폴더 없음: {ArtDir}"); return; }
            foreach (var file in System.IO.Directory.GetFiles(ArtDir, "*.png"))
            {
                var assetPath = file.Replace('\\', '/');
                if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) continue;
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (assetPath.EndsWith("textbox.png") && importer.spriteBorder == Vector4.zero)
                {
                    importer.spriteBorder = new Vector4(34, 34, 34, 34);
                    dirty = true;
                }
                if (dirty) importer.SaveAndReimport();
            }
        }

        static List<TutorialSequenceSO.Step> BuildSteps()
        {
            var s = new List<TutorialSequenceSO.Step>();
            void Add(string text, RoaTutorialEmote emote, string highlight = "", string requiredClick = "",
                float delay = 0f, float auto = 0f)
                => s.Add(new TutorialSequenceSO.Step
                {
                    text = text, emote = emote, highlightAnchor = highlight,
                    requiredClickAnchor = requiredClick, appearDelay = delay, autoAdvanceSeconds = auto
                });

            // ── 도입(중앙) — 기획 p8~10, 진입 2초 후 시작
            Add("안녕, {{Player}}!\n오늘 하루, 마음에 들어?", RoaTutorialEmote.Basic, delay: 2f);
            Add("여기서는 오늘 하루의 여유 시간을 어떻게 보낼지 선택할 수 있어.\n지금 {{Player}}의 상태를 확인할 수도 있고!", RoaTutorialEmote.Basic);
            Add("어떤 사람이 되고 싶은지 고민하고, 그에 따라서 성장하는 거야.\n너는 무엇이든 너가 원하는 대로 될 수 있거든! 내가 도와줄게!", RoaTutorialEmote.Basic);

            // ── 좌/우 창 소개 — p11~13
            Add("자, 그럼 하나씩 설명해 줄게.\n왼쪽 창은 {{Player}}의 상태를 나타내.", RoaTutorialEmote.Smile, "LeftPanel");
            Add("오른쪽 창은 오늘 어떤 일을 할 지\n선택할 수 있는 곳이야.", RoaTutorialEmote.Smile, "RightPanel");
            Add("왼쪽 창부터 한 번 볼까?", RoaTutorialEmote.Smile, "LeftPanel");

            // ── 인포/스탯 — p14~19
            Add("위쪽의 정보 창에서는 우리가 함께한 지 며칠이 됐는지 알 수 있어.\n그리고 지금 {{Player}}의 지갑 사정도 볼 수 있지!", RoaTutorialEmote.Basic, "InfoPanel");
            Add("피로도는 아래쪽의 스탯 창에서 볼 수 있어.\n총 5개의 스탯을 볼 수 있는 곳이야.", RoaTutorialEmote.Bright, "StatPanel");
            Add("체력, 지성, 사교성, 끈기 모두 멋진 사람의 필수 역량이지!\n어떤 행동을 하느냐에 따라서 다르게 성장해.", RoaTutorialEmote.Smile, "StatPanel");
            Add("하지만 피로도는 너무 높아지면 아무런 활동도 하지 못하고\n쉬어야 하니까, 지켜보면서 조심해야 해.", RoaTutorialEmote.Basic, "StatPanel");
            Add("나 자신을 아낄 줄 아는 게 가장 매력적인 사람의 특징이라고!", RoaTutorialEmote.Smile, "StatPanel");
            Add("참, 첫 날을 잘 보내고 있으니 특별히 비밀을 말해주자면…\n어떤 매력을 특히 잘 보여주는지에 따라서\n어떤 사람과 가까워질 수 있을지 달라질 수도 있어!", RoaTutorialEmote.Basic, "StatPanel");

            // ── 행동 선택 — p20~23
            Add("이번에는 오른쪽 창을 한 번 볼까?\n오른쪽 창은 어떤 행동을 할 지 선택하는 곳이야.", RoaTutorialEmote.Smile, "RightPanel");
            Add("아르바이트, 운동, 공부 중 한 개의 행동만 할 수 있고,\n한 번 선택하면 바꾸거나 취소할 수 없으니 조심해!", RoaTutorialEmote.Smile, "ActionArea");
            Add("만약 돈을 더 벌고 싶다면, 오른쪽 창에서 아르바이트 탭을 선택해봐.\n할 수 있는 아르바이트의 종류를 자유롭게 선택할 수 있고,\n종류별로 벌 수 있는 금액과 쌓이는 피로도가 달라.", RoaTutorialEmote.Basic, "PartTimeTab");
            Add("운동이나 공부 활동을 선택하면 그에 따라 {{Player}}의 스탯이 달라져.\n어떤 행동을 얼마나 하는지에 따라서 어떤 스탯이 변화할지도 달라지지!", RoaTutorialEmote.Basic, "WorkStudyArea");

            // ── 상점 진입(강제 클릭) — p24~25
            Add("가장 오른쪽에는 아이템 구매 버튼이 있어.\n이 버튼을 누르면 상점으로 이동해. 한 번 눌러봐!", RoaTutorialEmote.Basic, "ShopButton", requiredClick: "ShopButton");
            Add("상점에서는 여러가지 아이템을 살 수 있어.\n이 중 누군가가 특별히 좋아하는 아이템도 섞여 있다고!", RoaTutorialEmote.Basic);

            // ── 상점 내부 — p26~29
            Add("왼쪽 위를 보면 지금 {{Player}}가 가진 돈의 액수가 표시되고,", RoaTutorialEmote.Basic, "ShopBalance");
            Add("그 아래쪽에는 내가 고른 물건을 볼 수 있는 장바구니가 있어.\n한번에 여러가지 물건을 여러 개 살 수도 있고, 총합해서 얼마인지도 보여.", RoaTutorialEmote.Basic, "ShopCart");
            Add("오른쪽에서는 어떤 아이템을 살 수 있는지 볼 수 있어.\n다양한 아이템이 있으니까, 자세히 살펴봐!", RoaTutorialEmote.Basic, "ShopItems");
            Add("아이템마다 스탯에 미치는 영향이 전부 다르고, 구매하면 그 영향이 바로 적용돼.\n어떤 활동에 도움이 될 아이템인지를 떠올려 보면 유추할 수 있을지도 몰라!", RoaTutorialEmote.Basic, "ShopItems");

            // ── 돌아가기(강제 클릭) — p30
            Add("돌아가기 버튼을 누르면 이전 화면으로 돌아갈 수 있어.\n소개는 이쯤이니까, 한 번 눌러볼래?", RoaTutorialEmote.Basic, "ShopBack", requiredClick: "ShopBack");

            // ── 마무리(중앙) — p31~34
            Add("지루한 설명이지만 들어줘서 고마워, {{Player}}!", RoaTutorialEmote.Basic);
            Add("행동 선택창은 하루에 한 번 열려.\n그 때마다 {{Player}}가 어떻게 시간을 보낼지 너무 궁금해!", RoaTutorialEmote.Basic);
            Add("어떤 사람이 되어갈지, 기대할게. 분명히… 멋, 멋있을 거니까!", RoaTutorialEmote.Basic);
            Add("그럼 난 이제 {{Player}}를 기다리고 있을게. 다녀와!", RoaTutorialEmote.Beam, auto: 4f);

            return s;
        }
    }
}
