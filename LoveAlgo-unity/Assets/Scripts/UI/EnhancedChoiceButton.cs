using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 선택지 전용 향상된 버튼
    /// - 부드러운 호버 애니메이션 (스케일 + 글로우)
    /// - 클릭 시 펀치 효과
    /// - 선택 확정 시 하이라이트 애니메이션
    /// - 사운드 피드백 자동 연동
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class EnhancedChoiceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References")]
        [SerializeField] Image backgroundImage;
        [SerializeField] TMP_Text buttonText;
        [SerializeField] Image glowImage;  // 호버 시 글로우 효과 (선택)
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Hover Animation")]
        [SerializeField] float hoverScale = 1.05f;
        [SerializeField] float hoverDuration = 0.2f;
        [SerializeField] Ease hoverEase = Ease.OutBack;
        [SerializeField] float glowFadeInAlpha = 0.3f;
        [SerializeField] float glowFadeDuration = 0.2f;

        [Header("Click Animation")]
        [SerializeField] float clickPunchScale = 0.95f;
        [SerializeField] float clickDuration = 0.15f;
        [SerializeField] Ease clickEase = Ease.InOutSine;

        [Header("Selection Animation")]
        [SerializeField] float selectionFlashDuration = 0.3f;
        [SerializeField] Color selectionFlashColor = new Color(1f, 0.9f, 0.5f, 1f);

        [Header("Entrance Animation")]
        [SerializeField] float entranceDelay = 0f;  // 순차 등장용
        [SerializeField] float entranceScale = 0.8f;
        [SerializeField] float entranceDuration = 0.4f;
        [SerializeField] Ease entranceEase = Ease.OutBack;

        Button button;
        Vector3 originalScale;
        Color originalTextColor;
        Color originalBgColor;
#pragma warning disable CS0414
        bool isHovered;
#pragma warning restore CS0414
        Sequence currentAnimation;

        void Awake()
        {
            button = GetComponent<Button>();
            originalScale = transform.localScale;

            if (buttonText != null)
                originalTextColor = buttonText.color;
            if (backgroundImage != null)
                originalBgColor = backgroundImage.color;

            // 글로우 초기 상태
            if (glowImage != null)
            {
                glowImage.color = new Color(glowImage.color.r, glowImage.color.g, glowImage.color.b, 0f);
            }

            // 버튼 클릭 이벤트에 애니메이션 연결
            button?.onClick.AddListener(PlayClickAnimation);
        }

        void OnEnable()
        {
            // 등장 애니메이션
            PlayEntranceAnimation();
        }

        /// <summary>
        /// 등장 애니메이션 (스케일 + 페이드)
        /// </summary>
        public void PlayEntranceAnimation()
        {
            currentAnimation?.Kill();

            // 초기 상태
            transform.localScale = originalScale * entranceScale;
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            currentAnimation = DOTween.Sequence();
            currentAnimation.AppendInterval(entranceDelay);
            currentAnimation.Append(transform.DOScale(originalScale, entranceDuration).SetEase(entranceEase));

            if (canvasGroup != null)
            {
                currentAnimation.Join(canvasGroup.DOFade(1f, entranceDuration).SetEase(Ease.OutQuad));
            }
        }

        /// <summary>
        /// 등장 딜레이 설정 (순차 등장용)
        /// </summary>
        public void SetEntranceDelay(float delay)
        {
            entranceDelay = delay;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null || !button.interactable) return;
            isHovered = true;
            PlayHoverEnterAnimation();

            // 사운드 재생
            UISoundManager.Instance?.PlayHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            PlayHoverExitAnimation();
        }

        /// <summary>
        /// 호버 진입 애니메이션
        /// </summary>
        void PlayHoverEnterAnimation()
        {
            currentAnimation?.Kill();

            currentAnimation = DOTween.Sequence();

            // 스케일 업
            currentAnimation.Append(transform.DOScale(originalScale * hoverScale, hoverDuration).SetEase(hoverEase));

            // 글로우 페이드 인
            if (glowImage != null)
            {
                currentAnimation.Join(glowImage.DOFade(glowFadeInAlpha, glowFadeDuration).SetEase(Ease.OutQuad));
            }

            // 텍스트 색상 밝게 (선택적)
            if (buttonText != null)
            {
                Color brightColor = originalTextColor * 1.2f;
                brightColor.a = originalTextColor.a;
                currentAnimation.Join(buttonText.DOColor(brightColor, hoverDuration));
            }
        }

        /// <summary>
        /// 호버 종료 애니메이션
        /// </summary>
        void PlayHoverExitAnimation()
        {
            currentAnimation?.Kill();

            currentAnimation = DOTween.Sequence();

            // 스케일 복원
            currentAnimation.Append(transform.DOScale(originalScale, hoverDuration).SetEase(Ease.OutQuad));

            // 글로우 페이드 아웃
            if (glowImage != null)
            {
                currentAnimation.Join(glowImage.DOFade(0f, glowFadeDuration).SetEase(Ease.OutQuad));
            }

            // 텍스트 색상 복원
            if (buttonText != null)
            {
                currentAnimation.Join(buttonText.DOColor(originalTextColor, hoverDuration));
            }
        }

        /// <summary>
        /// 클릭 애니메이션 (펀치 효과)
        /// </summary>
        void PlayClickAnimation()
        {
            // 약간 축소되었다가 복원
            transform.DOScale(originalScale * clickPunchScale, clickDuration * 0.5f)
                .SetEase(clickEase)
                .OnComplete(() =>
                {
                    transform.DOScale(originalScale, clickDuration * 0.5f).SetEase(Ease.OutQuad);
                });
        }

        /// <summary>
        /// 선택 확정 애니메이션 (선택된 후 호출)
        /// </summary>
        public async void PlaySelectionAnimation()
        {
            currentAnimation?.Kill();

            // 플래시 효과
            if (backgroundImage != null)
            {
                Color flashColor = selectionFlashColor;
                await backgroundImage.DOColor(flashColor, selectionFlashDuration * 0.5f)
                    .SetEase(Ease.OutQuad)
                    .AsyncWaitForCompletion();

                await backgroundImage.DOColor(originalBgColor, selectionFlashDuration * 0.5f)
                    .SetEase(Ease.InQuad)
                    .AsyncWaitForCompletion();
            }

            // 펄스 효과 (크게 → 작게)
            await transform.DOScale(originalScale * 1.1f, selectionFlashDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .AsyncWaitForCompletion();

            await transform.DOScale(originalScale * 0.9f, selectionFlashDuration * 0.4f)
                .SetEase(Ease.InQuad)
                .AsyncWaitForCompletion();
        }

        void OnDisable()
        {
            currentAnimation?.Kill();
            transform.localScale = originalScale;

            if (buttonText != null)
                buttonText.color = originalTextColor;
            if (backgroundImage != null)
                backgroundImage.color = originalBgColor;
            if (glowImage != null)
                glowImage.color = new Color(glowImage.color.r, glowImage.color.g, glowImage.color.b, 0f);
        }

        /// <summary>
        /// 텍스트 설정
        /// </summary>
        public void SetText(string text)
        {
            if (buttonText != null)
                buttonText.text = text;
        }
    }
}
