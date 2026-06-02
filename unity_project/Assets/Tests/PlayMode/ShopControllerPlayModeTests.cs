using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // MoneyChangedEvent
using LoveAlgo.Shop;   // ShopController, PurchaseRequestedCommand, ItemDatabase

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// Shop 슬라이스1 PlayMode 검증: ShopController OnEnable 구독 경로 —
    /// PurchaseRequestedCommand 발행 → 순수 Purchase → MoneyChanged/ShopPurchased 통지.
    /// </summary>
    public class ShopControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator OnEnable_Subscribes_So_Purchase_Charges_And_Notifies()
        {
            var item = ItemDatabase.GetByCategory(ItemCategory.Consumable).FirstOrDefault(i => i.EffectValue > 0);
            Assert.IsNotNull(item);

            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            gs.ResetRuntime();
            gs.SetStat("Fatigue", 80);
            gs.Money = item.Price;

            var go = new GameObject("ShopController_PlayTest");
            var c = go.AddComponent<ShopController>(); // OnEnable → 구독
            c.State = gs;

            bool moneyFired = false, purchasedFired = false;
            long money = -1;
            var t1 = EventBus.Subscribe<MoneyChangedEvent>(e => { moneyFired = true; money = e.NewMoney; });
            var t2 = EventBus.Subscribe<ShopPurchasedEvent>(e => purchasedFired = true);
            try
            {
                yield return null;

                EventBus.Publish(new PurchaseRequestedCommand(new Dictionary<string, int> { { item.Id, 1 } }));

                Assert.IsTrue(moneyFired, "OnEnable 구독으로 구매 처리→MoneyChanged 발행");
                Assert.AreEqual(0, money);
                Assert.IsTrue(purchasedFired);
                Assert.AreEqual(0, gs.Money);
            }
            finally
            {
                t1.Dispose(); t2.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(gs);
            }
        }
    }
}
