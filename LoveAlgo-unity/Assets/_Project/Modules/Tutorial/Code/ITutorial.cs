using LoveAlgo.UI;

namespace LoveAlgo.Tutorial
{
    /// <summary>
    /// 튜토리얼 오버레이 모듈 외부 계약.
    /// 구현: <see cref="TutorialModule"/>.
    /// </summary>
    public interface ITutorial
    {
        TutorialOverlay Overlay { get; }
    }
}
