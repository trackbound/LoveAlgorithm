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
    /// 스케줄 선택 UI (단순화 버전)
    /// 탭 3개, 각 탭당 버튼 3개 = 총 9개 고정
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

        [Header("오른쪽 패널 - 탭")]
        [SerializeField] Button partTimeTab;
        [SerializeField] Button exerciseTab;
        [SerializeField] Button studyTab;

        [Header("오른쪽 패널 - 컨텐츠 패널")]
        [SerializeField] GameObject partTimePanel;
        [SerializeField] GameObject exercisePanel;
        [SerializeField] GameObject studyPanel;

        [Header("스케줄 슬롯")]
        [SerializeField] ScheduleSlot[] scheduleSlots;

        [Header("카테고리 설명")]
        [SerializeField] TMP_Text categoryDescText;

        readonly Dictionary<ScheduleCategory, string> categoryDesc = new()
        {
            { ScheduleCategory.PartTime, "돈을 벌 수 있어요. 피로도가 오릅니다." },
            { ScheduleCategory.Exercise, "체력과 끈기를 키울 수 있어요." },
            { ScheduleCategory.Study, "지성을 높일 수 있어요." }
        };

        ScheduleCategory currentCategory = ScheduleCategory.PartTime;
        Action<ScheduleType> onScheduleSelected;

        void Awake()
        {
            // 탭 버튼
            partTimeTab?.onClick.AddListener(() => SwitchCategory(ScheduleCategory.PartTime));
            exerciseTab?.onClick.AddListener(() => SwitchCategory(ScheduleCategory.Exercise));
            studyTab?.onClick.AddListener(() => SwitchCategory(ScheduleCategory.Study));

            // 스케줄 슬롯 콜백 설정
            foreach (var slot in scheduleSlots)
            {
                slot?.SetCallback(OnScheduleClick);
            }

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
            SwitchCategory(ScheduleCategory.PartTime);

            gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            await canvasGroup.DOFade(1f, showDuration).SetEase(Ease.OutQuad).AsyncWaitForCompletion();
        }

        /// <summary>
        /// UI 숨기기
        /// </summary>
        public async UniTask HideAsync()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            await canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.OutQuad).AsyncWaitForCompletion();
            gameObject.SetActive(false);
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
        }

        /// <summary>
        /// 스탯 갱신
        /// </summary>
        void RefreshStats()
        {
            var gs = GameState.Instance;
            const int max = 100;
            
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
        /// 카테고리 전환
        /// </summary>
        void SwitchCategory(ScheduleCategory category)
        {
            currentCategory = category;

            // 패널 on/off
            partTimePanel?.SetActive(category == ScheduleCategory.PartTime);
            exercisePanel?.SetActive(category == ScheduleCategory.Exercise);
            studyPanel?.SetActive(category == ScheduleCategory.Study);

            // 설명 텍스트
            if (categoryDescText != null && categoryDesc.TryGetValue(category, out var desc))
                categoryDescText.text = desc;
        }

        /// <summary>
        /// 스케줄 클릭
        /// </summary>
        async void OnScheduleClick(ScheduleType type)
        {
            var effect = ScheduleTable.Get(type);
            string effectText = BuildEffectText(effect);

            // 확인 팝업
            var confirmed = await LoveAlgo.UI.PopupManager.Instance.ScheduleConfirmAsync(
                $"'{effect.displayName}'를 선택하시겠습니까?",
                effectText
            );

            if (confirmed)
            {
                onScheduleSelected?.Invoke(type);
                await HideAsync();
            }
        }

        /// <summary>
        /// 효과 텍스트 생성
        /// </summary>
        string BuildEffectText(ScheduleEffect effect)
        {
            var lines = new List<string>();

            if (effect.moneyChange > 0)
                lines.Add($"금액: +{effect.moneyChange:N0}원");
            else if (effect.moneyChange < 0)
                lines.Add($"금액: {effect.moneyChange:N0}원");

            if (effect.strengthChange != 0)
                lines.Add($"체력: {FormatStat(effect.strengthChange)}");
            if (effect.intelligenceChange != 0)
                lines.Add($"지성: {FormatStat(effect.intelligenceChange)}");
            if (effect.socialChange != 0)
                lines.Add($"사교성: {FormatStat(effect.socialChange)}");
            if (effect.perseveranceChange != 0)
                lines.Add($"끈기: {FormatStat(effect.perseveranceChange)}");
            if (effect.fatigueChange != 0)
                lines.Add($"피로: {FormatStat(effect.fatigueChange)}");

            return string.Join("\n", lines);
        }

        string FormatStat(int value) => value > 0 ? $"+{value}" : value.ToString();
    }
}
