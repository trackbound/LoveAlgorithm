using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 최초 실행(설치 후 첫 구동) 여부 플래그. PlayerPrefs에 1회성으로 기록 — 첫 구동 때만 인트로 오버레이를 띄우고
    /// 이후 구동은 타이틀로 직행하기 위한 판정. 세이브와 무관(세이브를 지워도 유지). <see cref="FirstLaunchBootstrap"/>이
    /// 읽고 기록한다. 얇은 PlayerPrefs 어댑터 — 키 하나.
    /// </summary>
    public static class FirstLaunchFlag
    {
        public const string Key = "LoveAlgo.FirstLaunch.Seen";

        /// <summary>이전에 첫실행 인트로를 표시한 적이 있으면 true.</summary>
        public static bool Seen => PlayerPrefs.GetInt(Key, 0) != 0;

        /// <summary>첫실행 인트로를 표시했음을 영구 기록(이후 구동은 타이틀로).</summary>
        public static void MarkSeen()
        {
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>플래그 초기화(테스트/디버그 — 다시 첫실행처럼 동작).</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
