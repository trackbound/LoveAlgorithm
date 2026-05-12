using System;
using TMPro;
using UnityEngine;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// PC잠금 화면 시계 위젯.
    /// 실시간 시각(시:분) + 날짜를 갱신. (게임 인게임 날짜 아닌 실제 OS 시각)
    /// </summary>
    public class ClockWidget : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text dateText;

        [Header("Format")]
        [Tooltip("시각 포맷 (TMP 기본 string.Format)")]
        [SerializeField] string timeFormat = "HH:mm";

        [Tooltip("날짜 포맷")]
        [SerializeField] string dateFormat = "yyyy.MM.dd (ddd)";

        float nextRefreshAt;

        void OnEnable()
        {
            Refresh();
            nextRefreshAt = Time.unscaledTime + 1f;
        }

        void Update()
        {
            if (Time.unscaledTime >= nextRefreshAt)
            {
                Refresh();
                nextRefreshAt = Time.unscaledTime + 1f;
            }
        }

        void Refresh()
        {
            var now = DateTime.Now;
            if (timeText != null) timeText.text = now.ToString(timeFormat);
            if (dateText != null) dateText.text = now.ToString(dateFormat);
        }
    }
}
