namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 게임 Phase.
    /// C4-Phase A에서 LoveAlgo.Core → LoveAlgo.Contracts 로 이동.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>상태 전환 중 (재진입 방지용)</summary>
        Transitioning = -1,

        Title,
        Username,
        Prologue,
        DayLoop,
        Ending
    }
}
