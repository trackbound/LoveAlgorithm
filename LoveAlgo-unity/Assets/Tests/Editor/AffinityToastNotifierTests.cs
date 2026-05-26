using NUnit.Framework;
using LoveAlgo.Modules.Affinity;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D2 등급 상승 토스트 — 라벨 매핑이 모든 AffinityTier에 대해 비어있지 않은지 검증.
    /// 새 Tier 추가 시 메시지 안 채워두면 여기서 즉시 잡힘.
    /// </summary>
    [TestFixture]
    public class AffinityToastNotifierTests
    {
        [Test]
        public void TierUpMessage_HasNonEmptyLabelForEveryTier()
        {
            foreach (AffinityTier tier in System.Enum.GetValues(typeof(AffinityTier)))
            {
                string msg = AffinityToastNotifier.TierUpMessage(tier);
                Assert.IsFalse(string.IsNullOrWhiteSpace(msg),
                    $"AffinityTier.{tier} 에 대한 등급 상승 메시지가 비어 있음");
            }
        }

        [Test]
        public void TierUpMessage_LoveTier_HasRomanticPhrasing()
        {
            // Love 등급은 단순한 친구 표현이 아니라야 함 — 회귀 방지.
            string love = AffinityToastNotifier.TierUpMessage(AffinityTier.Love);
            Assert.IsNotNull(love);
            StringAssert.DoesNotContain("친구", love, "Love 등급 메시지가 친구 표현으로 회귀");
        }
    }
}
