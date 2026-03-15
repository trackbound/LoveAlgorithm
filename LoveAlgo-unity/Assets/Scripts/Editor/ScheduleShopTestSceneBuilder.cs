using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 스케줄/상점 테스트 씬 자동 생성
    /// Main.unity 씬의 구조를 기반으로, 필요한 매니저와 UI만 포함하는 경량 테스트 씬 생성
    /// </summary>
    public static class ScheduleShopTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/ScheduleShopTest.unity";

        // 프리팹 경로
        const string PFX = "Assets/Prefabs/";
        const string ScheduleUIPrefab = PFX + "Schedule/ScheduleUI.prefab";
        const string DialogueUIPrefab = PFX + "UI/Main/DialogueUI.prefab";
        const string ChoiceUIPrefab   = PFX + "UI/Main/ChoiceUI.prefab";
        const string TitleUIPrefab    = PFX + "UI/Main/TitleUI.prefab";
        const string UsernameUIPrefab = PFX + "UI/Main/UsernameUI.prefab";
        const string PlaceInfoPrefab  = PFX + "UI/Main/PlaceInfoBar.prefab";

        const string ShopPanelPrefab    = PFX + "Shop/ShopPanel.prefab";
        const string ScheduleHelpPrefab = PFX + "Schedule/ScheduleHelpModal.prefab";
        const string SaveLoadPopupPrefab = PFX + "UI/Popup/SaveLoadPopup.prefab";
        const string GiftPopupPrefab    = PFX + "Shop/GiftPopup.prefab";

        const string ConfirmPopupPrefab     = PFX + "UI/Popup/Top/ConfirmPopup.prefab";
        const string AlertPopupPrefab       = PFX + "UI/Popup/Top/AlertPopup.prefab";
        const string ToastPopupPrefab       = PFX + "UI/Popup/Top/ToastPopup.prefab";
        const string ScheduleResultPrefab   = PFX + "UI/Popup/Top/ScheduleResultPopup.prefab";
        const string LogPopupPrefab         = PFX + "UI/Popup/Log/LogPopup.prefab";

        const string ScreenFXPrefab = PFX + "Common/ScreenFX.prefab";

        [MenuItem("LoveAlgo/Tools/Create Schedule·Shop Test Scene", false, 200)]
        public static void CreateTestScene()
        {
            if (!EditorUtility.DisplayDialog(
                "테스트 씬 생성",
                $"스케줄/상점 테스트 씬을 생성합니다.\n경로: {ScenePath}\n\n기존 파일이 있으면 덮어씁니다.\n계속하시겠습니까?",
                "생성", "취소"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 1. Camera ──
            var cameraGo = new GameObject("Main Camera");
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            cam.orthographic = true;
            cam.tag = "MainCamera";
            cameraGo.AddComponent<AudioListener>();

            // ── 2. Directional Light ──
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // ── 3. EventSystem ──
            var eventSys = new GameObject("EventSystem");
            eventSys.AddComponent<EventSystem>();
            eventSys.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // ── 4. Canvas 생성 ──
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // -- UILayer --
            var uiLayer = CreateChild(canvasGo, "UILayer");
            SetStretch(uiLayer);

            // -- PopupLayer --
            var popupLayer = CreateChild(canvasGo, "PopupLayer");
            SetStretch(popupLayer);

            // -- Modal Layer (PopupManager의 layerModal) --
            var modalLayer = CreateChild(popupLayer, "ModalLayer");
            SetStretch(modalLayer);

            // -- Top Layer (PopupManager의 layerTop) --
            var topLayer = CreateChild(popupLayer, "TopLayer");
            SetStretch(topLayer);

            // ── 5. 프리팹 인스턴스화 ──

            // UILayer 자식으로 메인 UI 추가
            var dialogueUI = InstantiatePrefab(DialogueUIPrefab, uiLayer.transform);
            var choiceUI   = InstantiatePrefab(ChoiceUIPrefab,   uiLayer.transform);
            var scheduleUI = InstantiatePrefab(ScheduleUIPrefab, uiLayer.transform);
            var titleUI    = InstantiatePrefab(TitleUIPrefab,    uiLayer.transform);
            var usernameUI = InstantiatePrefab(UsernameUIPrefab, uiLayer.transform);
            var placeUI    = InstantiatePrefab(PlaceInfoPrefab,  uiLayer.transform);

            // ShopPanel을 ScheduleUI 내부에 크로스페이드 패널로 임베드
            EmbedShopPanel(scheduleUI);

            // Top 팝업 (인스턴스, TopLayer에)
            var confirmPopup  = InstantiatePrefab(ConfirmPopupPrefab, topLayer.transform);
            var alertPopup    = InstantiatePrefab(AlertPopupPrefab,   topLayer.transform);
            var toastPopup    = InstantiatePrefab(ToastPopupPrefab,   topLayer.transform);
            var logPopup      = InstantiatePrefab(LogPopupPrefab,     topLayer.transform);

            // ScheduleConfirmPopup (별도 디자인, ConfirmPopup 프리팹을 복제)
            var scheduleConfirm = InstantiatePrefab(ConfirmPopupPrefab, topLayer.transform);
            if (scheduleConfirm != null)
                scheduleConfirm.name = "ScheduleConfirmPopup";

            // ScheduleResultPopup (스케줄 결과용)
            var scheduleResult = InstantiatePrefab(ScheduleResultPrefab, topLayer.transform);

            // Dimmer (Modal 팝업 배경 어둡게)
            var dimmerGo = CreateChild(popupLayer, "Dimmer");
            SetStretch(dimmerGo);
            var dimmerImg = dimmerGo.AddComponent<Image>();
            dimmerImg.color = new Color(0, 0, 0, 0.5f);
            var dimmerCG = dimmerGo.AddComponent<CanvasGroup>();
            dimmerCG.alpha = 0;
            dimmerGo.SetActive(false);
            // Dimmer를 ModalLayer 앞에 배치
            dimmerGo.transform.SetSiblingIndex(modalLayer.transform.GetSiblingIndex());

            // Modal 프리팹 리스트 (ShopUI는 크로스페이드 패널로 이동했으므로 제외)
            var modalPrefabs = new System.Collections.Generic.List<GameObject>();
            AddPrefabToList(ScheduleHelpPrefab, modalPrefabs);
            AddPrefabToList(SaveLoadPopupPrefab, modalPrefabs);
            AddPrefabToList(GiftPopupPrefab, modalPrefabs);
            AddPrefabToList(PFX + "Phone/PhonePanel.prefab", modalPrefabs);

            // ── 6. 매니저들 ──

            // GameManager
            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<Core.GameManager>();

            // GameState
            var gsGo = new GameObject("GameState");
            gsGo.AddComponent<Story.GameState>();

            // UIManager - SerializedField 바인딩
            var uimGo = new GameObject("UIManager");
            var uim = uimGo.AddComponent<UI.UIManager>();
            BindField(uim, "dialogueUI", dialogueUI?.GetComponent<Story.DialogueUI>());
            BindField(uim, "choiceUI",   dialogueUI != null ? dialogueUI.GetComponentInChildren<Story.ChoiceUI>(true) : choiceUI?.GetComponent<Story.ChoiceUI>());
            BindField(uim, "scheduleUI", scheduleUI?.GetComponent<Schedule.ScheduleUI>());
            BindField(uim, "titleUI",    titleUI?.GetComponent<UI.TitleUI>());
            BindField(uim, "usernameUI", usernameUI?.GetComponent<UI.UsernameUI>());
            BindField(uim, "placeUI",    placeUI?.GetComponent<UI.PlaceUI>());

            // PopupManager - SerializedField 바인딩
            var pmGo = new GameObject("PopupManager");
            var pm = pmGo.AddComponent<UI.PopupManager>();
            BindField(pm, "layerModal", modalLayer.transform);
            BindField(pm, "layerTop",   topLayer.transform);
            BindField(pm, "dimmer",     dimmerGo);
            BindField(pm, "dimmerCanvasGroup", dimmerCG);
            // confirmPopups 배열 바인딩 (ConfirmPopupEntry[])
            {
                var so = new SerializedObject(pm);
                var arr = so.FindProperty("confirmPopups");
                arr.ClearArray();

                void AddEntry(int idx, UI.ConfirmPopupType type, GameObject go)
                {
                    arr.InsertArrayElementAtIndex(idx);
                    var elem = arr.GetArrayElementAtIndex(idx);
                    elem.FindPropertyRelative("type").enumValueIndex = (int)type;
                    elem.FindPropertyRelative("popup").objectReferenceValue = go?.GetComponent<UI.ConfirmPopup>();
                }

                AddEntry(0, UI.ConfirmPopupType.Default, confirmPopup);
                AddEntry(1, UI.ConfirmPopupType.Schedule, scheduleConfirm);
                AddEntry(2, UI.ConfirmPopupType.ScheduleResult, scheduleResult);

                so.ApplyModifiedPropertiesWithoutUndo();
            }
            BindField(pm, "alertPopup",           alertPopup?.GetComponent<UI.AlertPopup>());
            BindField(pm, "toastPopup",           toastPopup?.GetComponent<UI.ToastPopup>());
            BindField(pm, "logPopup",             logPopup?.GetComponent<UI.LogPopup>());
            BindField(pm, "modalPrefabs",         modalPrefabs);

            // ScreenFX
            var fxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenFXPrefab);
            if (fxPrefab != null)
            {
                var fxInstance = (GameObject)PrefabUtility.InstantiatePrefab(fxPrefab);
                fxInstance.name = "ScreenFX";
            }
            else
            {
                // 최소한의 ScreenFX 오브젝트
                var fxGo = new GameObject("ScreenFX");
                fxGo.AddComponent<Core.ScreenFX>();
            }

            // StoryInputHandler (마우스 클릭/Space → 대사 진행)
            var sihGo = new GameObject("StoryInputHandler");
            sihGo.AddComponent<Core.StoryInputHandler>();

            // ScriptRunner (ScheduleUI 결과 토스트 등에서 필요할 수 있음)
            var srGo = new GameObject("ScriptRunner");
            srGo.AddComponent<Story.ScriptRunner>();

            // LoadingScreen (EndDay 흐름에서 필요)
            var lsGo = new GameObject("LoadingScreen");
            lsGo.AddComponent<Core.LoadingScreen>();

            // ── 7. TestController ──
            var testGo = new GameObject("── TestController ──");
            testGo.AddComponent<ScheduleShopTestController>();

            // ── 8. 저장 ──
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[ScheduleShopTestSceneBuilder] 테스트 씬 생성 완료: {ScenePath}");

            // Build Settings에 추가
            AddSceneToBuildSettings(ScenePath);

            EditorUtility.DisplayDialog("완료", $"테스트 씬 생성 완료!\n{ScenePath}\n\n※ 프리팹 바인딩을 인스펙터에서 확인해 주세요.\n    (ChoiceUI가 DialogueUI 자식인 경우 등)", "확인");
        }

        #region Helpers

        /// <summary>
        /// ShopPanel 프리팹을 ScheduleUI 내부에 크로스페이드 패널로 임베드
        /// ScheduleContent 래퍼 생성 → ShopPanel 인스턴스화 → 필드 바인딩
        /// </summary>
        static void EmbedShopPanel(GameObject scheduleUIGo)
        {
            if (scheduleUIGo == null) return;

            var scheduleComp = scheduleUIGo.GetComponent<Schedule.ScheduleUI>();
            if (scheduleComp == null)
            {
                Debug.LogWarning("[TestSceneBuilder] ScheduleUI 컴포넌트를 찾을 수 없음");
                return;
            }

            // 프리팹 인스턴스인 경우 언팩 (자식 재배치를 위해)
            if (PrefabUtility.IsPartOfPrefabInstance(scheduleUIGo))
            {
                PrefabUtility.UnpackPrefabInstance(
                    PrefabUtility.GetOutermostPrefabInstanceRoot(scheduleUIGo),
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                Debug.Log("[TestSceneBuilder] ScheduleUI 프리팹 인스턴스를 언팩했습니다.");
            }

            // ScheduleContent 래퍼 생성 — 기존 자식들을 래핑
            var contentGo = new GameObject("ScheduleContent", typeof(RectTransform), typeof(CanvasGroup));
            contentGo.transform.SetParent(scheduleUIGo.transform, false);
            SetStretch(contentGo);

            // 기존 자식들을 ScheduleContent로 이동
            var children = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < scheduleUIGo.transform.childCount; i++)
            {
                var child = scheduleUIGo.transform.GetChild(i);
                if (child.gameObject != contentGo)
                    children.Add(child);
            }
            foreach (var child in children)
            {
                child.SetParent(contentGo.transform, true);
            }
            contentGo.transform.SetAsFirstSibling();

            // ShopPanel 인스턴스화
            var shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPanelPrefab);
            if (shopPrefab == null)
            {
                Debug.LogWarning($"[TestSceneBuilder] ShopPanel 프리팹 없음: {ShopPanelPrefab}");
                return;
            }

            var shopInstance = (GameObject)PrefabUtility.InstantiatePrefab(shopPrefab, scheduleUIGo.transform);
            shopInstance.name = "ShopPanel";
            shopInstance.SetActive(false);

            var shopContentCG = shopInstance.GetComponent<CanvasGroup>();
            if (shopContentCG == null)
                shopContentCG = shopInstance.AddComponent<CanvasGroup>();

            var shopPopup = shopInstance.GetComponent<Shop.ShopPopup>();

            // ScheduleUI 필드 바인딩
            var so = new SerializedObject(scheduleComp);
            var scheduleContentProp = so.FindProperty("scheduleContent");
            var shopContentProp = so.FindProperty("shopContent");
            var shopPanelProp = so.FindProperty("shopPanel");

            if (scheduleContentProp != null)
                scheduleContentProp.objectReferenceValue = contentGo.GetComponent<CanvasGroup>();
            if (shopContentProp != null)
                shopContentProp.objectReferenceValue = shopContentCG;
            if (shopPanelProp != null)
                shopPanelProp.objectReferenceValue = shopPopup;

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[TestSceneBuilder] ShopPanel을 ScheduleUI에 임베드 완료");
        }

        static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static void SetStretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static GameObject InstantiatePrefab(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[TestSceneBuilder] 프리팹 없음: {path}");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return go;
        }

        static void AddPrefabToList(string path, System.Collections.Generic.List<GameObject> list)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                list.Add(prefab);
            else
                Debug.LogWarning($"[TestSceneBuilder] Modal 프리팹 없음: {path}");
        }

        static void BindField<T>(MonoBehaviour target, string fieldName, T value) where T : class
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null && value is UnityEngine.Object obj)
            {
                prop.objectReferenceValue = obj;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (prop != null && value is System.Collections.Generic.List<GameObject> list)
            {
                prop.ClearArray();
                foreach (var item in list)
                {
                    prop.InsertArrayElementAtIndex(prop.arraySize);
                    prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = item;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (prop == null)
            {
                Debug.LogWarning($"[TestSceneBuilder] 필드 바인딩 실패: {target.GetType().Name}.{fieldName}");
            }
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool exists = false;
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    exists = true;
                    s.enabled = true;
                    break;
                }
            }
            if (!exists)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[TestSceneBuilder] Build Settings에 씬 추가: {scenePath}");
        }

        #endregion
    }
}
