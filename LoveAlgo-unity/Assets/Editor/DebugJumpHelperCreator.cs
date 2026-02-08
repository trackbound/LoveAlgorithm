using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// DebugJumpHelper UI 자동 생성 에디터 툴
    /// </summary>
    public class DebugJumpHelperCreator : EditorWindow
    {
        [MenuItem("LoveAlgo/Tools/Create Debug Jump Helper UI")]
        static void CreateDebugJumpHelperUI()
        {
            // 1. Canvas 생성
            var canvasGO = new GameObject("Canvas_DebugJumpHelper");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;  // 최상위
            
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasGO.AddComponent<GraphicRaycaster>();

            // 2. Root 생성 (화면 전체 Stretch)
            var rootGO = new GameObject("DebugJumpHelper_Root");
            rootGO.transform.SetParent(canvasGO.transform, false);
            
            var rootRect = rootGO.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            
            // Root에는 Image 없음 (투명, 클릭 통과)

            // 3. Panel 생성 (왼쪽 중앙 정렬)
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(rootGO.transform, false);
            
            var panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0.5f);
            panelRect.anchorMax = new Vector2(0, 0.5f);
            panelRect.pivot = new Vector2(0, 0.5f);
            panelRect.anchoredPosition = new Vector2(20, 0);
            panelRect.sizeDelta = new Vector2(280, 450);
            
            var panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            var panelLayout = panelGO.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(15, 15, 15, 15);
            panelLayout.spacing = 8;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = false;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            var panelFitter = panelGO.AddComponent<ContentSizeFitter>();
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 4. 타이틀 생성
            CreateLabel(panelGO.transform, "🎮 QA Demo Helper", 22, FontStyles.Bold);
            CreateLabel(panelGO.transform, "F2 to toggle", 12, FontStyles.Italic, new Color(0.7f, 0.7f, 0.7f));

            // 5. 구분선
            CreateSeparator(panelGO.transform);

            // 6. 버튼 컨테이너 생성
            var containerGO = new GameObject("ButtonContainer");
            containerGO.transform.SetParent(panelGO.transform, false);
            
            var containerRect = containerGO.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(250, 0);
            
            var containerLayout = containerGO.AddComponent<VerticalLayoutGroup>();
            containerLayout.spacing = 6;
            containerLayout.childControlWidth = true;
            containerLayout.childControlHeight = false;
            containerLayout.childForceExpandWidth = true;
            containerLayout.childForceExpandHeight = false;

            // 7. 버튼 프리팹 생성 (템플릿, 비활성화)
            var templateBtn = CreateButton(containerGO.transform, "Template Button");
            templateBtn.gameObject.SetActive(false);

            // 8. 구분선 + 닫기 버튼
            CreateSeparator(panelGO.transform);
            var closeBtn = CreateButton(panelGO.transform, "✕ Close", new Color(0.5f, 0.2f, 0.2f, 1f));

            // 9. DebugJumpHelper 컴포넌트 추가 (Root에)
            var helper = rootGO.AddComponent<Tester.DebugJumpHelper>();
            
            // SerializedObject로 private 필드 설정
            var so = new SerializedObject(helper);
            so.FindProperty("panelRoot").objectReferenceValue = panelGO;
            so.FindProperty("buttonContainer").objectReferenceValue = containerGO.transform;
            so.FindProperty("buttonPrefab").objectReferenceValue = templateBtn;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedProperties();

            // 패널 비활성화 (F2로 토글)
            panelGO.SetActive(false);

            // Undo 등록
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Debug Jump Helper");

            Selection.activeGameObject = rootGO;
            EditorUtility.DisplayDialog("완료", 
                "DebugJumpHelper UI가 생성되었습니다!\n\n" +
                "• F2 키로 패널 토글\n" +
                "• 점프 위치는 Inspector에서 수정 가능\n" +
                "• Canvas_DebugJumpHelper가 생성됨", 
                "확인");
            
            Debug.Log("[DebugJumpHelperCreator] UI 생성 완료!");
        }

        static TMP_Text CreateLabel(Transform parent, string text, int fontSize, FontStyles style, Color? color = null)
        {
            var go = new GameObject("Label_" + text.Substring(0, Mathf.Min(10, text.Length)));
            go.transform.SetParent(parent, false);
            
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250, fontSize + 10);
            
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color ?? Color.white;
            
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = fontSize + 10;
            
            return tmp;
        }

        static void CreateSeparator(Transform parent)
        {
            var go = new GameObject("Separator");
            go.transform.SetParent(parent, false);
            
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250, 2);
            
            var image = go.AddComponent<Image>();
            image.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 2;
            layout.flexibleWidth = 1;
        }

        static Button CreateButton(Transform parent, string text, Color? bgColor = null)
        {
            var go = new GameObject("Btn_" + text.Replace(" ", "").Replace("✕", "Close"));
            go.transform.SetParent(parent, false);
            
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250, 42);
            
            var image = go.AddComponent<Image>();
            image.color = bgColor ?? new Color(0.25f, 0.28f, 0.35f, 1f);
            
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.4f, 0.5f, 1f);
            colors.pressedColor = new Color(0.2f, 0.22f, 0.28f, 1f);
            btn.colors = colors;
            
            // 텍스트
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);
            
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 42;
            
            return btn;
        }

        [MenuItem("LoveAlgo/Tools/Create Debug Jump Helper UI", true)]
        static bool ValidateCreate()
        {
            return !Application.isPlaying;
        }
    }
}
