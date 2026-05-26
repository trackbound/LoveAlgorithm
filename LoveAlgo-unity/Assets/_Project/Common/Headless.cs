using UnityEngine;

namespace LoveAlgo.Common
{
    /// <summary>
    /// 자동화 테스트(B2 PlayMode smoke 등)를 위한 헤드리스 모드 토글.
    /// IsEnabled=true이면 사용자 입력을 기다리는 진입점들이 즉시 자동 통과한다.
    /// 진입점별 정확한 동작은 docs/research/2026-05-22-headless-mode.md 참조.
    ///
    /// 사용 (테스트 setup/teardown):
    ///   Headless.Enable();
    ///   try { /* 스크립트 실행 */ }
    ///   finally { Headless.Disable(); }
    ///
    /// 기본값 false이며, Reload Domain Off 환경에서 PlayMode 진입 시 자동으로 false로 복원
    /// — 이전 테스트의 토글 잔재가 일반 플레이를 깨뜨리는 것 방지.
    /// </summary>
    public static class Headless
    {
        public static bool IsEnabled { get; private set; }

        /// <summary>헤드리스 시 UsernameFlowCommand가 자동 설정할 기본 이름.</summary>
        public const string DefaultUsername = "테스터";

        public static void Enable()  => IsEnabled = true;
        public static void Disable() => IsEnabled = false;

        /// <summary>Reload Domain Off 가드 — PlayMode 진입 시 옛 토글 잔재 방지.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticStateOnLoad() => IsEnabled = false;
    }
}
