using TMPro;
using UnityEngine;
using UnityEngine.EventSystems; // IPointerEnterHandler, IPointerExitHandler

namespace LoveAlgo.UI
{
    /// <summary>
    /// 버튼 호버 시 라벨 글씨색 전환. Unity Button의 Sprite Swap은 배경 스프라이트만 바꾸고 자식 TMP 라벨 색은
    /// 못 건드리므로 그 부분만 보완한다(마우스 진입=hover색, 이탈=normal색). 스프라이트 호버는 Button 네이티브
    /// (Sprite Swap)가 처리 — 이 컴포넌트는 텍스트 색만. 색이 안 변하는 버튼(예: No 흰→흰)엔 안 붙이면 된다.
    /// 버튼 GO(레이캐스트 타깃 Image 보유)에 부착하면 포인터 이벤트를 받는다.
    /// </summary>
    public class ButtonHoverTextColor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("색을 바꿀 라벨(TMP). 미바인딩 시 아무것도 안 함.")]
        [SerializeField] TMP_Text label;
        [Tooltip("평소 글씨색.")]
        [SerializeField] Color normalColor = Color.black;
        [Tooltip("호버 시 글씨색.")]
        [SerializeField] Color hoverColor = Color.white;

        public TMP_Text Label { get => label; set => label = value; }
        public Color NormalColor { get => normalColor; set => normalColor = value; }
        public Color HoverColor { get => hoverColor; set => hoverColor = value; }

        void OnEnable()
        {
            // 미바인딩 시 자식 라벨 자동 연결(프리팹 스캐폴딩 편의 — 버튼 하위 단일 TMP 가정).
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            Apply(normalColor); // 활성/스폰 시 기본색으로(잔여 호버색 방지).
        }

        public void OnPointerEnter(PointerEventData eventData) => Apply(hoverColor);
        public void OnPointerExit(PointerEventData eventData) => Apply(normalColor);

        void Apply(Color c) { if (label != null) label.color = c; }
    }
}
