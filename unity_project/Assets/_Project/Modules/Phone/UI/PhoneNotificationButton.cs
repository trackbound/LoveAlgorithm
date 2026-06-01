using LoveAlgo.Contracts;
using DG.Tweening;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Stage;
using LoveAlgo.Story;
using LoveAlgo.UI;
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
    /// - 새 메시지: 핸들 스프라이트 통째로 교체 (btn_phone → btn_phone_new)
    ///   * N 뱃지를 위에 얹지 않음 — normalImage / newMessageImage GameObject 토글
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

        [Header("핸들 이미지 (둘 중 하나만 활성 — 통이미지 교체)")]
        [Tooltip("평상시 스프라이트: btn_phone (말풍선 + MESSAGE)")]
        [SerializeField] GameObject normalImage;
        [Tooltip("새 메시지 시 스프라이트: btn_phone_new (N이 통합된 단일 이미지)")]
        [SerializeField] GameObject newMessageImage;

        [Header("클릭 버튼")]
        [SerializeField] Button openButton;

        [Header("애니메이션")]
        [Tooltip("호버 시 슬라이드 거리 (음수 = 좌측으로). 핸들 너비 정도")]
        [SerializeField] float slideOffsetX = -120f;
        [SerializeField] float slideDuration = 0.25f;
        [SerializeField] Ease slideEase = Ease.OutCubic;

        [Header("새 메시지 진동")]
        [Tooltip("새 메시지 도착 시 흔드는 시간 (초)")]
        [SerializeField] float shakeDuration = 2f;
        [Tooltip("흔들기 강도 (픽셀)")]
        [SerializeField] float shakeStrength = 8f;
        [Tooltip("흔들기 진동수")]
        [SerializeField] int shakeVibrato = 18;

        [Header("폴링")]
        [Tooltip("새 메시지 카운트 + Stage CG/SD 가림 감지 간격 (0이면 OnEnable에서만)")]
        [SerializeField] float pollInterval = 0.2f;

        float collapsedX;
        Tween currentTween;
        Tween shakeTween;
        float nextPollAt;
        int lastUnreadCount;
        bool pendingShake;          // 도착 시점이 blocked → 가시화 후 1회 발화 예약
        bool _warnedSelfBinding;    // expandedView 자가 바인딩 경고 1회만

        readonly ListenerBag _listeners = new();

        void Awake()
        {
            if (slideContainer != null) collapsedX = slideContainer.anchoredPosition.x;
            _listeners.Bind(openButton, OnOpenClick);
            SetExpanded(false, instant: true);
        }

        void OnEnable()
        {
            lastUnreadCount = MessengerSystem.GetTotalUnreadCount();
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

        /// <summary>
        /// 표시 가능 컨텍스트 판정 — 다음 중 하나라도 해당이면 가림:
        ///   • CG / SD 컷씬 표시 중 (몰입 방해)
        ///   • Video 재생 중 (풀스크린 컷씬)
        ///   • ScreenFX 페이드 검정 / 눈감김 활성 (페이드 도중 핑크 아이콘 어색)
        ///   • LoadingScreen 표시 중
        ///   • Popup(모달/Dialog 등) 활성 — Choice/Save/Load/Confirm 모두 포함
        ///   • GamePhase가 Prologue/DayLoop 아닐 때 (Title/Username/Ending 등 스토리 외)
        /// </summary>
        void SyncStageVisibility()
        {
            SetVisualVisible(!IsBlocked());
        }

        bool IsBlocked()
        {
            // Phase — 스토리 컨텍스트 외에는 노출 X
            var gm = GameManager.Instance;
            if (gm != null)
            {
                var phase = gm.CurrentPhase;
                if (phase != GamePhase.Prologue && phase != GamePhase.DayLoop)
                    return true;
            }

            // Stage 컷씬
            var sm = StageModule.Instance;
            if (sm != null)
            {
                if (sm.CG != null && sm.CG.IsShowing) return true;
                if (sm.SDCutscene != null && sm.SDCutscene.IsShowing) return true;
            }

            // Video 풀스크린
            if (VideoLayer.Instance != null && VideoLayer.Instance.IsPlaying) return true;

            // ScreenFX 페이드 / EyeMask
            var fx = ScreenFX.Instance;
            if (fx != null && (fx.IsFadeBlack || fx.IsEyeClosed)) return true;

            // 로딩 화면
            if (LoadingScreen.Instance != null && LoadingScreen.Instance.IsShowing) return true;

            // 팝업 (Choice/Save/Load/Confirm — PopupBase 기반 모두 포함)
            if (PopupSystem.Instance != null && PopupSystem.Instance.IsAnyPopupOpen) return true;

            return false;
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

            // 가드 — prefab의 expandedView가 루트 GameObject 자체로 바인딩되어 있으면
            // SetActive(false) 시 본인이 비활성 → Update/이벤트 안 받음 → 영구 hide 버그.
            // expandedView는 반드시 "확장 시 추가로 노출되는 child" (예: MESSAGE 텍스트)여야 함.
            if (expandedView != null && expandedView != gameObject)
                expandedView.SetActive(expanded);
            else if (expandedView == gameObject)
            {
                // 1회만 경고 — 매 호버마다 폭주 방지
                if (!_warnedSelfBinding)
                {
                    Debug.LogWarning(
                        "[PhoneNotificationButton] expandedView가 루트와 같음 — prefab에서 child로 변경해야 호버 떼면 사라지는 버그 해소. 일단 SetActive 호출 스킵.",
                        this);
                    _warnedSelfBinding = true;
                }
            }

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
            int unread = MessengerSystem.GetTotalUnreadCount();
            bool hasNew = unread > 0;
            if (normalImage != null) normalImage.SetActive(!hasNew);
            if (newMessageImage != null) newMessageImage.SetActive(hasNew);

            // 새 메시지 도착 감지 — blocked 중이면 진동 보류, 가시화 시점에 1회 발화
            if (unread > lastUnreadCount)
            {
                if (IsBlocked()) pendingShake = true;
                else             PlayShake();
            }
            lastUnreadCount = unread;

            // 보류된 진동 — 가시 상태이면서 새 메시지 있을 때 발화
            if (pendingShake && hasNew && !IsBlocked())
            {
                PlayShake();
                pendingShake = false;
            }
        }

        void PlayShake()
        {
            if (slideContainer == null || shakeDuration <= 0f) return;
            shakeTween?.Kill(complete: true);
            shakeTween = slideContainer
                .DOShakeAnchorPos(shakeDuration, new Vector2(shakeStrength, 0f), shakeVibrato, 90f, false, true)
                .SetUpdate(true);
        }

        void OnOpenClick()
        {
            Services.TryGet<IPhone>()?.ShowPhoneUI();
            // 폰 열고 나면 알림 자동 갱신
            UpdateBadge();
        }

        void OnDestroy()
        {
            _listeners.Dispose();
            currentTween?.Kill();
            shakeTween?.Kill();
        }
    }
}
