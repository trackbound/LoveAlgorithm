using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 캐릭터 생동감 시스템
    /// - 미묘한 숨쉬기 애니메이션
    /// - 대화 중 강조 효과 (말하는 캐릭터 highlight)
    /// - 깜빡임 애니메이션
    /// - 미세한 흔들림 (Idle)
    /// </summary>
    public class CharacterReactionSystem : MonoBehaviour
    {
        [Header("Breathing Animation")]
        [SerializeField] bool enableBreathing = true;
        [SerializeField] float breathingScale = 1.02f;  // 1.02 = 2% 확대
        [SerializeField] float breathingDuration = 3f;
        [SerializeField] Ease breathingEase = Ease.InOutSine;

        [Header("Speaking Highlight")]
        [SerializeField] bool enableSpeakingHighlight = true;
        [SerializeField] float highlightScale = 1.05f;
        [SerializeField] float highlightDuration = 0.3f;
        [SerializeField] float highlightBrightness = 1.2f;  // Color multiplier

        [Header("Blink Animation")]
        [SerializeField] bool enableBlink = false;  // 눈 스프라이트 교체 필요
        [SerializeField] float blinkInterval = 4f;
        [SerializeField] float blinkDuration = 0.15f;

        [Header("Idle Sway")]
        [SerializeField] bool enableIdleSway = false;
        [SerializeField] float swayAmount = 2f;  // 픽셀
        [SerializeField] float swayDuration = 5f;

        [Header("References")]
        [SerializeField] RectTransform characterTransform;
        [SerializeField] CanvasGroup characterCanvasGroup;

        Vector3 originalScale;
        Vector2 originalPosition;
        Color originalColor = Color.white;
        Sequence breathingSequence;
        Sequence swaySequence;
        CancellationTokenSource blinkCts;
        bool isSpeaking;

        void Awake()
        {
            if (characterTransform == null)
                characterTransform = GetComponent<RectTransform>();

            if (characterTransform != null)
            {
                originalScale = characterTransform.localScale;
                originalPosition = characterTransform.anchoredPosition;
            }

            // CanvasGroup 색상은 직접 접근 불가, Image 컴포넌트에서 가져와야 함
            var image = GetComponent<UnityEngine.UI.Image>();
            if (image != null)
                originalColor = image.color;
        }

        void OnEnable()
        {
            StartIdleAnimations();
        }

        void OnDisable()
        {
            StopAllIdleAnimations();
        }

        /// <summary>
        /// Idle 애니메이션 시작 (숨쉬기, 흔들림, 깜빡임)
        /// </summary>
        public void StartIdleAnimations()
        {
            if (enableBreathing)
                StartBreathing();

            if (enableIdleSway)
                StartIdleSway();

            if (enableBlink)
                StartBlinking();
        }

        /// <summary>
        /// 모든 Idle 애니메이션 중지
        /// </summary>
        public void StopAllIdleAnimations()
        {
            breathingSequence?.Kill();
            swaySequence?.Kill();
            blinkCts?.Cancel();
            blinkCts?.Dispose();
            blinkCts = null;

            if (characterTransform != null)
            {
                characterTransform.localScale = originalScale;
                characterTransform.anchoredPosition = originalPosition;
            }
        }

        /// <summary>
        /// 숨쉬기 애니메이션 시작
        /// </summary>
        void StartBreathing()
        {
            if (characterTransform == null) return;

            breathingSequence?.Kill();

            Vector3 breathScale = originalScale * breathingScale;

            breathingSequence = DOTween.Sequence();
            breathingSequence.Append(characterTransform.DOScale(breathScale, breathingDuration).SetEase(breathingEase));
            breathingSequence.Append(characterTransform.DOScale(originalScale, breathingDuration).SetEase(breathingEase));
            breathingSequence.SetLoops(-1, LoopType.Restart);
        }

        /// <summary>
        /// Idle 흔들림 시작 (좌우 미세 이동)
        /// </summary>
        void StartIdleSway()
        {
            if (characterTransform == null) return;

            swaySequence?.Kill();

            Vector2 leftPos = originalPosition + new Vector2(-swayAmount, 0);
            Vector2 rightPos = originalPosition + new Vector2(swayAmount, 0);

            swaySequence = DOTween.Sequence();
            swaySequence.Append(characterTransform.DOAnchorPos(rightPos, swayDuration).SetEase(Ease.InOutSine));
            swaySequence.Append(characterTransform.DOAnchorPos(leftPos, swayDuration).SetEase(Ease.InOutSine));
            swaySequence.SetLoops(-1, LoopType.Restart);
        }

        /// <summary>
        /// 깜빡임 시작 (주기적으로 눈 감기 - 스프라이트 교체 필요)
        /// </summary>
        void StartBlinking()
        {
            blinkCts?.Cancel();
            blinkCts = new CancellationTokenSource();
            BlinkLoopAsync(blinkCts.Token).Forget();
        }

        async UniTaskVoid BlinkLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // 랜덤 간격으로 깜빡임
                float interval = Random.Range(blinkInterval * 0.7f, blinkInterval * 1.3f);
                await UniTask.Delay(System.TimeSpan.FromSeconds(interval), cancellationToken: ct);

                // 깜빡임 (스케일 Y축 축소로 대체 - 실제로는 눈 스프라이트 교체 필요)
                if (characterTransform != null && !isSpeaking)
                {
                    // Y 스케일 축소 → 복원
                    var originalY = characterTransform.localScale.y;
                    await characterTransform.DOScaleY(originalY * 0.95f, blinkDuration * 0.5f)
                        .SetEase(Ease.OutQuad)
                        .ToUniTask(cancellationToken: ct);

                    await characterTransform.DOScaleY(originalY, blinkDuration * 0.5f)
                        .SetEase(Ease.InQuad)
                        .ToUniTask(cancellationToken: ct);
                }
            }
        }

        /// <summary>
        /// 말하는 중 강조 (대화 시작 시 호출)
        /// </summary>
        public async UniTaskVoid StartSpeaking()
        {
            if (!enableSpeakingHighlight) return;
            if (characterTransform == null) return;

            isSpeaking = true;

            // 숨쉬기 일시정지
            breathingSequence?.Pause();

            try
            {
                // 강조 효과: 약간 확대 + 밝게
                Vector3 highlightScaleVec = originalScale * highlightScale;
                await characterTransform.DOScale(highlightScaleVec, highlightDuration)
                    .SetEase(Ease.OutQuad)
                    .AsyncWaitForCompletion();
            }
            catch (System.Exception) { /* 트윈 중단 시 무시 */ }

            // 밝기 조정 (Image 컴포넌트 필요)
            var image = GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                Color brightColor = originalColor * highlightBrightness;
                brightColor.a = originalColor.a;
                _ = image.DOColor(brightColor, highlightDuration).SetEase(Ease.OutQuad);
            }
        }

        /// <summary>
        /// 말하기 종료 (대화 끝날 때 호출)
        /// </summary>
        public async UniTaskVoid StopSpeaking()
        {
            if (!isSpeaking) return;
            isSpeaking = false;

            if (characterTransform == null) return;

            try
            {
                // 원래 스케일 복원
                await characterTransform.DOScale(originalScale, highlightDuration)
                    .SetEase(Ease.OutQuad)
                    .AsyncWaitForCompletion();
            }
            catch (System.Exception) { /* 트윈 중단 시 무시 */ }

            // 밝기 복원
            var image = GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                _ = image.DOColor(originalColor, highlightDuration).SetEase(Ease.OutQuad);
            }

            // 숨쉬기 재개
            breathingSequence?.Play();
        }

        /// <summary>
        /// 반응 애니메이션 (놀람, 기쁨 등 특정 감정 표현)
        /// </summary>
        public async UniTaskVoid PlayReaction(ReactionType type)
        {
            if (characterTransform == null) return;

            try
            {
                switch (type)
                {
                    case ReactionType.Surprise:
                        // 뒤로 살짝 밀리는 효과
                        await characterTransform.DOPunchPosition(new Vector3(-10f, 5f, 0), 0.4f, 10, 1f)
                            .SetEase(Ease.OutQuad)
                            .AsyncWaitForCompletion();
                        break;

                    case ReactionType.Joy:
                        // 위로 뛰는 효과
                        await characterTransform.DOPunchPosition(new Vector3(0, 15f, 0), 0.5f, 10, 1f)
                            .SetEase(Ease.OutQuad)
                            .AsyncWaitForCompletion();
                        break;

                    case ReactionType.Sad:
                        // 아래로 처지는 효과
                        await characterTransform.DOPunchPosition(new Vector3(0, -10f, 0), 0.6f, 5, 1f)
                            .SetEase(Ease.InQuad)
                            .AsyncWaitForCompletion();
                        break;

                    case ReactionType.Shake:
                        // 고개를 젓는 효과
                        await characterTransform.DOShakePosition(0.3f, new Vector3(8f, 0, 0), 15, 90f, false, true)
                            .SetEase(Ease.OutQuad)
                            .AsyncWaitForCompletion();
                        break;
                }
            }
            catch (System.Exception) { /* 트윈 중단 시 무시 */ }
        }
    }

    public enum ReactionType
    {
        Surprise,   // 놀람
        Joy,        // 기쁨
        Sad,        // 슬픔
        Shake       // 고개 젓기
    }
}
