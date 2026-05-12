namespace LoveAlgo.Modules.DayLoop
{
    /// <summary>
    /// 게임 진행 흐름 모듈 외부 계약.
    /// 일자·페이즈 쿼리만 제공. 일자 진행 책임은 기존 DayLoopController 유지.
    /// 구현: <see cref="DayLoopModule"/>.
    /// </summary>
    public interface IDayLoop
    {
        /// <summary>현재 일자 (1부터 시작).</summary>
        int CurrentDay { get; }

        /// <summary>현재 페이즈.</summary>
        EventPhase CurrentPhase { get; }

        /// <summary>해당 일자가 어떤 페이즈에 속하는지 계산. (현재 일자에 의존하지 않음)</summary>
        EventPhase GetPhaseForDay(int day);

        /// <summary>
        /// 해당 일자가 이벤트일인지 (자유행동 불가).
        /// Event1/Festival/Event2/MT/Event3/Confession이면 true.
        /// </summary>
        bool IsEventDay(int day);

        /// <summary>해당 일자가 자유행동일인지.</summary>
        bool IsFreeActionDay(int day);
    }
}
