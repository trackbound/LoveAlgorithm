namespace LoveAlgo.Events
{
    // ── 개발 디버그 토스트 명령(DevToast 헬퍼 발행 → DevToastView가 우측 상단에 표시) ──
    // 게임용 토스트(ShowToastCommand)와 완전히 분리된 별도 채널. 개발/테스트 빌드 전용(릴리즈엔 미포함).

    /// <summary>개발 디버그 토스트 심각도 = 박스 색. Info=초록(단순 알림·로그), Warn=노랑(경고), Error=빨강(위험·에러).</summary>
    public enum DevToastSeverity { Info, Warn, Error }

    /// <summary>
    /// 개발 디버그 토스트 표시 명령. <see cref="DevToastSeverity"/>에 따라 색이 다른 작은 박스를 화면 우측 상단에
    /// 잠깐 띄운다(비차단 — 입력을 막지 않음). 발행은 <c>LoveAlgo.Common.DevToast</c> 헬퍼([Conditional]로 릴리즈
    /// 빌드에선 호출 제거), 표시는 <c>DevToastView</c>(개발 빌드 전용). <see cref="Duration"/>이 0 이하면 뷰 기본값.
    /// </summary>
    public readonly struct ShowDevToastCommand
    {
        public readonly string Message;
        public readonly DevToastSeverity Severity;
        public readonly float Duration;

        public ShowDevToastCommand(string message, DevToastSeverity severity = DevToastSeverity.Info, float duration = 0f)
        {
            Message = message;
            Severity = severity;
            Duration = duration;
        }
    }
}
