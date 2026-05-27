namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 배경 전환 타입.
    /// C4-Phase A에서 LoveAlgo.Story (BackgroundLayer.cs) → LoveAlgo.Contracts 로 이동.
    /// </summary>
    public enum BGTransition
    {
        Cut,    // 즉시 교체
        Fade,   // 페이드 (검은색 경유)
        Cross   // 크로스페이드
    }
}
