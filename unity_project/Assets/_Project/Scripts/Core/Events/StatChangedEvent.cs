namespace LoveAlgo.Events
{
    /// <summary>
    /// 플레이어 스탯이 변경되면 발행되는 통지 이벤트(EventBus). 구독자(HUD UI 등, M5)가 반응.
    /// 구 <c>LoveAlgo.Contracts.StatChangedEvent</c>의 1:1 이식 — 필드/Delta 동일, 네임스페이스만
    /// Contracts→Events(I* 계약 부활 금지, ADR-007). StatId는 GameStateSO 스탯 id("Str"/"Int"/"Soc"/"Per"/"Fatigue").
    ///
    /// 발행 경계: 상태 변경 공식(ScheduleEffects 등)은 순수하게 두고, 통합층(호출자)이 변경 전/후 값을
    /// 스냅샷해 이 이벤트를 발행한다. 즉 GameStateSO.SetStat 자체는 이벤트를 쏘지 않는다(로드/리셋 시 오발행 방지).
    /// Core asmdef에 두는 이유: 발행자(Schedule 등)와 구독자(UI)가 공통으로 참조할 수 있는 최하위 계층.
    /// </summary>
    public readonly struct StatChangedEvent
    {
        public readonly string StatId;
        public readonly int OldValue;
        public readonly int NewValue;

        public StatChangedEvent(string statId, int oldValue, int newValue)
        {
            StatId = statId;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public int Delta => NewValue - OldValue;
    }
}
