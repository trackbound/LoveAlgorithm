using LoveAlgo.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core;
using LoveAlgo.Story;
using LoveAlgo.UI;

// DOTween + UniTask 브릿지에서 Tweener가 awaitable로 인식되어 발생하는 경고 억제
// seq.Join()에 전달되므로 의도된 동작
#pragma warning disable CS4014

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 선택 UI
    /// 탭 3개(알바/운동/공부) × 슬롯 3개 + 상점 크로스페이드 패널
    /// </summary>
    public class ScheduleUI : MonoBehaviour
    {
        [Header("애니메이션")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] float showDuration = 0.3f;
        [SerializeField] float hideDuration = 0.2f;
        [SerializeField] float crossFadeDuration = 0.25f;

        [Header("왼쪽 패널 - 정보")]
        [SerializeField] TMP_Text dayText;
        [SerializeField] TMP_Text moneyText;
        [SerializeField] TMP_Text remainingActionsText;

        [Header("왼쪽 패널 - 스탯 게이지")]
        [SerializeField] StatGauge strengthGauge;
        [SerializeField] StatGauge intelligenceGauge;
        [SerializeField] StatGauge socialGauge;
        [SerializeField] StatGauge perseveranceGauge;
        [SerializeField] StatGauge fatigueGauge;

        [Header("탭 그룹 (알바/운동/공부)")]
        [SerializeField] TabGroup tabGroup;

        [Header("카테고리 컨테이너 (탭별 슬롯 그룹)")]
        [SerializeField] GameObject containerPartTime;
        [SerializeField] GameObject containerExercise;
        [SerializeField] GameObject containerStudy;

        [Header("탭 직접 행동 모드 (탭 클릭 → 슬롯 표시 대신 즉시 행동 팝업)")]
        [Tooltip("체크 시 운동 탭 클릭 → exerciseDirectAction 즉시 실행 (슬롯 미표시).\n해제 시 기존 탭 동작 (슬롯 표시).")]
        [SerializeField] bool exerciseAsDirectAction = true;
        [SerializeField] ScheduleType exerciseDirectAction = ScheduleType.Exercise_A; // 헬스장 (체력+3)

        [Tooltip("체크 시 공부 탭 클릭 → studyDirectAction 즉시 실행 (슬롯 미표시).\n해제 시 기존 탭 동작 (슬롯 표시).")]
        [SerializeField] bool studyAsDirectAction = true;
        [SerializeField] ScheduleType studyDirectAction = ScheduleType.Study_D;       // 독서실 (지성+3)

        [Header("카테고리 설명")]
        [SerializeField] TMP_Text categoryDescText;

        [Header("스케줄 슬롯 (전체 9개)")]
        [SerializeField] ScheduleSlot[] scheduleSlots;

        [Header("하단 버튼 (행동 소비 없음)")]
        [SerializeField] Button shopButton;
        [SerializeField] Button phoneButton;
        [SerializeField] GameObject phoneNewBadge;
        [SerializeField] Button helpButton;

        [Header("헬프 패널")]
        [SerializeField] ScheduleHelpPopup helpPanel;

        // 튜토리얼 오버레이는 화면 전체 dim을 위해 ScheduleUI 외부 — Tutorial 모듈이 관리
        LoveAlgo.UI.TutorialOverlay tutorialOverlay => Services.TryGet<ITutorial>()?.Overlay;

        [Header("세션 버프 표시")]
        [SerializeField] GameObject buffIndicator;
        [SerializeField] TMP_Text buffText;

        [Header("크로스페이드 패널")]
        [Tooltip("스케줄 콘텐츠 그룹 (Schedule 프리합 내부). ShopUI는 UIManager가 별도 관리.")]
        [SerializeField] CanvasGroup scheduleContent;

        // Shop은 IShop.ShopUI 로 lazy 접근 (C3-4 — Services 경로)
        Shop.ShopUI shopPanel => Services.TryGet<IShop>()?.ShopUI;
        CanvasGroup shopContent => shopPanel != null ? shopPanel.CanvasGroup : null;

        readonly ListenerBag _listeners = new();

        /// <summary>오늘 상하차를 이미 했는지 (하루 1회 제한)</summary>
        public bool UsedLoadingToday { get; set; }

        /// <summary>이번 스케줄 세션에서 이미 하나를 선택했는지 (알바/운동/공부 통합 1회 제한)</summary>
        bool usedScheduleThisSession;

        /// <summary>현재 상점 패널이 활성화 상태인지</summary>
        bool isShopVisible;

        /// <summary>크로스페이드 진행 중 여부</summary>
        bool isCrossFading;

        /// <summary>현재 활성 탭</summary>
        ScheduleCategory activeTab;

        /// <summary>스케줄 확인 팝업 처리 중 (중복 클릭 방지)</summary>
        bool isProcessingSchedule;

        Action<ScheduleType> onScheduleSelected;

        void Awake()
        {
            // 스케줄 슬롯 콜백 설정
            foreach (var slot in scheduleSlots)
            {
                slot?.SetCallback(OnScheduleClick);
            }

            // 탭 그룹 콜백
            if (tabGroup != null)
                tabGroup.OnTabChanged += OnTabChanged;

            // 상점/폰 버튼 (행동 소비 없음)
            _listeners.Bind(shopButton, OnShopClick);
            _listeners.Bind(phoneButton, OnPhoneClick);
            _listeners.Bind(helpButton, OnHelpClick);

            // 퀵메뉴 돌아가기 — UIManager가 관리하는 공용 인스턴스
            var quickMenu = Services.TryGet<ISimulation>()?.QuickMenu;
            if (quickMenu != null)
                quickMenu.OnBackRequested += OnBackPressed;

            // 초기 상태
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            // 크로스페이드 초기 상태: 스케줄 표시, 상점 숨김
            SetPanelVisible(scheduleContent, true);
            SetPanelVisible(shopContent, false);
            isShopVisible = false;

            // 기본 탭: 알바
            tabGroup?.Select(0, notify: false);
            SwitchTab(ScheduleCategory.PartTime);
        }

        void OnEnable()
        {
            if (GameState.Instance != null)
                GameState.Instance.OnChanged += OnGameStateChanged;
        }

        void OnDisable()
        {
            if (GameState.Instance != null)
                GameState.Instance.OnChanged -= OnGameStateChanged;
        }

        /// <summary>GameState 변경 시 스탯/머니 실시간 갱신</summary>
        void OnGameStateChanged()
        {
            if (!gameObject.activeInHierarchy) return;
            RefreshInfo();
            RefreshStats();
        }

        /// <summary>
        /// UI 표시 (항상 스케줄 패널로 시작)
        /// </summary>
        public async UniTask ShowAsync(Action<ScheduleType> onSelected, CancellationToken ct = default)
        {
            onScheduleSelected = onSelected;

            // 새 스케줄 세션 시작 — 통합 1회 제한 초기화
            usedScheduleThisSession = false;

            // 항상 스케줄 패널로 리셋
            SetPanelVisible(scheduleContent, true);
            SetPanelVisible(shopContent, false);
            isShopVisible = false;

            // 기본 탭으로 리셋
            tabGroup?.Select(0, notify: false);
            SwitchTab(ScheduleCategory.PartTime);

            RefreshInfo();
            RefreshStats();

            gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            canvasGroup.DOKill();
            await canvasGroup.DOFade(1f, showDuration).SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: ct);

            // 첫 진입 시 튜토리얼 표시 (스케줄 UI 등장 후 3초 뒤)
            // GameState 플래그(현재 세션) + PlayerPrefs(영구) 둘 다 확인 — 새 게임으로 ResetAll 해도 안 뜸.
            if (tutorialOverlay != null
                && !LoveAlgo.UI.TutorialOverlay.HasSeen("HasSeenScheduleTutorial")
                && GameState.Instance != null
                && !GameState.Instance.GetFlag("HasSeenScheduleTutorial"))
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(3f), cancellationToken: ct);
                    await tutorialOverlay.RunAsync("Story/ScheduleTutorial", "HasSeenScheduleTutorial", ct);
                }
                finally
                {
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
            }
        }

        /// <summary>
        /// UI 숨기기
        /// </summary>
        public async UniTask HideAsync(CancellationToken ct = default)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            canvasGroup.DOKill();
            await canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: ct);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 하루 시작 시 제한 초기화
        /// </summary>
        public void ResetDailyLimits()
        {
            UsedLoadingToday = false;
        }

        /// <summary>
        /// 정보 갱신
        /// </summary>
        void RefreshInfo()
        {
            var gm = GameManager.Instance;
            var gs = GameState.Instance;

            if (dayText != null)
                dayText.text = gm != null ? $"{gm.CurrentDay}일차" : "1일차";

            if (moneyText != null)
                moneyText.text = gs != null ? MoneyFormat.Currency(gs.Money) : MoneyFormat.Currency(0);

            if (remainingActionsText != null)
                remainingActionsText.text = gm != null ? $"남은 행동: {gm.RemainingActions}" : "남은 행동: 0";

            // 폰 새 메시지 뱃지
            if (phoneNewBadge != null)
                phoneNewBadge.SetActive(Phone.MessengerManager.GetTotalUnreadCount() > 0);

            // 세션 버프 표시
            RefreshBuffIndicator();
        }

        /// <summary>활성 세션 버프 인디케이터 갱신</summary>
        void RefreshBuffIndicator()
        {
            var (stat, bonus) = Shop.ItemEffectSystem.PeekSessionBuff();
            bool hasBuff = stat != null && bonus > 0;

            if (buffIndicator != null)
                buffIndicator.SetActive(hasBuff);

            if (buffText != null)
                buffText.text = hasBuff ? $"{stat} +{bonus}" : "";
        }

        /// <summary>
        /// 스탯 갱신
        /// </summary>
        void RefreshStats()
        {
            var gs = GameState.Instance;
            const int max = GameConstants.MaxStat;

            if (gs != null)
            {
                strengthGauge?.SetValue(gs.GetStat("Str"), max);
                intelligenceGauge?.SetValue(gs.GetStat("Int"), max);
                socialGauge?.SetValue(gs.GetStat("Soc"), max);
                perseveranceGauge?.SetValue(gs.GetStat("Per"), max);
                fatigueGauge?.SetValue(gs.GetStat("Fatigue"), max);
            }
            else
            {
                strengthGauge?.SetValue(0, max);
                intelligenceGauge?.SetValue(0, max);
                socialGauge?.SetValue(0, max);
                perseveranceGauge?.SetValue(0, max);
                fatigueGauge?.SetValue(0, max);
            }
        }

        /// <summary>
        /// 스케줄 클릭
        /// </summary>
        void OnScheduleClick(ScheduleType type)
        {
            OnScheduleClickAsync(type, null).Forget();
        }

        /// <summary>
        /// 스케줄 클릭 (표시 이름 오버라이드 — 탭 직접 행동 모드용)
        /// </summary>
        void OnScheduleClick(ScheduleType type, string displayNameOverride)
        {
            OnScheduleClickAsync(type, displayNameOverride).Forget();
        }

        async UniTaskVoid OnScheduleClickAsync(ScheduleType type, string displayNameOverride)
        {
            if (isProcessingSchedule) return;
            isProcessingSchedule = true;

            try
            {
                var ct = destroyCancellationToken;
                var gs = GameState.Instance;

                // 상하차 하루 1회 제한
                if (type == ScheduleType.PartTime_Loading && UsedLoadingToday)
                {
                    LoveAlgo.UI.PopupManager.Instance?.Toast("제한", "상하차는 하루에 1번만 가능합니다.");
                    return;
                }

                // 이번 스케줄 세션에서 알바/운동/공부 통합 1회만 선택 가능
                if (usedScheduleThisSession)
                {
                    LoveAlgo.UI.PopupManager.Instance?.Toast("제한", "이번 스케줄에서는 이미 하나를 선택했습니다.");
                    return;
                }

                // 투자 조건: 자산 ≥ 30,000원
                if (type == ScheduleType.Invest && (gs == null || gs.Money < 30000))
                {
                    LoveAlgo.UI.PopupManager.Instance?.Toast("자산 부족", $"투자는 자산 {MoneyFormat.Currency(30000)} 이상일 때 가능합니다.");
                    return;
                }

                var effect = ScheduleTable.Get(type);
                string statName = GetPrimaryStatName(effect);
                string displayName = string.IsNullOrEmpty(displayNameOverride) ? effect.displayName : displayNameOverride;
                string message = $"{statName} 스탯이 증가합니다.\n{displayName}을 진행하시겠습니까?";
                string effectText = BuildEffectText(type, effect);

                // 기획서: dim + 확인 팝업
                var confirmed = await LoveAlgo.UI.PopupManager.Instance.ConfirmAsync(
                    new LoveAlgo.UI.ConfirmPopupData { mainText = message, sub1 = effectText }
                );

                if (confirmed)
                {
                    // 상하차 사용 기록
                    if (type == ScheduleType.PartTime_Loading)
                        UsedLoadingToday = true;

                    // 이번 스케줄 세션 사용 기록 (알바/운동/공부 통합 1회)
                    usedScheduleThisSession = true;

                    // 스탯 적용 (DayLoopController에서 행동 소모 + EndDay 자동 처리)
                    onScheduleSelected?.Invoke(type);

                    // 남은 행동이 0이면 인터랙션 비활성 (EndDay 전환 대기)
                    // 단, 인라인 모드에서는 뒤로가기를 눌러야 하므로 비활성화하지 않음
                    var gm = GameManager.Instance;
                    if (gm != null && gm.RemainingActions <= 0
                        && !gm.DayLoop.IsInlineSchedule)
                    {
                        canvasGroup.interactable = false;
                        canvasGroup.blocksRaycasts = false;
                    }
                }
            }
            finally
            {
                isProcessingSchedule = false;
            }
        }

        /// <summary>
        /// 주요 스탯 이름 (팝업 메인 텍스트용)
        /// </summary>
        string GetPrimaryStatName(ScheduleEffect effect)
        {
            if (effect.strengthChange > 0)     return "체력";
            if (effect.intelligenceChange > 0) return "지성";
            if (effect.socialChange > 0)       return "사교성";
            if (effect.perseveranceChange > 0) return "끈기";
            return effect.displayName;
        }

        /// <summary>
        /// 효과 텍스트 생성 (간략: 스탯명 + 변화량만)
        /// </summary>
        string BuildEffectText(ScheduleType type, ScheduleEffect effect)
        {
            if (type == ScheduleType.Invest)
            {
                var gs = GameState.Instance;
                int currentMoney = gs?.Money ?? 0;
                return $"자산 {MoneyFormat.Currency(currentMoney)} / 결과 -50% ~ +100%";
            }

            var parts = new List<string>();

            if (effect.strengthChange != 0)     parts.Add($"체력 {FormatStat(effect.strengthChange)}");
            if (effect.intelligenceChange != 0) parts.Add($"지성 {FormatStat(effect.intelligenceChange)}");
            if (effect.socialChange != 0)       parts.Add($"사교성 {FormatStat(effect.socialChange)}");
            if (effect.perseveranceChange != 0) parts.Add($"끈기 {FormatStat(effect.perseveranceChange)}");

            return string.Join(" / ", parts);
        }

        string FormatStat(int value) => value > 0 ? $"+{value}" : value.ToString();

        #region 탭 전환

        static readonly ScheduleCategory[] TabCategories =
        {
            ScheduleCategory.PartTime,
            ScheduleCategory.Exercise,
            ScheduleCategory.Study,
        };

        void OnTabChanged(int index)
        {
            if (index < 0 || index >= TabCategories.Length) return;
            var category = TabCategories[index];

            // 직접 행동 모드: 슬롯 컨테이너 표시 대신 행동 팝업 즉시 실행
            // 탭 시각 선택은 알바(0)로 되돌리고 PartTime 컨테이너 유지
            if (TryGetDirectAction(category, out var directType))
            {
                tabGroup?.Select(0, notify: false);
                SwitchTab(ScheduleCategory.PartTime);
                // 카테고리 이름("운동"/"공부")으로 팝업 표시 (예: "운동을 진행하시겠습니까?")
                OnScheduleClick(directType, ScheduleTable.GetCategoryName(category));
                return;
            }

            SwitchTab(category);
        }

        /// <summary>탭 전환 — 해당 카테고리 컨테이너만 표시</summary>
        void SwitchTab(ScheduleCategory category)
        {
            activeTab = category;

            if (containerPartTime != null) containerPartTime.SetActive(category == ScheduleCategory.PartTime);
            if (containerExercise != null) containerExercise.SetActive(category == ScheduleCategory.Exercise);
            if (containerStudy != null)    containerStudy.SetActive(category == ScheduleCategory.Study);

            if (categoryDescText != null)
                categoryDescText.text = ScheduleTable.GetCategoryDescription(category);
        }

        /// <summary>해당 카테고리가 "직접 행동 모드"로 설정됐는지</summary>
        bool TryGetDirectAction(ScheduleCategory category, out ScheduleType type)
        {
            switch (category)
            {
                case ScheduleCategory.Exercise when exerciseAsDirectAction:
                    type = exerciseDirectAction;
                    return true;
                case ScheduleCategory.Study when studyAsDirectAction:
                    type = studyDirectAction;
                    return true;
                default:
                    type = default;
                    return false;
            }
        }

        #endregion

        #region 상점/선물

        /// <summary>상점 열기 — 크로스페이드로 상점 패널 전환</summary>
        void OnShopClick()
        {
            if (isShopVisible || isCrossFading) return;
            CrossFadeToShopAsync().Forget();
        }

        /// <summary>외부에서 상점 패널을 직접 열 때 사용 (테스트 등)</summary>
        public void OpenShop()
        {
            OnShopClick();
        }

        /// <summary>퀵메뉴 돌아가기: 상점이면 스케줄 복귀, 스케줄이면 스토리 진행 확인</summary>
        void OnBackPressed()
        {
            if (isCrossFading) return;

            if (isShopVisible)
            {
                // 상점 → 스케줄 복귀
                CrossFadeToScheduleAsync().Forget();
            }
            else
            {
                // 스케줄 → 스토리 진행 확인
                OnBackToStoryAsync().Forget();
            }
        }

        async UniTaskVoid OnBackToStoryAsync()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // 인라인 스케줄 모드: 확인 후 스토리 복귀 (페이드는 ScheduleFlowCommand에서 처리)
            if (gm.DayLoop.IsInlineSchedule)
            {
                bool proceed = await LoveAlgo.UI.PopupManager.Instance.ConfirmAsync(
                    "일정을 진행하지 않고 돌아가시겠습니까?");
                if (!proceed) return;

                gm.DayLoop.CompleteInlineSchedule();
                return;
            }

            // 데모 종료 조건: 확인 후 타이틀로
            if (gm.ShouldReturnToDemoEnd())
            {
                bool confirmed = await LoveAlgo.UI.PopupManager.Instance.ConfirmAsync(
                    "데모 플레이가 종료되었습니다.\n타이틀로 돌아가시겠습니까?");
                if (!confirmed) return;

                await HideAsync(destroyCancellationToken);
                gm.OnContentEnd();
                return;
            }

            bool proceedStory = await LoveAlgo.UI.PopupManager.Instance.ConfirmAsync(
                "일정을 진행하지 않고 돌아가시겠습니까?");
            if (!proceedStory) return;
            gm.RemainingActions = 0;
            await HideAsync(destroyCancellationToken);
            gm.OnScheduleCompleted();
        }

        /// <summary>상점에서 뒤로가기 — 크로스페이드로 스케줄 패널 복귀</summary>
        void OnShopBack()
        {
            if (!isShopVisible || isCrossFading) return;
            CrossFadeToScheduleAsync().Forget();
        }

        /// <summary>스케줄 → 상점 크로스페이드</summary>
        async UniTaskVoid CrossFadeToShopAsync()
        {
            var ct = destroyCancellationToken;
            isCrossFading = true;

            // 상점 데이터 초기화
            shopPanel?.Open();

            // 상점 패널 활성화 (alpha 0에서 시작)
            if (shopContent != null)
            {
                shopContent.gameObject.SetActive(true);
                shopContent.alpha = 0f;
            }

            // 동시 페이드: 스케줄 out + 상점 in
            var seq = DOTween.Sequence();
            if (scheduleContent != null)
            {
                seq.Join(scheduleContent.DOFade(0f, crossFadeDuration).SetEase(Ease.OutQuad));
            }
            if (shopContent != null)
            {
                seq.Join(shopContent.DOFade(1f, crossFadeDuration).SetEase(Ease.OutQuad));
            }
            seq.SetUpdate(true);

            await seq.ToUniTask(cancellationToken: ct);

            // 스케줄 패널 비활성화
            SetPanelVisible(scheduleContent, false);
            SetPanelVisible(shopContent, true);
            isShopVisible = true;
            isCrossFading = false;
        }

        /// <summary>상점 → 스케줄 크로스페이드</summary>
        async UniTaskVoid CrossFadeToScheduleAsync()
        {
            var ct = destroyCancellationToken;
            isCrossFading = true;

            // 구매 후 정보 갱신
            RefreshInfo();
            RefreshStats();

            // 스케줄 패널 활성화 (alpha 0에서 시작)
            if (scheduleContent != null)
            {
                scheduleContent.gameObject.SetActive(true);
                scheduleContent.alpha = 0f;
            }

            // 동시 페이드: 상점 out + 스케줄 in
            var seq = DOTween.Sequence();
            if (shopContent != null)
            {
                seq.Join(shopContent.DOFade(0f, crossFadeDuration).SetEase(Ease.OutQuad));
            }
            if (scheduleContent != null)
            {
                seq.Join(scheduleContent.DOFade(1f, crossFadeDuration).SetEase(Ease.OutQuad));
            }
            seq.SetUpdate(true);

            await seq.ToUniTask(cancellationToken: ct);

            // 상점 패널 비활성화
            SetPanelVisible(shopContent, false);
            SetPanelVisible(scheduleContent, true);
            isShopVisible = false;
            isCrossFading = false;
        }

        /// <summary>CanvasGroup 패널 즉시 표시/숨김</summary>
        static void SetPanelVisible(CanvasGroup cg, bool visible)
        {
            if (cg == null) return;
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
            cg.gameObject.SetActive(visible);
        }

        /// <summary>폰 열기 (행동 소비 없음)</summary>
        void OnPhoneClick()
        {
            LoveAlgo.Common.Services.TryGet<LoveAlgo.Contracts.IPhone>()?.ShowPhoneUI();
        }

        #endregion

        void OnDestroy()
        {
            _listeners.Dispose();
            if (tabGroup != null) tabGroup.OnTabChanged -= OnTabChanged;
            if (canvasGroup != null)
                canvasGroup.DOKill();
            if (scheduleContent != null)
                scheduleContent.DOKill();
            if (shopContent != null)
                shopContent.DOKill();

            var quickMenu = Services.TryGet<ISimulation>()?.QuickMenu;
            if (quickMenu != null)
                quickMenu.OnBackRequested -= OnBackPressed;
        }

        void OnHelpClick() => helpPanel?.Open();
    }
}
