using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.UI;

namespace LoveAlgo.UI.EditorTools
{
    /// <summary>
    /// TextBubble UI prefab 자동 빌더 — 9-slice Image + TMP + 동적 리사이즈.
    ///
    /// 구조:
    ///   TextBubble (RectTransform + Image[Sliced] + TextBubble)
    ///   └ Label    (TMP_Text, 부모 fill - padding offset)
    ///
    /// 메뉴: Tools > UI > Create TextBubble Prefab
    /// </summary>
    public static class TextBubblePrefabBuilder
    {
        const string PrefabPath = "Assets/_Project/UI/Prefabs/TextBubble.prefab";
        const string PrefabDir  = "Assets/_Project/UI/Prefabs";

        // 기본 스프라이트 후보 (첫 발견 사용)
        static readonly string[] DefaultSpriteCandidates =
        {
            "Assets/_Project/Modules/Tutorial/Art/tutorial_textbox.png",
            "Assets/Art/UI/ScheduleTutorial/tutorial_textbox.png",
        };

        // 디자인 기본값 — 짧은 텍스트("텍스트") 기준 컴팩트한 박스
        // tutorial_textbox Border 합 (가로 173, 세로 133)을 약간 넘는 최소 크기
        const float InitialW = 200f, InitialH = 150f;
        const int LabelFontSize = 22;
        const float LabelPaddingX = 20f;   // 박스 좌우 여백
        const float LabelPaddingY = 15f;   // 박스 위아래 여백

        [MenuItem("Tools/UI/Create TextBubble Prefab")]
        public static void Build()
        {
            // 1. 기본 스프라이트 로드
            Sprite sprite = null;
            foreach (var p in DefaultSpriteCandidates)
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sprite != null) break;
            }
            if (sprite == null)
            {
                if (!EditorUtility.DisplayDialog("기본 스프라이트 없음",
                    "tutorial_textbox.png 못 찾음. 빈 박스로 prefab 만들까요?\n(나중에 인스펙터에서 sprite 바인딩 가능)",
                    "빈 박스로 진행", "취소"))
                    return;
            }
            else if (sprite.border == Vector4.zero)
            {
                EditorUtility.DisplayDialog("9-slice Border 미설정",
                    $"'{sprite.name}'의 Sprite Border가 0 입니다.\n" +
                    "Project 창에서 PNG 선택 → Sprite Editor → Border 4값(L/B/R/T) 설정 권장.\n" +
                    "(그래도 prefab은 생성됩니다 — 추후 설정해도 OK)",
                    "확인");
            }

            // 2. 기존 prefab 확인
            bool existed = File.Exists(PrefabPath);
            if (existed)
            {
                bool ok = EditorUtility.DisplayDialog("Prefab 재구성",
                    $"기존 prefab 덮어씁니다:\n{PrefabPath}\n계속할까요?", "재구성", "취소");
                if (!ok) return;
            }

            // 3. 루트 = RectTransform + Image(Sliced) + TextBubble
            var root = new GameObject("TextBubble",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(InitialW, InitialH);

            var box = root.GetComponent<Image>();
            if (sprite != null) box.sprite = sprite;
            box.type = Image.Type.Sliced;
            box.fillCenter = true;
            box.preserveAspect = false;
            box.raycastTarget = false;

            // 4. Label (자식 TMP, 부모 fill - padding)
            var lblGO = new GameObject("Label",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lblGO.transform.SetParent(root.transform, false);
            var lblRT = lblGO.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.pivot     = new Vector2(0.5f, 0.5f);
            lblRT.offsetMin = new Vector2(LabelPaddingX, LabelPaddingY);
            lblRT.offsetMax = new Vector2(-LabelPaddingX, -LabelPaddingY);

            var lbl = lblGO.GetComponent<TextMeshProUGUI>();
            lbl.text = "TextBubble 미리보기 텍스트";
            lbl.fontSize = LabelFontSize;
            lbl.color = new Color(0.25f, 0.1f, 0.18f);   // 분홍 박스에 어울리는 진한 자줏빛
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.textWrappingMode = TextWrappingModes.Normal;
            lbl.raycastTarget = false;

            // 5. TextBubble 컴포넌트 + 필드 바인딩
            var bubble = root.AddComponent<TextBubble>();
            var so = new SerializedObject(bubble);
            SetRef(so, "boxImage", box);
            SetRef(so, "label",    lbl);
            so.ApplyModifiedProperties();

            // 6. 첫 리사이즈 적용 (텍스트 길이에 맞춰)
            bubble.Refit();

            // 7. 저장
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            Debug.Log($"[TextBubblePrefabBuilder] {(existed ? "재구성" : "신규 생성")} 완료: {PrefabPath}\n" +
                      $"  sprite: {(sprite != null ? AssetDatabase.GetAssetPath(sprite) : "(없음)")}\n" +
                      $"  사용: bubble.SetText(\"...\") 또는 bubble.SetSprite(sprite)");
        }

        static void SetRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[TextBubblePrefabBuilder] '{fieldName}' 필드 못 찾음 — 스킵");
                return;
            }
            prop.objectReferenceValue = value;
        }
    }
}
