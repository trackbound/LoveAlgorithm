namespace LoveAlgo.Events
{
    /// <summary>
    /// 대사 로그(백로그) 열기(ADR-013 Overlay). 대사창 휠업(DialogueView)·후속 진입 버튼이 발행 →
    /// <c>DialogueLogView</c>가 구독·표시. 닫기 = 공용 뒤로가기(OverlayGate.CloseTop)/돌아가기 버튼.
    /// </summary>
    public readonly struct OpenDialogueLogCommand
    {
    }

    /// <summary>
    /// 플레이어 이름 입력 화면 표시(스토리 Flow <c>Username</c> — LockScreen 미러). 입력 확정까지
    /// 엔진이 <see cref="Handle"/>을 대기한다. UsernameScreenView가 구독·표시, 이름은
    /// GameStateSO(<c>Data.playerName</c>)에 저장(세이브 직렬화 포함).
    /// </summary>
    public readonly struct ShowUsernameCommand
    {
        public readonly CompletionHandle Handle;
        public ShowUsernameCommand(CompletionHandle handle) { Handle = handle; }
    }
}
