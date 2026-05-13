using DG.Tweening;
using LoveAlgo.Common;
using LoveAlgo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 스토리 진행 중 화면 우측에 떠있는 메신저 알림 버튼.
    ///
    /// 동작:
    /// - 평시: 핑크 아이콘만 우측 끝에 노출 (호버 영역만 보임)
    /// - 호버: 슬라이드 인 → "MESSAGE" 텍스트 + 핸들 전체 표시
    /// - 새 메시지: 우상단 N 뱃지 표시
    /// - 클릭: 메신저(PhonePopup) 열기
    ///
    /// 씬 배치: _UI/Narrative/PhoneNotificationButton (스토리 컨텍스트 상시)
    /// 또는 PhoneModule의 SerializeField로 sceneInstance/prefab 바인딩.
    /// </summary>
    public class PhoneNotificationButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("슬라이드 컨테이너 (호버 시 옆으로 이동)")]
        [Tooltip("전체 버튼 RectTransform — anchoredPosition.x를 애니메이션")]
        [SerializeField] RectTransform slideContainer;

        [Header("확장 시 노출되는 부분 (MESSAGE 텍스트 + 핸들)")]
        [SerializeField] GameObject expandedView;
        [SerializeField] TMP_Text labelText;
        [SerializeField] string labelDefault = "MESSAGE";

        [Header("핸들 이미지 (둘 중 하나만 활성)")]
        [Tooltip("평상시 핸들 이미지 (말풍선 + MESSAGE)")]
        [SerializeField] GameObject normalImage;
        [Tooltip("새 메시지 있을 때 핸들 이미지 (N 뱃지 포함된 통합 이미지)")]
        [SerializeField] GameObject newMessageImage;

        [Header("클릭 버튼")]
        [SerializeField] Button openButton;

        [Header("애니메이션")]
        [Tooltip("호버 시 슬라이드 거리 (음수 = 좌측으로). 핸들 너비 정도")]
        [SerializeField] float slideOffsetX = -120f;
        [SerializeField] float slideDuration = 0.25f;
        [SerializeField] Ease slideEase = Ease.OutCubic;

        [Header("폴링")]
        [Tooltip("새 메시지 카운트 + Stage CG/SD 가림 감지 간격 (0이면 OnEnable에서만)")]
        [SerializeField] float pollInterval = 0.2f;

        float collapsedX;
        Tween currentTween;
        float nextPollAt;

        void Awake()
        {
            if (slideContainer != null) collapsedX = slideContainer.anchoredPosition.x;
            if (openButton != null) openButton.onClick.AddListener(OnOpenClick);
            SetExpanded(false, instant: true);
        }

        void OnEnable()
        {
            UpdateBadge();
            nextPollAt = pollInterval > 0f ? Time.unscaledTime + pollInterval : float.PositiveInfinity;
        }

        void Update()
        {
            if (pollInterval > 0f && Time.unscaledTime >= nextPollAt)
            {
                UpdateBadge();
                SyncStageVisibility();
                nextPollAt = Time.unscaledTime + pollInterval;
            }
        }

        /// <summary>Stage CG/SD가 표시 중이면 자식 visual 가림 (외부 명시 제어와 별개).</summary>
        void SyncStageVisibility()
        {
            bool blocked = false;
            var sm = StageManager.Instance;
            if (sm != null)
            {
                blocked = (sm.CG != null && sm.CG.IsShowing) ||
                          (sm.SDCutscene != null && sm.SDCutscene.IsShowing);
            }
            SetVisualVisible(!blocked);
        }

        /// <summary>핸들 visual 자체 표시/숨김 (자기 GO는 활성 유지, polling 계속).</summary>
        public void SetVisualVisible(bool visible)
        {
            if (slideContainer != null && slideContainer.gameObject.activeSelf != visible)
                slideContainer.gameObject.SetActive(visible);
        }

        public void OnPointerEnter(PointerEventData _) => SetExpanded(true);
        public void OnPointerExit(PointerEventData _) => SetExpanded(false);

        void SetExpanded(bool expanded, bool instant = false)
        {
            currentTween?.Kill();

            if (expandedView != null) expandedView.SetActive(expanded);
            if (labelText != null && expanded) labelText.text = labelDefault;

            if (slideContainer == null) return;

            float targetX = expanded ? collapsedX + slideOffsetX : collapsedX;
            if (instant)
            {
                slideContainer.anchoredPosition = new Vector2(targetX, slideContainer.anchoredPosition.y);
            }
            else
            {
                currentTween = slideContainer
                    .DOAnchorPosX(targetX, slideDuration)
                    .SetEase(slideEase)
                    .SetUpdate(true);
            }
        }

        /// <summary>새 메시지 상태 갱신 — normalImage / newMessageImage 토글.</summary>
        public void UpdateBadge()
        {
            bool hasNew = MessengerManager.GetTotalUnreadCount() > 0;
            if (normalImage != null) normalImage.SetActive(!hasNew);
            if (newMessageImage != null) newMessageImage.SetActive(hasNew);
        }

        void OnOpenClick()
        {
            Services.Get<IPhone>()?.ShowPhoneUI();
            // 폰 열고 나면 알림 자동 갱신
            UpdateBadge();
        }

        void OnDestroy()
        {
            currentTween?.Kill();
        }
    }
}
