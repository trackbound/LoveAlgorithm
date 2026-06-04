using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 카테고리 탭 1개의 얇은 뷰. 선택 상태를 on/off 스프라이트 + 텍스트색으로만 표시한다.
    /// 클릭 수신·단일선택은 <see cref="CategoryTabBar"/>가 관리(이 컴포넌트는 시각만 — ADR-007 얇은 뷰).
    /// 구 ButtonEX(Simple/Hover/Toggle/ChildSwap·DOTween) 대체: 스케줄 탭이 실제로 쓰던 스프라이트/텍스트색
    /// 전환만 신규 제작(과설계 게이트 — 4모드·스케일 트윈·자동 캡처 불채택).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CategoryTab : MonoBehaviour
    {
        [SerializeField] Image targetImage;
        [SerializeField] Sprite onSprite;
        [SerializeField] Sprite offSprite;
        [SerializeField] TMP_Text label;
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color selectedColor = Color.white;

        Button _button;
        /// <summary>탭 버튼(<see cref="CategoryTabBar"/>가 onClick 배선). RequireComponent로 보장.</summary>
        public Button Button => _button != null ? _button : _button = GetComponent<Button>();

        /// <summary>선택 상태를 스프라이트 + 텍스트색으로 반영.</summary>
        public void SetSelected(bool selected)
        {
            if (targetImage != null) targetImage.sprite = selected ? onSprite : offSprite;
            if (label != null) label.color = selected ? selectedColor : normalColor;
        }
    }
}
