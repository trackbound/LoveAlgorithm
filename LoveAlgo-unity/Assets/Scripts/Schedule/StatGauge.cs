using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스탯 게이지 UI
    /// </summary>
    public class StatGauge : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] Image fillImage;
        [SerializeField] TMP_Text valueText;
        [SerializeField] TMP_Text labelText;

        [Header("색상")]
        [SerializeField] Gradient fillGradient;

        [Header("애니메이션")]
        [SerializeField] float animDuration = 0.3f;

        float currentValue;
        float maxValue = 100f;

        /// <summary>
        /// 값 설정
        /// </summary>
        public void SetValue(float value, float max = 100f)
        {
            maxValue = max;
            currentValue = Mathf.Clamp(value, 0f, max);

            float ratio = currentValue / maxValue;

            // 게이지 채우기
            if (fillImage != null)
            {
                fillImage.DOFillAmount(ratio, animDuration).SetEase(Ease.OutQuad);

                // 그라데이션 색상
                if (fillGradient != null)
                    fillImage.color = fillGradient.Evaluate(ratio);
            }

            // 텍스트
            if (valueText != null)
                valueText.text = $"{Mathf.RoundToInt(currentValue)}";
        }

        /// <summary>
        /// 즉시 값 설정 (애니메이션 없음)
        /// </summary>
        public void SetValueImmediate(float value, float max = 100f)
        {
            maxValue = max;
            currentValue = Mathf.Clamp(value, 0f, max);

            float ratio = currentValue / maxValue;

            if (fillImage != null)
            {
                fillImage.fillAmount = ratio;

                if (fillGradient != null)
                    fillImage.color = fillGradient.Evaluate(ratio);
            }

            if (valueText != null)
                valueText.text = $"{Mathf.RoundToInt(currentValue)}";
        }

        /// <summary>
        /// 라벨 설정
        /// </summary>
        public void SetLabel(string label)
        {
            if (labelText != null)
                labelText.text = label;
        }

        /// <summary>
        /// 현재 값 반환
        /// </summary>
        public float GetValue() => currentValue;

        /// <summary>
        /// 비율 반환 (0~1)
        /// </summary>
        public float GetRatio() => currentValue / maxValue;
    }
}
