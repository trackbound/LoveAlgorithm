using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 팝업 레이어 (네이밍 컨벤션의 UI 카테고리와 1:1).
    /// Modal: *Popup — 진행 차단 + dim / Notification: *Notification·*Tooltip — 비차단, 위에 표시.
    /// </summary>
    public enum PopupLayer { Modal, Notification }

    /// <summary>
    /// 모든 팝업의 공통 베이스.
    /// 애니메이션/Stack 통보를 표준화. Layer/UseDimmer는 타입의 본질이므로 코드에서 선언.
    /// 결과를 반환하는 팝업은 <see cref="PopupBase{TResult}"/>를 상속.
    /// </summary>
    public abstract class PopupBase : MonoBehaviour
    {
        [Header("애니메이션 (panelRect/canvasGroup 바인딩 시 자동 적용)")]
        [SerializeField] protected RectTransform panelRect;
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected float showDuration = 0.35f;
        [SerializeField] protected float hideDuration = 0.28f;
        [SerializeField] protected float slideOffset = 200f;

        protected Vector2 originalPosition;
        Sequence currentSequence;
        UniTaskCompletionSource hideCompletionSource;

        /// <summary>팝업이 속할 레이어. 기본 Modal — *Notification 계열은 override.</summary>
        public virtual PopupLayer Layer => PopupLayer.Modal;
        /// <summary>Dimmer 사용 여부. 기본 true — *Notification 계열은 override로 false.</summary>
        public virtual bool UseDimmer => true;
        public bool IsVisible => gameObject.activeSelf;

        protected virtual void Awake()
        {
            if (panelRect != null)
                originalPosition = panelRect.anchoredPosition;
        }

        public virtual void Show()
        {
            KillSequence();
            gameObject.SetActive(true);
            PopupManager.Instance?.NotifyOpened(this);
            PlayShowAnimation();
            UISoundManager.Instance?.PlayPopupOpen();
        }

        public virtual void Hide()
        {
            KillSequence();
            PlayHideAnimation();
            PopupManager.Instance?.NotifyClosed(this);
            UISoundManager.Instance?.PlayPopupClose();
        }

        /// <summary>Hide 애니메이션 완료까지 대기 가능한 버전.</summary>
        public virtual UniTask HideAsync()
        {
            var tcs = new UniTaskCompletionSource();
            hideCompletionSource = tcs;
            Hide();
            return tcs.Task;
        }

        /// <summary>Show 애니메이션: 우측에서 슬라이드 + 스케일 + 페이드.</summary>
        protected virtual void PlayShowAnimation()
        {
            if (panelRect == null || canvasGroup == null) return;

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            panelRect.anchoredPosition = originalPosition + new Vector2(slideOffset, 0);
            panelRect.localScale = new Vector3(0.97f, 0.97f, 1f);

            var seq = DOTween.Sequence();
            seq.Append(canvasGroup.DOFade(1f, showDuration * 0.6f).SetEase(Ease.OutCubic));
            seq.Join(panelRect.DOAnchorPos(originalPosition, showDuration).SetEase(Ease.OutCubic));
            seq.Join(panelRect.DOScale(1f, showDuration).SetEase(Ease.OutCubic));
            seq.OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });
            seq.SetUpdate(true);
            currentSequence = seq;
        }

        /// <summary>Hide 애니메이션: 우측으로 빠르게 빠져나감.</summary>
        protected virtual void PlayHideAnimation()
        {
            if (panelRect == null || canvasGroup == null)
            {
                gameObject.SetActive(false);
                hideCompletionSource?.TrySetResult();
                return;
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var seq = DOTween.Sequence();
            seq.Append(panelRect.DOAnchorPos(
                originalPosition + new Vector2(slideOffset, 0),
                hideDuration).SetEase(Ease.InCubic));
            seq.Join(panelRect.DOScale(0.97f, hideDuration).SetEase(Ease.InCubic));
            seq.Insert(hideDuration * 0.7f,
                canvasGroup.DOFade(0f, hideDuration * 0.3f).SetEase(Ease.InQuad));
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
                hideCompletionSource?.TrySetResult();
            }
            if (panelRect != null)
                panelRect.localScale = Vector3.one;
        }

        /// <summary>닫기 시도 (변경사항 확인 등). 닫아도 되면 true.</summary>
        public virtual UniTask<bool> TryCloseAsync() => UniTask.FromResult(true);

        /// <summary>닫기 버튼/외부 호출용.</summary>
        public void Close() => TryCloseAndDismiss().Forget();

        async UniTaskVoid TryCloseAndDismiss()
        {
            bool canClose = await TryCloseAsync();
            if (canClose) Hide();
        }

        protected virtual void OnDestroy()
        {
            KillSequence();
        }
    }

    /// <summary>
    /// 결과를 반환하는 팝업 (Confirm 등).
    /// 자식이 <see cref="Complete"/>로 결과 전달.
    /// </summary>
    public abstract class PopupBase<TResult> : PopupBase
    {
        UniTaskCompletionSource<TResult> tcs;

        /// <summary>호출자 측에서 await 가능한 결과 task. ShowAsync 구현에서 사용.</summary>
        protected UniTask<TResult> AwaitResult()
        {
            tcs?.TrySetCanceled();
            tcs = new UniTaskCompletionSource<TResult>();
            return tcs.Task;
        }

        /// <summary>결과 확정 (OnConfirm/OnCancel 등에서 호출).</summary>
        protected void Complete(TResult result)
        {
            var pending = tcs;
            tcs = null;
            Hide();
            pending?.TrySetResult(result);
        }

        public override void Hide()
        {
            base.Hide();
            // 외부에서 Hide() 호출 시에도 대기 중인 task는 default로 완료
            tcs?.TrySetResult(default);
            tcs = null;
        }

        protected override void OnDestroy()
        {
            tcs?.TrySetCanceled();
            tcs = null;
            base.OnDestroy();
        }
    }
}
