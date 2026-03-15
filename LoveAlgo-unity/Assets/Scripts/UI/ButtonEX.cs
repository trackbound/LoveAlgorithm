using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 프로젝트 통합 버튼 컴포넌트
    /// Button.Transition = None 전제, 모든 시각 피드백을 여기서 관리
    /// 
    /// 모드:
    /// - Simple: 클릭 시 PressedTint만 (호버 없음)
    /// - Hover:  SpriteSwap 호버 + PressedTint + TextColor
    /// - Toggle: On/Off 스프라이트 + 호버
    /// - ChildSwap: 자식 오브젝트 통째로 교체 (특수 레이아웃)
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonEX : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public enum ButtonMode
        {
            Simple,     // PressedTint만
            Hover,      // SpriteSwap + PressedTint + TextColor
            Toggle,     // On/Off 스프라이트 + 호버
            ChildSwap   // 자식 오브젝트 교체
        }

        [Header("모드")]
        [SerializeField] ButtonMode mode = ButtonMode.Hover;

        [Header("이미지 (비워두면 Button.targetGraphic 자동 사용)")]
        [SerializeField] Image overrideTargetImage;

        // ── Hover 모드 ──────────────────────────────
        [Header("Hover 모드 - 스프라이트")]
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite hoverSprite;

        // ── Toggle 모드 ─────────────────────────────
        [Header("Toggle 모드 - 스프라이트")]
        [SerializeField] Sprite onSprite;
        [SerializeField] Sprite offSprite;
        [SerializeField] Sprite onHoverSprite;
        [SerializeField] Sprite offHoverSprite;

        // ── ChildSwap 모드 ──────────────────────────
        [Header("ChildSwap 모드")]
        [SerializeField] GameObject normalChild;
        [SerializeField] GameObject hoverChild;
        [SerializeField] GameObject pressedChild;

        // ── PressedTint (모든 모드 공통) ─────────────
        [Header("클릭 틴트 (공통)")]
        [SerializeField] bool usePressedTint = true;
        [SerializeField] Color pressedTint = new(0.85f, 0.85f, 0.85f, 1f);

        // ── TextColor (Hover/Toggle 모드) ────────────
        [Header("텍스트 색상 전환")]
        [SerializeField] TMP_Text[] colorTargets;
        [SerializeField] Color[] normalColors;
        [SerializeField] Color[] hoverColors;

        // ── 스케일 효과 (선택, 모든 모드) ────────────
        [Header("스케일 효과 (선택)")]
        [SerializeField] bool useScaleEffect;
        [SerializeField] float hoverScale = 1.05f;
        [SerializeField] float pressedScale = 0.95f;
        [SerializeField] float scaleDuration = 0.1f;

        // ── 런타임 상태 ─────────────────────────────
        Button button;
        Image targetImage;
        bool isHovered;
        bool isPressed;
        bool isOn;  // Toggle 전용
        Vector3 originalScale;
        Tween currentScaleTween;

        // ══════════════════════════════════════════════
        //  라이프사이클
        // ══════════════════════════════════════════════

        void Awake()
        {
            button = GetComponent<Button>();
            targetImage = overrideTargetImage != null
                ? overrideTargetImage
                : button.targetGraphic as Image;
            originalScale = transform.localScale;

            // Button.Transition 강제 None
            button.transition = Selectable.Transition.None;

            // normalSprite 자동 캡처
            if (targetImage != null && normalSprite == null && mode == ButtonMode.Hover)
                normalSprite = targetImage.sprite;

            // Toggle: offSprite 자동 캡처
            if (targetImage != null && offSprite == null && mode == ButtonMode.Toggle)
                offSprite = targetImage.sprite;

            ResetState();
        }

        void OnEnable()
        {
            ResetState();
        }

        void OnDisable()
        {
            ResetState();
        }

        void ResetState()
        {
            isHovered = false;
            isPressed = false;
            currentScaleTween?.Kill();
            ApplyVisual();
        }

        // ══════════════════════════════════════════════
        //  포인터 이벤트
        // ══════════════════════════════════════════════

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isHovered = true;
            ApplyVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            ApplyVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isPressed = true;
            ApplyVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            ApplyVisual();
        }

        // ══════════════════════════════════════════════
        //  통합 시각 적용
        // ══════════════════════════════════════════════

        void ApplyVisual()
        {
            switch (mode)
            {
                case ButtonMode.Simple:
                    ApplySimple();
                    break;
                case ButtonMode.Hover:
                    ApplyHover();
                    break;
                case ButtonMode.Toggle:
                    ApplyToggle();
                    break;
                case ButtonMode.ChildSwap:
                    ApplyChildSwap();
                    break;
            }

            ApplyScale();
        }

        // ── Simple ──────────────────────────────────
        void ApplySimple()
        {
            ApplyPressedTint(isPressed);
        }

        // ── Hover ───────────────────────────────────
        void ApplyHover()
        {
            if (targetImage != null)
            {
                if (isPressed)
                {
                    // pressed: hover 스프라이트 유지 + 틴트
                    targetImage.sprite = hoverSprite != null ? hoverSprite : normalSprite;
                }
                else if (isHovered)
                {
                    targetImage.sprite = hoverSprite != null ? hoverSprite : normalSprite;
                }
                else
                {
                    targetImage.sprite = normalSprite;
                }
            }

            ApplyPressedTint(isPressed);
            ApplyTextColors(isHovered || isPressed);
        }

        // ── Toggle ──────────────────────────────────
        void ApplyToggle()
        {
            if (targetImage != null)
            {
                if (isOn)
                {
                    targetImage.sprite = (isHovered && onHoverSprite != null)
                        ? onHoverSprite : (onSprite != null ? onSprite : offSprite);
                }
                else
                {
                    targetImage.sprite = (isHovered && offHoverSprite != null)
                        ? offHoverSprite : offSprite;
                }
            }

            ApplyPressedTint(isPressed);
            ApplyTextColors(isHovered || isPressed);
        }

        // ── ChildSwap ───────────────────────────────
        void ApplyChildSwap()
        {
            if (isPressed)
            {
                // pressed: pressedChild가 있으면 사용, 없으면 hoverChild
                if (normalChild != null) normalChild.SetActive(false);
                if (pressedChild != null)
                {
                    if (hoverChild != null) hoverChild.SetActive(false);
                    pressedChild.SetActive(true);
                }
                else if (hoverChild != null)
                {
                    hoverChild.SetActive(true);
                }
            }
            else if (isHovered)
            {
                if (normalChild != null) normalChild.SetActive(false);
                if (hoverChild != null) hoverChild.SetActive(true);
                if (pressedChild != null) pressedChild.SetActive(false);
            }
            else
            {
                if (normalChild != null) normalChild.SetActive(true);
                if (hoverChild != null) hoverChild.SetActive(false);
                if (pressedChild != null) pressedChild.SetActive(false);
            }

            ApplyPressedTint(isPressed);
        }

        // ── 공통: PressedTint ────────────────────────
        void ApplyPressedTint(bool pressed)
        {
            if (!usePressedTint || targetImage == null) return;
            targetImage.color = pressed ? pressedTint : Color.white;
        }

        // ── 공통: TextColor ─────────────────────────
        void ApplyTextColors(bool highlight)
        {
            if (colorTargets == null) return;

            var colors = highlight ? hoverColors : normalColors;
            if (colors == null) return;

            int count = Mathf.Min(colorTargets.Length, colors.Length);
            for (int i = 0; i < count; i++)
            {
                if (colorTargets[i] != null)
                    colorTargets[i].color = colors[i];
            }
        }

        // ── 공통: Scale ─────────────────────────────
        void ApplyScale()
        {
            if (!useScaleEffect) return;

            Vector3 target;
            if (isPressed)
                target = originalScale * pressedScale;
            else if (isHovered)
                target = originalScale * hoverScale;
            else
                target = originalScale;

            currentScaleTween?.Kill();
            if (scaleDuration > 0)
                currentScaleTween = transform.DOScale(target, scaleDuration).SetEase(Ease.OutQuad);
            else
                transform.localScale = target;
        }

        // ══════════════════════════════════════════════
        //  Toggle API
        // ══════════════════════════════════════════════

        /// <summary>토글 상태 설정 (Toggle 모드 전용)</summary>
        public void SetToggle(bool on)
        {
            isOn = on;
            ApplyVisual();
        }

        /// <summary>토글 상태 반전</summary>
        public bool ToggleState()
        {
            isOn = !isOn;
            ApplyVisual();
            return isOn;
        }

        /// <summary>현재 토글 상태</summary>
        public bool IsOn => isOn;

        // ══════════════════════════════════════════════
        //  에디터 / 런타임 헬퍼
        // ══════════════════════════════════════════════

        /// <summary>ChildSwap의 normal/hover/pressed 자식 텍스트를 동시 설정</summary>
        public void SetText(string text)
        {
            if (normalChild != null)
            {
                var tmp = normalChild.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = text;
            }
            if (hoverChild != null)
            {
                var tmp = hoverChild.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = text;
            }
            if (pressedChild != null)
            {
                var tmp = pressedChild.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = text;
            }
        }

        /// <summary>호버 스프라이트 설정 (에디터/런타임)</summary>
        public void SetHoverSprite(Sprite sprite)
        {
            hoverSprite = sprite;
            if (mode != ButtonMode.Hover)
                mode = ButtonMode.Hover;
        }

        /// <summary>자식 오브젝트 설정 (에디터/런타임)</summary>
        public void SetChildSwap(GameObject normal, GameObject hover)
        {
            normalChild = normal;
            hoverChild = hover;
            if (mode != ButtonMode.ChildSwap)
                mode = ButtonMode.ChildSwap;
        }

        void OnDestroy()
        {
            currentScaleTween?.Kill();
        }
    }
}
