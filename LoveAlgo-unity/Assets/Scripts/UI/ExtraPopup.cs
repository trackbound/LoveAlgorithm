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
        [Header("탭")]
        [SerializeField] Button tabScene;
        [SerializeField] Button tabCG;
        [SerializeField] Button tabCollection;

        [Header("컨테이너")]
        [SerializeField] GameObject sceneContainer;
        [SerializeField] GameObject cgContainer;
        [SerializeField] GameObject collectionContainer;

        protected override void Awake()
        {
            base.Awake();

            tabScene?.onClick.AddListener(() => SelectTab(0));
            tabCG?.onClick.AddListener(() => SelectTab(1));
            tabCollection?.onClick.AddListener(() => SelectTab(2));
        }

        public override void Show()
        {
            gameObject.SetActive(true);
            SelectTab(0);
            PlayShowAnimation();
        }

        public override void Hide()
        {
            KillSequence();
            PlayHideAnimation();
        }

        void SelectTab(int index)
        {
            if (sceneContainer != null) sceneContainer.SetActive(index == 0);
            if (cgContainer != null) cgContainer.SetActive(index == 1);
            if (collectionContainer != null) collectionContainer.SetActive(index == 2);
        }
    }
}