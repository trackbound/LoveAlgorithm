using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // PlaySfxCommand

namespace LoveAlgo.UI
{
    /// <summary>
    /// 버튼 상태 비주얼 통합 드라이버(StyledButton·ButtonSpriteSwap·TitleHighlightSwitcher 수렴 대상).
    /// 배경은 상태별 자식을 정확히 하나 SetActive(child-swap), 라벨은 단일 TMP 색 코드 구동,
    /// pressed는 활성 자식 Image에 틴트 곱, UI 사운드도 발행. raw 포인터 이벤트 구동(Selectable 미상속 → 포커스 가림 부재).
    ///
    /// <para>상태 자식은 <b>명시적 직렬화 참조</b>(normalState/hoverState/onState/disabledState). 비운 슬롯은 Normal로 폴백.
    /// 라벨은 항상 켜진 단일 TMP — 텍스트는 외부(예: ChoiceSlot.Bind)가, 색은 이 드라이버가 구동(동적 텍스트 버튼 호환).</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonStateDriver : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        /// <summary>비주얼 상태. 우선순위 Disabled &gt; On &gt; Hover &gt; Normal.</summary>
        public enum State { Normal, Hover, On, Disabled }

        /// <summary>호버/클릭이 UiSound 테이블의 어느 항목을 쓸지.</summary>
        public enum UiSoundRole { General, Choice, Silent }

        /// <summary>상태별 라벨(TMP) 색. drive=false면 라벨 색 미관여. 상태 4종과 1:1.</summary>
        [Serializable]
        public struct TextColorBlock
        {
            [Tooltip("켜면 상태별로 라벨 색을 구동. 끄면 라벨 색 미관여.")]
            public bool drive;
            public Color normal;   // OFF/기본
            public Color hover;
            public Color on;       // 토글 ON
            public Color disabled;

            public static TextColorBlock Default => new TextColorBlock
            {
                drive = false,
                normal = Color.black,
                hover = Color.white,
                on = Color.white,
                disabled = new Color(0.5f, 0.5f, 0.5f, 1f),
            };
        }

        [Header("상태 배경 자식 (비우면 Normal로 폴백)")]
        [SerializeField] GameObject normalState;
        [SerializeField] GameObject hoverState;
        [SerializeField] GameObject onState;       // 토글 ON
        [SerializeField] GameObject disabledState;

        [Tooltip("눌림 시 활성 자식 Image의 base 컬러에 곱하는 틴트(≈C7C7C7).")]
        [SerializeField] Color pressedTint = new Color(0.7803922f, 0.7803922f, 0.7803922f, 1f);

        [Header("상태별 라벨 색")]
        [SerializeField] TextColorBlock textColors = TextColorBlock.Default;
        [Tooltip("색을 바꿀 라벨(TMP). 텍스트는 외부가 주입, 색만 구동.")]
        [SerializeField] TMP_Text label;

        [Header("UI 사운드")]
        [Tooltip("호버/클릭 SFX 묶음. General=일반 버튼/모달, Choice=선택지, Silent=무음.")]
        [SerializeField] UiSoundRole soundRole = UiSoundRole.General;

        Button _button;
        bool _pointerInside;
        bool _pressed;
        bool _isOn;

        // 활성 자식에 곱하기 전 base 컬러 1회 캡처(틴트 누적 방지).
        Color _normalBase = Color.white, _hoverBase = Color.white, _onBase = Color.white, _disabledBase = Color.white;
        bool _basesCaptured;

        public GameObject NormalState { get => normalState; set => normalState = value; }
        public GameObject HoverState { get => hoverState; set => hoverState = value; }
        public GameObject OnState { get => onState; set => onState = value; }
        public GameObject DisabledState { get => disabledState; set => disabledState = value; }
        public Color PressedTint { get => pressedTint; set => pressedTint = value; }
        public TextColorBlock TextColors { get => textColors; set => textColors = value; }
        public TMP_Text Label { get => label; set => label = value; }
        public UiSoundRole SoundRole { get => soundRole; set => soundRole = value; }
        public bool IsOn => _isOn;

        // ── 순수 결정층 (GameObject 불필요 — EditMode 테스트 대상) ──────────────────────

        /// <summary>활성 상태(우선순위 Disabled &gt; On &gt; Hover &gt; Normal).</summary>
        public static State ResolveActiveState(bool interactable, bool isOn, bool pointerInside)
        {
            if (!interactable) return State.Disabled;
            if (isOn) return State.On;
            if (pointerInside) return State.Hover;
            return State.Normal;
        }

        /// <summary>눌림(interactable &amp;&amp; pressed)일 때 baseColor*pressedTint(어두워짐), 아니면 baseColor 유지.</summary>
        public static Color ResolvePressedTint(bool interactable, bool pressed, Color baseColor, Color pressedTint)
            => (interactable && pressed) ? baseColor * pressedTint : baseColor;

        /// <summary>상태별 라벨 색(drive 판단은 호출 측 책임).</summary>
        public static Color ResolveTextColor(State state, in TextColorBlock c)
        {
            switch (state)
            {
                case State.Hover: return c.hover;
                case State.On: return c.on;
                case State.Disabled: return c.disabled;
                default: return c.normal; // Normal
            }
        }

        /// <summary>역할+호버/클릭 → SFX 이름(table/항목 없으면 null). StyledButton.ResolveSfx 이식.</summary>
        public static string ResolveSfx(UiSoundRole role, bool hover, UiSoundSO table)
        {
            if (table == null) return null;
            switch (role)
            {
                case UiSoundRole.Silent: return null;
                case UiSoundRole.Choice: return hover ? table.ChoiceHover : table.ChoiceClick;
                default:                 return hover ? table.ButtonHover : table.ButtonClick;
            }
        }

        // ── 얇은 어댑터 ───────────────────────────────────────────────────────────────

        void OnEnable()
        {
            EnsureRefs();
            CaptureBases();
            Apply();
        }

        void OnDisable()
        {
            _pressed = false;       // 눌린 채 비활성 잔류 방지
            _pointerInside = false;
        }

        /// <summary>토글 ON 표시 구동(소유 View가 호출).</summary>
        public void SetOn(bool on)
        {
            if (_isOn == on) return;
            _isOn = on;
            Apply();
        }

        /// <summary>interactable 토글 + Disabled 자식 즉시 반영.</summary>
        public void SetInteractable(bool value)
        {
            EnsureRefs();
            if (_button != null) _button.interactable = value;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true; Apply();
            if (IsInteractable()) PlayUiSfx(hover: true); // 비활성 버튼은 무음
        }

        public void OnPointerExit(PointerEventData eventData) { _pointerInside = false; Apply(); }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pressed = true; Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pressed = false; Apply();
        }

        // 클릭음: 네이티브 Button이 onClick을 트리거하는 조건과 동일(상호작용 가능 + 좌클릭).
        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsInteractable() && eventData.button == PointerEventData.InputButton.Left)
                PlayUiSfx(hover: false);
        }

        bool IsInteractable() => _button == null || _button.IsInteractable();

        void PlayUiSfx(bool hover)
        {
            string sfx = ResolveSfx(soundRole, hover, UiSoundSO.Shared);
            if (!string.IsNullOrEmpty(sfx)) EventBus.Publish(new PlaySfxCommand(sfx));
        }

        void EnsureRefs()
        {
            if (_button == null) _button = GetComponent<Button>();
        }

        // 각 상태 자식 Image의 원본 컬러를 1회 캡처(틴트 적용 전).
        void CaptureBases()
        {
            if (_basesCaptured) return;
            _normalBase = BaseColorOf(normalState);
            _hoverBase = BaseColorOf(hoverState);
            _onBase = BaseColorOf(onState);
            _disabledBase = BaseColorOf(disabledState);
            _basesCaptured = true;
        }

        static Color BaseColorOf(GameObject go)
        {
            if (go == null) return Color.white;
            var img = go.GetComponent<Image>();
            return img != null ? img.color : Color.white;
        }

        // 상태 → 자식(없으면 Normal 폴백) + 그 base 컬러.
        GameObject StateObject(State state, out Color baseColor)
        {
            switch (state)
            {
                case State.Hover: baseColor = hoverState != null ? _hoverBase : _normalBase; return hoverState != null ? hoverState : normalState;
                case State.On: baseColor = onState != null ? _onBase : _normalBase; return onState != null ? onState : normalState;
                case State.Disabled: baseColor = disabledState != null ? _disabledBase : _normalBase; return disabledState != null ? disabledState : normalState;
                default: baseColor = _normalBase; return normalState;
            }
        }

        void Apply()
        {
            EnsureRefs();
            CaptureBases();
            bool interactable = IsInteractable();
            var state = ResolveActiveState(interactable, _isOn, _pointerInside);
            var active = StateObject(state, out var baseColor);

            // 정확히 하나만 활성.
            SetActiveSafe(normalState, active);
            SetActiveSafe(hoverState, active);
            SetActiveSafe(onState, active);
            SetActiveSafe(disabledState, active);

            // 활성 자식 Image에 pressed 틴트(base에 곱).
            if (active != null)
            {
                var img = active.GetComponent<Image>();
                if (img != null) img.color = ResolvePressedTint(interactable, _pressed, baseColor, pressedTint);
            }

            // 라벨 색.
            if (textColors.drive && label != null)
                label.color = ResolveTextColor(state, textColors);
        }

        static void SetActiveSafe(GameObject go, GameObject active)
        {
            if (go != null) go.SetActive(go == active);
        }
    }
}
