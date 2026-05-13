using System;
using LoveAlgo.Common;
using TMPro;
using UnityEngine;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// PC잠금 시계 위젯.
    /// 기획서 §구성: 시간 고정 (실제 시간 X). 게임 인게임 시간과 별개로 스크립트별 표기.
    /// 모드: Auto(ILockScreen 위임) / Real(OS) / Fixed(인스펙터)
    /// </summary>
    public class ClockWidget : MonoBehaviour
    {
        public enum ClockMode { Auto, Real, Fixed }

        [Header("Refs")]
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text dateText;

        [Header("Mode")]
        [Tooltip("Auto = ILockScreen에 위임 (권장). Real = 실시간. Fixed = 아래 텍스트 그대로.")]
        [SerializeField] ClockMode mode = ClockMode.Auto;

        [Tooltip("Fixed 모드 시 표시할 시각 (예: \"23:58\")")]
        [SerializeField] string fixedTimeOverride = "23:58";

        [Header("Real-time Format")]
        [SerializeField] string timeFormat = "HH:mm";
        [Tooltip("빈 문자열이면 날짜 미표시")]
        [SerializeField] string dateFormat = "";

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

        public void Refresh()
        {
            if (timeText != null) timeText.text = ResolveTime();
            if (dateText != null)
            {
                if (mode == ClockMode.Real && !string.IsNullOrEmpty(dateFormat))
                    dateText.text = DateTime.Now.ToString(dateFormat);
                else
                    dateText.text = "";
            }
        }

        string ResolveTime()
        {
            switch (mode)
            {
                case ClockMode.Real:
                    return DateTime.Now.ToString(timeFormat);
                case ClockMode.Fixed:
                    return fixedTimeOverride ?? "";
                case ClockMode.Auto:
                default:
                    var ls = Services.TryGet<ILockScreen>();
                    if (ls != null) return ls.GetClockTime();
                    return DateTime.Now.ToString(timeFormat);
            }
        }

        /// <summary>외부에서 1회 시각 오버라이드. 모드를 Fixed로 강제.</summary>
        public void SetFixedTime(string hhmm)
        {
            mode = ClockMode.Fixed;
            fixedTimeOverride = hhmm ?? "";
            Refresh();
        }
    }
}
