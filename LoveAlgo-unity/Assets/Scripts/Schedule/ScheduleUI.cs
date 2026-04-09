using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        [SerializeField] ScheduleHelpPanel helpPanel;

        [Header("세션 버프 표시")]
        [SerializeField] GameObject buffIndicator;
        [SerializeField] TMP_Text buffText;

        [Header("크로스페이드 패널")]
        [SerializeField] CanvasGroup scheduleContent;
        [SerializeField] CanvasGroup shopContent;
        [SerializeField] Shop.ShopPopup shopPanel;

        /// <summary>오늘 상하차를 이미 했는지 (하루 1회 제한)</summary>
        public bool UsedLoadingToday { get; set; }

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
            if (shopButton != null)
                shopButton.onClick.AddListener(OnShopClick);
            if (phoneButton != null)
                phoneButton.onClick.AddListener(OnPhoneClick);
            if (helpButton != null)
                helpButton.onClick.AddListener(() => helpPanel?.Open());

            // 퀵메뉴 돌아가기 (QuickMenu는 ScheduleUI의 형제 — 부모(Simulate)에서 검색)
            var quickMenu = transform.parent?.GetComponentInChildren<LoveAlgo.UI.QuickMenuUI>(true);
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
            OnScheduleClickAsync(type).Forget();
        }

        async UniTaskVoid OnScheduleClickAsync(ScheduleType type)
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

                // 투자 조건: 자산 ≥ 30,000원
                if (type == ScheduleType.Invest && (gs == null || gs.Money < 30000))
                {
                    LoveAlgo.UI.PopupManager.Instance?.Toast("자산 부족", $"투자는 자산 {MoneyFormat.Currency(30000)} 이상일 때 가능합니다.");
                    return;
                }

                var effect = ScheduleTable.Get(type);
                string statName = GetPrimaryStatName(effect);
                string message = $"{statName} 스탯이 증가합니다.\n{effect.displayName}을 진행하시겠습니까?";
                string effectText = BuildEffectText(type, effect);

                // 기획서: dim + 확인 팝업
                var confirmed = await LoveAlgo.UI.PopupManager.Instance.ConfirmAsync(
                    "Schedule",
                    new LoveAlgo.UI.ConfirmPopupData { mainText = message, sub1 = effectText }
                );

                if (confirmed)
                {
                    // 상하차 사용 기록
                    if (type == ScheduleType.PartTime_Loading)
                        UsedLoadingToday = true;

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
            if (index >= 0 && index < TabCategories.Length)
                SwitchTab(TabCategories[index]);
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

            // 인라인 스케줄 모드: 확인 후 스토리 복귀
            if (gm.DayLoop.IsInlineSchedule)
            {
                bool proceed = await LoveAlgo.UI.PopupManager.Instance.ConfirmAsync(
                    "스토리를 진행하시겠습니까?");
                if (!proceed) return;

                await HideAsync(destroyCancellationToken);
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
                "스토리를 진행하시겠습니까?");
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
            LoveAlgo.UI.PopupManager.Instance?.ShowModal<Phone.PhonePanel>();
        }

        #endregion

        void OnDestroy()
        {
            if (canvasGroup != null)
                canvasGroup.DOKill();
            if (scheduleContent != null)
                scheduleContent.DOKill();
            if (shopContent != null)
                shopContent.DOKill();

            var quickMenu = transform.parent?.GetComponentInChildren<LoveAlgo.UI.QuickMenuUI>(true);
            if (quickMenu != null)
                quickMenu.OnBackRequested -= OnBackPressed;
        }
    }
}
