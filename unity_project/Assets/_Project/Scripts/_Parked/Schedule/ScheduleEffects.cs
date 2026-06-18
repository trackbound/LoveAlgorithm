using System;
using LoveAlgo.Core;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 효과 적용 공식 (REWRITE_FEATURE_INVENTORY §5 자유행동 9종).
    ///
    /// 구 <c>DayLoopController.OnScheduleSelected</c>에서 UI 토스트·세션버프·투자 RNG 같은
    /// 오케스트레이션을 걷어낸 순수 상태 변경만 재구현한다. 상태는 <see cref="GameStateSO"/>에 두고
    /// 이 클래스는 상태를 인자로 받는 정적 함수만 제공한다 (Service Locator/싱글톤 없음, ADR-007).
    ///
    /// StatChangedEvent 발행 등 통지는 통합층(ScheduleModule 재작성, M4 후속)이 Apply 직후 EventBus로 담당한다.
    /// 여기서 RNG/Random을 쓰지 않는 이유: 순수·결정적이라 EditMode에서 검증 가능하게 하기 위함
    /// (투자 배수는 호출자가 주입).
    /// </summary>
    public static class ScheduleEffects
    {
        /// <summary>
        /// 스케줄 효과의 스탯/소지금 변화를 상태에 적용한다(세션버프·아이템 보조효과 제외).
        /// 스탯은 GameStateSO에서 0~MaxStat 클램프, 소지금은 0 바닥 클램프.
        /// </summary>
        public static void Apply(GameStateSO gs, ScheduleEffect e)
        {
            if (gs == null) return;
            gs.AddStat("Str", e.strengthChange);
            gs.AddStat("Int", e.intelligenceChange);
            gs.AddStat("Soc", e.socialChange);
            gs.AddStat("Per", e.perseveranceChange);
            gs.AddStat("Fatigue", e.fatigueChange);
            gs.AddMoney(e.moneyChange);
        }

        /// <summary>
        /// 투자 결과 적용. 구 로직: 변화액 = round(현재소지금 × 배수), 배수 ∈ [-0.5, +1.0](±50~100%, §5).
        /// RNG는 호출자가 배수로 주입(순수성 유지). 소지금은 0 바닥(GameStateSO.AddMoney).
        /// </summary>
        /// <returns>실제로 반영된 변화액(0 바닥 클램프 반영 후 = 적용 후 − 적용 전).</returns>
        public static long ApplyInvest(GameStateSO gs, float multiplier)
        {
            if (gs == null) return 0;
            long before = gs.Money;
            long change = (long)Math.Round(before * (double)multiplier);
            gs.AddMoney(change);
            return gs.Money - before;
        }
    }
}
