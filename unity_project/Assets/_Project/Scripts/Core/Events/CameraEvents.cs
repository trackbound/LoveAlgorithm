namespace LoveAlgo.Events
{
    // ── 카메라 FX 명령(M3 슬라이스2: CamZoom/CamPan/CamReset) ──
    // 화면/흔들기 FX와 같은 구조: 순수 CameraParser가 FX Value를 인텐트로 분해 → 엔진(NarrativeController)이
    // 동결 수치(CameraTuningSO)로 duration을 해석 → 명령에 실어 발행 → 뷰(CameraView)가 _Stage 콘텐츠 래퍼의
    // localScale(줌)·anchoredPosition(팬)을 코루틴 lerp하고 완료 핸들을 푼다(ADR-007).
    //
    // UI(Screen Space-Overlay) 무대엔 월드 카메라가 없으므로 "카메라" = _Stage/Content 트랜스폼 조작이다(구 ScreenFX가
    // stageTransform을 DOScale/DOAnchorPos 한 것과 동치). 줌/팬은 지속 상태(다음 명령/리셋까지 유지) — 세이브엔 미포함
    // (스테이지 세이브 상태에 카메라 없음). CamShake는 흔들기 가족(ShakeParser, ShakeTarget.Stage)으로 처리.

    /// <summary>카메라 FX 종류(이번 슬라이스 범위).</summary>
    public enum CameraKind
    {
        Zoom,  // localScale → 균일 배율
        Pan,   // anchoredPosition → 절대 오프셋(px)
        Reset  // 줌·팬 동시 원점 복귀
    }

    /// <summary>
    /// 카메라 FX 명령. <see cref="Duration"/>은 엔진이 동결 수치로 해석한 최종값(초). <see cref="ZoomScale"/>은
    /// <see cref="CameraKind.Zoom"/>, <see cref="PanX"/>/<see cref="PanY"/>는 <see cref="CameraKind.Pan"/>에만 의미.
    /// </summary>
    public readonly struct CameraCommand
    {
        public readonly CameraKind Kind;
        public readonly float ZoomScale;
        public readonly float PanX;
        public readonly float PanY;
        public readonly float Duration;
        public readonly CompletionHandle Handle;

        public CameraCommand(CameraKind kind, float zoomScale, float panX, float panY, float duration, CompletionHandle handle)
        {
            Kind = kind;
            ZoomScale = zoomScale;
            PanX = panX;
            PanY = panY;
            Duration = duration;
            Handle = handle;
        }
    }
}
