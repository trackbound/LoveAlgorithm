namespace LoveAlgo.LockScreen.Events
{
    /// <summary>비번 검증 실패 시 발행. 누적 실패 횟수 포함.</summary>
    public readonly struct PasswordFailedEvent
    {
        public readonly int FailCount;
        public readonly bool ShowKeyIcon; // FailCount >= 3
        public PasswordFailedEvent(int failCount, bool showKeyIcon)
        {
            FailCount = failCount;
            ShowKeyIcon = showKeyIcon;
        }
    }
}
