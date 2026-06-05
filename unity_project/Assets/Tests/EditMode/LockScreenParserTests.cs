using NUnit.Framework;
using LoveAlgo.Story;  // LockScreenParser, LockScreenIntent
using LoveAlgo.Events; // LockMode

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 잠금화면 Flow 순수 파서 검증: <see cref="LockScreenParser"/>. mode·FadeOut·Time= 추출(Time의 ':'
    /// split 결합 포함), 비-LockScreen/미지 mode → IsValid=false.
    /// </summary>
    [TestFixture]
    public class LockScreenParserTests
    {
        [Test]
        public void FirstSetup_With_FadeOut()
        {
            var r = LockScreenParser.Parse("LockScreen:FirstSetup:FadeOut");
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(LockMode.FirstSetup, r.Mode);
            Assert.IsTrue(r.FadeOut);
            Assert.IsNull(r.TimeOverride);
        }

        [Test]
        public void Mode_Only_Defaults_NoFadeOut()
        {
            var r = LockScreenParser.Parse("LockScreen:FirstSetup");
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(LockMode.FirstSetup, r.Mode);
            Assert.IsFalse(r.FadeOut);
        }

        [Test]
        public void Modes_Parsed_Case_Insensitive()
        {
            Assert.AreEqual(LockMode.Normal, LockScreenParser.Parse("lockscreen:normal").Mode);
            Assert.AreEqual(LockMode.Auto, LockScreenParser.Parse("LockScreen:AUTO").Mode);
            Assert.AreEqual(LockMode.GameStart, LockScreenParser.Parse("LockScreen:GameStart").Mode);
            Assert.AreEqual(LockMode.Reset, LockScreenParser.Parse("LockScreen:Reset").Mode);
        }

        [Test]
        public void Time_With_Colon_Is_Rejoined()
        {
            var r = LockScreenParser.Parse("LockScreen:Normal:Time=07:30:FadeOut");
            Assert.AreEqual(LockMode.Normal, r.Mode);
            Assert.AreEqual("07:30", r.TimeOverride, "':' split된 분(mm)을 다시 합쳐야 함");
            Assert.IsTrue(r.FadeOut);
        }

        [Test]
        public void Invalid_When_Not_LockScreen_Or_Unknown_Mode()
        {
            Assert.IsFalse(LockScreenParser.Parse("Setup:BG=x").IsValid);
            Assert.IsFalse(LockScreenParser.Parse("LockScreen").IsValid, "mode 누락");
            Assert.IsFalse(LockScreenParser.Parse("LockScreen:Nonsense").IsValid, "미지 mode");
            Assert.IsFalse(LockScreenParser.Parse("").IsValid);
        }
    }
}
