#if UNITY_EDITOR
using System.Collections.Generic;
using LoveAlgo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// 로그 UI 디자이너 — 디자인 상수 라이브 편집 + 목업 오버레이.
    /// 메뉴: Tools > LoveAlgo > Log UI Designer
    /// </summary>
    public class LogUIDesignerWindow : EditorWindow
    {
        const string MOCKUP_PATH = "Assets/_Project/Modules/Narrative/Art/Log/_mockup.png";
        const string OVERLAY_NAME = "__LogMockupOverlay__";

        LogUIDesignConfig cfg;
        SerializedObject so;
        Vector2 scroll;
        float overlayAlpha = 0.5f;
        bool overlayInFront = true;
        bool liveUpdate;
        double pendingApplyAt = -1;
        const double APPLY_DEBOUNCE = 0.15; // 슬라이더 드래그 종료 후 0.15초 뒤 적용

        [MenuItem("Tools/LoveAlgo/Log UI Designer")]
        public static void Open()
        {
            var w = GetWindow<LogUIDesignerWindow>("Log UI Designer");
            w.minSize = new Vector2(380, 600);
            w.Show();
        }

        void OnEnable()
        {
            ReloadConfig();
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        void OnEditorUpdate()
        {
            if (pendingApplyAt > 0 && EditorApplication.timeSinceStartup >= pendingApplyAt)
            {
                pendingApplyAt = -1;
                ApplyAndRefresh();
            }
        }

        void ReloadConfig()
        {
            cfg = LogUIRebuilder.LoadOrCreateConfig();
            LogUIRebuilder.ResolveAutoRefs(cfg);
            so = new SerializedObject(cfg);
        }

        void OnGUI()
        {
            if (cfg == null) { ReloadConfig(); }
            if (so == null) return;
            so.Update();

            // ── 액션 버튼 ────────────────────────────────
            EditorGUILayout.LabelField("Log UI Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
                if (GUILayout.Button("Apply ▶ (Rebuild + Re-Spawn)", GUILayout.Height(34)))
                {
                    so.ApplyModifiedProperties();
                    ApplyAndRefresh();
                }
                GUI.backgroundColor = Color.white;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Prefabs"))
                {
                    so.ApplyModifiedProperties();
                    LogUIRebuilder.Rebuild(cfg);
                }
                if (GUILayout.Button("Reload Config")) ReloadConfig();
            }
            liveUpdate = EditorGUILayout.ToggleLeft("Live Update (값 변경 시 자동 Apply)", liveUpdate);

            // ── 샘플 엔트리 ──────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Sample Entries (목업 데이터)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Spawn Sample")) SpawnSampleEntries();
                if (GUILayout.Button("Clear")) ClearSpawnedEntries();
            }
            EditorGUILayout.HelpBox("활성 씬 또는 LogPopup Prefab Stage에서 contentRoot에 목업 텍스트로 엔트리를 채움. Rebuild 후 다시 Spawn하면 갱신.", MessageType.None);

            // ── 목업 오버레이 ────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Mockup Overlay", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show Overlay")) ShowOverlay();
                if (GUILayout.Button("Hide Overlay")) HideOverlay();
            }
            using (var ch = new EditorGUI.ChangeCheckScope())
            {
                overlayAlpha = EditorGUILayout.Slider("Alpha", overlayAlpha, 0f, 1f);
                overlayInFront = EditorGUILayout.Toggle("In Front (위)", overlayInFront);
                if (ch.changed) ApplyOverlaySettings();
            }
            EditorGUILayout.HelpBox($"Overlay 이미지 경로: {MOCKUP_PATH}", MessageType.None);

            // ── 디자인 상수 ──────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Design Constants", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawAllProperties();

            EditorGUILayout.EndScrollView();

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(cfg);
                if (liveUpdate)
                    pendingApplyAt = EditorApplication.timeSinceStartup + APPLY_DEBOUNCE;
            }
        }

        /// <summary>Config 값을 프리팹에 반영 + 씬 샘플 인스턴스 갱신.</summary>
        void ApplyAndRefresh()
        {
            LogUIRebuilder.Rebuild(cfg);
            var popup = FindLogPopup();
            if (popup == null) return;
            using var sop = new SerializedObject(popup);
            var contentRoot = sop.FindProperty("contentRoot").objectReferenceValue as RectTransform;
            if (contentRoot == null) return;
            bool hadSamples = HasSampleChildren(contentRoot);
            if (hadSamples)
            {
                ClearSpawnedChildren(contentRoot);
                SpawnSampleEntries();
            }
        }

        static bool HasSampleChildren(RectTransform contentRoot)
        {
            for (int i = 0; i < contentRoot.childCount; i++)
                if (contentRoot.GetChild(i).name.StartsWith("__Sample_")) return true;
            return false;
        }

        void DrawAllProperties()
        {
            var p = so.GetIterator();
            bool enterChildren = true;
            while (p.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (p.name == "m_Script") continue;
                EditorGUILayout.PropertyField(p, true);
            }
        }

        // ── Overlay 로직 ─────────────────────────────────
        void ShowOverlay()
        {
            var mockup = AssetDatabase.LoadAssetAtPath<Sprite>(MOCKUP_PATH);
            if (mockup == null)
            {
                EditorUtility.DisplayDialog("Mockup not found",
                    $"목업 PNG가 없음: {MOCKUP_PATH}\n해당 경로에 _mockup.png를 넣어줘.", "OK");
                return;
            }

            var canvas = FindActiveCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Canvas not found",
                    "활성 씬에 Canvas가 없어. LogPopup이 포함된 씬을 먼저 열어줘.", "OK");
                return;
            }

            var overlay = GetOrCreateOverlay(canvas, mockup);
            ApplyOverlaySettings(overlay);
            Selection.activeGameObject = overlay.gameObject;
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        }

        void HideOverlay()
        {
            var canvas = FindActiveCanvas();
            if (canvas == null) return;
            var existing = canvas.transform.Find(OVERLAY_NAME);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            }
        }

        void ApplyOverlaySettings()
        {
            var canvas = FindActiveCanvas();
            if (canvas == null) return;
            var existing = canvas.transform.Find(OVERLAY_NAME);
            if (existing != null) ApplyOverlaySettings(existing.GetComponent<Image>());
        }

        void ApplyOverlaySettings(Image overlay)
        {
            if (overlay == null) return;
            overlay.color = new Color(1, 1, 1, overlayAlpha);
            if (overlayInFront) overlay.transform.SetAsLastSibling();
            else overlay.transform.SetAsFirstSibling();
        }

        static Canvas FindActiveCanvas()
        {
            // Prefab Stage 우선
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                var c = stage.prefabContentsRoot.GetComponentInChildren<Canvas>(true);
                if (c != null) return c;
            }
            // 활성 씬에서 첫 번째 Canvas
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (var root in s.GetRootGameObjects())
                {
                    var c = root.GetComponentInChildren<Canvas>(true);
                    if (c != null) return c;
                }
            }
            return null;
        }

        // ── 샘플 엔트리 ──────────────────────────────────
        struct SampleLine { public string speaker, characterId, text; }

        static readonly SampleLine[] SAMPLE = new[]
        {
            new SampleLine{ speaker="로아",       characterId="roa",     text="이야기는 내일이야! 지금 벌써 밤 12시라구.\n같은 씬 (한 스크립트 창에 표시되면) 이렇게 같은 박스에 들어갑니다." },
            new SampleLine{ speaker="로아",       characterId="roa",     text="다음 스크립트에서 한 인물이 계속해서 얘기하면 새로운 박스가 생깁니다." },
            new SampleLine{ speaker="한예은",     characterId="hayeeun", text="좌측 썸네일보다 대화박스 높이가 작은 경우 좌측 썸네일과 가운데 정렬됩니다." },
            new SampleLine{ speaker="여덟글자유저닉넴", characterId="",    text="주인공이 말하는 대사가 뜨는 부분입니다. 정렬 규칙은 히로인과 같습니다." },
            new SampleLine{ speaker="",           characterId="",         text="데이터 로드가 끝나자마자 하는 첫 마디가 잔소리라니. 아무래도 설정을 잘못한 것 같다." },
            new SampleLine{ speaker="",           characterId="",         text="주인공이 말하지 않고 속으로 생각하는 나레이션입니다. 텍스트 그림자효과 가능한가요?" },
            new SampleLine{ speaker="로아",       characterId="roa",     text="어, 지금 재미없는 잔소리 했다고 생각했지!" },
        };

        void SpawnSampleEntries()
        {
            var popup = FindLogPopup();
            if (popup == null) { EditorUtility.DisplayDialog("LogPopup not found", "활성 씬 또는 Prefab Stage에서 LogPopup을 찾지 못함.", "OK"); return; }

            using var sop = new SerializedObject(popup);
            var contentRoot = sop.FindProperty("contentRoot").objectReferenceValue as RectTransform;
            var dialogueEntry = sop.FindProperty("dialogueEntryPrefab").objectReferenceValue as LogEntryBase;
            var narrationEntry = sop.FindProperty("narrationEntryPrefab").objectReferenceValue as LogEntryBase;
            var emptyMsg = sop.FindProperty("emptyMessage").objectReferenceValue as GameObject;
            var portraitsProp = sop.FindProperty("portraits");

            if (contentRoot == null || dialogueEntry == null || narrationEntry == null)
            {
                EditorUtility.DisplayDialog("Missing refs", "LogPopup의 contentRoot/엔트리 프리팹 필드가 비어있음.", "OK"); return;
            }

            // 초상화 룩업
            var portraits = new Dictionary<string, Sprite>();
            if (portraitsProp != null && portraitsProp.isArray)
            {
                for (int i = 0; i < portraitsProp.arraySize; i++)
                {
                    var item = portraitsProp.GetArrayElementAtIndex(i);
                    var cid = item.FindPropertyRelative("characterId")?.stringValue;
                    var spr = item.FindPropertyRelative("sprite")?.objectReferenceValue as Sprite;
                    if (!string.IsNullOrEmpty(cid) && spr != null) portraits[cid.ToLower()] = spr;
                }
            }

            ClearSpawnedChildren(contentRoot);
            if (emptyMsg != null) emptyMsg.SetActive(false);

            string lastSpeaker = null, lastCharId = null;
            LogEntryBase lastGroup = null;
            foreach (var line in SAMPLE)
            {
                bool isNarration = string.IsNullOrEmpty(line.speaker);
                bool sameGroup = lastGroup != null && line.speaker == lastSpeaker && line.characterId == lastCharId;

                if (!sameGroup)
                {
                    LogEntryBase prefab;
                    Sprite portrait = null;
                    bool isUser = false;
                    if (isNarration) prefab = narrationEntry;
                    else
                    {
                        prefab = dialogueEntry;
                        isUser = string.IsNullOrEmpty(line.characterId);
                        if (!isUser && !string.IsNullOrEmpty(line.characterId))
                            portraits.TryGetValue(line.characterId.ToLower(), out portrait);
                    }

                    var go = (LogEntryBase)PrefabUtility.InstantiatePrefab(prefab, contentRoot);
                    go.gameObject.name = $"__Sample_{(isNarration ? "N" : isUser ? "U" : !string.IsNullOrEmpty(line.characterId) && portrait != null ? "H" : "E")}_{line.speaker}";
                    go.Init(line.speaker, portrait, isUser);
                    lastGroup = go;
                    lastSpeaker = line.speaker;
                    lastCharId = line.characterId;
                }
                lastGroup.AddLine(line.text);
            }

            // 레이아웃 즉시 갱신
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);

            var scene = popup.gameObject.scene;
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
        }

        void ClearSpawnedEntries()
        {
            var popup = FindLogPopup();
            if (popup == null) return;
            using var sop = new SerializedObject(popup);
            var contentRoot = sop.FindProperty("contentRoot").objectReferenceValue as RectTransform;
            if (contentRoot != null) ClearSpawnedChildren(contentRoot);
        }

        static void ClearSpawnedChildren(RectTransform contentRoot)
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var c = contentRoot.GetChild(i).gameObject;
                if (c.name.StartsWith("__Sample_")) Object.DestroyImmediate(c);
            }
        }

        static LogPopup FindLogPopup()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                var p = stage.prefabContentsRoot.GetComponentInChildren<LogPopup>(true);
                if (p != null) return p;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (var root in s.GetRootGameObjects())
                {
                    var p = root.GetComponentInChildren<LogPopup>(true);
                    if (p != null) return p;
                }
            }
            return null;
        }

        static Image GetOrCreateOverlay(Canvas canvas, Sprite mockup)
        {
            var existing = canvas.transform.Find(OVERLAY_NAME);
            if (existing != null) return existing.GetComponent<Image>();

            var go = new GameObject(OVERLAY_NAME, typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            Undo.RegisterCreatedObjectUndo(go, "Create Mockup Overlay");
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = mockup;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }
    }
}
#endif
