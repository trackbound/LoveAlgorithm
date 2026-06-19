namespace LoveAlgo.Events
{
    // ── 아이마스크 FX 명령(M3 슬라이스2: 눈감김/뜨기) ──
    // 다른 FX와 같은 구조: 순수 EyeMaskParser가 FX Value를 인텐트로 분해 → 엔진(NarrativeController)이 동결 수치
    // (EyeMaskTuningSO)로 지속을 해석 → 명령에 실어 발행 → 뷰(EyeMaskView)가 상/하 검은 바를 보간해 눈꺼풀처럼
    // 닫고/열고 완료 핸들을 푼다(ADR-007). 닫히면 화면 전체 암전(POV 눈감김) — 페이드와 별개 연출.

    /// <summary>아이마스크 동작.</summary>
    public enum EyeMaskAction
    {
        Open,            // 눈뜨기(바 후퇴 → 화면 보임)
        Close,           // 눈감기(바 중앙 합류 → 암전)
        CloseImmediate,  // 즉시 감김(애니 없음)
        Blink            // 깜빡임(감기 → 유지 → 뜨기)
    }

    /// <summary>
    /// 아이마스크 명령. 동작별로 의미 있는 지속이 다르다: Open=<see cref="OpenDuration"/>, Close=<see cref="CloseDuration"/>,
    /// Blink=셋 다, CloseImmediate=무시. 모두 엔진이 동결 수치로 해석한 최종값(초).
    /// </summary>
    public readonly struct EyeMaskCommand
    {
        public readonly EyeMaskAction Action;
        public readonly float CloseDuration;
        public readonly float OpenDuration;
        public readonly float HoldDuration;
        public readonly CompletionHandle Handle;

        public EyeMaskCommand(EyeMaskAction action, float closeDuration, float openDuration, float holdDuration, CompletionHandle handle)
        {
            Action = action;
            CloseDuration = closeDuration;
            OpenDuration = openDuration;
            HoldDuration = holdDuration;
            Handle = handle;
        }
    }

    /// <summary>
    /// 아이마스크 차폐(눈꺼풀 바) 표시 상태 변화. <c>Active=true</c>=바가 화면을 덮는 중(감김/유지/뜨는 중),
    /// <c>false</c>=완전히 열려 바 비표시. 대사창(DialogueView)이 구독해 차폐 동안에만 정렬을 눈꺼풀 위로 올려
    /// 암전 위에 대사가 보이게 한다(ADR-007: 뷰 간 직접참조 없이 이벤트로). 평상시엔 모달/팝업 아래 유지.
    /// </summary>
    public readonly struct EyeMaskShroudChanged
    {
        public readonly bool Active;
        public EyeMaskShroudChanged(bool active) { Active = active; }
    }
}
