using LoveAlgo.Common;
using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 앱 라이프사이클 훅.
    /// EventBus/Services를 도메인 리로드(에디터 Play stop)·앱 종료 시 청소.
    /// 모듈은 자기 Awake에서 self-register. Bootstrap은 등록 흐름에 개입하지 않음.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            // 도메인 리로드 후 첫 진입 — 잔여 등록 제거
            EventBus.Clear();
            Services.Clear();

            Application.quitting += OnQuit;
        }

        static void OnQuit()
        {
            Application.quitting -= OnQuit;
            EventBus.Clear();
            Services.Clear();
        }
    }
}
