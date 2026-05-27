namespace LoveAlgo.Contracts
{
    // C4-Phase A: LoveAlgo.LockScreen → LoveAlgo.Contracts 로 이동.

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
        Reset,

        /// <summary>게임 설치 후 최초 진입 — 비번/입력 없이 LOGIN 버튼만으로 통과 (시각 연출).</summary>
        GameStart
    }
}
