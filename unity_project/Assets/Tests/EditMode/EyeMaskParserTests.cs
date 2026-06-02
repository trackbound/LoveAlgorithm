using NUnit.Framework;
using LoveAlgo.Story;  // EyeMaskParser, EyeMaskIntent
using LoveAlgo.Events; // EyeMaskAction

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="EyeMaskParser"/>. EyeClose/EyeOpen/EyeCloseImmediate/EyeBlink의 동작·지속
    /// 파싱과, 아이마스크 외 FX는 IsValid=false로 스킵 위임하는지. 지속 해석(동결값)은 엔진 몫이라 "미지정=음수"만 확인.
    /// </summary>
    [TestFixture]
    public class EyeMaskParserTests
    {
        [Test]
        public void EyeClose_Bare_Unspecified_Duration()
        {
            var e = EyeMaskParser.Parse("EyeClose");
            Assert.IsTrue(e.IsValid);
            Assert.AreEqual(EyeMaskAction.Close, e.Action);
            Assert.Less(e.CloseDuration, 0f);
        }

        [Test]
        public void EyeClose_With_Duration()
        {
            var e = EyeMaskParser.Parse("EyeClose:1.2");
            Assert.AreEqual(EyeMaskAction.Close, e.Action);
            Assert.AreEqual(1.2f, e.CloseDuration, 1e-4f);
        }

        [Test]
        public void EyeOpen_With_Duration()
        {
            var e = EyeMaskParser.Parse("EyeOpen:0.6");
            Assert.AreEqual(EyeMaskAction.Open, e.Action);
            Assert.AreEqual(0.6f, e.OpenDuration, 1e-4f);
        }

        [Test]
        public void EyeCloseImmediate_Parsed()
        {
            var e = EyeMaskParser.Parse("EyeCloseImmediate");
            Assert.AreEqual(EyeMaskAction.CloseImmediate, e.Action);
            Assert.IsTrue(e.IsValid);
        }

        [Test]
        public void EyeBlink_Close_Open_Hold()
        {
            var e = EyeMaskParser.Parse("EyeBlink:0.12:0.18:0.06");
            Assert.AreEqual(EyeMaskAction.Blink, e.Action);
            Assert.AreEqual(0.12f, e.CloseDuration, 1e-4f);
            Assert.AreEqual(0.18f, e.OpenDuration, 1e-4f);
            Assert.AreEqual(0.06f, e.HoldDuration, 1e-4f);
        }

        [Test]
        public void EyeBlink_Bare_All_Unspecified()
        {
            var e = EyeMaskParser.Parse("EyeBlink");
            Assert.AreEqual(EyeMaskAction.Blink, e.Action);
            Assert.Less(e.CloseDuration, 0f);
            Assert.Less(e.OpenDuration, 0f);
            Assert.Less(e.HoldDuration, 0f);
        }

        [Test]
        public void CaseInsensitive()
        {
            Assert.AreEqual(EyeMaskAction.Close, EyeMaskParser.Parse("eyeclose").Action);
            Assert.AreEqual(EyeMaskAction.Open, EyeMaskParser.Parse("EYEOPEN").Action);
            Assert.AreEqual(EyeMaskAction.CloseImmediate, EyeMaskParser.Parse("eyecloseimmediate").Action);
            Assert.AreEqual(EyeMaskAction.Blink, EyeMaskParser.Parse("EyeBlink").Action);
        }

        [Test]
        public void NonEye_Fx_Is_Invalid()
        {
            Assert.IsFalse(EyeMaskParser.Parse("FadeOut").IsValid);
            Assert.IsFalse(EyeMaskParser.Parse("ColorTint:Red").IsValid);
            Assert.IsFalse(EyeMaskParser.Parse("CamZoom:1.5").IsValid);
            Assert.IsFalse(EyeMaskParser.Parse("").IsValid);
            Assert.IsFalse(EyeMaskParser.Parse(null).IsValid);
        }
    }
}
