using System.Diagnostics; // Conditional
using EngineLog = LoveAlgo.Common.Log;

namespace LoveAlgo.Common
{
    /// <summary>
    /// 입력 디버그 로깅 전역 토글. <see cref="Enabled"/>가 참이면 좌클릭/스페이스/Esc가 일으킨 동작
    /// (모달 확인·취소, 대사 타이핑 스킵·진행 등)을 콘솔에 찍는다 — 입력이 먹는지/무엇을 했는지 추적용.
    ///
    /// <see cref="Log"/>는 <c>[Conditional(UNITY_EDITOR/DEVELOPMENT_BUILD)]</c>이라 **릴리즈 빌드에선 호출
    /// 자체가 제거**된다(인자 보간 비용·콘솔 스팸 0). 에디터/개발 빌드에선 <see cref="Enabled"/> 플래그로
    /// 런타임 on/off — 평소 끄고, 디버깅할 때만 켠다(에디터: Tools/Debug/Input Logging 메뉴, EditorPrefs 영속).
    /// </summary>
    public static class DebugInput
    {
        /// <summary>켜짐 상태를 영속하는 EditorPrefs 키(에디터 메뉴·단축키 공유 단일 소스).</summary>
        public const string PrefKey = "LoveAlgo.DebugInput.Enabled";

        /// <summary>입력 로깅 on/off(전역 런타임 플래그). 에디터 메뉴·단축키(F8) 또는 코드에서 토글.</summary>
        public static bool Enabled;

        /// <summary>켜져 있을 때만 <c>[Input] {message}</c>를 출력. 릴리즈 빌드에선 호출 제거(Conditional).</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message)
        {
            if (Enabled) EngineLog.Info($"[Input] {message}");
        }
    }
}
