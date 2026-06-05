using NUnit.Framework;
using LoveAlgo.UI; // StageView

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// StageView.ResolveSlotForSpeaker 순수 검증: 슬롯(L/C/R)→캐릭터 배열에서 화자(직접·대소문자무시·trim)
    /// 매칭 슬롯 인덱스를 찾는다. 인라인 <c>&lt;emote&gt;</c> 화자→슬롯 해석의 핵심(별칭 정규화는 후속).
    /// </summary>
    public class StageViewResolutionTests
    {
        [Test]
        public void Matches_Slot_By_Direct_Name()
        {
            var slots = new string[] { null, "로아", null };
            Assert.AreEqual(1, StageView.ResolveSlotForSpeaker(slots, "로아"));
        }

        [Test]
        public void Case_Insensitive_And_Trimmed()
        {
            var slots = new string[] { "HaYeEun", null, null };
            Assert.AreEqual(0, StageView.ResolveSlotForSpeaker(slots, " hayeeun "));
        }

        [Test]
        public void No_Match_Returns_Negative()
        {
            var slots = new string[] { "로아", null, null };
            Assert.AreEqual(-1, StageView.ResolveSlotForSpeaker(slots, "하예은"));
        }

        [Test]
        public void Null_Or_Empty_Speaker_Returns_Negative()
        {
            var slots = new string[] { "로아", null, null };
            Assert.AreEqual(-1, StageView.ResolveSlotForSpeaker(slots, null));
            Assert.AreEqual(-1, StageView.ResolveSlotForSpeaker(slots, ""));
        }

        [Test]
        public void Empty_Slots_Returns_Negative()
        {
            var slots = new string[] { null, null, null };
            Assert.AreEqual(-1, StageView.ResolveSlotForSpeaker(slots, "로아"));
        }

        [Test]
        public void First_Match_Wins()
        {
            var slots = new string[] { "로아", "로아", null };
            Assert.AreEqual(0, StageView.ResolveSlotForSpeaker(slots, "로아"));
        }
    }
}
