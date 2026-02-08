using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// Modal 팝업 기본 클래스
    /// panelRect/canvasGroup 바인딩 시 쫀득한 슬라이드 애니메이션 자동 적용
    /// </summary>
    public abstract class ModalPopupBase : MonoBehaviour
    {
        [Header("팝업 애니메이션 (공통)")]
        [SerializeField] protected RectTransform panelRect;
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected float showDuration = 0.35f;
        [SerializeField] protected float hideDuration = 0.22f;
        [SerializeField] protected float slideOffset = 280f;

        protected Vector2 originalPosition;
        Sequence currentSequence;

        protected virtual void Awake()
        {
            if (panelRect != null)
                originalPosition = panelRect.anchoredPosition;
        }

        public virtual void Show()
        {
            KillSequence();
            gameObject.SetActive(true);
            PlayShowAnimation();
        }

        public virtual void Hide()
        {
            KillSequence();
            PlayHideAnimation();
        }

        /// <summary>
        /// Show 애니메이션: 우측에서 슬라이드 + 스케일 + 페이드
        /// OutBack 오버슈트로 쫀득한 느낌
        /// </summary>
        protected virtual void PlayShowAnimation()
        {
            if (panelRect == null || canvasGroup == null) return;

            // 초기 상태: 우측 밖, 투명, 살짝 축소
            canvasGroup.alpha = 0f;
            panelRect.anchoredPosition = originalPosition + new Vector2(slideOffset, 0);
            panelRect.localScale = new Vector3(0.95f, 0.95f, 1f);

            var seq = DOTween.Sequence();

            // 페이드 인 (전체 듀레이션의 60%에 완료)
            seq.Append(canvasGroup.DOFade(1f, showDuration * 0.6f).SetEase(Ease.OutCubic));

            // 슬라이드: OutBack으로 살짝 오버슈트 후 착지
            seq.Join(panelRect.DOAnchorPos(originalPosition, showDuration).SetEase(Ease.OutBack, 1.05f));

            // 스케일: 0.95 → 1.0 자연스럽게
            seq.Join(panelRect.DOScale(1f, showDuration).SetEase(Ease.OutCubic));

            seq.SetUpdate(true);
            currentSequence = seq;
        }

        /// <summary>
        /// Hide 애니메이션: 우측으로 빠르게 빠져나감 + 스케일 다운 + 페이드
        /// InCubic 가속감으로 찰진 느낌
        /// </summary>
        protected virtual void PlayHideAnimation()
        {
            if (panelRect == null || canvasGroup == null)
            {
                gameObject.SetActive(false);
                return;
            }

            var seq = DOTween.Sequence();

            // 슬라이드 아웃: 가속하며 우측으로
            seq.Append(panelRect.DOAnchorPos(
                originalPosition + new Vector2(slideOffset * 0.8f, 0),
                hideDuration).SetEase(Ease.InCubic));

            // 스케일 다운: 살짝 줄어들며
            seq.Join(panelRect.DOScale(0.96f, hideDuration).SetEase(Ease.InCubic));

            // 페이드 아웃: 후반 40%에서 빠르게
            seq.Insert(hideDuration * 0.5f,
                canvasGroup.DOFade(0f, hideDuration * 0.5f).SetEase(Ease.InQuad));

            seq.SetUpdate(true);
            seq.OnComplete(() =>
            {
                // 상태 복원
                panelRect.localScale = Vector3.one;
                gameObject.SetActive(false);
            });

            currentSequence = seq;
        }

        protected void KillSequence()
        {
            if (currentSequence != null && currentSequence.IsActive())
            {
                currentSequence.Kill();
                currentSequence = null;
            }

            // 스케일 복원 (Kill 후 중간값 방지)
            if (panelRect != null)
                panelRect.localScale = Vector3.one;
        }

        /// <summary>
        /// 닫기 시도 (변경사항 확인 등)
        /// 닫아도 되면 true 반환, 취소하면 false 반환
        /// </summary>
        public virtual UniTask<bool> TryCloseAsync()
        {
            // 기본: 바로 닫기 허용
            return UniTask.FromResult(true);
        }

        /// <summary>
        /// 닫기 버튼에서 호출
        /// </summary>
        public void Close()
        {
            TryCloseAndDismiss().Forget();
        }

        /// <summary>
        /// 닫기 시도 후 실제 닫기
        /// </summary>
        async UniTaskVoid TryCloseAndDismiss()
        {
            bool canClose = await TryCloseAsync();
            if (canClose)
            {
                PopupManager.Instance?.CloseModal();
            }
        }

        protected virtual void OnDestroy()
        {
            KillSequence();
        }
    }
}
