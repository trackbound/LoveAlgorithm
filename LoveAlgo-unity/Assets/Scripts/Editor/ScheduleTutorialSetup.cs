using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.Schedule;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 스케줄 튜토리얼 오버레이 프리팹 자동 셋업
    /// Window → LoveAlgo → Schedule Tutorial Setup
    /// </summary>
    public static class ScheduleTutorialSetup
    {
        const string PrefabPath = "Assets/Prefabs/Schedule/Schedule.prefab";
        const string SpriteFolder = "Assets/Art/UI/ScheduleTutorial";

        [MenuItem("Window/LoveAlgo/Schedule Tutorial Setup")]
        static void Run()
        {
            // ─── 1. 프리팹 열기 ───
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[TutorialSetup] 프리팹을 찾을 수 없습니다: {PrefabPath}");
                return;
            }

            // 프리팹 편집 모드로 열기
            var contentsRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            var scheduleUI = contentsRoot.GetComponent<ScheduleUI>();
            if (scheduleUI == null)
            {
                Debug.LogError("[TutorialSetup] ScheduleUI 컴포넌트를 찾을 수 없습니다.");
                PrefabUtility.UnloadPrefabContents(contentsRoot);
                return;
            }

            // 기존 오버레이가 있으면 삭제
            var existing = contentsRoot.transform.Find("TutorialOverlay");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                Debug.Log("[TutorialSetup] 기존 TutorialOverlay 삭제됨.");
            }

            // ─── 2. 오버레이 루트 생성 ───
            var overlayRoot = CreateUI("TutorialOverlay", contentsRoot.transform);
            Stretch(overlayRoot);

            // CanvasGroup
            var canvasGroup = overlayRoot.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            // 클릭 수신용 투명 Image + Button
            var clickImage = overlayRoot.AddComponent<Image>();
            clickImage.color = new Color(0f, 0f, 0f, 0f); // 완전 투명
            clickImage.raycastTarget = true;

            // ScheduleTutorialOverlay 컴포넌트
            var overlay = overlayRoot.AddComponent<ScheduleTutorialOverlay>();

            // Button → OnClick 연결
            var button = overlayRoot.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            // UnityEvent persistent call 추가
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                button.onClick, overlay.OnClick);

            // ─── 3. DimImage ───
            var dimGO = CreateUI("DimImage", overlayRoot.transform);
            Stretch(dimGO);
            var dimImage = dimGO.AddComponent<Image>();
            dimImage.color = Color.white;
            dimImage.raycastTarget = false;
            dimImage.enabled = false; // 초기 비활성

            // ─── 4. RoaImage ───
            var roaGO = CreateUI("RoaImage", overlayRoot.transform);
            var roaRT = roaGO.GetComponent<RectTransform>();
            // 좌하단 기준, 캐릭터 위치 (456×396 원본)
            roaRT.anchorMin = new Vector2(0f, 0f);
            roaRT.anchorMax = new Vector2(0f, 0f);
            roaRT.pivot = new Vector2(0f, 0f);
            roaRT.anchoredPosition = new Vector2(50f, 290f);
            roaRT.sizeDelta = new Vector2(456f, 396f);
            var roaImage = roaGO.AddComponent<Image>();
            roaImage.raycastTarget = false;
            roaImage.preserveAspect = true;

            // ─── 5. Textbox ───
            var textboxGO = CreateUI("Textbox", overlayRoot.transform);
            var textboxRT = textboxGO.GetComponent<RectTransform>();
            // 좌하단 기준, 텍스트박스 위치 (999×281 원본)
            textboxRT.anchorMin = new Vector2(0f, 0f);
            textboxRT.anchorMax = new Vector2(0f, 0f);
            textboxRT.pivot = new Vector2(0f, 0f);
            textboxRT.anchoredPosition = new Vector2(80f, 0f);
            textboxRT.sizeDelta = new Vector2(999f, 281f);
            var textboxImage = textboxGO.AddComponent<Image>();
            textboxImage.raycastTarget = false;
            textboxImage.preserveAspect = true;

            // ─── 6. DialogueText (Textbox 자식) ───
            var textGO = CreateUI("DialogueText", textboxGO.transform);
            var textRT = textGO.GetComponent<RectTransform>();
            // Textbox 내부에 패딩 적용
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(40f, 30f);  // left, bottom 패딩
            textRT.offsetMax = new Vector2(-40f, -40f); // right, top 패딩
            var dialogueText = textGO.AddComponent<TextMeshProUGUI>();
            dialogueText.fontSize = 32f;
            dialogueText.color = Color.white;
            dialogueText.alignment = TextAlignmentOptions.TopLeft;
            dialogueText.raycastTarget = false;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.overflowMode = TextOverflowModes.Overflow;

            // ─── 7. 스프라이트 로드 & 바인딩 ───
            var dimSprites = LoadSprites("tutorial_dim_", 11);
            var roaSprites = new Sprite[]
            {
                LoadSprite("tutorial_roa_default"),
                LoadSprite("tutorial_roa_eyeSmile"),
                LoadSprite("tutorial_roa_brightSmile"),
            };
            var textboxSprite = LoadSprite("tutorial_textbox");

            // textbox에 스프라이트 지정
            if (textboxSprite != null)
            {
                textboxImage.sprite = textboxSprite;
                textboxImage.type = Image.Type.Sliced;
            }

            // roa 기본 스프라이트 지정
            if (roaSprites[0] != null)
                roaImage.sprite = roaSprites[0];

            // ─── 8. SerializedObject로 필드 바인딩 ───
            var so = new SerializedObject(overlay);
            so.FindProperty("overlayGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("dimImage").objectReferenceValue = dimImage;
            so.FindProperty("roaImage").objectReferenceValue = roaImage;
            so.FindProperty("textboxImage").objectReferenceValue = textboxImage;
            so.FindProperty("dialogueText").objectReferenceValue = dialogueText;

            // dimSprites 배열
            var dimProp = so.FindProperty("dimSprites");
            dimProp.arraySize = dimSprites.Length;
            for (int i = 0; i < dimSprites.Length; i++)
                dimProp.GetArrayElementAtIndex(i).objectReferenceValue = dimSprites[i];

            // roaSprites 배열
            var roaProp = so.FindProperty("roaSprites");
            roaProp.arraySize = roaSprites.Length;
            for (int i = 0; i < roaSprites.Length; i++)
                roaProp.GetArrayElementAtIndex(i).objectReferenceValue = roaSprites[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            // ─── 9. ScheduleUI에 tutorialOverlay 바인딩 ───
            var suiSO = new SerializedObject(scheduleUI);
            suiSO.FindProperty("tutorialOverlay").objectReferenceValue = overlay;
            suiSO.ApplyModifiedPropertiesWithoutUndo();

            // ─── 10. 저장 ───
            PrefabUtility.SaveAsPrefabAsset(contentsRoot, PrefabPath);
            PrefabUtility.UnloadPrefabContents(contentsRoot);

            Debug.Log("[TutorialSetup] ✅ 스케줄 튜토리얼 오버레이 셋업 완료!");
            Debug.Log("[TutorialSetup] 위치/크기는 프리팹에서 직접 조정하세요.");
        }

        /// <summary>RectTransform이 부모를 꽉 채우도록 stretch 설정</summary>
        static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>RectTransform 포함 UI 오브젝트 생성</summary>
        static GameObject CreateUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return go;
        }

        /// <summary>이름 패턴으로 연번 스프라이트 일괄 로드 (tutorial_dim_01 ~ tutorial_dim_11)</summary>
        static Sprite[] LoadSprites(string prefix, int count)
        {
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                string name = $"{prefix}{(i + 1):D2}";
                sprites[i] = LoadSprite(name);
            }
            return sprites;
        }

        /// <summary>단일 스프라이트 로드 (AssetDatabase 경로 검색)</summary>
        static Sprite LoadSprite(string name)
        {
            string path = $"{SpriteFolder}/{name}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[TutorialSetup] 스프라이트 찾을 수 없음: {path}");
            return sprite;
        }
    }
}
