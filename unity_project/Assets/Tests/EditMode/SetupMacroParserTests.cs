using NUnit.Framework;
using LoveAlgo.Story; // SetupMacroParser, WaitMacroParser, SetupIntent

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// FX 매크로 순수 파서 검증: <see cref="SetupMacroParser"/>(BG/BGM/Char[:slot]/Overlay/Eye 분해, 순서무관·
    /// 케이스무시·빈값무시·head 검사) + <see cref="WaitMacroParser"/>(생략 시 1.0s, 음수 무시, 비-Wait=false).
    /// </summary>
    [TestFixture]
    public class SetupMacroParserTests
    {
        // ── Setup ──

        [Test]
        public void Setup_Parses_All_Fields()
        {
            var s = SetupMacroParser.Parse("Setup:BG=bg_60_01|BGM=로아|Char=로아:C|Overlay=비|Eye=Close");
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual("bg_60_01", s.Bg);
            Assert.AreEqual("로아", s.Bgm);
            Assert.AreEqual("로아", s.CharName);
            Assert.AreEqual("C", s.CharSlot);
            Assert.AreEqual("비", s.Overlay);
            Assert.AreEqual("Close", s.Eye);
        }

        [Test]
        public void Setup_Char_Without_Slot_Leaves_Slot_Null()
        {
            var s = SetupMacroParser.Parse("Setup:Char=로아");
            Assert.AreEqual("로아", s.CharName);
            Assert.IsNull(s.CharSlot);
        }

        [Test]
        public void Setup_BG_Only_With_Spaces_Is_Valid()
        {
            var s = SetupMacroParser.Parse("Setup:BG=빈 화면");
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual("빈 화면", s.Bg);
            Assert.IsNull(s.Bgm);
            Assert.IsNull(s.CharName);
        }

        [Test]
        public void Setup_Order_Independent_And_Case_Insensitive_Keys()
        {
            var s = SetupMacroParser.Parse("Setup:bgm=로아|bg=캠퍼스");
            Assert.AreEqual("캠퍼스", s.Bg);
            Assert.AreEqual("로아", s.Bgm);
        }

        [Test]
        public void Setup_Empty_Value_Segment_Ignored()
        {
            var s = SetupMacroParser.Parse("Setup:BG=|BGM=로아");
            Assert.IsNull(s.Bg);
            Assert.AreEqual("로아", s.Bgm);
        }

        [Test]
        public void Setup_Non_Setup_Head_Is_Invalid()
        {
            Assert.IsFalse(SetupMacroParser.Parse("FadeOut:1").IsValid);
            Assert.IsFalse(SetupMacroParser.Parse("Wait:1").IsValid);
            Assert.IsFalse(SetupMacroParser.Parse("").IsValid);
        }

        // ── Wait ──

        [Test]
        public void Wait_Default_When_No_Arg()
        {
            Assert.IsTrue(WaitMacroParser.TryParse("Wait", out float s));
            Assert.AreEqual(1.0f, s, 1e-4f);
        }

        [Test]
        public void Wait_Explicit_Seconds()
        {
            Assert.IsTrue(WaitMacroParser.TryParse("Wait:2.5", out float s));
            Assert.AreEqual(2.5f, s, 1e-4f);
        }

        [Test]
        public void Wait_Negative_Falls_Back_To_Default()
        {
            Assert.IsTrue(WaitMacroParser.TryParse("Wait:-3", out float s));
            Assert.AreEqual(WaitMacroParser.DefaultSeconds, s, 1e-4f);
        }

        [Test]
        public void Wait_Non_Wait_Head_Is_False()
        {
            Assert.IsFalse(WaitMacroParser.TryParse("Setup:BG=x", out _));
            Assert.IsFalse(WaitMacroParser.TryParse("FadeIn", out _));
            Assert.IsFalse(WaitMacroParser.TryParse("", out _));
        }
    }
}
