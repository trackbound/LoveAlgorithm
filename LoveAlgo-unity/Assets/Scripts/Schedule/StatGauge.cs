using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스탯 게이지 UI — 평행사변형 그라데이션 스프라이트를 anchorMax.x로 리사이즈
    /// fillAmount 대신 너비 조절하여 대각선 끝 형태를 항상 유지
    /// </summary>
    public class StatGauge : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] Image fillImage;
        [SerializeField] TMP_Text valueText;
        [SerializeField] TMP_Text labelText;

        [Header("애니메이션")]
        [SerializeField] float animDuration = 0.3f;

        float currentValue;
        float maxValue = 100f;
        bool initialized;

        void EnsureInit()
        {
            if (initialized || fillImage == null) return;
            initialized = true;

            // Filled → Simple 모드로 전환 (스프라이트 형태 유지)
            fillImage.type = Image.Type.Simple;

            // X축 오프셋 정리 (anchorMax.x만으로 너비 제어)
            var rt = fillImage.rectTransform;
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }

        /// <summary>
        /// 값 설정
        /// </summary>
        public void SetValue(float value, float max = 100f)
        {
            maxValue = max;
            currentValue = Mathf.Clamp(value, 0f, max);
            float ratio = currentValue / maxValue;

            EnsureInit();

            if (fillImage != null)
            {
                var rt = fillImage.rectTransform;
                DOTween.Kill(rt);
                DOTween.To(
                    () => rt.anchorMax.x,
                    x => rt.anchorMax = new Vector2(x, rt.anchorMax.y),
                    ratio,
                    animDuration
                ).SetTarget(rt).SetEase(Ease.OutQuad);
            }

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

            EnsureInit();

            if (fillImage != null)
            {
                var rt = fillImage.rectTransform;
                DOTween.Kill(rt);
                rt.anchorMax = new Vector2(ratio, rt.anchorMax.y);
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
