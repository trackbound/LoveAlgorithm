using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// Extra 화면 (골격)
    /// - 탭: Scene / CG / Collection
    /// - 슬라이드 애니메이션
    /// </summary>
    public class ExtraPopup : ModalPopupBase
    {
        [Header("애니메이션")]
        [SerializeField] RectTransform panelRect;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] float showDuration = 0.3f;
        [SerializeField] float hideDuration = 0.2f;
        [SerializeField] float slideOffset = 300f;

        [Header("탭")]
        [SerializeField] Button tabScene;
        [SerializeField] Button tabCG;
        [SerializeField] Button tabCollection;

        [Header("컨테이너")]
        [SerializeField] GameObject sceneContainer;
        [SerializeField] GameObject cgContainer;
        [SerializeField] GameObject collectionContainer;

        Vector2 originalPosition;

        void Awake()
        {
            if (panelRect != null)
                originalPosition = panelRect.anchoredPosition;

            tabScene?.onClick.AddListener(() => SelectTab(0));
            tabCG?.onClick.AddListener(() => SelectTab(1));
            tabCollection?.onClick.AddListener(() => SelectTab(2));
        }

        public override void Show()
        {
            gameObject.SetActive(true);
            SelectTab(0);
            PlayShowAnimation().Forget();
        }

        public override void Hide()
        {
            PlayHideAnimation().Forget();
        }

        async UniTaskVoid PlayShowAnimation()
        {
            if (panelRect == null || canvasGroup == null)
            {
                base.Show();
                return;
            }

            canvasGroup.alpha = 0f;
            panelRect.anchoredPosition = originalPosition + new Vector2(slideOffset, 0);

            await DOTween.Sequence()
                .Append(canvasGroup.DOFade(1f, showDuration))
                .Join(panelRect.DOAnchorPos(originalPosition, showDuration).SetEase(Ease.OutQuad))
                .AsyncWaitForCompletion();
        }

        async UniTaskVoid PlayHideAnimation()
        {
            if (panelRect == null || canvasGroup == null)
            {
                base.Hide();
                return;
            }

            await DOTween.Sequence()
                .Append(panelRect.DOAnchorPos(originalPosition + new Vector2(slideOffset, 0), hideDuration).SetEase(Ease.InQuad))
                .Insert(hideDuration * 0.6f, canvasGroup.DOFade(0f, hideDuration * 0.4f))
                .AsyncWaitForCompletion();

            gameObject.SetActive(false);
        }

        void SelectTab(int index)
        {
            if (sceneContainer != null) sceneContainer.SetActive(index == 0);
            if (cgContainer != null) cgContainer.SetActive(index == 1);
            if (collectionContainer != null) collectionContainer.SetActive(index == 2);
        }
    }
}