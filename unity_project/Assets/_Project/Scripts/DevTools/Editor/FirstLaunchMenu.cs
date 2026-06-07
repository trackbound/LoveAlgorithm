using UnityEditor;
using UnityEngine;
using LoveAlgo.UI; // FirstLaunchFlag

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 첫실행 인트로 오버레이를 다시 테스트하기 위한 에디터 메뉴 — <see cref="FirstLaunchFlag"/>를 초기화한다.
    /// (Tools/Debug/Reset First Launch). 에디터 전용(빌드 무관).
    /// </summary>
    static class FirstLaunchMenu
    {
        [MenuItem("Tools/Debug/Reset First Launch (인트로 다시 보기)")]
        static void ResetFirstLaunch()
        {
            FirstLaunchFlag.Reset();
            Debug.Log("[FirstLaunch] 플래그 초기화 — 다음 Play/실행은 첫실행처럼 인트로 오버레이를 표시합니다.");
        }
    }
}
