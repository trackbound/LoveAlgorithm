using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // MoneyChangedEvent, StatChangedEvent
using LoveAlgo.Shop;   // ShopService, ShopController, ItemDatabase

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// Shop 슬라이스1 검증: 순수 <see cref="ShopService.Purchase"/>(자금 게이트·소지금 차감·Consumable 즉시
    /// 피로 회복) + 어댑터 <see cref="ShopController"/>(Money/Stat/Purchased/Rejected 발행).
    /// 카탈로그(에셋/폴백) 값에 무관하도록 실제 ItemData에서 Price/EffectValue를 읽어 단언한다.
    /// </summary>
    [TestFixture]
    public class ShopServiceTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        static ItemData FirstConsumable()
            => ItemDatabase.GetByCategory(ItemCategory.Consumable).FirstOrDefault(i => i.EffectValue > 0);

        static Dictionary<string, int> Cart(string id, int qty) => new() { { id, qty } };

        [Test]
        public void Purchase_Consumable_Deducts_Money_And_Recovers_Fatigue()
        {
            var item = FirstConsumable();
            Assert.IsNotNull(item, "Consumable 아이템이 카탈로그/폴백에 존재해야 함");

            var gs = MakeState();
            try
            {
                gs.SetStat("Fatigue", 80);
                gs.Money = item.Price + 1000;

                var r = ShopService.Purchase(gs, Cart(item.Id, 1));

                Assert.IsTrue(r.Ok);
                Assert.AreEqual(item.Price, r.TotalCost);
                Assert.AreEqual(1000, gs.Money, "소지금 = 잔액");
                Assert.AreEqual(80 - item.EffectValue, gs.GetStat("Fatigue"), "피로 회복(감소)");
                Assert.AreEqual(1, r.StatChanges.Length);
                Assert.AreEqual("Fatigue", r.StatChanges[0].StatId);
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Purchase_InsufficientFunds_Rejected_NoChange()
        {
            var item = FirstConsumable();
            var gs = MakeState();
            try
            {
                gs.SetStat("Fatigue", 80);
                gs.Money = item.Price - 1;

                var r = ShopService.Purchase(gs, Cart(item.Id, 1));

                Assert.IsFalse(r.Ok);
                Assert.AreEqual(ShopRejection.InsufficientFunds, r.Reason);
                Assert.AreEqual(item.Price - 1, gs.Money, "거부 시 소지금 불변");
                Assert.AreEqual(80, gs.GetStat("Fatigue"), "거부 시 스탯 불변");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Purchase_UnknownItem_Rejected()
        {
            var gs = MakeState();
            try
            {
                gs.Money = 999999;
                var r = ShopService.Purchase(gs, Cart("___nope___", 1));
                Assert.IsFalse(r.Ok);
                Assert.AreEqual(ShopRejection.ItemNotFound, r.Reason);
                Assert.AreEqual(999999, gs.Money);
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Purchase_EmptyCart_Rejected()
        {
            var gs = MakeState();
            try
            {
                var r = ShopService.Purchase(gs, new Dictionary<string, int>());
                Assert.IsFalse(r.Ok);
                Assert.AreEqual(ShopRejection.EmptyCart, r.Reason);
            }
            finally { Object.DestroyImmediate(gs); }
        }

        // ── 어댑터 ShopController ──

        static ShopController MakeController(GameStateSO state, out GameObject go)
        {
            go = new GameObject("ShopController_Test");
            var c = go.AddComponent<ShopController>();
            c.State = state;
            return c;
        }

        [Test]
        public void Controller_Publishes_Money_Stat_Purchased_On_Success()
        {
            var item = FirstConsumable();
            var gs = MakeState();
            GameObject go = null;
            bool moneyFired = false, purchasedFired = false; int statCount = 0;
            long money = -1, cost = -1;
            var t1 = EventBus.Subscribe<MoneyChangedEvent>(e => { moneyFired = true; money = e.NewMoney; });
            var t2 = EventBus.Subscribe<StatChangedEvent>(e => statCount++);
            var t3 = EventBus.Subscribe<ShopPurchasedEvent>(e => { purchasedFired = true; cost = e.TotalCost; });
            try
            {
                gs.SetStat("Fatigue", 80);
                gs.Money = item.Price;
                var c = MakeController(gs, out go);
                c.HandlePurchase(new PurchaseRequestedCommand(Cart(item.Id, 1)));

                Assert.IsTrue(moneyFired); Assert.AreEqual(0, money);
                Assert.IsTrue(purchasedFired); Assert.AreEqual(item.Price, cost);
                Assert.AreEqual(1, statCount, "Consumable 피로 변화 1건");
            }
            finally
            {
                t1.Dispose(); t2.Dispose(); t3.Dispose();
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(gs);
            }
        }

        [Test]
        public void Controller_Publishes_Rejected_On_InsufficientFunds()
        {
            var item = FirstConsumable();
            var gs = MakeState();
            GameObject go = null;
            bool rejectedFired = false, moneyFired = false;
            ShopRejection reason = ShopRejection.None;
            var t1 = EventBus.Subscribe<ShopRejectedEvent>(e => { rejectedFired = true; reason = e.Reason; });
            var t2 = EventBus.Subscribe<MoneyChangedEvent>(e => moneyFired = true);
            try
            {
                gs.Money = 0;
                var c = MakeController(gs, out go);
                c.HandlePurchase(new PurchaseRequestedCommand(Cart(item.Id, 1)));

                Assert.IsTrue(rejectedFired);
                Assert.AreEqual(ShopRejection.InsufficientFunds, reason);
                Assert.IsFalse(moneyFired, "거부 시 소지금 통지 없음");
            }
            finally
            {
                t1.Dispose(); t2.Dispose();
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(gs);
            }
        }

        // ── 슬라이스2: SessionBuff 즉시가산 + 중복 50% 페널티 ──

        static ItemData FirstSessionBuff()
            => ItemDatabase.GetByCategory(ItemCategory.SessionBuff)
                .FirstOrDefault(i => !string.IsNullOrEmpty(i.EffectStat) && i.EffectValue != 0);

        static ItemData CompositeSessionBuff()
            => ItemDatabase.GetByCategory(ItemCategory.SessionBuff)
                .FirstOrDefault(i => !string.IsNullOrEmpty(i.SubEffectStat) && i.SubEffectValue != 0
                                  && !string.IsNullOrEmpty(i.EffectStat) && i.EffectStat != i.SubEffectStat);

        // dup 2회차 기대값(부호 유지 절반, 0 소멸 방지) — Penalized와 동일 규칙.
        static int ExpectPenalized(int baseEffect)
        {
            int h = baseEffect / 2;
            if (h == 0) h = baseEffect > 0 ? 1 : baseEffect < 0 ? -1 : 0;
            return h;
        }

        [Test]
        public void Purchase_SessionBuff_Immediately_AddsMainStat()
        {
            var item = FirstSessionBuff();
            Assert.IsNotNull(item, "SessionBuff 아이템이 카탈로그/폴백에 존재해야 함");
            var gs = MakeState();
            try
            {
                gs.Money = item.Price;
                int before = gs.GetStat(item.EffectStat);
                var r = ShopService.Purchase(gs, Cart(item.Id, 1));
                Assert.IsTrue(r.Ok);
                Assert.AreEqual(before + item.EffectValue, gs.GetStat(item.EffectStat), "주효과 즉시 가산(1개는 페널티 없음)");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        public void Purchase_CompositeSessionBuff_Applies_Main_And_Sub()
        {
            var item = CompositeSessionBuff();
            if (item == null)
                Assert.Ignore("현 카탈로그(ItemCatalog.asset)에 복합효과 SessionBuff 없음 — SO 데이터 보강 시 자동 활성. 복합효과(주+부) 처리 코드는 ShopService.Purchase에 존재.");

            var gs = MakeState();
            try
            {
                gs.SetStat("Fatigue", 50); // 부효과(피로 감소) 여유
                gs.Money = item.Price;
                int mainBefore = gs.GetStat(item.EffectStat);
                int subBefore = gs.GetStat(item.SubEffectStat);
                var r = ShopService.Purchase(gs, Cart(item.Id, 1));
                Assert.IsTrue(r.Ok);
                Assert.AreEqual(mainBefore + item.EffectValue, gs.GetStat(item.EffectStat), "주효과");
                Assert.AreEqual(subBefore + item.SubEffectValue, gs.GetStat(item.SubEffectStat), "부효과(부호 그대로)");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Purchase_SameTag_SecondTime_SameDay_Halved()
        {
            var item = FirstSessionBuff();
            var gs = MakeState();
            try
            {
                gs.Money = item.Price * 2;
                int before = gs.GetStat(item.EffectStat);
                ShopService.Purchase(gs, Cart(item.Id, 2)); // 1번째 base, 2번째 절반
                int expected = before + item.EffectValue + ExpectPenalized(item.EffectValue);
                Assert.AreEqual(expected, gs.GetStat(item.EffectStat), "같은 날 2회차부터 중복 50% 페널티");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void Purchase_NewDay_Resets_DuplicatePenalty()
        {
            var item = FirstSessionBuff();
            var gs = MakeState();
            try
            {
                gs.Money = item.Price * 2;
                gs.Day = 1;
                ShopService.Purchase(gs, Cart(item.Id, 1));
                int afterDay1 = gs.GetStat(item.EffectStat);

                gs.Day = 2; // 날짜 변경 → 카운트 리셋
                ShopService.Purchase(gs, Cart(item.Id, 1));
                Assert.AreEqual(afterDay1 + item.EffectValue, gs.GetStat(item.EffectStat), "날짜 바뀌면 다시 base(페널티 리셋)");
            }
            finally { Object.DestroyImmediate(gs); }
        }
    }
}
