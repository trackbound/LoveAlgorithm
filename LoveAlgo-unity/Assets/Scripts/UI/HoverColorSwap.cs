using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 호버 시 복수 타겟의 색상 전환
    /// HoverButton과 함께 또는 독립적으로 사용 가능
    /// </summary>
    public class HoverColorSwap : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] List<ColorSwapTarget> targets = new List<ColorSwapTarget>();
        [SerializeField] float fadeDuration = 0.1f;

        Button button;
        bool isHovered;
        bool isPressed;

        void Awake()
        {
            button = GetComponent<Button>();

            // Button 내장 ColorTint가 DOColor 트윈과 충돌하면
            // OnPointerExit 시 normalColor로 복귀가 안 되는 버그 발생
            if (button != null && button.transition == Selectable.Transition.ColorTint)
                button.transition = Selectable.Transition.None;

            // 초기 상태 리셋 및 색상 적용
            ResetState();
        }

        void OnEnable()
        {
            ResetState();
        }

        /// <summary>
        /// 상태 리셋 (버튼 재사용/활성화 시)
        /// </summary>
        void ResetState()
        {
            isHovered = false;
            isPressed = false;
            
            // 즉시 색상 적용 (애니메이션 없이)
            foreach (var target in targets)
            {
                if (target.target == null) continue;
                target.currentTween?.Kill();
                target.target.color = target.normalColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isHovered = true;
            ApplyState(true, isPressed);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            ApplyState(false, isPressed);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isPressed = true;
            ApplyState(isHovered, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            ApplyState(isHovered, false);
        }

        void ApplyState(bool hover, bool pressed)
        {
            foreach (var target in targets)
            {
                if (target.target == null) continue;

                Color targetColor;

                if (pressed && target.usePressedColor)
                {
                    targetColor = target.pressedColor;
                }
                else if (hover)
                {
                    targetColor = target.hoverColor;
                }
                else
                {
                    targetColor = target.normalColor;
                }

                TweenColor(target, targetColor);
            }
        }

        void TweenColor(ColorSwapTarget target, Color color)
        {
            target.currentTween?.Kill();

            if (fadeDuration > 0 && gameObject.activeInHierarchy)
            {
                var endColor = color;
                target.currentTween = target.target.DOColor(endColor, fadeDuration)
                    .OnComplete(() =>
                    {
                        // DOTween 완료 시 최종 색상 보장 (엔진 리빌드 시 색상 유실 방지)
                        if (target.target != null)
                            target.target.color = endColor;
                    });
            }
            else
            {
                target.target.color = color;
            }
        }

        void OnDisable()
        {
            // 호버 중 비활성화 시 색상이 hoverColor에 멈추는 버그 방지
            ResetState();
        }

        #region 에디터 헬퍼

        /// <summary>
        /// 타겟 추가 (에디터/런타임)
        /// </summary>
        public void AddTarget(Graphic graphic, Color normal, Color hover, Color? pressed = null)
        {
            var target = new ColorSwapTarget
            {
                target = graphic,
                normalColor = normal,
                hoverColor = hover,
                usePressedColor = pressed.HasValue,
                pressedColor = pressed ?? hover
            };
            targets.Add(target);
        }

        /// <summary>
        /// 현재 색상으로 Normal 설정
        /// </summary>
        public void CaptureNormalColors()
        {
            foreach (var target in targets)
            {
                if (target.target != null)
                    target.normalColor = target.target.color;
            }
        }

        #endregion
    }

    [Serializable]
    public class ColorSwapTarget
    {
        [Tooltip("색상을 변경할 대상 (TMP_Text, Image 등)")]
        public Graphic target;

        [Tooltip("기본 색상")]
        public Color normalColor = Color.black;

        [Tooltip("호버 시 색상")]
        public Color hoverColor = Color.white;

        [Tooltip("Pressed 색상 사용 여부")]
        public bool usePressedColor = false;

        [Tooltip("클릭 시 색상")]
        public Color pressedColor = Color.gray;

        // 런타임용
        [NonSerialized] public Tween currentTween;
    }
}
