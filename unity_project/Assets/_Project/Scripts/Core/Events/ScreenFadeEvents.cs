namespace LoveAlgo.Events
{
    // ── 스크린 페이드 명령(M3 슬라이스2: FadeOut/FadeIn/Flash) ──
    // 셋 다 "전체화면을 색으로 페이드"하는 효과(검정 페이드/흰색 섬광)라 한 family로 묶는다. 구 ScreenFX 모놀리스의
    // 이름을 답습하지 않고 실제 범위(전체화면 색 페이드)를 드러내는 ScreenFade로 명명. 다른 화면 효과(셰이크/카메라/
    // 틴트/아이마스크/CG)는 각자 family로 분리됨.
    // 흐름: 순수 ScreenFadeParser가 Value를 인텐트로 분해 → 엔진이 동결 수치(ScreenFadeTuningSO)로 duration 해석 →
    // 명령 발행 → 뷰(ScreenFadeView)가 최상위 전체화면 오버레이 알파를 코루틴 lerp + 완료 핸들(ADR-007).
    // 대사 UI까지 덮는 전환용 — _Stage(대사 뒤)와 별개.

    /// <summary>스크린 페이드 종류(전체화면 색 페이드).</summary>
    public enum ScreenFadeKind
    {
        FadeOut, // 화면을 검정으로(알파 0→1, 검정 유지)
        FadeIn,  // 검정에서 해제(알파 1→0)
        Flash    // 흰 섬광(알파 0→1→0) — 흰색으로의 순간 페이드
    }

    /// <summary>
    /// 스크린 페이드 표시 명령. <see cref="Duration"/>은 엔진이 해석한 최종값(초). 색은 <see cref="Kind"/>가 결정
    /// (페이드=검정, 플래시=흰색) — 뷰 소관.
    /// </summary>
    public readonly struct ShowScreenFadeCommand
    {
        public readonly ScreenFadeKind Kind;
        public readonly float Duration;
        public readonly CompletionHandle Handle;

        public ShowScreenFadeCommand(ScreenFadeKind kind, float duration, CompletionHandle handle)
        {
            Kind = kind;
            Duration = duration;
            Handle = handle;
        }
    }
}
