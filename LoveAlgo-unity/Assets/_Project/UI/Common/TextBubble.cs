using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 텍스트 길이에 따라 너비·높이가 자동으로 늘어나는 9-slice 말풍선 UI 위젯.
    ///
    /// 구조:
    ///   TextBubble (RectTransform + Image(Sliced))
    ///   └ Label  (TMP_Text, padding offset)
    ///
    /// 사용:
    ///   bubble.SetText("긴 텍스트도 자동으로 박스가 늘어남");
    ///   bubble.SetSprite(otherSkinSprite);   // 다른 스킨 박스로 교체
    ///
    /// 9-slice 요구:
    ///   sprite의 Border 4값(L/B/R/T)이 0이 아닐 것. Sprite Editor에서 설정.
    ///
    /// prefab 자동 생성: Tools > UI > Create TextBubble Prefab
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class TextBubble : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] Image boxImage;
        [SerializeField] TMP_Text label;

        [Header("Padding (px) — 박스 안 텍스트 여백")]
        [Tooltip("가로 padding (양쪽 합). 9-slice Border가 큰 sprite일수록 작게.")]
        [SerializeField] float paddingHorizontal = 40f;
        [SerializeField] float paddingVertical = 30f;

        [Header("Size constraints (px)")]
        [Tooltip("최소 너비. 9-slice 깨짐 방지 위해 sprite Border L+R 합 이상 권장 (tutorial_textbox: 173).")]
        [SerializeField] float minWidth = 180f;
        [Tooltip("최소 높이. sprite Border T+B 합 이상 권장 (tutorial_textbox: 133).")]
        [SerializeField] float minHeight = 140f;
        [Tooltip("이 너비를 넘으면 wrap 후 세로로 늘어남")]
        [SerializeField] float maxWidth = 700f;

        public TMP_Text Label => label;
        public Image BoxImage => boxImage;

        /// <summary>현재 텍스트.</summary>
        public string Text
        {
            get => label != null ? label.text : "";
            set { if (label != null) { label.text = value; } Refit(); }
        }

        /// <summary>텍스트 설정 + 자동 리사이즈.</summary>
        public void SetText(string text)
        {
            if (label != null) label.text = text;
            Refit();
        }

        /// <summary>박스 sprite 교체 (다른 스킨). 9-slice Border가 0이면 경고.</summary>
        public void SetSprite(Sprite sprite)
        {
            if (boxImage == null || sprite == null) return;
            boxImage.sprite = sprite;
            EnforceSliced();
            if (sprite.border == Vector4.zero)
                Debug.LogWarning($"[TextBubble] '{sprite.name}' Sprite Border 미설정 — 9-slice 안 됨. Sprite Editor에서 Border 4값 설정 필요.", this);
        }

        /// <summary>
        /// 텍스트 내용에 맞춰 부모 RectTransform sizeDelta 동적 갱신.
        /// 1) no-wrap preferredWidth 측정 → min/max 클램프 (padding 포함)
        /// 2) wrap 설정 + 새 가로로 preferredHeight 측정
        /// 3) sizeDelta = (newW, newH)
        /// </summary>
        public void Refit()
        {
            if (label == null) return;

            // 1. 자연 가로 측정
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.ForceMeshUpdate();
            float naturalW = label.preferredWidth;

            // 2. 가로 결정
            float availableTextW = Mathf.Max(minWidth, maxWidth) - paddingHorizontal;
            float newTextW = Mathf.Clamp(naturalW, minWidth - paddingHorizontal, availableTextW);
            float newBoxW = Mathf.Clamp(naturalW + paddingHorizontal, minWidth, maxWidth);

            // 3. wrap 후 세로 측정
            label.textWrappingMode = TextWrappingModes.Normal;
            var labelRT = label.rectTransform;
            labelRT.sizeDelta = new Vector2(newTextW, labelRT.sizeDelta.y);
            label.ForceMeshUpdate();
            float textH = label.preferredHeight;
            float newBoxH = Mathf.Max(textH + paddingVertical, minHeight);

            // 4. 루트 sizeDelta 갱신
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(newBoxW, newBoxH);
        }

        void Awake() => EnforceSliced();

        /// <summary>Image.Type = Sliced + Border 경고.</summary>
        void EnforceSliced()
        {
            if (boxImage == null) return;
            boxImage.type = Image.Type.Sliced;
            boxImage.fillCenter = true;
            boxImage.preserveAspect = false;
            var sp = boxImage.sprite;
            if (sp != null && sp.border == Vector4.zero)
            {
                Debug.LogWarning($"[TextBubble] '{sp.name}' Sprite Border = 0 — 9-slice 작동 안 함. Sprite Editor에서 설정 필요.", this);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor에서 인스펙터 값 변경 시 자동 미리보기. delayCall로 안전한 호출.</summary>
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                EnforceSliced();
                Refit();
            };
        }
#endif
    }
}
