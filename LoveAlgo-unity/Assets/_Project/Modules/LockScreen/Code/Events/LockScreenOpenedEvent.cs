using LoveAlgo.Contracts;

namespace LoveAlgo.LockScreen.Events
{
    /// <summary>잠금화면이 열렸을 때 발행. 모드 정보 포함.</summary>
    public readonly struct LockScreenOpenedEvent
    {
        public readonly LockScreenMode Mode;
        public LockScreenOpenedEvent(LockScreenMode mode) { Mode = mode; }
    }
}
