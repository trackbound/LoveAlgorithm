namespace LoveAlgo.Events
{
    /// <summary>
    /// CSV Flow 명령 실행을 요청하는 명령 이벤트(EventBus). 발행자(내러티브 엔진=후속, 또는 디버그/테스트)는
    /// 명령 문자열만 알리고, <c>FlowCommandController</c>가 순수 <c>FlowCommandInterpreter</c>로 적용 후 통지한다(ADR-007).
    /// 예: "Affinity:EventChoice:HaYeEun:Event1:3", "Day:5".
    /// </summary>
    public readonly struct FlowCommandRequestedEvent
    {
        public readonly string Command;

        public FlowCommandRequestedEvent(string command)
        {
            Command = command;
        }
    }
}
