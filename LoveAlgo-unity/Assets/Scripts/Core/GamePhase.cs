namespace LoveAlgo.Core
{
    /// <summary>
    /// 게임 Phase
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
