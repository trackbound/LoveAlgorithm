using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core;
using LoveAlgo.Story;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 선택 UI
    /// 기획서 기준: 운동 / 공부 / 알바(편의점·상하차) / 투자 / 아이템 구매
    /// 탭 없이 전체 메뉴를 한 화면에 표시
    /// </summary>
    public class ScheduleUI : MonoBehaviour
    {
        [Header("애니메이션")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] float showDuration = 0.3f;
        [SerializeField] float hideDuration = 0.2f;

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

        [Header("스케줄 슬롯")]
        [SerializeField] ScheduleSlot[] scheduleSlots;

        [Header("하단 버튼 (행동 소비 없음)")]
        [SerializeField] Button shopButton;
        [SerializeField] Button giftButton;
        [SerializeField] Button phoneButton;
        [SerializeField] GameObject phoneNewBadge;

        /// <summary>오늘 상하차를 이미 했는지 (하루 1회 제한)</summary>
        bool usedLoadingToday;

        Action<ScheduleType> onScheduleSelected;

        void Awake()
        {
            // 스케줄 슬롯 콜백 설정
            foreach (var slot in scheduleSlots)
            {
                slot?.SetCallback(OnScheduleClick);
            }

            // 상점/선물/폰 버튼 (행동 소비 없음)
            if (shopButton != null)
                shopButton.onClick.AddListener(OnShopClick);
            if (giftButton != null)
                giftButton.onClick.AddListener(OnGiftClick);
            if (phoneButton != null)
                phoneButton.onClick.AddListener(OnPhoneClick);

            // 초기 상태
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// UI 표시
        /// </summary>
        public async UniTask ShowAsync(Action<ScheduleType> onSelected)
        {
            onScheduleSelected = onSelected;

            RefreshInfo();
            RefreshStats();

            gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            await canvasGroup.DOFade(1f, showDuration).SetEase(Ease.OutQuad).ToUniTask();
        }

        /// <summary>
        /// UI 숨기기
        /// </summary>
        public async UniTask HideAsync()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            await canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.OutQuad).ToUniTask();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 하루 시작 시 제한 초기화
        /// </summary>
        public void ResetDailyLimits()
        {
            usedLoadingToday = false;
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
                moneyText.text = gs != null ? $"{gs.Money:N0}원" : "0원";

            if (remainingActionsText != null)
                remainingActionsText.text = gm != null ? $"남은 행동: {gm.RemainingActions}" : "남은 행동: 0";

            // 폰 새 메시지 뱃지
            if (phoneNewBadge != null)
                phoneNewBadge.SetActive(Phone.MessengerManager.GetTotalUnreadCount() > 0);
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
            var gs = GameState.Instance;

            // 상하차 하루 1회 제한
            if (type == ScheduleType.PartTime_Loading && usedLoadingToday)
            {
                LoveAlgo.UI.PopupManager.Instance?.Toast("제한", "상하차는 하루에 1번만 가능합니다.");
                return;
            }

            // 투자 조건: 자산 ≥ 30,000원
            if (type == ScheduleType.Invest && (gs == null || gs.Money < 30000))
            {
                LoveAlgo.UI.PopupManager.Instance?.Toast("자산 부족", "투자는 자산 30,000원 이상일 때 가능합니다.");
                return;
            }

            var effect = ScheduleTable.Get(type);
            string effectText = BuildEffectText(type, effect);

            // 기획서: dim + 확인 팝업
            var confirmed = await LoveAlgo.UI.PopupManager.Instance.ScheduleConfirmAsync(
                $"{effect.displayName}",
                effectText
            );

            if (confirmed)
            {
                // 상하차 사용 기록
                if (type == ScheduleType.PartTime_Loading)
                    usedLoadingToday = true;

                onScheduleSelected?.Invoke(type);
                await HideAsync();
            }
        }

        /// <summary>
        /// 효과 텍스트 생성 (기획서: 팝업에 스탯/효과 표시)
        /// </summary>
        string BuildEffectText(ScheduleType type, ScheduleEffect effect)
        {
            // 투자는 별도 표시
            if (type == ScheduleType.Invest)
            {
                var gs = GameState.Instance;
                int currentMoney = gs?.Money ?? 0;
                return $"현재 자산: {currentMoney:N0}원\n결과: ±50~100% (랜덤)";
            }

            var lines = new List<string>();

            if (effect.strengthChange != 0)
                lines.Add($"체력 {FormatStat(effect.strengthChange)}");
            if (effect.intelligenceChange != 0)
                lines.Add($"지성 {FormatStat(effect.intelligenceChange)}");
            if (effect.socialChange != 0)
                lines.Add($"사교성 {FormatStat(effect.socialChange)}");
            if (effect.perseveranceChange != 0)
                lines.Add($"끈기 {FormatStat(effect.perseveranceChange)}");
            if (effect.fatigueChange != 0)
                lines.Add($"피로 {FormatStat(effect.fatigueChange)}");
            if (effect.moneyChange > 0)
                lines.Add($"+{effect.moneyChange:N0}원");
            else if (effect.moneyChange < 0)
                lines.Add($"{effect.moneyChange:N0}원");

            return string.Join("\n", lines);
        }

        string FormatStat(int value) => value > 0 ? $"+ {value}" : value.ToString();

        #region 상점/선물

        /// <summary>상점 열기 (행동 소비 없음)</summary>
        void OnShopClick()
        {
            LoveAlgo.UI.PopupManager.Instance?.ShowModal<Shop.ShopPopup>();
        }

        /// <summary>선물 주기 (행동 소비 없음)</summary>
        void OnGiftClick()
        {
            if (Shop.ShopManager.IsInventoryEmpty())
            {
                LoveAlgo.UI.PopupManager.Instance?.Toast("인벤토리 비어있음", "선물할 아이템이 없습니다.\n상점에서 먼저 구매해주세요.");
                return;
            }
            LoveAlgo.UI.PopupManager.Instance?.ShowModal<Shop.GiftPopup>();
        }

        /// <summary>폰 열기 (행동 소비 없음)</summary>
        void OnPhoneClick()
        {
            LoveAlgo.UI.PopupManager.Instance?.ShowModal<Phone.PhonePanel>();
        }

        #endregion
    }
}
