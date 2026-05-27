using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using LoveAlgo.Common;
using LoveAlgo.Contracts;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 토글 버튼 - On/Off 상태를 가진 버튼
    /// 상태: Normal(Off), Hover, On, On+Hover
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ToggleButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public enum ToggleMode
        {
            SpriteSwap,         // Image의 sprite 교체
            ChildSwap,          // 자식 오브젝트 Active 토글
            Both                // SpriteSwap + ChildSwap 동시
        }

        [Header("토글 모드")]
        [SerializeField] ToggleMode toggleMode = ToggleMode.SpriteSwap;

        [Header("상태")]
        [SerializeField] bool isOn;

        [Header("SpriteSwap 설정")]
        [SerializeField] Image targetImage;
        [SerializeField] Sprite offSprite;
        [SerializeField] Sprite offHoverSprite;
        [SerializeField] Sprite onSprite;
        [SerializeField] Sprite onHoverSprite;

        [Header("ChildSwap 설정")]
        [SerializeField] GameObject offChild;
        [SerializeField] GameObject offHoverChild;
        [SerializeField] GameObject onChild;
        [SerializeField] GameObject onHoverChild;

        [Header("ColorTint 설정")]
        [SerializeField] bool useColorTint = true;
        [SerializeField] Graphic colorTarget;
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] Color disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        [SerializeField] float colorFadeDuration = 0.1f;

        [Header("스케일 효과")]
        [SerializeField] bool useScaleEffect = false;
        [SerializeField] float hoverScale = 1.05f;
        [SerializeField] float scaleDuration = 0.1f;

        [Header("오디오")]
        [SerializeField] bool playSound = true;
        [SerializeField] string toggleOnSfx = "ToggleOn";
        [SerializeField] string toggleOffSfx = "ToggleOff";

        /// <summary>
        /// 토글 상태 변경 이벤트 (bool: 새로운 isOn 상태)
        /// </summary>
        public event Action<bool> OnToggleChanged;

        Button button;
        bool isHovered;
        Vector3 originalScale;
        Tween currentScaleTween;
        Tween currentColorTween;

        /// <summary>
        /// 현재 토글 상태
        /// </summary>
        public bool IsOn
        {
            get => isOn;
            set => SetOn(value, notify: true);
        }

        void Awake()
        {
            button = GetComponent<Button>();
            originalScale = transform.localScale;

            // 자동 바인딩
            if (targetImage == null)
                targetImage = GetComponent<Image>();
            if (colorTarget == null)
                colorTarget = targetImage;
            if (offSprite == null && targetImage != null)
                offSprite = targetImage.sprite;

            // 초기 상태 적용
            ApplyVisualState(instant: true);
        }

        void OnEnable()
        {
            ApplyVisualState(instant: true);
        }

        void OnDisable()
        {
            currentScaleTween?.Kill();
            currentColorTween?.Kill();
        }

        #region 포인터 이벤트

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isHovered = true;
            ApplyVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            ApplyVisualState();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            Toggle();
        }

        #endregion

        #region 공개 메서드

        /// <summary>
        /// 토글 상태 전환
        /// </summary>
        public void Toggle()
        {
            SetOn(!isOn, notify: true);
        }

        /// <summary>
        /// 토글 상태 설정
        /// </summary>
        /// <param name="value">새로운 상태</param>
        /// <param name="notify">OnToggleChanged 이벤트 발생 여부</param>
        public void SetOn(bool value, bool notify = true)
        {
            if (isOn == value) return;

            isOn = value;
            ApplyVisualState();

            // 사운드
            if (playSound && Application.isPlaying)
            {
                var sfx = isOn ? toggleOnSfx : toggleOffSfx;
                Services.TryGet<IAudio>()?.PlaySFX(sfx);
            }

            // 이벤트
            if (notify)
            {
                OnToggleChanged?.Invoke(isOn);
            }
        }

        /// <summary>
        /// 상태만 설정 (이벤트 없이, UI 동기화용)
        /// </summary>
        public void SetOnWithoutNotify(bool value)
        {
            SetOn(value, notify: false);
        }

        #endregion

        #region 비주얼 상태 적용

        void ApplyVisualState(bool instant = false)
        {
            ToggleVisualState state = GetCurrentState();

            // SpriteSwap
            if (toggleMode == ToggleMode.SpriteSwap || toggleMode == ToggleMode.Both)
            {
                ApplySprite(state);
            }

            // ChildSwap
            if (toggleMode == ToggleMode.ChildSwap || toggleMode == ToggleMode.Both)
            {
                ApplyChildSwap(state);
            }

            // ColorTint
            if (useColorTint)
            {
                Color targetColor = GetColorForState(state);
                if (instant)
                {
                    if (colorTarget != null)
                        colorTarget.color = targetColor;
                }
                else
                {
                    TweenColor(targetColor);
                }
            }

            // Scale
            if (useScaleEffect)
            {
                Vector3 targetScale = isHovered ? originalScale * hoverScale : originalScale;
                if (instant)
                {
                    transform.localScale = targetScale;
                }
                else
                {
                    TweenScale(targetScale);
                }
            }
        }

        ToggleVisualState GetCurrentState()
        {
            if (isOn)
            {
                return isHovered ? ToggleVisualState.OnHover : ToggleVisualState.On;
            }
            else
            {
                return isHovered ? ToggleVisualState.OffHover : ToggleVisualState.Off;
            }
        }

        void ApplySprite(ToggleVisualState state)
        {
            if (targetImage == null) return;

            Sprite sprite = state switch
            {
                ToggleVisualState.Off => offSprite,
                ToggleVisualState.OffHover => offHoverSprite ?? offSprite,
                ToggleVisualState.On => onSprite ?? offSprite,
                ToggleVisualState.OnHover => onHoverSprite ?? onSprite ?? offSprite,
                _ => offSprite
            };

            if (sprite != null)
                targetImage.sprite = sprite;
        }

        void ApplyChildSwap(ToggleVisualState state)
        {
            // 모두 비활성화
            if (offChild != null) offChild.SetActive(false);
            if (offHoverChild != null) offHoverChild.SetActive(false);
            if (onChild != null) onChild.SetActive(false);
            if (onHoverChild != null) onHoverChild.SetActive(false);

            // 현재 상태만 활성화 (폴백 로직 포함)
            switch (state)
            {
                case ToggleVisualState.Off:
                    if (offChild != null) offChild.SetActive(true);
                    break;
                case ToggleVisualState.OffHover:
                    if (offHoverChild != null) offHoverChild.SetActive(true);
                    else if (offChild != null) offChild.SetActive(true);
                    break;
                case ToggleVisualState.On:
                    if (onChild != null) onChild.SetActive(true);
                    else if (offChild != null) offChild.SetActive(true);
                    break;
                case ToggleVisualState.OnHover:
                    if (onHoverChild != null) onHoverChild.SetActive(true);
                    else if (onChild != null) onChild.SetActive(true);
                    else if (offChild != null) offChild.SetActive(true);
                    break;
            }
        }

        Color GetColorForState(ToggleVisualState state)
        {
            // 비활성화 상태 체크
            if (button != null && !button.interactable)
                return disabledColor;

            // Hover(Pressed) 상태는 pressedColor, 그 외는 normalColor
            bool isHover = state == ToggleVisualState.OffHover || state == ToggleVisualState.OnHover;
            return isHover ? pressedColor : normalColor;
        }

        #endregion

        #region 트윈

        void TweenColor(Color target)
        {
            currentColorTween?.Kill();
            if (colorTarget == null) return;

            currentColorTween = colorTarget.DOColor(target, colorFadeDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        void TweenScale(Vector3 target)
        {
            currentScaleTween?.Kill();
            currentScaleTween = transform.DOScale(target, scaleDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        #endregion

        #region 에디터 지원

#if UNITY_EDITOR
        void OnValidate()
        {
            // 에디터에서 isOn 변경 시 즉시 반영
            if (!Application.isPlaying && gameObject.activeInHierarchy)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null)
                        ApplyVisualState(instant: true);
                };
            }
        }

        [ContextMenu("Toggle On")]
        void EditorToggleOn() => SetOn(true, notify: false);

        [ContextMenu("Toggle Off")]
        void EditorToggleOff() => SetOn(false, notify: false);
#endif

        #endregion

        enum ToggleVisualState
        {
            Off,
            OffHover,
            On,
            OnHover
        }
    }
}
