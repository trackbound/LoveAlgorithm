using System.Collections.Generic;
using LoveAlgo;        // GameConstants (LoveAlgo.Data asmdef)
using LoveAlgo.Core;   // GameStateSO, DayLoop
using LoveAlgo.Events; // StatChangedEvent

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 1회 수행의 순수 거래 로직 — M4 slice2 통합층의 결정적 코어.
    ///
    /// 구 <c>DayLoopController.OnScheduleSelected</c>의 책임 중 RNG·EventBus·토스트·세션버프를 걷어내고
    /// 남는 부분(게이트 → 효과 적용 → 행동 소모 → 변화 집계)만 정적 함수로 재구현한다. 상태는
    /// <see cref="GameStateSO"/>에만 두고 RNG/통지는 호출자(<see cref="ScheduleController"/>)가 담당하므로
    /// 이 클래스는 결정적 → EditMode에서 게이트/적용/소모/변화목록을 검증할 수 있다(ADR-007).
    ///
    /// 호출하는 준비물은 이미 존재: <see cref="ScheduleEffects"/>(효과 적용, slice1)·<see cref="DayLoop"/>(행동 소모, M2).
    /// 통합층은 이 둘을 호출하고 결과를 <see cref="ScheduleResult"/>로 모아 돌려줄 뿐이다(handoff M4 slice2).
    /// </summary>
    public static class ScheduleService
    {
        /// <summary>
        /// 스케줄 1회 수행. 게이트(투자 자금·1일1회 제한) 통과 시 효과를 적용하고 행동을 1회 소모한다.
        /// </summary>
        /// <param name="gs">런타임 상태.</param>
        /// <param name="type">선택된 스케줄.</param>
        /// <param name="investMultiplier">투자 배수(호출자가 RNG로 주입, ∈[-0.5,+1.0]). 비투자 스케줄에선 무시.</param>
        /// <returns>적용 결과 또는 거부 사유. 거부 시 상태는 변경되지 않는다.</returns>
        public static ScheduleResult Execute(GameStateSO gs, ScheduleType type, float investMultiplier)
        {
            if (gs == null) return ScheduleResult.Reject(type, default, ScheduleRejection.None);

            var effect = ScheduleTable.Get(type);

            // ── 게이트 (상태 변경 전 검사 — 미충족 시 부작용 없음) ──
            if (type == ScheduleType.Invest && gs.Money < GameConstants.MinInvestMoney)
                return ScheduleResult.Reject(type, effect, ScheduleRejection.InsufficientFunds);

            string limitKey = type.ToString();
            if (effect.isLimited && gs.HasUsedLimited(limitKey))
                return ScheduleResult.Reject(type, effect, ScheduleRejection.DailyLimitReached);

            // ── 변경 전 스냅샷 (StatChangedEvent old/new 산출용) ──
            long moneyBefore = gs.Money;
            var ids = GameStateSO.StatIds;
            var before = new int[ids.Length];
            for (int i = 0; i < ids.Length; i++) before[i] = gs.GetStat(ids[i]);

            // ── 효과 적용 (순수층 위임) ──
            // 투자는 효과표가 전부 0이고 변화는 RNG 배수로만 발생 → ApplyInvest. 그 외는 스탯/소지금 표 적용.
            if (type == ScheduleType.Invest) ScheduleEffects.ApplyInvest(gs, investMultiplier);
            else ScheduleEffects.Apply(gs, effect);

            if (effect.isLimited) gs.MarkLimitedUsed(limitKey);

            // ── 변화 집계 ──
            var changes = new List<StatChangedEvent>();
            for (int i = 0; i < ids.Length; i++)
            {
                int now = gs.GetStat(ids[i]);
                if (now != before[i]) changes.Add(new StatChangedEvent(ids[i], before[i], now));
            }
            long moneyDelta = gs.Money - moneyBefore;

            // ── 행동 소모 (순수층 위임) — 소진 시 하루 종료 신호 ──
            bool dayEnded = DayLoop.ConsumeAction(gs);

            return ScheduleResult.Applied(type, effect, changes.ToArray(), moneyDelta, dayEnded);
        }
    }

    /// <summary>
    /// <see cref="ScheduleService.Execute"/> 결과. 통합층(<see cref="ScheduleController"/>)이 이 정보로
    /// EventBus 통지를 발행한다(거부 시 발행 안 함). <see cref="StatChanges"/>는 변경된 스탯만 담긴
    /// 발행 준비 완료 이벤트 배열이다.
    /// </summary>
    public readonly struct ScheduleResult
    {
        public readonly bool Rejected;
        public readonly ScheduleRejection Reason;
        public readonly ScheduleType Type;
        public readonly ScheduleEffect Effect;
        public readonly StatChangedEvent[] StatChanges;
        public readonly long MoneyDelta;
        public readonly bool DayEnded;

        ScheduleResult(bool rejected, ScheduleRejection reason, ScheduleType type, ScheduleEffect effect,
            StatChangedEvent[] statChanges, long moneyDelta, bool dayEnded)
        {
            Rejected = rejected;
            Reason = reason;
            Type = type;
            Effect = effect;
            StatChanges = statChanges;
            MoneyDelta = moneyDelta;
            DayEnded = dayEnded;
        }

        public static ScheduleResult Applied(ScheduleType type, ScheduleEffect effect,
            StatChangedEvent[] statChanges, long moneyDelta, bool dayEnded)
            => new(false, ScheduleRejection.None, type, effect, statChanges, moneyDelta, dayEnded);

        public static ScheduleResult Reject(ScheduleType type, ScheduleEffect effect, ScheduleRejection reason)
            => new(true, reason, type, effect, System.Array.Empty<StatChangedEvent>(), 0, false);
    }
}
