using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 무대 동기화·점프 시스템 공통 로그 헬퍼.
    /// PlayerPrefs("StageSync.Verbose") 토글로 상세 로그 on/off.
    ///
    /// 사용:
    ///   StageSyncLog.Info("F5 NextDay", "phase=DayLoop day=1→2");        // 항상 출력
    ///   StageSyncLog.Detail("StageSync", "L142 BG: school → cafe");      // verbose ON일 때만
    ///   StageSyncLog.Section("QuickLoad", "Stop → Cleanup");             // 단계 구분자
    /// </summary>
    public static class StageSyncLog
    {
        const string PrefKey = "StageSync.Verbose";

        /// <summary>verbose 모드 (PlayerPrefs 기반). 매 호출마다 읽음 — 토글 즉시 반영.</summary>
        public static bool Verbose
        {
            get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>verbose 토글.</summary>
        public static void ToggleVerbose() => Verbose = !Verbose;

        /// <summary>요약 로그 — 항상 출력.</summary>
        public static void Info(string scope, string msg)
            => Debug.Log($"[{scope}] {msg}");

        /// <summary>상세 로그 — verbose ON일 때만.</summary>
        public static void Detail(string scope, string msg)
        {
            if (Verbose) Debug.Log($"[{scope}] {msg}");
        }

        /// <summary>단계 구분자 — verbose 가독성 향상.</summary>
        public static void Section(string scope, string title)
        {
            if (Verbose) Debug.Log($"[{scope}] ▸ {title}");
        }

        /// <summary>경고 — 항상 출력.</summary>
        public static void Warn(string scope, string msg)
            => Debug.LogWarning($"[{scope}] {msg}");
    }
}
