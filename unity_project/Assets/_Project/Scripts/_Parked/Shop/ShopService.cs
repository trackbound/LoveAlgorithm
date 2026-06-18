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

            // 4. 즉시 효과. 전 스탯 스냅샷 → 적용 → 변경분만 StatChangedEvent.
            //    SessionBuff도 즉시 가산(인벤토리/지연 없음): 자유행동 1턴이 상점→스케줄 한 사이클이라
            //    이번 행동에만 반영되고 사라지는 동작이 즉시가산으로 자연 성립한다.
            //    중복 50% 페널티: 같은 날 같은 태그 2회차부터. 한 아이템 구매당 1회 등록(주효과·부효과는 같은 배수).
            var before = SnapshotStats(gs);
            foreach (var kv in cart)
            {
                if (kv.Value <= 0) continue;
                var item = ItemDatabase.Get(kv.Key);
                string tag = item.GetDuplicateTag();
                for (int i = 0; i < kv.Value; i++)
                {
                    bool dup = gs.RegisterDuplicateUse(tag, gs.Day);
                    switch (item.Category)
                    {
                        case ItemCategory.Consumable:
                            if (item.EffectValue != 0)
                                gs.AddStat("Fatigue", -Penalized(item.EffectValue, dup)); // 회복 = 피로 감소
                            break;
                        case ItemCategory.SessionBuff:
                            if (!string.IsNullOrEmpty(item.EffectStat) && item.EffectValue != 0)
                                gs.AddStat(item.EffectStat, Penalized(item.EffectValue, dup));
                            if (!string.IsNullOrEmpty(item.SubEffectStat) && item.SubEffectValue != 0)
                                gs.AddStat(item.SubEffectStat, Penalized(item.SubEffectValue, dup)); // 복합효과(부효과)
                            break;
                        // Gift: 인벤토리(소비처 = 내러티브 Event2/3) 미구현 — 범위 밖.
                    }
                }
            }
            var changes = DiffStats(before, gs);

            return PurchaseResult.Success(total, changes);
        }

        // 전 플레이어 스탯(StatIds) 스냅샷 — 구매 효과 적용 후 변경분만 통지하기 위함.
        static int[] SnapshotStats(GameStateSO gs)
        {
            var ids = GameStateSO.StatIds;
            var snap = new int[ids.Length];
            for (int i = 0; i < ids.Length; i++) snap[i] = gs.GetStat(ids[i]);
            return snap;
        }

        // 중복 페널티 적용값. duplicate면 절반(부호 유지, 0 소멸 방지로 최소 크기 1).
        // ※ 구 ItemEffectSystem은 Mathf.Max(1, base/2)라 음수 부효과(무릎담요 Fatigue -2)에서 부호가
        //   뒤집혔다 — 여기선 부호를 유지해 효과 절반의 의도를 보존한다.
        static int Penalized(int baseEffect, bool duplicate)
        {
            if (!duplicate) return baseEffect;
            int halved = baseEffect / 2;
            if (halved == 0) halved = baseEffect > 0 ? 1 : baseEffect < 0 ? -1 : 0;
            return halved;
        }

        static StatChangedEvent[] DiffStats(int[] before, GameStateSO gs)
        {
            var ids = GameStateSO.StatIds;
            var list = new List<StatChangedEvent>();
            for (int i = 0; i < ids.Length; i++)
            {
                int after = gs.GetStat(ids[i]);
                if (after != before[i]) list.Add(new StatChangedEvent(ids[i], before[i], after));
            }
            return list.ToArray();
        }
    }
}
