using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;
using LoveAlgo.Events;
using LoveAlgo.Gacha;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 가챠 어댑터 PlayMode — 구매 진입 시 즉시 추첨·기록·결과 통지(뷰는 연출만),
    /// 현황 보기는 무추첨, 완성 후 구매는 보너스(연출만)+업적 카운트.
    /// </summary>
    public class GachaControllerPlayModeTests
    {
        GameStateSO _gs;
        GachaTuningSO _tuning;

        [SetUp]
        public void SetUp()
        {
            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _tuning = ScriptableObject.CreateInstance<GachaTuningSO>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_tuning);
            UnityEngine.Object.DestroyImmediate(_gs);
        }

        GachaController Build(out GameObject go, Func<float> roll)
        {
            go = new GameObject("GachaController");
            go.SetActive(false);
            var controller = go.AddComponent<GachaController>();
            controller.State = _gs;
            controller.Tuning = _tuning;
            controller.RollSource = roll;
            go.SetActive(true);
            return controller;
        }

        [UnityTest]
        public IEnumerator Purchase_Open_Draws_Records_And_Publishes()
        {
            Build(out var go, () => 0f); // 결정적 — 풀 첫 조각
            yield return null;

            var results = new List<GachaDrawResultEvent>();
            using var sub = EventBus.Subscribe<GachaDrawResultEvent>(e => results.Add(e));
            try
            {
                EventBus.Publish(new OpenGachaCommand(fromPurchase: true));

                Assert.AreEqual(1, results.Count, "결과 통지 1건");
                Assert.AreEqual(0, results[0].PieceIndex, "roll 0 = 첫 미보유 조각");
                Assert.IsFalse(results[0].IsBonus);
                Assert.AreEqual(1, results[0].OwnedCount);
                Assert.IsTrue(GachaPuzzleService.IsOwned(_gs, 0), "연출 전 상태 확정(크래시 안전)");

                EventBus.Publish(new OpenGachaCommand(fromPurchase: false)); // 현황 보기
                Assert.AreEqual(1, results.Count, "현황 보기는 추첨 없음");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Complete_Then_Purchase_Is_Bonus_With_Achievement()
        {
            for (int i = 0; i < 30; i++) GachaPuzzleService.Own(_gs, _tuning, i); // 완성 상태
            _gs.Data.gachaBonusPurchases = GachaPuzzleService.CollectorBonusCount - 1; // 다음이 +5

            Build(out var go, () => 0.5f);
            yield return null;

            var results = new List<GachaDrawResultEvent>();
            using var sub = EventBus.Subscribe<GachaDrawResultEvent>(e => results.Add(e));
            try
            {
                EventBus.Publish(new OpenGachaCommand(fromPurchase: true));

                Assert.AreEqual(1, results.Count);
                Assert.IsTrue(results[0].IsBonus, "완성 후 구매 = 보너스(새 조각 없음)");
                Assert.AreEqual(-1, results[0].PieceIndex);
                Assert.IsTrue(results[0].IsComplete);
                Assert.AreEqual(30, results[0].OwnedCount, "보유 수 불변");
                Assert.IsTrue(_gs.GetFlag(GachaPuzzleService.CollectorFlag), "+5 도달 = 퍼즐 콜렉터 호칭 플래그");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
