using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 인터랙션 피드백 매니저
    /// - 클릭 시 파티클 효과
    /// - 리플 효과 (물결 퍼지는 효과)
    /// - 화면 플래시
    /// - 햅틱 피드백 (모바일)
    /// </summary>
    public class InteractionFeedbackManager : MonoBehaviour
    {
        public static InteractionFeedbackManager Instance { get; private set; }

        [Header("Click Particle")]
        [SerializeField] ParticleSystem clickParticlePrefab;
        [SerializeField] Transform particleContainer;

        [Header("Ripple Effect")]
        [SerializeField] GameObject ripplePrefab;  // UI Image with radial gradient
        [SerializeField] Canvas rippleCanvas;
        [SerializeField] float rippleDuration = 0.6f;
        [SerializeField] float rippleMaxScale = 3f;

        [Header("Screen Flash")]
        [SerializeField] CanvasGroup screenFlashGroup;
        [SerializeField] float flashDuration = 0.15f;

        [Header("Haptic Settings")]
        [SerializeField] bool enableHaptics = true;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 자동 생성
            if (particleContainer == null)
            {
                var go = new GameObject("ParticleContainer");
                particleContainer = go.transform;
                particleContainer.SetParent(transform);
            }
        }

        /// <summary>
        /// 클릭 위치에 파티클 효과
        /// </summary>
        public void PlayClickParticle(Vector3 worldPosition)
        {
            if (clickParticlePrefab == null) return;

            var particle = Instantiate(clickParticlePrefab, worldPosition, Quaternion.identity, particleContainer);
            particle.Play();
            Destroy(particle.gameObject, particle.main.duration + 1f);
        }

        /// <summary>
        /// 화면 좌표에서 리플 효과
        /// </summary>
        public async UniTaskVoid PlayRipple(Vector2 screenPosition, CancellationToken ct = default)
        {
            if (ripplePrefab == null || rippleCanvas == null) return;

            var ripple = Instantiate(ripplePrefab, rippleCanvas.transform);
            var rippleRect = ripple.GetComponent<RectTransform>();
            if (rippleRect == null)
            {
                Destroy(ripple);
                return;
            }

            // 화면 좌표를 Canvas 좌표로 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rippleCanvas.transform as RectTransform,
                screenPosition,
                rippleCanvas.worldCamera,
                out Vector2 localPoint
            );

            rippleRect.anchoredPosition = localPoint;
            rippleRect.localScale = Vector3.zero;

            var rippleGroup = ripple.GetComponent<CanvasGroup>();
            if (rippleGroup == null)
                rippleGroup = ripple.AddComponent<CanvasGroup>();

            rippleGroup.alpha = 1f;

            // 스케일 확장 + 페이드 아웃
            var sequence = DOTween.Sequence();
            _ = sequence.Append(rippleRect.DOScale(Vector3.one * rippleMaxScale, rippleDuration).SetEase(Ease.OutQuad));
            _ = sequence.Join(rippleGroup.DOFade(0f, rippleDuration).SetEase(Ease.OutQuad));

            try
            {
                await sequence.ToUniTask(cancellationToken: ct);
            }
            catch (System.OperationCanceledException)
            {
                // 취소됨
            }
            finally
            {
                if (ripple != null)
                    Destroy(ripple);
            }
        }

        /// <summary>
        /// 화면 플래시 (중요한 선택, 충격적인 장면 등)
        /// </summary>
        public async UniTask PlayScreenFlash(Color flashColor, float intensity = 0.5f, CancellationToken ct = default)
        {
            if (screenFlashGroup == null) return;

            // 플래시 그룹의 Image 색상 설정
            var image = screenFlashGroup.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
                image.color = flashColor;

            screenFlashGroup.alpha = 0f;
            screenFlashGroup.gameObject.SetActive(true);
            screenFlashGroup.blocksRaycasts = false;

            // 페이드 인 → 페이드 아웃
            try
            {
                await screenFlashGroup.DOFade(intensity, flashDuration * 0.3f)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct);

                await screenFlashGroup.DOFade(0f, flashDuration * 0.7f)
                    .SetEase(Ease.InQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            catch (System.OperationCanceledException)
            {
                // 취소됨
            }
            finally
            {
                screenFlashGroup.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 햅틱 피드백 (모바일 진동)
        /// </summary>
        public void PlayHaptic(HapticType type = HapticType.Selection)
        {
            if (!enableHaptics) return;

#if UNITY_IOS || UNITY_ANDROID
            switch (type)
            {
                case HapticType.Selection:
                    Handheld.Vibrate();
                    break;
                case HapticType.Success:
                    // 짧은 진동 (모바일 전용 구현 필요)
                    Handheld.Vibrate();
                    break;
                case HapticType.Warning:
                    // 긴 진동 (모바일 전용 구현 필요)
                    Handheld.Vibrate();
                    break;
            }
#endif
        }

        /// <summary>
        /// 선택지 선택 시 통합 피드백
        /// </summary>
        public void PlayChoiceSelectionFeedback(Vector2 screenPosition)
        {
            PlayRipple(screenPosition, default);
            PlayHaptic(HapticType.Selection);
            UISoundManager.Instance?.PlayChoiceSelect();
        }

        /// <summary>
        /// 중요한 대화 (선택 결과, 엔딩 분기 등)
        /// </summary>
        public async UniTaskVoid PlayImportantDialogueFeedback(Color flashColor)
        {
            try
            {
                await PlayScreenFlash(flashColor, 0.3f);
            }
            catch (System.OperationCanceledException) { }
            PlayHaptic(HapticType.Success);
        }
    }

    public enum HapticType
    {
        Selection,  // 가벼운 터치 (선택지 호버)
        Success,    // 중간 강도 (선택 확정)
        Warning     // 강한 진동 (중요한 선택, 게임오버 등)
    }
}
