using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 네이티브 <see cref="Button"/> 옆에 붙는 얇은 스프라이트 스왑 컴포넌트. hover / on(토글) / disabled 시
    /// 대상 Image의 <b>스프라이트</b>를 코드로 갈아끼우고, <b>눌림 틴트</b>(<see cref="pressedTint"/>≈C7C7C7)도
    /// 코드로 직접 곱한다.
    ///
    /// <para><b>왜 눌림 틴트를 코드로</b>: 이 컴포넌트가 스프라이트를 구동하는 버튼은 네이티브 Button의 transition을
    /// (SpriteSwap이면 스프라이트가 충돌하므로) None으로 두는 경우가 많아, 네이티브 ColorTint의 pressed 틴트에
    /// 기댈 수 없다. 그래서 raw 포인터 down/up으로 눌림을 잡아 <see cref="pressedTint"/>를 base 컬러에 곱해
    /// 네이티브 ColorBlock의 pressed(≈C7C7C7)와 동일한 "눌리면 살짝 어두워짐"을 transition 설정과 무관하게 보장한다.</para>
    ///
    /// <para><b>StyledButton과의 분담</b>: 모듈(타이틀·세이브로드·설정·DialogueView·ScheduleView·ShopView 등)의
    /// 일반 버튼은 = 네이티브 Button + 이 컴포넌트(루트가 모듈 단위로 등록). StyledButton은 프리팹
    /// 슬롯·단순/독립 버튼 전용. 이 컴포넌트는 <see cref="Selectable"/> 상태머신을 안 쓰고 <b>raw 포인터 이벤트</b>로
    /// 동작하므로 "EventSystem 포커스(Selected)가 호버(Highlighted)를 가리는" 문제가 구조적으로 없다.</para>
    ///
    /// 상태 소스: hover=포인터 enter/exit · pressed=포인터 down/up · on=<see cref="SetOn"/>(토글 소유 View가 구동) ·
    /// interactable=<see cref="SetInteractable"/>(또는 OnEnable 시점 반영). 결정은 순수 <see cref="ResolveSprite"/>/<see cref="ResolveTint"/>.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonSpriteSwap : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("스왑 대상 Image. 미바인딩 시 Button.targetGraphic → 자식 Image 자동 탐색.")]
        [SerializeField] Image targetImage;
        [Tooltip("기본(비우면 OnEnable 시 현재 Image 스프라이트를 기준으로 캡처).")]
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite hoverSprite;
        [SerializeField] Sprite onSprite;       // 토글 ON (auto 등)
        [SerializeField] Sprite disabledSprite; // 비활성(경계 회색 화살표 등)

        [Tooltip("눌림 시 base 컬러에 곱하는 틴트(네이티브 ColorBlock pressed와 동일한 ≈C7C7C7).")]
        [SerializeField] Color pressedTint = new Color(0.7803922f, 0.7803922f, 0.7803922f, 1f); // C7C7C7

        Button _button;
        bool _pointerInside;
        bool _pressed;
        bool _isOn;
        Color _baseColor = Color.white;
        bool _baseColorCaptured;

        public Image TargetImage { get => targetImage; set => targetImage = value; }
        public Sprite NormalSprite { get => normalSprite; set => normalSprite = value; }
        public Sprite HoverSprite { get => hoverSprite; set => hoverSprite = value; }
        public Sprite OnSprite { get => onSprite; set => onSprite = value; }
        public Sprite DisabledSprite { get => disabledSprite; set => disabledSprite = value; }
        public Color PressedTint { get => pressedTint; set => pressedTint = value; }
        public bool IsOn => _isOn;

        // ── 순수 결정층 (GameObject 불필요 — EditMode 테스트 대상) ────────────────────────
        /// <summary>
        /// 우선순위: <b>비활성</b> > <b>토글 ON</b> > <b>호버</b> > <b>기본</b>. 각 전용 스프라이트가 null이면
        /// <paramref name="normal"/>로 폴백한다. (on-hover 전용 아트는 없으므로 on이 호버를 이긴다.)
        /// </summary>
        public static Sprite ResolveSprite(bool interactable, bool isOn, bool pointerInside,
            Sprite normal, Sprite hover, Sprite on, Sprite disabled)
        {
            if (!interactable) return disabled != null ? disabled : normal;
            if (isOn) return on != null ? on : normal;
            if (pointerInside) return hover != null ? hover : normal;
            return normal;
        }

        /// <summary>
        /// 눌림 컬러(순수). 상호작용 가능 + 눌림일 때만 <paramref name="baseColor"/>에 <paramref name="pressedTint"/>를
        /// 곱해 살짝 어둡게(≈C7C7C7), 그 외엔 base 컬러 그대로. 비활성 색은 스프라이트(disabled)가 표현하므로 여기선 base 유지.
        /// </summary>
        public static Color ResolveTint(bool interactable, bool pressed, Color baseColor, Color pressedTint)
            => (interactable && pressed) ? baseColor * pressedTint : baseColor;

        // ── 얇은 어댑터 ───────────────────────────────────────────────────────────────
        void Reset() => targetImage = GetComponent<Image>();

        void OnEnable()
        {
            EnsureRefs();
            if (normalSprite == null && targetImage != null) normalSprite = targetImage.sprite; // 기준 캡처
            if (!_baseColorCaptured && targetImage != null) { _baseColor = targetImage.color; _baseColorCaptured = true; } // base 컬러 1회 캡처
            Apply();
        }

        void OnDisable()
        {
            _pressed = false;     // 눌린 채 비활성 잔류 방지
            _pointerInside = false;
        }

        /// <summary>토글 ON 표시 구동(자동진행 auto 버튼 등 — 소유 View가 호출).</summary>
        public void SetOn(bool on)
        {
            if (_isOn == on) return;
            _isOn = on;
            Apply();
        }

        /// <summary>interactable 토글 + 비활성 스프라이트 즉시 반영(네이티브는 interactable 변경 이벤트가 없어 이 경로로).</summary>
        public void SetInteractable(bool value)
        {
            EnsureRefs();
            if (_button != null) _button.interactable = value;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData) { _pointerInside = true; Apply(); }
        public void OnPointerExit(PointerEventData eventData) { _pointerInside = false; Apply(); }

        // 눌림 틴트: 좌클릭 down/up만. up은 down을 받은 대상으로 라우팅되므로 밖에서 떼도 정상 복귀.
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

        void EnsureRefs()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (targetImage == null)
                targetImage = (_button != null ? _button.targetGraphic as Image : null)
                              ?? GetComponentInChildren<Image>(true);
        }

        void Apply()
        {
            EnsureRefs();
            if (targetImage == null) return;
            bool interactable = _button == null || _button.IsInteractable();
            targetImage.sprite = ResolveSprite(interactable, _isOn, _pointerInside,
                normalSprite, hoverSprite, onSprite, disabledSprite);
            targetImage.color = ResolveTint(interactable, _pressed, _baseColor, pressedTint);
        }
    }
}
