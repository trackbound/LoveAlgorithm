using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// Extra 화면 — SCENE / CG / 수집
    /// 중앙에 3개 대형 메뉴 버튼, 호버 시 위로 살짝 올라가는 애니메이션
    /// </summary>
    public class ExtraPopup : ModalPopupBase
    {
        [Header("메뉴 버튼")]
        [SerializeField] Button sceneButton;
        [SerializeField] Button cgButton;
        [SerializeField] Button collectionButton;
        [SerializeField] Button closeButton;

        [Header("호버 설정")]
        [SerializeField] float hoverOffset = 20f;
        [SerializeField] float hoverDuration = 0.2f;

        protected override void Awake()
        {
            base.Awake();

            closeButton?.onClick.AddListener(Close);

            // 클릭 (추후 서브메뉴 진입)
            sceneButton?.onClick.AddListener(OnSceneClick);
            cgButton?.onClick.AddListener(OnCGClick);
            collectionButton?.onClick.AddListener(OnCollectionClick);

            // 호버 이펙트 바인딩
            BindHoverEffect(sceneButton);
            BindHoverEffect(cgButton);
            BindHoverEffect(collectionButton);
        }

        public override void Show()
        {
            gameObject.SetActive(true);
            PlayShowAnimation();
        }

        public override void Hide()
        {
            KillSequence();
            PlayHideAnimation();
        }

        #region 호버 이펙트

        void BindHoverEffect(Button button)
        {
            if (button == null) return;

            var rt = button.GetComponent<RectTransform>();
            if (rt == null) return;

            var originalY = rt.anchoredPosition.y;

            var trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            // Pointer Enter — 위로 올라감
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                rt.DOAnchorPosY(originalY + hoverOffset, hoverDuration)
                    .SetEase(Ease.OutCubic).SetUpdate(true);
            });
            trigger.triggers.Add(enter);

            // Pointer Exit — 원래 위치로
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                rt.DOAnchorPosY(originalY, hoverDuration)
                    .SetEase(Ease.OutCubic).SetUpdate(true);
            });
            trigger.triggers.Add(exit);
        }

        #endregion

        #region 버튼 핸들러 (추후 서브메뉴 구현)

        void OnSceneClick()
        {
            Debug.Log("[ExtraPopup] SCENE 선택 — 씬 다시보기 (미구현)");
            PopupManager.Instance?.Toast("SCENE", "씬 다시보기 기능은 추후 구현됩니다.");
        }

        void OnCGClick()
        {
            Debug.Log("[ExtraPopup] CG 선택 — 일러스트 보기 (미구현)");
            PopupManager.Instance?.Toast("CG", "일러스트 보기 기능은 추후 구현됩니다.");
        }

        void OnCollectionClick()
        {
            Debug.Log("[ExtraPopup] 수집 선택 — 수집 요소 보기 (미구현)");
            PopupManager.Instance?.Toast("수집", "수집 요소 보기 기능은 추후 구현됩니다.");
        }

        #endregion
    }
}