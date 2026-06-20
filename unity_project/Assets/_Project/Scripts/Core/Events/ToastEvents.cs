namespace LoveAlgo.Events
{
    // ── 토스트 알림 명령(호출부 발행 → ToastView가 표시) ──
    // ADR-007: 표시는 뷰, 문구·의미는 호출부. 모달(ShowModalCommand)과 달리 입력을 막지 않고 콜백도 없다.

    /// <summary>
    /// 토스트 알림 표시 명령. <see cref="ToastView"/>가 구독해 잠깐 떠올랐다 사라지는 비차단 알림을 띄운다
    /// (ADR-007: 표시만, 문구는 호출부). 단순 사용자 피드백("설정이 저장되었습니다" 등)용 — 모달과 달리 입력을
    /// 막지 않으며 선택 핸들이 없다. <see cref="Duration"/>이 0 이하면 뷰의 기본 지속시간을 사용한다.
    /// </summary>
    public readonly struct ShowToastCommand
    {
        public readonly string Title;
        public readonly string Message;
        public readonly float Duration;

        public ShowToastCommand(string message, string title = null, float duration = 0f)
        {
            Message = message;
            Title = title;
            Duration = duration;
        }
    }
}
