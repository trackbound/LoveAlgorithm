using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LoveAlgo.Core;  // GameStateSO
using LoveAlgo.Gacha;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 가챠 화면 프리팹 + 감독 검수용 데브 씬 일괄 생성(Tools ▸ Gacha). 전용 아트 미도착이라
    /// 그레이박스(단색+라벨) — 기능/연출 타이밍 검수용, 아트 도착 시 스프라이트 교체.
    /// 재실행 = 같은 경로 덮어쓰기(GUID 보존). 위치/크기/색은 시작값(감독 튜닝 영역 🟢).
    /// </summary>
    public static class GachaPrefabBuilder
    {
        const string PrefabDir = "Assets/_Project/Prefabs/Gacha";
        const string DataDir = "Assets/Resources/Data";
        const string DevDir = "Assets/_Dev/Gacha";
        const string ScenePath = DevDir + "/GachaDev.unity";
        const string BodyFont = "Assets/Fonts/Pretendard-SemiBold SDF.asset";

        static readonly Color PanelBg = new(0.16f, 0.15f, 0.24f, 0.97f);
        static readonly Color CellEmpty = new(0.27f, 0.25f, 0.38f);
        static readonly Color CellPiece = new(1f, 0.72f, 0.82f);
        static readonly Color Cream = new(0.98f, 0.93f, 0.82f);

        [MenuItem("Tools/Gacha/Build Gacha Prefabs")]
        public static void BuildPrefabs()
        {
            MessengerPrefabBuilder.EnsureFolder(PrefabDir);
            var tuning = MessengerPrefabBuilder.EnsureAsset<GachaTuningSO>($"{DataDir}/GachaTuning.asset");
            var state = MessengerPrefabBuilder.FindGameState();

            var pieceSlot = SavePrefab(BuildPieceSlot(), $"{PrefabDir}/GachaPieceSlot.prefab").GetComponent<GachaPieceSlot>();
            SavePrefab(BuildGacha(state, tuning, pieceSlot), $"{PrefabDir}/Gacha.prefab");

            AssetDatabase.SaveAssets();
            Debug.Log($"[GachaPrefabBuilder] 산출 완료 → {PrefabDir} (Gacha + PieceSlot, 그레이박스 — 아트 도착 시 교체).");
        }

        [MenuItem("Tools/Gacha/Build Dev Scene")]
        public static void BuildDevScene()
        {
            MessengerPrefabBuilder.EnsureFolder(DevDir);
            var tuning = MessengerPrefabBuilder.EnsureAsset<GachaTuningSO>($"{DataDir}/GachaTuning.asset");
            var state = MessengerPrefabBuilder.FindGameState();

            var active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
            {
                foreach (var root in active.GetRootGameObjects())
                    Object.DestroyImmediate(root);
                BuildSceneContents(state, tuning);
                EditorSceneManager.MarkSceneDirty(active);
                EditorSceneManager.SaveScene(active);
                Debug.Log($"[GachaPrefabBuilder] 열린 데브 씬 재구성 완료 → {ScenePath} (바로 Play로 검수)");
            }
            else
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                try
                {
                    BuildSceneContents(state, tuning);
                    EditorSceneManager.SaveScene(scene, ScenePath);
                    Debug.Log($"[GachaPrefabBuilder] 데브 씬 산출 완료 → {ScenePath} (열고 Play로 검수)");
                }
                finally
                {
                    SceneManager.SetActiveScene(active);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // ───────────────────────── 프리팹 ─────────────────────────

        static GameObject BuildPieceSlot()
        {
            var go = MessengerPrefabBuilder.Rect("GachaPieceSlot", null);
            MessengerPrefabBuilder.Size(go, 96, 96);
            var slot = go.AddComponent<GachaPieceSlot>();

            var empty = MessengerPrefabBuilder.Rect("Empty", go.transform);
            MessengerPrefabBuilder.Stretch(empty);
            var emptyImg = empty.AddComponent<Image>();
            emptyImg.color = CellEmpty;
            slot.EmptyRoot = empty;

            var reveal = MessengerPrefabBuilder.Rect("Reveal", go.transform);
            MessengerPrefabBuilder.Stretch(reveal);
            reveal.AddComponent<RectMask2D>(); // 보드 크기 일러스트를 칸 영역으로 자르기
            slot.RevealRoot = reveal;

            var illust = MessengerPrefabBuilder.Rect("Illust", reveal.transform);
            var illustImg = illust.AddComponent<Image>();
            illustImg.raycastTarget = false;
            slot.IllustImage = illustImg; // 스프라이트/오프셋은 Setup이 베이킹

            var ph = MessengerPrefabBuilder.Rect("Placeholder", reveal.transform);
            MessengerPrefabBuilder.Stretch(ph);
            var phImg = ph.AddComponent<Image>();
            phImg.color = CellPiece;
            phImg.raycastTarget = false;
            slot.PlaceholderImage = phImg;

            var label = MessengerPrefabBuilder.Label(reveal.transform, "Index", BodyFont, 22,
                new Color(0.35f, 0.2f, 0.28f), Vector2.zero, new Vector2(90, 30), TextAlignmentOptions.Center);
            var lRt = (RectTransform)label.transform;
            lRt.anchorMin = lRt.anchorMax = lRt.pivot = new Vector2(0.5f, 0.5f);
            lRt.anchoredPosition = Vector2.zero;
            slot.IndexLabel = label;

            reveal.SetActive(false);
            return go;
        }

        static GameObject BuildGacha(GameStateSO state, GachaTuningSO tuning, GachaPieceSlot pieceSlot)
        {
            var gachaGo = MessengerPrefabBuilder.Rect("Gacha", null);
            MessengerPrefabBuilder.Stretch(gachaGo);
            var view = gachaGo.AddComponent<GachaView>();
            view.State = state;
            view.Tuning = tuning;
            view.PieceSlotPrefab = pieceSlot;

            var root = MessengerPrefabBuilder.Rect("Root", gachaGo.transform);
            MessengerPrefabBuilder.Stretch(root);
            view.Root = root;

            var dim = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("Dim", root.transform), null, new Color(0f, 0f, 0f, 0.55f));
            MessengerPrefabBuilder.Stretch(dim.gameObject);

            var window = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("Window", root.transform), null, PanelBg);
            MessengerPrefabBuilder.Size(window.gameObject, 1100, 720);

            var title = MessengerPrefabBuilder.Label(window.transform, "Title", BodyFont, 28, Color.white,
                new Vector2(36, -16), new Vector2(400, 40), TextAlignmentOptions.MidlineLeft);
            var tRt = (RectTransform)title.transform;
            tRt.anchorMin = tRt.anchorMax = new Vector2(0, 1); tRt.pivot = new Vector2(0, 1);
            title.text = "랜덤 가챠 — 퍼즐 콜렉션";

            // 퍼즐판(좌측) — 6×5 그리드
            var board = MessengerPrefabBuilder.Rect("Board", window.transform);
            var bRt = (RectTransform)board.transform;
            bRt.anchorMin = new Vector2(0, 0.5f); bRt.pivot = new Vector2(0, 0.5f);
            bRt.anchorMax = new Vector2(0, 0.5f);
            bRt.anchoredPosition = new Vector2(36, 10);
            bRt.sizeDelta = new Vector2(6 * 96 + 5 * 6, 5 * 96 + 4 * 6);
            var grid = board.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(96, 96);
            grid.spacing = new Vector2(6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            view.BoardContainer = bRt;

            // 카운터(좌하단, 기획 "0/25" 자리 — 실제 30)
            var counter = MessengerPrefabBuilder.Label(window.transform, "Counter", BodyFont, 24, Cream,
                new Vector2(36, 26), new Vector2(200, 34), TextAlignmentOptions.MidlineLeft);
            var cRt = (RectTransform)counter.transform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(0, 0); cRt.pivot = new Vector2(0, 0);
            counter.text = "0/30";
            view.CounterText = counter;

            // 우측 연출 구역(가챠권/조각 공개)
            var slotArea = MessengerPrefabBuilder.Rect("SlotArea", window.transform);
            var sRt = (RectTransform)slotArea.transform;
            sRt.anchorMin = new Vector2(1, 0.5f); sRt.anchorMax = new Vector2(1, 0.5f);
            sRt.pivot = new Vector2(1, 0.5f);
            sRt.anchoredPosition = new Vector2(-60, 60);
            sRt.sizeDelta = new Vector2(320, 360);

            var ticket = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("Ticket", slotArea.transform), null, Cream);
            var tkRt = (RectTransform)ticket.transform;
            tkRt.sizeDelta = new Vector2(140, 190);
            ticket.gameObject.SetActive(false);
            view.TicketImage = tkRt;

            var fly = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("PieceFly", slotArea.transform), null, CellPiece);
            var flyRt = (RectTransform)fly.transform;
            flyRt.sizeDelta = new Vector2(96, 96);
            fly.gameObject.SetActive(false);
            view.PieceFlyImage = flyRt;

            var bonus = MessengerPrefabBuilder.Label(slotArea.transform, "BonusLabel", BodyFont, 22, Cream,
                Vector2.zero, new Vector2(300, 60), TextAlignmentOptions.Center);
            var bnRt = (RectTransform)bonus.transform;
            bnRt.anchorMin = bnRt.anchorMax = bnRt.pivot = new Vector2(0.5f, 0.5f);
            bnRt.anchoredPosition = new Vector2(0, -140);
            bonus.text = "이미 완성! (업적 카운트 +1)";
            bonus.gameObject.SetActive(false);
            view.BonusLabel = bonus;

            var confetti = MessengerPrefabBuilder.Rect("Confetti", window.transform);
            MessengerPrefabBuilder.Stretch(confetti);
            view.ConfettiContainer = (RectTransform)confetti.transform;

            // 버튼: 전체화면 보기 / 나가기(우하단)
            view.FullscreenButton = GreyButton(window.transform, "전체화면 보기", new Vector2(-220, 26), out _);
            view.ExitButton = GreyButton(window.transform, "나가기", new Vector2(-36, 26), out _);

            // 전체화면(일러스트) — 미완성 상태 그대로도 허용(기획)
            var fullscreen = MessengerPrefabBuilder.Rect("Fullscreen", root.transform);
            MessengerPrefabBuilder.Stretch(fullscreen);
            var fsBg = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("Bg", fullscreen.transform), null, new Color(0f, 0f, 0f, 0.92f));
            MessengerPrefabBuilder.Stretch(fsBg.gameObject);
            var fsImg = MessengerPrefabBuilder.Img(MessengerPrefabBuilder.Rect("Illust", fullscreen.transform), null, new Color(0.9f, 0.85f, 0.9f));
            var fsRt = (RectTransform)fsImg.transform;
            fsRt.sizeDelta = new Vector2(1280, 720);
            fsImg.preserveAspect = true;
            view.FullscreenImage = fsImg;
            view.FullscreenCloseButton = GreyButton(fullscreen.transform, "전체화면 종료", new Vector2(-24, 24), out var fsClose);
            fsClose.sizeDelta = new Vector2(170, 44); // 우하단 작게(기획 p49)
            fullscreen.SetActive(false);
            view.FullscreenRoot = fullscreen;

            root.SetActive(false); // 부팅 닫힘
            return gachaGo;
        }

        /// <summary>그레이박스 버튼(우하단 기준 배치).</summary>
        static Button GreyButton(Transform parent, string label, Vector2 pos, out RectTransform rt)
        {
            var go = MessengerPrefabBuilder.Rect(label, parent);
            rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(170, 52);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.42f, 0.38f, 0.56f);
            var button = go.AddComponent<Button>();
            var text = MessengerPrefabBuilder.Label(go.transform, "Label", BodyFont, 19, Color.white,
                Vector2.zero, new Vector2(160, 36), TextAlignmentOptions.Center);
            var lRt = (RectTransform)text.transform;
            lRt.anchorMin = lRt.anchorMax = lRt.pivot = new Vector2(0.5f, 0.5f);
            lRt.anchoredPosition = Vector2.zero;
            text.text = label;
            return button;
        }

        // ───────────────────────── 데브 씬 ─────────────────────────

        static void BuildSceneContents(GameStateSO state, GachaTuningSO tuning)
        {
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.12f, 0.16f);
            camGo.tag = "MainCamera";

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Gacha.prefab");
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(canvasGo.transform, false);
            }
            else Debug.LogError($"[GachaPrefabBuilder] {PrefabDir}/Gacha.prefab 없음 — 먼저 Build Gacha Prefabs 실행.");

            var boot = new GameObject("_Bootstrap");
            var controller = boot.AddComponent<GachaController>();
            controller.State = state;
            controller.Tuning = tuning;
            var trigger = boot.AddComponent<GachaDevTrigger>();
            trigger.State = state;

            var panel = MessengerPrefabBuilder.Rect("DevPanel", canvasGo.transform);
            var pRt = (RectTransform)panel.transform;
            pRt.anchorMin = pRt.anchorMax = new Vector2(0, 1);
            pRt.pivot = new Vector2(0, 1);
            pRt.anchoredPosition = new Vector2(16, -16);
            pRt.sizeDelta = new Vector2(240, 200);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlHeight = false; layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            trigger.PurchaseButton = MessengerDevSceneBuilder.DevButton(panel.transform, "가챠권 구매(추첨)");
            trigger.ViewButton = MessengerDevSceneBuilder.DevButton(panel.transform, "현황 보기");
            trigger.ResetButton = MessengerDevSceneBuilder.DevButton(panel.transform, "보유/업적 초기화");
        }

        static GameObject SavePrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
