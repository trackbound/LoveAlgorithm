namespace LoveAlgo.Events
{
    // ── 스테이지 이미지 레이어 명령(M3 슬라이스2: CG/SD/Overlay) ──
    // 셋은 문법·동작(이미지 레이어를 페이드로 보이고/닫기)이 동일하고 차이는 z-위치와 CG의 대사/캐릭터 숨김뿐이라
    // 명령을 공유하고 종류는 <see cref="StageLayerKind"/>로 구분한다(과설계 게이트: 거의 같은 파서/뷰 3벌 금지).
    // 흐름: 순수 StageLayerParser가 Value를 인텐트로 분해 → 엔진이 동결 수치(StageLayerTuningSO)로 fade 해석 →
    // 명령 발행 → 뷰(StageLayerView)가 컨벤션 로딩(CG/SD/Overlay/{name})해 알파 lerp + 완료 핸들(ADR-007).

    /// <summary>스테이지 이미지 레이어 종류. CG=전체화면 컷신(대사/캐릭터 숨김), SD=치비 부분, Overlay=무드 보조배경.</summary>
    public enum StageLayerKind { CG, SD, Overlay }

    /// <summary>레이어 전환. 동일 이름 BG 전환(BgTransition)과 별개의 단순 2종.</summary>
    public enum LayerTransition { Cut, Fade }

    /// <summary>
    /// 스테이지 레이어 표시/종료 명령. <see cref="IsClose"/>=true면 현재 레이어를 페이드 아웃(이름 무시).
    /// 아니면 <see cref="Name"/>을 로드해 <see cref="Transition"/>으로 표시. <see cref="Duration"/>은 엔진 해석 최종값(초).
    /// </summary>
    public readonly struct ShowStageLayerCommand
    {
        public readonly StageLayerKind Kind;
        public readonly bool IsClose;
        public readonly string Name;
        public readonly LayerTransition Transition;
        public readonly float Duration;
        public readonly CompletionHandle Handle;

        public ShowStageLayerCommand(StageLayerKind kind, bool isClose, string name, LayerTransition transition, float duration, CompletionHandle handle)
        {
            Kind = kind;
            IsClose = isClose;
            Name = name;
            Transition = transition;
            Duration = duration;
            Handle = handle;
        }
    }

    /// <summary>
    /// CG 모드 토글 — CG 진입 시 <see cref="Active"/>=true로 발행해 대사창·캐릭터를 숨기고, 종료 시 false로 복원.
    /// DialogueView/StageView가 구독해 자기 표시를 토글한다(ADR-007: CG 뷰가 그들을 직접 참조하지 않음).
    /// </summary>
    public readonly struct SetCgModeCommand
    {
        public readonly bool Active;
        public SetCgModeCommand(bool active) { Active = active; }
    }
}
