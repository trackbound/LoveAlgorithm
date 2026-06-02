using System.Collections.Generic;
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // StatChangedEvent

namespace LoveAlgo.Shop
{
    /// <summary>
    /// <see cref="ShopService.Purchase"/> 결과. 변경 내역(거부 사유·총비용·즉시 스탯 변화)을 담아 호출자(어댑터)가
    /// 통지 발행 여부를 결정한다 — 서비스 자체는 EventBus를 모른다.
    /// </summary>
    public readonly struct PurchaseResult
    {
        public readonly bool Ok;
        public readonly ShopRejection Reason;
        public readonly long TotalCost;
        public readonly StatChangedEvent[] StatChanges; // 즉시 Consumable 효과(피로 회복 등)

        PurchaseResult(bool ok, ShopRejection reason, long totalCost, StatChangedEvent[] statChanges)
        {
            Ok = ok; Reason = reason; TotalCost = totalCost; StatChanges = statChanges;
        }

        public static PurchaseResult Success(long totalCost, StatChangedEvent[] statChanges)
            => new(true, ShopRejection.None, totalCost, statChanges);

        public static PurchaseResult Fail(ShopRejection reason)
            => new(false, reason, 0, System.Array.Empty<StatChangedEvent>());
    }

    /// <summary>
    /// 구매 순수층: 장바구니를 검증·정산하고 즉시 효과를 <see cref="GameStateSO"/>에 적용한다.
    /// MonoBehaviour/EventBus/Services를 모른다 = 결정적·EditMode 테스트 가능(ADR-007).
    /// 구 <c>ShopSystem.BuyBatchAndApply</c>의 자금 게이트·소지금 차감·Consumable 즉시효과만 재현.
    ///
    /// 슬라이스1 범위: 자금 게이트 + 소지금 차감 + Consumable 즉시 피로 회복(스탯 변화 산출).
    /// 범위 밖(후속, 스키마/통합 필요): Gift 인벤토리 보관, SessionBuff 지연 적용, 중복 50% 페널티, 해금 게이트.
    /// </summary>
    public static class ShopService
    {
        /// <summary>장바구니 구매. 자금 부족/미존재 아이템/빈 장바구니면 거부(상태 불변).</summary>
        public static PurchaseResult Purchase(GameStateSO gs, IReadOnlyDictionary<string, int> cart)
        {
            if (gs == null) return PurchaseResult.Fail(ShopRejection.ItemNotFound);
            if (cart == null || cart.Count == 0) return PurchaseResult.Fail(ShopRejection.EmptyCart);

            // 1. 검증 + 총비용 합산
            long total = 0;
            foreach (var kv in cart)
            {
                if (kv.Value <= 0) continue;
                var item = ItemDatabase.Get(kv.Key);
                if (item == null) return PurchaseResult.Fail(ShopRejection.ItemNotFound);
                total += (long)item.Price * kv.Value;
            }

            // 2. 자금 게이트
            if (gs.Money < total) return PurchaseResult.Fail(ShopRejection.InsufficientFunds);

            // 3. 소지금 차감
            gs.AddMoney(-total);

            // 4. 즉시 효과(Consumable = 피로 회복). 합산 적용 후 한 번의 StatChange 산출.
            int fatigueBefore = gs.GetStat(GameStateSO.StatIds[4]); // "Fatigue"
            foreach (var kv in cart)
            {
                if (kv.Value <= 0) continue;
                var item = ItemDatabase.Get(kv.Key);
                if (item.Category == ItemCategory.Consumable && item.EffectValue != 0)
                    gs.AddStat("Fatigue", -item.EffectValue * kv.Value); // 회복 = 피로 감소
                // Gift / SessionBuff: 슬라이스1 범위 밖(인벤토리/지연 적용 = 후속).
            }
            int fatigueAfter = gs.GetStat("Fatigue");

            var changes = fatigueAfter != fatigueBefore
                ? new[] { new StatChangedEvent("Fatigue", fatigueBefore, fatigueAfter) }
                : System.Array.Empty<StatChangedEvent>();

            return PurchaseResult.Success(total, changes);
        }
    }
}
