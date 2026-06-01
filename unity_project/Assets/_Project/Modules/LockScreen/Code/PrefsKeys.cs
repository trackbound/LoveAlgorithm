namespace LoveAlgo.LockScreen
{
    /// <summary>
    /// PC잠금 모듈 PlayerPrefs 키 상수.
    /// </summary>
    internal static class PrefsKeys
    {
        public const string PasswordHash = "lock_screen.password_hash";
        public const string PasswordSalt = "lock_screen.password_salt";
        public const string FirstStartDone = "lock_screen.first_start_done";
    }
}
