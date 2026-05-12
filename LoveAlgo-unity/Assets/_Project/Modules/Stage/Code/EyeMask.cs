using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 눈 감기/뜨기 연출용 검은 바 (Top/Bottom).
    /// Stage 캔버스 하위에 배치되어 BG/캐릭터는 가리되 대화창(상위 캔버스)은 가리지 않음.
    /// 바 자체와 트윈 로직 모두 본 컴포넌트가 소유. ScreenFX는 위임만.
    /// </summary>
    public class EyeMask : MonoBehaviour
    {
        [SerializeField] Image top;
        [SerializeField] Image bottom;

        public Image Top => top;
        public Image Bottom => bottom;

        RectTransform rtTop;
        RectTransform rtBottom;
        float halfHeight;
        bool initialized;
        Sequence sequence;

        /// <summary>눈이 감긴 상태인지 (세이브용)</summary>
        public bool IsClosed => top != null && top.gameObject.activeSelf;

        void EnsureSetup()
        {
            if (initialized) return;
            if (top == null || bottom == null) return;

            rtTop = top.rectTransform;
            rtBottom = bottom.rectTransform;

            var parentRect = (RectTransform)rtTop.parent;
            float screenHeight = parentRect.rect.height;
            float screenWidth = parentRect.rect.width;
            halfHeight = screenHeight / 2f;

            // top: 상단 앵커, pivot 하단
            rtTop.anchorMin = new Vector2(0.5f, 1f);
            rtTop.anchorMax = new Vector2(0.5f, 1f);
            rtTop.pivot = new Vector2(0.5f, 0f);
            rtTop.sizeDelta = new Vector2(screenWidth + 100f, halfHeight + 50f);

            // bottom: 하단 앵커, pivot 상단
            rtBottom.anchorMin = new Vector2(0.5f, 0f);
            rtBottom.anchorMax = new Vector2(0.5f, 0f);
            rtBottom.pivot = new Vector2(0.5f, 1f);
            rtBottom.sizeDelta = new Vector2(screenWidth + 100f, halfHeight + 50f);

            initialized = true;
        }

        void KillSequence()
        {
            if (sequence != null && sequence.IsActive())
                sequence.Kill();
            sequence = null;
        }

        /// <summary>
        /// 눈 뜨는 효과 — 2단계: 살짝 틈 → 확 열림
        /// Phase 1 (30%): 살짝 열림 — 눈부심으로 멈칫 (OutSine)
        /// Phase 2 (70%): 부드럽게 완전히 열림 (OutCubic)
        /// </summary>
        public async UniTask OpenAsync(float duration = 1f, CancellationToken ct = default)
        {
            EnsureSetup();
            if (!initialized) return;

            KillSequence();

            rtTop.anchoredPosition = new Vector2(0, -halfHeight);
            rtBottom.anchoredPosition = new Vector2(0, halfHeight);
            top.gameObject.SetActive(true);
            bottom.gameObject.SetActive(true);

            float peekRatio = 0.15f;
            float peekY = halfHeight * (1f - peekRatio);
            float phase1 = duration * 0.3f;
            float phase2 = duration * 0.7f;

            sequence = DOTween.Sequence()
                .Append(rtTop.DOAnchorPosY(-peekY, phase1).SetEase(Ease.OutSine))
                .Join(rtBottom.DOAnchorPosY(peekY, phase1).SetEase(Ease.OutSine))
                .Append(rtTop.DOAnchorPosY(0, phase2).SetEase(Ease.OutCubic))
                .Join(rtBottom.DOAnchorPosY(0, phase2).SetEase(Ease.OutCubic));

            await sequence.ToUniTask(cancellationToken: ct);

            top.gameObject.SetActive(false);
            bottom.gameObject.SetActive(false);
            sequence = null;
        }

        /// <summary>
        /// 눈 감는 효과 — 서서히 → 끝에서 가속 (눈꺼풀 무게감)
        /// </summary>
        public async UniTask CloseAsync(float duration = 1f, CancellationToken ct = default)
        {
            EnsureSetup();
            if (!initialized) return;

            KillSequence();

            rtTop.anchoredPosition = new Vector2(0, 0);
            rtBottom.anchoredPosition = new Vector2(0, 0);
            top.gameObject.SetActive(true);
            bottom.gameObject.SetActive(true);

            sequence = DOTween.Sequence()
                .Append(rtTop.DOAnchorPosY(-halfHeight, duration).SetEase(Ease.InCubic))
                .Join(rtBottom.DOAnchorPosY(halfHeight, duration).SetEase(Ease.InCubic));

            await sequence.ToUniTask(cancellationToken: ct);
            sequence = null;
        }

        /// <summary>
        /// 눈 깜빡임 — 닫기 → 잠깐 유지(hold) → 열기
        /// </summary>
        public async UniTask BlinkAsync(float closeDuration = 0.1f, float openDuration = 0.15f,
            float holdTime = 0.05f, CancellationToken ct = default)
        {
            EnsureSetup();
            if (!initialized) return;

            KillSequence();

            rtTop.anchoredPosition = new Vector2(0, 0);
            rtBottom.anchoredPosition = new Vector2(0, 0);
            top.gameObject.SetActive(true);
            bottom.gameObject.SetActive(true);

            sequence = DOTween.Sequence()
                .Append(rtTop.DOAnchorPosY(-halfHeight, closeDuration).SetEase(Ease.InQuad))
                .Join(rtBottom.DOAnchorPosY(halfHeight, closeDuration).SetEase(Ease.InQuad))
                .AppendInterval(holdTime)
                .Append(rtTop.DOAnchorPosY(0, openDuration).SetEase(Ease.OutCubic))
                .Join(rtBottom.DOAnchorPosY(0, openDuration).SetEase(Ease.OutCubic));

            await sequence.ToUniTask(cancellationToken: ct);

            top.gameObject.SetActive(false);
            bottom.gameObject.SetActive(false);
            sequence = null;
        }

        /// <summary>눈 즉시 닫힘 (애니메이션 없이)</summary>
        public void CloseImmediate()
        {
            EnsureSetup();
            if (!initialized) return;

            KillSequence();

            rtTop.anchoredPosition = new Vector2(0, -halfHeight);
            rtBottom.anchoredPosition = new Vector2(0, halfHeight);
            top.gameObject.SetActive(true);
            bottom.gameObject.SetActive(true);
        }

        /// <summary>눈 즉시 열림 (애니메이션 없이, 바 비활성화)</summary>
        public void OpenImmediate()
        {
            KillSequence();
            if (top != null) top.gameObject.SetActive(false);
            if (bottom != null) bottom.gameObject.SetActive(false);
        }
    }
}
