using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LoveAlgo.Messenger;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 메신저 감독 검수용 데브 씬 일괄 생성(Tools ▸ Messenger ▸ Build Dev Scene).
    /// Game.unity가 병렬 작업 중이라 독립 씬(Assets/_Dev/Messenger/MessengerDev.unity)으로 산출 —
    /// 열려 있는 씬을 건드리지 않도록 Additive로 만들고 저장 후 닫는다. 재실행 = 같은 경로 덮어쓰기.
    /// 데모 시퀀스는 StreamingAssets/Messenger/Demo_*.csv + 전용 데브 카탈로그(프로덕션 카탈로그 오염 없음).
    /// </summary>
    public static class MessengerDevSceneBuilder
    {
        const string DevDir = "Assets/_Dev/Messenger";
        const string ScenePath = DevDir + "/MessengerDev.unity";
        const string PrefabDir = "Assets/_Project/Prefabs/Messenger";

        [MenuItem("Tools/Messenger/Build Dev Scene")]
        public static void Build()
        {
            MessengerPrefabBuilder.EnsureFolder(DevDir);

            // 데브 전용 시퀀스 카탈로그 — 데모 3종(전부 수동 트리거, deliverDay 0)
            var devCatalog = MessengerPrefabBuilder.EnsureAsset<MessengerScriptCatalogSO>($"{DevDir}/DevMessengerCatalog.asset");
            devCatalog.SetEntries(new List<MessengerScriptCatalogSO.Entry>
            {
                new() { sequenceId = "Demo_Roa", roomId = "c01", csvPath = "Demo_Roa.csv", deliverDay = 0 },
                new() { sequenceId = "Demo_Yeeun", roomId = "c03", csvPath = "Demo_Yeeun.csv", deliverDay = 0 },
                new() { sequenceId = "Demo_Heewon", roomId = "c04", csvPath = "Demo_Heewon.csv", deliverDay = 0 },
            });
            EditorUtility.SetDirty(devCatalog);

            var state = MessengerPrefabBuilder.FindGameState();

            var active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
            {
                // 데브 씬이 이미 열려 있으면 그 자리에서 재구성(열린 씬 위에 다른 Scene 객체 저장은 불가).
                foreach (var root in active.GetRootGameObjects())
                    Object.DestroyImmediate(root);
                BuildContents(state, devCatalog);
                EditorSceneManager.MarkSceneDirty(active);
                EditorSceneManager.SaveScene(active);
                Debug.Log($"[MessengerDevSceneBuilder] 열린 데브 씬 재구성 완료 → {ScenePath} (바로 Play로 검수)");
            }
            else
            {
                // 열린 씬을 보존하기 위해 Additive 빈 씬에 조립 → 저장 → 닫기
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                try
                {
                    BuildContents(state, devCatalog);
                    EditorSceneManager.SaveScene(scene, ScenePath);
                    Debug.Log($"[MessengerDevSceneBuilder] 데브 씬 산출 완료 → {ScenePath} (열고 Play로 검수)");
                }
                finally
                {
                    SceneManager.SetActiveScene(active);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static void BuildContents(LoveAlgo.Core.GameStateSO state, MessengerScriptCatalogSO devCatalog)
        {
            // 카메라(배경색만 — UI 전용 씬)
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.15f, 0.18f);
            camGo.tag = "MainCamera";

            // 입력(프로젝트는 Input System — 구 StandaloneInputModule은 런타임 예외)
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // 캔버스(프리팹 기준 해상도)
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 메신저 + 폰 버튼 프리팹 인스턴스(프리팹 링크 유지)
            var messengerGo = InstantiateUnderCanvas($"{PrefabDir}/Messenger.prefab", canvasGo.transform);
            InstantiateUnderCanvas($"{PrefabDir}/PhoneButton.prefab", canvasGo.transform);

            // ⚠️ 프리팹에는 프로덕션 카탈로그(현재 빈 것)가 박혀 있다 — 채팅 뷰들이 데모 시퀀스를
            // 해석하도록 인스턴스 오버라이드로 데브 카탈로그 주입(컨트롤러와 동일 카탈로그 = 정합).
            if (messengerGo != null)
            {
                var mView = messengerGo.GetComponent<MessengerView>();
                if (mView != null)
                {
                    if (mView.ChatRoom != null) mView.ChatRoom.Catalog = devCatalog;
                    if (mView.ChatList != null) mView.ChatList.Catalog = devCatalog;
                }
            }

            // 부트스트랩: 컨트롤러(도착 처리) + 데브 트리거(페이즈/이름 세팅 + 버튼)
            var boot = new GameObject("_Bootstrap");
            var controller = boot.AddComponent<MessengerController>();
            controller.State = state;
            controller.Catalog = devCatalog;
            var trigger = boot.AddComponent<MessengerDevTrigger>();
            trigger.State = state;

            // 데브 패널(좌상단 버튼 4개)
            var panel = MessengerPrefabBuilder.Rect("DevPanel", canvasGo.transform);
            var pRt = (RectTransform)panel.transform;
            pRt.anchorMin = new Vector2(0, 1); pRt.anchorMax = new Vector2(0, 1);
            pRt.pivot = new Vector2(0, 1);
            pRt.anchoredPosition = new Vector2(16, -16);
            pRt.sizeDelta = new Vector2(240, 260);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlHeight = false; layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            trigger.OpenButton = DevButton(panel.transform, "메신저 열기");
            trigger.DeliverRoaButton = DevButton(panel.transform, "도착: 로아(선택지+호감도)");
            trigger.DeliverHeewonButton = DevButton(panel.transform, "도착: 도희원(재진입 확인)");
            trigger.DeliverYeeunButton = DevButton(panel.transform, "도착: 하예은(읽기만)");
        }

        static GameObject InstantiateUnderCanvas(string prefabPath, Transform canvas)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[MessengerDevSceneBuilder] 프리팹 없음: {prefabPath} — 먼저 Build Messenger Prefabs 실행.");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(canvas, false);
            return instance;
        }

        static Button DevButton(Transform parent, string label)
        {
            var go = MessengerPrefabBuilder.Rect(label, parent);
            MessengerPrefabBuilder.Size(go, 240, 52);
            go.AddComponent<LayoutElement>().preferredHeight = 52;
            var img = MessengerPrefabBuilder.Img(go, "chat_select_box");
            img.type = Image.Type.Sliced;
            var button = go.AddComponent<Button>();
            var text = MessengerPrefabBuilder.Label(go.transform, "Label",
                "Assets/Fonts/Pretendard-SemiBold SDF.asset", 17, new Color(0.3f, 0.25f, 0.28f),
                Vector2.zero, new Vector2(230, 40), TextAlignmentOptions.Center);
            var tRt = (RectTransform)text.transform;
            tRt.anchorMin = tRt.anchorMax = tRt.pivot = new Vector2(0.5f, 0.5f);
            tRt.anchoredPosition = Vector2.zero;
            text.text = label;
            return button;
        }
    }
}
