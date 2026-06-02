namespace LoveAlgo.Events
{
    /// <summary>
    /// 히로인 호감도 총점이 변경되면 발행되는 통지 이벤트(EventBus). 구독자(호감도 HUD 등, M5)가 반응.
    /// <c>FlowCommandController</c>가 <c>FlowCommandInterpreter</c> 결과(Affinity 계열)로 발행한다.
    /// <see cref="NewScore"/>=보너스(스탯/로아피로) 포함 최종 총점.
    /// </summary>
    public readonly struct AffinityChangedEvent
    {
        public readonly string HeroineId;
        public readonly int NewScore;

        public AffinityChangedEvent(string heroineId, int newScore)
        {
            HeroineId = heroineId;
            NewScore = newScore;
        }
    }
}
