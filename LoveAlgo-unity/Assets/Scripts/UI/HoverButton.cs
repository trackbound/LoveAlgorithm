using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 확장 호버 버튼
    /// - SpriteSwap: 같은 Image의 sprite 교체
    /// - ChildSwap: 자식 오브젝트 Active 토글 (텍스트 색상 변경 등)
    /// - ColorTint 방식은 DOTween 복귀 버그로 제거됨
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HoverButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum HoverMode
        {
            None,
            SpriteSwap,         // 같은 Image의 sprite 교체
            ChildSwap,          // 자식 오브젝트 Active 토글
            Both                // SpriteSwap + ChildSwap 동시
        }

        [Header("호버 모드")]
        [SerializeField] HoverMode hoverMode = HoverMode.SpriteSwap;

        [Header("SpriteSwap 설정")]
        [SerializeField] Image targetImage;
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite hoverSprite;
        [SerializeField] Sprite pressedSprite;

        [Header("ChildSwap 설정")]
        [SerializeField] GameObject normalChild;
        [SerializeField] GameObject hoverChild;
        [SerializeField] GameObject pressedChild;

        [Header("스케일 효과 (선택)")]
        [SerializeField] bool useScaleEffect = false;
        [SerializeField] float hoverScale = 1.05f;
        [SerializeField] float pressedScale = 0.95f;
        [SerializeField] float scaleDuration = 0.1f;

        Button button;
        bool isHovered;
        bool isPressed;
        Vector3 originalScale;
        Tween currentScaleTween;

        void Awake()
        {
            button = GetComponent<Button>();

            originalScale = transform.localScale;

            // 자동 바인딩
            if (targetImage == null)
                targetImage = GetComponent<Image>();
            if (normalSprite == null && targetImage != null)
                normalSprite = targetImage.sprite;

            // 초기 상태 리셋
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
            
            // 즉시 normal 상태 적용 (애니메이션 없이)
            currentScaleTween?.Kill();
            
            ApplyNormalImmediate();
        }

        /// <summary>
        /// Normal 상태 즉시 적용 (애니메이션 없이)
        /// </summary>
        void ApplyNormalImmediate()
        {
            // SpriteSwap
            if (hoverMode == HoverMode.SpriteSwap || hoverMode == HoverMode.Both)
            {
                if (targetImage != null && normalSprite != null)
                    targetImage.sprite = normalSprite;
            }

            // ChildSwap
            if (hoverMode == HoverMode.ChildSwap || hoverMode == HoverMode.Both)
            {
                if (normalChild != null) normalChild.SetActive(true);
                if (hoverChild != null) hoverChild.SetActive(false);
                if (pressedChild != null) pressedChild.SetActive(false);
            }

            // Scale - 즉시 적용
            if (useScaleEffect)
            {
                transform.localScale = originalScale;
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
            // 우선순위: pressed > hover > normal
            if (pressed)
            {
                ApplyPressed();
            }
            else if (hover)
            {
                ApplyHover();
            }
            else
            {
                ApplyNormal();
            }
        }

        void ApplyNormal()
        {
            // SpriteSwap
            if (hoverMode == HoverMode.SpriteSwap || hoverMode == HoverMode.Both)
            {
                if (targetImage != null && normalSprite != null)
                    targetImage.sprite = normalSprite;
            }

            // ChildSwap
            if (hoverMode == HoverMode.ChildSwap || hoverMode == HoverMode.Both)
            {
                if (normalChild != null) normalChild.SetActive(true);
                if (hoverChild != null) hoverChild.SetActive(false);
                if (pressedChild != null) pressedChild.SetActive(false);
            }

            // Scale
            if (useScaleEffect)
            {
                TweenScale(originalScale);
            }
        }

        void ApplyHover()
        {
            // SpriteSwap
            if (hoverMode == HoverMode.SpriteSwap || hoverMode == HoverMode.Both)
            {
                if (targetImage != null && hoverSprite != null)
                    targetImage.sprite = hoverSprite;
            }

            // ChildSwap
            if (hoverMode == HoverMode.ChildSwap || hoverMode == HoverMode.Both)
            {
                if (normalChild != null) normalChild.SetActive(false);
                if (hoverChild != null) hoverChild.SetActive(true);
                if (pressedChild != null) pressedChild.SetActive(false);
            }

            // Scale
            if (useScaleEffect)
            {
                TweenScale(originalScale * hoverScale);
            }
        }

        void ApplyPressed()
        {
            // SpriteSwap - pressed가 없으면 hover 사용
            if (hoverMode == HoverMode.SpriteSwap || hoverMode == HoverMode.Both)
            {
                if (targetImage != null)
                {
                    targetImage.sprite = pressedSprite != null ? pressedSprite : 
                                         (hoverSprite != null ? hoverSprite : normalSprite);
                }
            }

            // ChildSwap - pressed가 없으면 hover 사용
            if (hoverMode == HoverMode.ChildSwap || hoverMode == HoverMode.Both)
            {
                if (normalChild != null) normalChild.SetActive(false);
                
                if (pressedChild != null)
                {
                    if (hoverChild != null) hoverChild.SetActive(false);
                    pressedChild.SetActive(true);
                }
                else
                {
                    if (hoverChild != null) hoverChild.SetActive(true);
                }
            }

            // Scale
            if (useScaleEffect)
            {
                TweenScale(originalScale * pressedScale);
            }
        }

        void TweenScale(Vector3 targetScale)
        {
            currentScaleTween?.Kill();

            if (scaleDuration > 0)
            {
                currentScaleTween = transform.DOScale(targetScale, scaleDuration)
                    .SetEase(Ease.OutQuad);
            }
            else
            {
                transform.localScale = targetScale;
            }
        }

        void OnDisable()
        {
            // 호버 중 비활성화 시 색상이 hoverColor에 멈추는 버그 방지
            ResetState();
        }

        #region 에디터 헬퍼

        /// <summary>
        /// 호버 스프라이트 설정 (에디터용)
        /// </summary>
        public void SetHoverSprite(Sprite sprite)
        {
            hoverSprite = sprite;
            hoverMode = HoverMode.SpriteSwap;
        }

        /// <summary>
        /// 자식 오브젝트 설정 (에디터용)
        /// </summary>
        public void SetChildSwap(GameObject normal, GameObject hover)
        {
            normalChild = normal;
            hoverChild = hover;
            hoverMode = HoverMode.ChildSwap;
        }

        #endregion
    }
}
