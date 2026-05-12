namespace LoveAlgo.LockScreen
{
    /// <summary>
    /// PC잠금 진입 모드.
    /// </summary>
    public enum LockScreenMode
    {
        /// <summary>첫 시작 — 비번 미설정 상태. 신규 비번 입력 받기.</summary>
        FirstSetup,

        /// <summary>일반 진입 — 저장된 비번 검증.</summary>
        Normal,

        /// <summary>재설정 — 기존 비번 확인 후 새 비번 입력.</summary>
        Reset
    }
}
