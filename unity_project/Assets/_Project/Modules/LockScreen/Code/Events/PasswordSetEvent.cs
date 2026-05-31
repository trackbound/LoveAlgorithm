namespace LoveAlgo.LockScreen.Events
{
    /// <summary>최초 설정 또는 재설정으로 비번이 새로 저장됐을 때 발행.</summary>
    public readonly struct PasswordSetEvent
    {
        public readonly bool IsFirstTime; // true = FirstSetup, false = Reset
        public PasswordSetEvent(bool isFirstTime) { IsFirstTime = isFirstTime; }
    }
}
