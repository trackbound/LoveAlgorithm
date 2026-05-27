#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoveAlgo.Contracts;
using LoveAlgo.Phone;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LoveAlgo.PhoneEditor
{
    /// <summary>
    /// 채팅창 / 메신저 UI 디자이너 — 디자인 라이브 튜닝 + Mockup carousel + 샘플 메시지 spawn.
    /// 메뉴: Tools > LoveAlgo > Phone > Phone UI Designer
    /// </summary>
    public class PhoneUIDesignerWindow : EditorWindow
    {
        const string MOCKUP_DIR = "Assets/_Project/Modules/Phone/Art/Mockups";
        const string OVERLAY_NAME = "__PhoneMockupOverlay__";
        const string CHATROOM_PATH_HINT = "PhoneChatRoom"; // GameObject 이름 또는 컴포넌트 검색

        PhoneUIDesignConfig cfg;
        SerializedObject so;
        Vector2 scroll;

        // Mockup carousel
        Sprite[] mockups = new Sprite[0];
        int mockupIdx;
        float overlayAlpha = 0.5f;
        bool overlayInFront = true;

        // Live update
        bool liveUpdate;
        double pendingApplyAt = -1;
        const double APPLY_DEBOUNCE = 0.2;

        [MenuItem("Tools/LoveAlgo/Phone/Phone UI Designer")]
        public static void Open()
        {
            var w = GetWindow<PhoneUIDesignerWindow>("Phone UI Designer");
            w.minSize = new Vector2(420, 640);
            w.Show();
        }

        void OnEnable()
        {
            ReloadConfig();
            ReloadMockups();
            EditorApplication.update += OnEditorUpdate;
        }
        void OnDisable() => EditorApplication.update -= OnEditorUpdate;

        void ReloadConfig()
        {
            cfg = ChatBubbleBuilder.LoadOrCreateConfig();
            so = new SerializedObject(cfg);
        }

        void ReloadMockups()
        {
            if (!Directory.Exists(MOCKUP_DIR))
            {
                Directory.CreateDirectory(MOCKUP_DIR);
                AssetDatabase.Refresh();
            }
            mockups = AssetDatabase.FindAssets("t:Sprite", new[] { MOCKUP_DIR })
                .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                .ToArray();
            if (mockupIdx >= mockups.Length) mockupIdx = Mathf.Max(0, mockups.Length - 1);
        }

        void OnEditorUpdate()
        {
            if (pendingApplyAt > 0 && EditorApplication.timeSinceStartup >= pendingApplyAt)
            {
                pendingApplyAt = -1;
                ApplyAndRefresh();
            }
        }

        void OnGUI()
        {
            if (cfg == null) ReloadConfig();
            if (so == null) return;
            so.Update();

            EditorGUILayout.LabelField("Phone UI Designer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // === 액션 ===
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
                if (GUILayout.Button("Apply ▶ (Rebuild + Re-Spawn)", GUILayout.Height(32)))
                {
                    so.ApplyModifiedProperties();
                    ApplyAndRefresh();
                }
                GUI.backgroundColor = Color.white;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild ChatBubble"))
                {
                    so.ApplyModifiedProperties();
                    ChatBubbleBuilder.Rebuild(cfg);
                }
                if (GUILayout.Button("Reload"))
                {
                    ReloadConfig();
                    ReloadMockups();
                }
            }
            liveUpdate = EditorGUILayout.ToggleLeft("Live Update (값 변경 시 자동 Apply)", liveUpdate);

            // === 샘플 메시지 ===
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Sample Messages", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Spawn Sample (c01)")) SpawnSampleMessages("c01");
                if (GUILayout.Button("Clear")) ClearSampleMessages();
            }
            EditorGUILayout.HelpBox("씬에 PhoneChatRoom이 활성 상태여야 함. 폰 팝업 열고 채팅방 진입 후 Spawn.", MessageType.None);

            // === Mockup Carousel ===
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Mockup Carousel", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"폴더: {MOCKUP_DIR} (PNG/JPG 자동 검색, 총 {mockups.Length}장)", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀", GUILayout.Width(40))) NextMockup(-1);
                EditorGUILayout.LabelField(mockups.Length > 0 ? $"[{mockupIdx + 1}/{mockups.Length}] {mockups[mockupIdx].name}" : "(목업 없음)", EditorStyles.boldLabel);
                if (GUILayout.Button("▶", GUILayout.Width(40))) NextMockup(+1);
            }

            // 썸네일 프리뷰
            if (mockups.Length > 0)
            {
                var tex = AssetPreview.GetAssetPreview(mockups[mockupIdx]) ?? mockups[mockupIdx].texture;
                if (tex != null)
                {
                    var rect = GUILayoutUtility.GetRect(position.width - 20, 180);
                    GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show Overlay")) ShowOverlay();
                if (GUILayout.Button("Hide Overlay")) HideOverlay();
            }
            using (var ch = new EditorGUI.ChangeCheckScope())
            {
                overlayAlpha = EditorGUILayout.Slider("Alpha", overlayAlpha, 0f, 1f);
                overlayInFront = EditorGUILayout.Toggle("In Front", overlayInFront);
                if (ch.changed) ApplyOverlaySettings();
            }

            // 키보드 단축키 안내
            EditorGUILayout.HelpBox("단축키: ◀ ▶ 또는 키보드 ↑↓ (윈도우 포커스 시)", MessageType.None);
            HandleArrowKeys();

            // === 디자인 상수 ===
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Design Constants", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawAllProperties();
            EditorGUILayout.EndScrollView();

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(cfg);
                if (liveUpdate) pendingApplyAt = EditorApplication.timeSinceStartup + APPLY_DEBOUNCE;
            }
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

        void HandleArrowKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.UpArrow) { NextMockup(-1); e.Use(); }
            else if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.DownArrow) { NextMockup(+1); e.Use(); }
        }

        void NextMockup(int delta)
        {
            if (mockups.Length == 0) return;
            mockupIdx = (mockupIdx + delta + mockups.Length) % mockups.Length;
            ApplyOverlaySettings();
            Repaint();
        }

        // === Apply ===
        void ApplyAndRefresh()
        {
            ChatBubbleBuilder.Rebuild(cfg);
            // ChatRoom 내 spacing/padding은 빌더가 아닌 PhoneChatRoom prefab의 직접 수정이라 추후 확장
            // 일단 ChatBubble 갱신만으로도 가시 효과 큼
            // 씬에 spawned sample 있으면 re-spawn
            var chatRoom = FindActiveChatRoom();
            if (chatRoom != null)
            {
                bool had = HasSampleChildren(chatRoom.transform);
                if (had)
                {
                    ClearSampleMessages();
                    SpawnSampleMessages("c01");
                }
            }
        }

        // === Mockup overlay ===
        void ShowOverlay()
        {
            if (mockups.Length == 0)
            {
                EditorUtility.DisplayDialog("Phone UI Designer", $"목업 없음. {MOCKUP_DIR}에 PNG 추가.", "OK");
                return;
            }
            var canvas = FindActiveCanvas();
            if (canvas == null) { EditorUtility.DisplayDialog("Phone UI Designer", "활성 Canvas 없음.", "OK"); return; }
            var overlay = GetOrCreateOverlay(canvas, mockups[mockupIdx]);
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
            if (existing == null) return;
            var img = existing.GetComponent<Image>();
            if (img == null) return;
            if (mockups.Length > 0) img.sprite = mockups[mockupIdx];
            ApplyOverlaySettings(img);
        }

        void ApplyOverlaySettings(Image overlay)
        {
            if (overlay == null) return;
            overlay.color = new Color(1, 1, 1, overlayAlpha);
            if (overlayInFront) overlay.transform.SetAsLastSibling();
            else overlay.transform.SetAsFirstSibling();
        }

        static Image GetOrCreateOverlay(Canvas canvas, Sprite mockup)
        {
            var existing = canvas.transform.Find(OVERLAY_NAME);
            if (existing != null)
            {
                var im = existing.GetComponent<Image>();
                if (im != null) im.sprite = mockup;
                return im;
            }
            var go = new GameObject(OVERLAY_NAME, typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            Undo.RegisterCreatedObjectUndo(go, "Create Phone Mockup Overlay");
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

        // === Sample messages spawn ===
        struct SampleMsg { public bool self; public string text; }
        static readonly SampleMsg[] SAMPLES = {
            new() { self = false, text = "안녕!" },
            new() { self = false, text = "난 로아라고 해." },
            new() { self = true,  text = "날 닮은 너, 너 누구야?" },
            new() { self = false, text = "사건은 다가와, ah-oh, ayy\n거세게 커져가, ah-oh, ayy" },
            new() { self = true,  text = "지금 내 안에선,\nsu-su-su-supernova" },
            new() { self = false, text = "원초 그걸 찾아" },
        };

        void SpawnSampleMessages(string heroineId)
        {
            var chatRoom = FindActiveChatRoom();
            if (chatRoom == null)
            {
                EditorUtility.DisplayDialog("Phone UI Designer", "활성 씬에서 PhoneChatRoom을 찾지 못함. 폰 팝업 열고 채팅방 진입 후 시도.", "OK");
                return;
            }
            using var sop = new SerializedObject(chatRoom);
            var container = sop.FindProperty("messageContainer").objectReferenceValue as Transform;
            var selfPrefab = sop.FindProperty("selfBubblePrefab").objectReferenceValue as ChatBubble;
            var otherPrefab = sop.FindProperty("otherBubblePrefab").objectReferenceValue as ChatBubble;
            if (container == null || selfPrefab == null || otherPrefab == null)
            {
                EditorUtility.DisplayDialog("Phone UI Designer", "PhoneChatRoom 필드(messageContainer/selfBubblePrefab/otherBubblePrefab) 미설정.", "OK");
                return;
            }

            ClearSampleChildren(container);
            foreach (var s in SAMPLES)
            {
                var prefab = s.self ? selfPrefab : otherPrefab;
                var bubble = (ChatBubble)PrefabUtility.InstantiatePrefab(prefab, container);
                bubble.gameObject.name = "__Sample_" + (s.self ? "Self" : "Other");
                // ChatBubble.Setup은 ChatMessage 받아서 messageText.text 설정 — 직접 TMP 찾아 텍스트만 주입
                var tmp = bubble.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (tmp != null) tmp.text = s.text;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)container);
            EditorSceneManager.MarkSceneDirty(chatRoom.gameObject.scene);
        }

        void ClearSampleMessages()
        {
            var chatRoom = FindActiveChatRoom();
            if (chatRoom == null) return;
            using var sop = new SerializedObject(chatRoom);
            var container = sop.FindProperty("messageContainer").objectReferenceValue as Transform;
            if (container != null) ClearSampleChildren(container);
        }

        static void ClearSampleChildren(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var c = container.GetChild(i).gameObject;
                if (c.name.StartsWith("__Sample_")) Object.DestroyImmediate(c);
            }
        }

        static bool HasSampleChildren(Transform container)
        {
            for (int i = 0; i < container.childCount; i++)
                if (container.GetChild(i).name.StartsWith("__Sample_")) return true;
            return false;
        }

        // === Scene 검색 ===
        static PhoneChatRoom FindActiveChatRoom()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                var p = stage.prefabContentsRoot.GetComponentInChildren<PhoneChatRoom>(true);
                if (p != null) return p;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (var root in s.GetRootGameObjects())
                {
                    var p = root.GetComponentInChildren<PhoneChatRoom>(true);
                    if (p != null) return p;
                }
            }
            return null;
        }

        static Canvas FindActiveCanvas()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                var c = stage.prefabContentsRoot.GetComponentInChildren<Canvas>(true);
                if (c != null) return c;
            }
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
    }
}
#endif
