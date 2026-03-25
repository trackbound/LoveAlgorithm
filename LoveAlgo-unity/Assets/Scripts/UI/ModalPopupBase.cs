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
        UniTaskCompletionSource hideCompletionSource;

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
        /// Hide 애니메이션 완료까지 대기 가능한 버전
        /// </summary>
        public virtual UniTask HideAsync()
        {
            var tcs = new UniTaskCompletionSource();
            hideCompletionSource = tcs;
            Hide();
            return tcs.Task;
        }

        /// <summary>
        /// Show 애니메이션: 우측에서 슬라이드 + 스케일 + 페이드
        /// OutBack 오버슈트로 쫀득한 느낌
        /// </summary>
        protected virtual void PlayShowAnimation()
        {
            if (panelRect == null || canvasGroup == null) return;

            // 초기 상태: 우측 밖, 투명, 살짝 축소, 인터랙션 차단
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            panelRect.anchoredPosition = originalPosition + new Vector2(slideOffset, 0);
            panelRect.localScale = new Vector3(0.97f, 0.97f, 1f);

            var seq = DOTween.Sequence();

            // 페이드 인: 초반에 빠르게 불투명
            seq.Append(canvasGroup.DOFade(1f, showDuration * 0.5f).SetEase(Ease.OutQuad));

            // 슬라이드: OutQuart — 빠르게 감속하며 부드럽게 착지 (튕김 없음)
            seq.Join(panelRect.DOAnchorPos(originalPosition, showDuration).SetEase(Ease.OutQuart));

            // 스케일: 0.97 → 1.0
            seq.Join(panelRect.DOScale(1f, showDuration).SetEase(Ease.OutQuart));

            // 애니메이션 완료 후 인터랙션 활성화
            seq.OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });

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
                hideCompletionSource?.TrySetResult();
                return;
            }

            // 즉시 인터랙션 차단
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var seq = DOTween.Sequence();

            // 슬라이드 아웃: 가속하며 우측으로 완전히 밀려남
            seq.Append(panelRect.DOAnchorPos(
                originalPosition + new Vector2(slideOffset, 0),
                hideDuration).SetEase(Ease.InQuart));

            // 스케일 다운: 살짝 줄어들며
            seq.Join(panelRect.DOScale(0.97f, hideDuration).SetEase(Ease.InQuad));

            // 페이드 아웃: 마지막 20%에서만 — 슬라이드 끝무렵에 사라짐
            seq.Insert(hideDuration * 0.8f,
                canvasGroup.DOFade(0f, hideDuration * 0.2f).SetEase(Ease.InQuad));

            seq.SetUpdate(true);
            seq.OnComplete(() =>
            {
                panelRect.localScale = Vector3.one;
                gameObject.SetActive(false);
                hideCompletionSource?.TrySetResult();
            });

            currentSequence = seq;
        }

        protected void KillSequence()
        {
            if (currentSequence != null && currentSequence.IsActive())
            {
                currentSequence.Kill();
                currentSequence = null;

                // Kill된 Hide 애니메이션의 대기 중인 HideAsync 해제
                hideCompletionSource?.TrySetResult();
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
