using NUnit.Framework;
using LoveAlgo.UI; // DialogueView

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// step 3 검증: 순수 <see cref="DialogueView.ShouldTypeBlip"/>. 타이핑 블립 스로틀(stride 글자마다 1회)·
    /// 공백 제외·경계 가드. 발행/재생(EventBus·AudioManager)은 범위 밖 — 여기선 "낼지 말지" 결정만.
    /// </summary>
    [TestFixture]
    public class DialogueTypeBlipTests
    {
        [Test]
        public void Stride1_Every_NonSpace_Char()
        {
            Assert.IsTrue(DialogueView.ShouldTypeBlip("abc", 1, 1));
            Assert.IsTrue(DialogueView.ShouldTypeBlip("abc", 2, 1));
            Assert.IsTrue(DialogueView.ShouldTypeBlip("abc", 3, 1));
        }

        [Test]
        public void Stride2_Every_Second_Char()
        {
            Assert.IsFalse(DialogueView.ShouldTypeBlip("abcd", 1, 2));
            Assert.IsTrue(DialogueView.ShouldTypeBlip("abcd", 2, 2));
            Assert.IsFalse(DialogueView.ShouldTypeBlip("abcd", 3, 2));
            Assert.IsTrue(DialogueView.ShouldTypeBlip("abcd", 4, 2));
        }

        [Test]
        public void Whitespace_Skipped()
        {
            // "a b": text[1]=' ' → i=2는 무음, i=1('a')·i=3('b')는 발행.
            Assert.IsTrue(DialogueView.ShouldTypeBlip("a b", 1, 1));
            Assert.IsFalse(DialogueView.ShouldTypeBlip("a b", 2, 1));
            Assert.IsTrue(DialogueView.ShouldTypeBlip("a b", 3, 1));
        }

        [Test]
        public void Bounds_And_Empty_Guarded()
        {
            Assert.IsFalse(DialogueView.ShouldTypeBlip("abc", 0, 1)); // 아무것도 표시 전
            Assert.IsFalse(DialogueView.ShouldTypeBlip("abc", 4, 1)); // i > 길이
            Assert.IsFalse(DialogueView.ShouldTypeBlip("", 1, 1));    // 빈 텍스트
            Assert.IsFalse(DialogueView.ShouldTypeBlip(null, 1, 1));  // null
        }

        [Test]
        public void Stride_Below_One_Treated_As_One()
        {
            Assert.IsTrue(DialogueView.ShouldTypeBlip("ab", 1, 0));
            Assert.IsTrue(DialogueView.ShouldTypeBlip("ab", 2, -5));
        }
    }
}
