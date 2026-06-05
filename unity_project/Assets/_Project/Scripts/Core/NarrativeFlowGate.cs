namespace LoveAlgo.Core
{
    /// <summary>
    /// 흐름-critical 내러티브 구간 게이트(기획 도구 안전장치). GameManager 저녁 이벤트 씨임처럼 내러티브 완료를
    /// 기다려 게임 흐름을 진행(하루 넘기기)하는 구간이 <see cref="Lock"/>하면, 스토리 도구(에디터창/런타임 패널)는
    /// <see cref="IsLocked"/>를 보고 Apply(재생 교체)를 막는다 — 그 구간에 스크립트를 교체하면 기다리던 완료
    /// 이벤트가 영영 안 와 데이루프가 데드락되기 때문. 깊이 카운터라 재진입 안전. 정적이라 도메인 리로드 시 0으로
    /// 리셋되고, 비정상 경로 대비 <see cref="Reset"/> 제공. (현재 유일 사용처=GameManager 저녁 이벤트.)
    /// </summary>
    public static class NarrativeFlowGate
    {
        static int _depth;

        /// <summary>흐름-critical 구간 진행 중인가(도구 Apply 차단 신호).</summary>
        public static bool IsLocked => _depth > 0;

        public static void Lock() => _depth++;
        public static void Unlock() { if (_depth > 0) _depth--; }

        /// <summary>강제 0 리셋(비정상 종료 안전망 — 일반 흐름은 Lock/Unlock 짝).</summary>
        public static void Reset() => _depth = 0;
    }
}
