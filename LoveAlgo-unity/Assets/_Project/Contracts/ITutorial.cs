namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 튜토리얼 오버레이 모듈 외부 계약.
    /// 구현: <see cref="LoveAlgo.Tutorial.TutorialModule"/>.
    /// </summary>
    public interface ITutorial
    {
        ITutorialOverlay Overlay { get; }
    }
}
