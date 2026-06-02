namespace LoveAlgo.Events
{
    // ── 스크린 오버레이 FX 명령(M3 슬라이스2: FadeOut/FadeIn/Flash) ──
    // 스테이지/사운드와 같은 구조: 순수 FxParser가 FX Value를 인텐트로 분해 → 엔진이 동결 수치
    // (ScreenFxTuningSO)로 duration 해석 → 명령에 실어 발행 → 뷰(ScreenFxView)가 최상위 전체화면 오버레이
    // 알파를 코루틴 lerp하고 완료 핸들(CompletionHandle)을 푼다(ADR-007). 대사 UI까지 덮는 씬 전환용 — _Stage(대사 뒤)와 별개.
    //
    // 이번 슬라이스 밖(다음 FX 서브슬라이스): 카메라(CamShake/Zoom/Pan/Reset)·아이마스크(Eye*)·색(ColorTint)·
    // 흔들기(Stage/Dialogue/Char)·캐릭터(CharJump/Dim/Glitch)·매크로(Setup/Day*/Scene*/Wait).

    /// <summary>스크린 오버레이 FX 종류(이번 슬라이스 범위).</summary>
    public enum ScreenFxKind
    {
        FadeOut, // 화면을 검정으로(알파 0→1, 검정 유지)
        FadeIn,  // 검정에서 해제(알파 1→0)
        Flash    // 흰 섬광(알파 0→1→0)
    }

    /// <summary>
    /// 스크린 오버레이 FX 표시 명령. <see cref="Duration"/>은 엔진이 해석한 최종값(초). 색은 <see cref="Kind"/>가 결정
    /// (페이드=검정, 플래시=흰색) — 뷰 소관.
    /// </summary>
    public readonly struct ShowScreenFxCommand
    {
        public readonly ScreenFxKind Kind;
        public readonly float Duration;
        public readonly CompletionHandle Handle;

        public ShowScreenFxCommand(ScreenFxKind kind, float duration, CompletionHandle handle)
        {
            Kind = kind;
            Duration = duration;
            Handle = handle;
        }
    }
}
