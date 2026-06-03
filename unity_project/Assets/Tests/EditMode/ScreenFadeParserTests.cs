using NUnit.Framework;
using LoveAlgo.Story;  // ScreenFadeParser, ScreenFadeIntent
using LoveAlgo.Events; // ScreenFadeKind

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="ScreenFadeParser"/>. 스크린 페이드(FadeOut/FadeIn/Flash)만 인식하고
    /// 범위 밖 FX(카메라/Eye/Tint/매크로)는 IsValid=false로 위임하는지. 케이스/duration 파싱 포함.
    /// </summary>
    [TestFixture]
    public class ScreenFadeParserTests
    {
        [Test]
        public void FadeOut_Default_Duration_Unspecified()
        {
            var fx = ScreenFadeParser.Parse("FadeOut");
            Assert.IsTrue(fx.IsValid);
            Assert.AreEqual(ScreenFadeKind.FadeOut, fx.Kind);
            Assert.Less(fx.Duration, 0f); // -1 = 기본 위임
        }

        [Test]
        public void FadeIn_WithDuration()
        {
            var fx = ScreenFadeParser.Parse("FadeIn:1.5");
            Assert.AreEqual(ScreenFadeKind.FadeIn, fx.Kind);
            Assert.AreEqual(1.5f, fx.Duration, 1e-4f);
        }

        [Test]
        public void Flash_Parsed()
        {
            var fx = ScreenFadeParser.Parse("Flash:0.2");
            Assert.AreEqual(ScreenFadeKind.Flash, fx.Kind);
            Assert.AreEqual(0.2f, fx.Duration, 1e-4f);
        }

        [Test]
        public void CaseInsensitive()
        {
            Assert.AreEqual(ScreenFadeKind.FadeOut, ScreenFadeParser.Parse("fadeout").Kind);
            Assert.AreEqual(ScreenFadeKind.FadeIn, ScreenFadeParser.Parse("FADEIN").Kind);
            Assert.AreEqual(ScreenFadeKind.Flash, ScreenFadeParser.Parse("flash").Kind);
        }

        [Test]
        public void OutOfFamily_Fx_Is_Invalid()
        {
            // 카메라/Eye/Tint/흔들기/캐릭터/매크로 = 이 family 밖 → 엔진이 다른 파서로 위임.
            Assert.IsFalse(ScreenFadeParser.Parse("CamShake:0.3").IsValid);
            Assert.IsFalse(ScreenFadeParser.Parse("EyeOpen").IsValid);
            Assert.IsFalse(ScreenFadeParser.Parse("ColorTint:red:0.25").IsValid);
            Assert.IsFalse(ScreenFadeParser.Parse("DayEnd").IsValid);
            Assert.IsFalse(ScreenFadeParser.Parse("").IsValid);
            Assert.IsFalse(ScreenFadeParser.Parse(null).IsValid);
        }
    }
}
