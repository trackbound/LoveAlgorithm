using NUnit.Framework;
using LoveAlgo.UI; // StageView

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class StageViewKeyTests
    {
        [Test]
        public void Key_Joins_Char_And_Emote_With_Slash()
        {
            Assert.AreEqual("Roa/기본", StageView.CharSpriteKey("Roa", "기본"));
        }

        [Test]
        public void Key_Null_When_Character_Empty()
        {
            Assert.IsNull(StageView.CharSpriteKey("", "기본"));
            Assert.IsNull(StageView.CharSpriteKey(null, "기본"));
        }

        [Test]
        public void Key_CharOnly_When_Emote_Empty()
        {
            Assert.AreEqual("Roa", StageView.CharSpriteKey("Roa", ""));
        }
    }
}
