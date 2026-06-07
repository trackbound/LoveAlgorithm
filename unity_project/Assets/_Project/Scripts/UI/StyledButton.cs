using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // OnPointerEnter/Exit (호버 자체 추적)

namespace LoveAlgo.UI
{
    /// <summary>
    /// 상태별 비주얼을 통합 제어하는 <see cref="Button"/>. Unity Button의 Transition은 단일 모드라
    /// "hover 스프라이트 스왑"과 "pressed 컬러 틴트(C8C8C8)"를 **동시에** 못 건다. 이 클래스는 Transition을
    /// ColorTint로 두어 네이티브 <c>ColorBlock</c>(pressed≈C8C8C8)을 그대로 살리고, 스프라이트는
    /// <see cref="Graphic"/>의 <c>overrideSprite</c>로 직접 갈아끼워 둘을 결합한다 — normal→hover 스왑 위에
    /// 클릭 시 C8C8C8 틴트가 곱해진다. 라벨(TMP) 색도 상태별로 구동(예: Yes 버튼 검정→흰). 탭 active는
    /// <see cref="SetSelected"/>로 강제. 결정 로직은 순수 정적(<see cref="VisualState"/> 기반, 테스트 대상)이고
    /// MonoBehaviour는 <c>SelectionState↔VisualState</c>만 잇는 얇은 어댑터다.
    ///
    /// <para><b>왜 상속인가</b>: Selectable의 검증된 상태머신(포인터 enter/exit/down/up, disabled, navigation)과
    /// <c>onClick</c>을 그대로 재사용 → 기존 모든 <c>Button</c> 필드(ChoiceSlot·TitleView·CategoryTab)에 무변경으로
    /// 꽂힌다. 별도의 포인터 핸들러 컴포넌트는 상태머신을 중복 구현해야 하고 ColorTint pressed와
    /// 결합할 수 없어 이 클래스가 그것을 포섭(대체)한다.</para>
    /// </summary>
    public class StyledButton : Button
    {
        /// <summary><see cref="Selectable.SelectionState"/>(protected)의 public 미러 — 순수층이 외부에 노출하는 상태.</summary>
        public enum VisualState { Normal, Highlighted, Pressed, Selected, Disabled }

        /// <summary>상태별 라벨 글씨색. <see cref="drive"/>=false면 라벨 색을 건드리지 않는다(흰→흰 버튼 등).</summary>
        [Serializable]
        public struct TextColorBlock
        {
            [Tooltip("켜면 상태별로 라벨 색을 구동. 끄면 라벨 색 미관여.")]
            public bool drive;
            public Color normal;
            public Color highlighted;
            public Color pressed;
            public Color selected;
            public Color disabled;

            /// <summary>합리적 기본값(검정 평상→흰 강조). 신규 컴포넌트의 인스펙터 초기값.</summary>
            public static TextColorBlock Default => new TextColorBlock
            {
                drive = false,
                normal = Color.black,
                highlighted = Color.white,
                pressed = Color.white,
                selected = Color.white,
                disabled = new Color(0.5f, 0.5f, 0.5f, 1f),
            };
        }

        [Header("상태별 스프라이트 (비우면 base sprite 사용/미변경)")]
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite highlightedSprite;
        [SerializeField] Sprite pressedSprite;
        [SerializeField] Sprite selectedSprite;
        [SerializeField] Sprite disabledSprite;

        [Header("상태별 라벨 색")]
        [SerializeField] TextColorBlock textColors = TextColorBlock.Default;

        [Tooltip("색을 바꿀 라벨(TMP). 미바인딩 시 자식에서 자동 탐색.")]
        [SerializeField] TMP_Text label;

        bool _selectedOverride;
        bool _pointerInside; // 호버 중 여부 — 포커스(Selected)가 Highlighted를 가리는 문제 보정용

        public TMP_Text Label { get => label; set => label = value; }
        public TextColorBlock TextColors { get => textColors; set => textColors = value; }
        public bool IsSelectedOverride => _selectedOverride;
        public Sprite NormalSprite { get => normalSprite; set => normalSprite = value; }
        public Sprite HighlightedSprite { get => highlightedSprite; set => highlightedSprite = value; }
        public Sprite PressedSprite { get => pressedSprite; set => pressedSprite = value; }
        public Sprite SelectedSprite { get => selectedSprite; set => selectedSprite = value; }
        public Sprite DisabledSprite { get => disabledSprite; set => disabledSprite = value; }

        // ── 순수 정적 결정층 (VisualState 기반, GameObject 불필요 — EditMode 단위테스트 대상) ──────

        /// <summary>
        /// 우선순위: <b>Disabled</b> > <b>토글 active</b>(<paramref name="selectedOverride"/>) > <b>Pressed</b> >
        /// <b>호버</b>(<paramref name="pointerInside"/>) > <b>Normal</b>.
        ///
        /// <para>왜 pointerInside가 필요한가: Unity Selectable은 EventSystem 포커스(<c>hasSelection</c>)가 있으면
        /// <c>Highlighted</c>보다 먼저 <see cref="VisualState.Selected"/>를 돌려준다. 그래서 클릭으로 포커스가 남은
        /// 버튼은 다시 마우스를 올려도 raw가 Selected라 호버가 죽었다. 포인터가 안에 있으면(<paramref name="pointerInside"/>)
        /// 포커스보다 호버를 우선해 <c>Highlighted</c>로 보정하고(=호버 복구), 포커스만 남고 호버는 아니면 Normal로
        /// 눌러 **스티키 하이라이트**를 제거한다. 탭/토글 active(<paramref name="selectedOverride"/>)는 의도적이라
        /// 호버보다 우선한다.</para>
        /// </summary>
        public static VisualState ResolveEffective(VisualState raw, bool selectedOverride, bool pointerInside)
        {
            if (raw == VisualState.Disabled) return VisualState.Disabled; // 상호작용 불가 최우선
            if (selectedOverride) return VisualState.Selected;            // 의도적 토글 ON
            if (raw == VisualState.Pressed) return VisualState.Pressed;   // 눌림(이탈 중에도 Unity가 유지)
            if (pointerInside) return VisualState.Highlighted;            // 호버는 포커스 잔류보다 우선
            return VisualState.Normal;                                    // 포커스 잔류 Selected 포함 → Normal
        }

        /// <summary>
        /// 상태별 스프라이트. Pressed는 전용(<paramref name="pressed"/>)이 없으면 <paramref name="highlighted"/>(hover)를
        /// 유지한다 — 그 위에 네이티브 ColorBlock의 pressed 틴트(C8C8C8)가 곱해져 "hover 스프라이트 + 눌림 틴트"가 된다.
        /// 반환 null이면 호출 측이 overrideSprite를 비워 base sprite로 복귀한다.
        /// </summary>
        public static Sprite SpriteForState(VisualState state, Sprite normal, Sprite highlighted, Sprite pressed, Sprite selected, Sprite disabled)
        {
            switch (state)
            {
                case VisualState.Highlighted: return highlighted;
                case VisualState.Pressed: return pressed != null ? pressed : highlighted;
                case VisualState.Selected: return selected != null ? selected : highlighted;
                case VisualState.Disabled: return disabled;
                default: return normal; // Normal
            }
        }

        /// <summary>상태별 라벨 색(<see cref="TextColorBlock.drive"/> 판단은 호출 측 책임).</summary>
        public static Color TextColorForState(VisualState state, in TextColorBlock c)
        {
            switch (state)
            {
                case VisualState.Highlighted: return c.highlighted;
                case VisualState.Pressed: return c.pressed;
                case VisualState.Selected: return c.selected;
                case VisualState.Disabled: return c.disabled;
                default: return c.normal; // Normal
            }
        }

        // ── 얇은 어댑터 (SelectionState ↔ VisualState 매핑 + 적용) ─────────────────────────

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            var effective = ResolveEffective(ToVisual(state), _selectedOverride, _pointerInside);
            base.DoStateTransition(ToSelection(effective), instant); // 네이티브 ColorBlock 틴트(pressed≈C8C8C8)
            ApplyVisualState(effective);
        }

        // 스프라이트 오버라이드 + 라벨 색 적용(틴트는 base가 처리). 색·스프라이트는 독립이라 ColorTint와 공존한다.
        void ApplyVisualState(VisualState state)
        {
            if (targetGraphic is Image img)
                img.overrideSprite = SpriteForState(state, normalSprite, highlightedSprite, pressedSprite, selectedSprite, disabledSprite);

            if (textColors.drive)
            {
                if (label == null) label = GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.color = TextColorForState(state, textColors);
            }
        }

        /// <summary>탭 active 등 외부 강제 선택 표시 토글. 즉시 비주얼 갱신(스티키 하이라이트와 구분되는 의도적 Selected).</summary>
        public void SetSelected(bool selected)
        {
            if (_selectedOverride == selected) return;
            _selectedOverride = selected;
            DoStateTransition(currentSelectionState, true);
        }

        // ── 호버 추적: 포커스(Selected)가 호버를 가릴 때 강제 재적용 ─────────────────────────
        // Unity는 hasSelection이면 호버해도 SelectionState가 Selected로 고정돼 DoStateTransition을 안 부른다.
        // 그래서 enter/exit에서 직접 재적용해 호버를 복구한다(pointerInside가 ResolveEffective에서 포커스를 이긴다).
        public override void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            base.OnPointerEnter(eventData);
            RefreshState();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            base.OnPointerExit(eventData);
            RefreshState();
        }

        protected override void OnDisable()
        {
            _pointerInside = false; // 비활성 중 호버 상태 잔류 방지
            base.OnDisable();
        }

        void RefreshState()
        {
            if (isActiveAndEnabled) DoStateTransition(currentSelectionState, false);
        }

        // protected SelectionState ↔ public VisualState (private이라 접근성 충돌 없음).
        static VisualState ToVisual(SelectionState s)
        {
            switch (s)
            {
                case SelectionState.Highlighted: return VisualState.Highlighted;
                case SelectionState.Pressed: return VisualState.Pressed;
                case SelectionState.Selected: return VisualState.Selected;
                case SelectionState.Disabled: return VisualState.Disabled;
                default: return VisualState.Normal;
            }
        }

        static SelectionState ToSelection(VisualState v)
        {
            switch (v)
            {
                case VisualState.Highlighted: return SelectionState.Highlighted;
                case VisualState.Pressed: return SelectionState.Pressed;
                case VisualState.Selected: return SelectionState.Selected;
                case VisualState.Disabled: return SelectionState.Disabled;
                default: return SelectionState.Normal;
            }
        }
    }
}
