using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 슬롯 - 각 스케줄 버튼에 부착
    /// </summary>
    public class ScheduleSlot : MonoBehaviour
    {
        [Header("스케줄 타입")]
        [SerializeField] ScheduleType scheduleType;

        [Header("UI 바인딩")]
        [SerializeField] Button button;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text effectText;

        public ScheduleType Type => scheduleType;

        Action<ScheduleType> onClick;

        void Awake()
        {
            button?.onClick.AddListener(() => onClick?.Invoke(scheduleType));
            RefreshDisplay();
        }

        /// <summary>
        /// 클릭 콜백 설정
        /// </summary>
        public void SetCallback(Action<ScheduleType> callback)
        {
            onClick = callback;
        }

        /// <summary>
        /// 표시 갱신
        /// </summary>
        public void RefreshDisplay()
        {
            var effect = ScheduleTable.Get(scheduleType);

            if (nameText != null)
                nameText.text = effect.displayName;

            if (effectText != null)
                effectText.text = effect.description;
        }

        /// <summary>
        /// 효과 텍스트 생성 (간략)
        /// </summary>
        string BuildEffectText(ScheduleEffect effect)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (effect.moneyChange > 0)
                parts.Add($"<color=#FFD700>{MoneyFormat.SignedCurrency(effect.moneyChange)}</color>");
            else if (effect.moneyChange < 0)
                parts.Add($"<color=#FF6B6B>{MoneyFormat.SignedCurrency(effect.moneyChange)}</color>");

            if (effect.strengthChange > 0)
                parts.Add($"<color=#4CAF50>체력+{effect.strengthChange}</color>");
            if (effect.intelligenceChange > 0)
                parts.Add($"<color=#4CAF50>지성+{effect.intelligenceChange}</color>");
            if (effect.socialChange > 0)
                parts.Add($"<color=#4CAF50>사교+{effect.socialChange}</color>");
            if (effect.perseveranceChange > 0)
                parts.Add($"<color=#4CAF50>끈기+{effect.perseveranceChange}</color>");
            if (effect.fatigueChange > 0)
                parts.Add($"<color=#FF6B6B>피로+{effect.fatigueChange}</color>");

            return string.Join(" ", parts);
        }
    }
}
