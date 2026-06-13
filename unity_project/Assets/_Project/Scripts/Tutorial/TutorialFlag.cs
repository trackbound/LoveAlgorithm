using UnityEngine;

namespace LoveAlgo.Tutorial
{
    /// <summary>
    /// 튜토리얼 1회 완료 기록(설치 단위, PlayerPrefs — FirstLaunchFlag 선례). 세이브와 무관:
    /// 기획서 "새 게임 시작 또는 두 번째로 플레이할때부터는 출력 X" = 기기당 최초 1회.
    /// </summary>
    public static class TutorialFlag
    {
        /// <summary>완료 여부. 빈 키 = 항상 미완료 취급(데브 시퀀스 반복 재생).</summary>
        public static bool IsDone(string prefsKey)
            => !string.IsNullOrEmpty(prefsKey) && PlayerPrefs.GetInt(prefsKey, 0) == 1;

        public static void MarkDone(string prefsKey)
        {
            if (string.IsNullOrEmpty(prefsKey)) return;
            PlayerPrefs.SetInt(prefsKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>기록 삭제(재테스트용 — 에디터 메뉴/데브에서 호출).</summary>
        public static void Reset(string prefsKey)
        {
            if (string.IsNullOrEmpty(prefsKey)) return;
            PlayerPrefs.DeleteKey(prefsKey);
            PlayerPrefs.Save();
        }
    }
}
