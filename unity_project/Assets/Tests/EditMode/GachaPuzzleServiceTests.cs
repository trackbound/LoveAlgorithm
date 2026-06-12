using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.Gacha;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 가챠 순수 로직 검증 — 미보유 풀 한정 추첨(가중치 경계·자연 천장), 보유 기록 중복 무시,
    /// 완성 판정, 완성 후 추가 구매 업적 임계(+5 콜렉터/+10 마스터), 튜닝 기본값(기획 분량표).
    /// </summary>
    public class GachaPuzzleServiceTests
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
            Object.DestroyImmediate(_tuning);
            Object.DestroyImmediate(_gs);
        }

        [Test]
        public void Tuning_Defaults_Match_Plan_Distribution()
        {
            Assert.AreEqual(30, _tuning.PieceCount, "기획: 6×5 = 30조각");
            Assert.AreEqual(30, _tuning.pieceRarities.Length);
            Assert.AreEqual(5, _tuning.rarityWeights.Length);

            // 분량표: 1×10 / 2×7 / 3×6 / 4×5 / 5×2 (기획서 p42)
            int[] counts = new int[6];
            foreach (var r in _tuning.pieceRarities) counts[r]++;
            CollectionAssert.AreEqual(new[] { 0, 10, 7, 6, 5, 2 }, counts);
        }

        [Test]
        public void Draw_Picks_Only_Unowned_Pieces()
        {
            // 0~27 보유 → 남은 풀 = {28, 29} — 어떤 난수에도 그 둘만 나온다.
            for (int i = 0; i < 28; i++) GachaPuzzleService.Own(_gs, _tuning, i);

            for (float roll = 0f; roll < 1f; roll += 0.05f)
            {
                int piece = GachaPuzzleService.Draw(_gs, _tuning, roll);
                Assert.That(piece, Is.EqualTo(28).Or.EqualTo(29), $"roll={roll}");
            }
        }

        [Test]
        public void Draw_Weight_Boundaries_Are_Proportional()
        {
            // 28(레어도5, 가중치 5)·29(레어도5, 가중치 5)만 남김 → 50:50 경계 = 0.5
            for (int i = 0; i < 28; i++) GachaPuzzleService.Own(_gs, _tuning, i);
            Assert.AreEqual(28, GachaPuzzleService.Draw(_gs, _tuning, 0.49f));
            Assert.AreEqual(29, GachaPuzzleService.Draw(_gs, _tuning, 0.51f));

            // 경계 클램프 — roll 1.0/음수도 안전.
            Assert.AreEqual(29, GachaPuzzleService.Draw(_gs, _tuning, 1f));
            Assert.AreEqual(28, GachaPuzzleService.Draw(_gs, _tuning, -1f));
        }

        [Test]
        public void Draw_Rarity_Weighting_Favors_Common()
        {
            // 0(레어도1, 가중치 40) vs 29(레어도5, 가중치 5)만 남김 → 경계 = 40/45 ≈ 0.888…
            for (int i = 1; i < 29; i++) GachaPuzzleService.Own(_gs, _tuning, i);
            Assert.AreEqual(0, GachaPuzzleService.Draw(_gs, _tuning, 0.85f), "가중 40/45 미만은 흔한 조각");
            Assert.AreEqual(29, GachaPuzzleService.Draw(_gs, _tuning, 0.92f), "가중 40/45 초과는 희귀 조각");
        }

        [Test]
        public void Draw_Returns_Minus1_When_Complete()
        {
            for (int i = 0; i < 30; i++) GachaPuzzleService.Own(_gs, _tuning, i);
            Assert.IsTrue(GachaPuzzleService.IsComplete(_gs, _tuning));
            Assert.AreEqual(-1, GachaPuzzleService.Draw(_gs, _tuning, 0.5f), "완성 후 = 새 조각 없음");
        }

        [Test]
        public void Own_Dedupes_And_Rejects_OutOfRange()
        {
            Assert.IsTrue(GachaPuzzleService.Own(_gs, _tuning, 3));
            Assert.IsFalse(GachaPuzzleService.Own(_gs, _tuning, 3), "중복 무시");
            Assert.IsFalse(GachaPuzzleService.Own(_gs, _tuning, 30), "범위 밖 무시");
            Assert.IsFalse(GachaPuzzleService.Own(_gs, _tuning, -1));
            Assert.AreEqual(1, GachaPuzzleService.OwnedCount(_gs));
        }

        [Test]
        public void BonusPurchase_Sets_Achievement_Flags_At_Thresholds()
        {
            for (int i = 0; i < GachaPuzzleService.CollectorBonusCount - 1; i++)
                GachaPuzzleService.RecordBonusPurchase(_gs);
            Assert.IsFalse(_gs.GetFlag(GachaPuzzleService.CollectorFlag), "+4까지는 미달성");

            GachaPuzzleService.RecordBonusPurchase(_gs); // +5
            Assert.IsTrue(_gs.GetFlag(GachaPuzzleService.CollectorFlag), "+5 = 퍼즐 콜렉터");
            Assert.IsFalse(_gs.GetFlag(GachaPuzzleService.MasterFlag));

            for (int i = 0; i < 5; i++) GachaPuzzleService.RecordBonusPurchase(_gs); // +10
            Assert.IsTrue(_gs.GetFlag(GachaPuzzleService.MasterFlag), "+10 = 퍼즐 마스터");
            Assert.AreEqual(10, _gs.Data.gachaBonusPurchases);
        }
    }
}
