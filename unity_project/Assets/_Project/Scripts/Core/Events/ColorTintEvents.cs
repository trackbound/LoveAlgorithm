namespace LoveAlgo.Events
{
    // ── 색 틴트 FX 명령(M3 슬라이스2: ColorTint) ──
    // 화면/카메라 FX와 같은 구조: 순수 ColorTintParser가 FX Value를 인텐트로 분해 → 엔진(NarrativeController)이
    // 동결 수치(ColorTintTuningSO)로 프리셋 색·알파·지속을 해석 → 명령에 실어 발행 → 뷰(ColorTintView)가
    // 전체화면 오버레이 Image 색을 코루틴 lerp하고 완료 핸들을 푼다(ADR-007).
    //
    // 무드 틴트(세피아/블루/핑크 등)는 지속 상태(다음 틴트/Clear까지 유지). 구 ScreenFX처럼 화면 최상위(대사 포함)에
    // 얹되 입력은 막지 않는다(raycastTarget=false). Core엔 UnityEngine Color를 두지 않으려 RGB를 분리해 싣는다.

    /// <summary>
    /// 색 틴트 명령. <see cref="Clear"/>=true면 현재 색을 유지하며 알파만 0으로(해제). 아니면 (<see cref="R"/>,
    /// <see cref="G"/>,<see cref="B"/>,<see cref="Alpha"/>)로 전이. <see cref="Duration"/>은 엔진이 해석한 최종값(초).
    /// </summary>
    public readonly struct ColorTintCommand
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float Alpha;
        public readonly float Duration;
        public readonly bool Clear;
        public readonly CompletionHandle Handle;

        public ColorTintCommand(float r, float g, float b, float alpha, float duration, bool clear, CompletionHandle handle)
        {
            R = r;
            G = g;
            B = b;
            Alpha = alpha;
            Duration = duration;
            Clear = clear;
            Handle = handle;
        }
    }
}
