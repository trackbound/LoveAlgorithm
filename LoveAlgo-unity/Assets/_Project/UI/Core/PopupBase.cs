using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 팝업 레이어 (z-order: Modal 뒤 ↔ Notification 앞).
    ///   Modal:        일반 *Popup (Log/Phone/Settings/Save/Choice 등)
    ///   Dialog:       Alert/Confirm — 모달 위 인터럽트
    ///   Notification: Toast/Place — 가장 위, 비차단
    /// </summary>
    public enum PopupLayer { Modal, Dialog, Notification }

    /// <summary>팝업 등장/퇴장 애니메이션 종류.</summary>
    public enum PopupAnimation
    {
        FloatUp,     // 기본 — 아래에서 위로 살짝 떠오르며 fade. Alert/Confirm/일반 popup
        Fade,        // 페이드만. 큰 popup(Log/Phone/Choice 등)에 적합
        SlideRight,  // 우측에서 슬라이드. Notification 계열
    }

    /// <summary>
    /// 모든 팝업의 공통 베이스.
    /// 애니메이션/Stack 통보를 표준화. Layer는 타입의 본질이므로 코드에서 선언. Dim은 popup prefab 자체에 포함.
    /// 결과를 반환하는 팝업은 <see cref="PopupBase{TResult}"/>를 상속.
    /// </summary>
    public abstract class PopupBase : MonoBehaviour
    {
        [Header("바인딩 (컨벤션: Root CG = 입력 정책, Panel CG = 애니메이션)")]
        [Tooltip("Root CanvasGroup — interactable/blocksRaycasts (popup 입력 정책). 없으면 GetComponent로 fallback.")]
        [SerializeField] protected CanvasGroup rootCanvasGroup;
        [Tooltip("Panel RectTransform — 이동/스케일 애니메이션 대상.")]
        [SerializeField] protected RectTransform panelRect;
        [Tooltip("Panel CanvasGroup — fade 애니메이션 (alpha 0↔1).")]
        [SerializeField] protected CanvasGroup canvasGroup;

        [Header("애니메이션 파라미터")]
        [Tooltip("Inspector에서 선택. 코드에서 override 시 그것이 우선.")]
        [SerializeField] protected PopupAnimation animationType = PopupAnimation.FloatUp;
        [SerializeField] protected float showDuration = 0.35f;
        [SerializeField] protected float hideDuration = 0.28f;
        [SerializeField] protected float slideOffset = 200f;

        protected Vector2 originalPosition;
        Sequence currentSequence;
        UniTaskCompletionSource hideCompletionSource;

        /// <summary>팝업이 속할 레이어. 기본 Modal — Dialog/Notification 계열은 override.</summary>
        public virtual PopupLayer Layer => PopupLayer.Modal;

        /// <summary>등장/퇴장 애니메이션 종류. 기본은 Inspector 값 사용 — 코드에서 override 시 그것이 우선.</summary>
        public virtual PopupAnimation AnimationType => animationType;

        /// <summary>FloatUp 거리 (Y+). FloatUp 모드에서만 사용.</summary>
        [SerializeField] protected float floatOffset = 15f;
        public bool IsVisible => gameObject.activeSelf;

        protected virtual void Awake()
        {
            // Root CG fallback (popup 객체 자체에 있는 CG)
            if (rootCanvasGroup == null) rootCanvasGroup = GetComponent<CanvasGroup>();
            // Panel CG가 비어있으면 Root CG를 fade에 재사용 (단일 CG 구조 호환)
            if (canvasGroup == null) canvasGroup = rootCanvasGroup;

            if (panelRect != null)
                originalPosition = panelRect.anchoredPosition;
        }

        /// <summary>입력 정책 토글 — Root CG의 interactable/blocksRaycasts 컨트롤.</summary>
        protected void SetInteractive(bool on)
        {
            var cg = rootCanvasGroup ?? canvasGroup;
            if (cg == null) return;
            cg.interactable = on;
            cg.blocksRaycasts = on;
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

        /// <summary>Show 애니메이션 — AnimationType에 따라 분기.</summary>
        protected virtual void PlayShowAnimation()
        {
            if (panelRect == null || canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            SetInteractive(false);

            Vector2 offset = AnimationType switch
            {
                PopupAnimation.SlideRight => new Vector2(slideOffset, 0),
                PopupAnimation.FloatUp    => new Vector2(0, -floatOffset),
                _                          => Vector2.zero,
            };
            panelRect.anchoredPosition = originalPosition + offset;
            panelRect.localScale = AnimationType == PopupAnimation.Fade ? Vector3.one : new Vector3(0.97f, 0.97f, 1f);

            var seq = DOTween.Sequence();
            seq.Append(canvasGroup.DOFade(1f, showDuration * 0.6f).SetEase(Ease.OutCubic));
            if (offset != Vector2.zero)
                seq.Join(panelRect.DOAnchorPos(originalPosition, showDuration).SetEase(Ease.OutCubic));
            if (AnimationType != PopupAnimation.Fade)
                seq.Join(panelRect.DOScale(1f, showDuration).SetEase(Ease.OutCubic));
            seq.OnComplete(() => SetInteractive(true));
            seq.SetUpdate(true);
            currentSequence = seq;
        }

        /// <summary>Hide 애니메이션 — AnimationType 대칭.</summary>
        protected virtual void PlayHideAnimation()
        {
            if (panelRect == null || canvasGroup == null)
            {
                gameObject.SetActive(false);
                hideCompletionSource?.TrySetResult();
                return;
            }

            SetInteractive(false);

            Vector2 offset = AnimationType switch
            {
                PopupAnimation.SlideRight => new Vector2(slideOffset, 0),
                PopupAnimation.FloatUp    => new Vector2(0, -floatOffset),
                _                          => Vector2.zero,
            };

            var seq = DOTween.Sequence();
            if (offset != Vector2.zero)
                seq.Append(panelRect.DOAnchorPos(originalPosition + offset, hideDuration).SetEase(Ease.InCubic));
            if (AnimationType != PopupAnimation.Fade)
                seq.Join(panelRect.DOScale(0.97f, hideDuration).SetEase(Ease.InCubic));
            seq.Insert(hideDuration * 0.5f,
                canvasGroup.DOFade(0f, hideDuration * 0.5f).SetEase(Ease.InQuad));
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
