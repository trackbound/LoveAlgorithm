using NUnit.Framework;
using LoveAlgo.Story;  // FxInterpreter, ScreenFxIntent
using LoveAlgo.Events; // ScreenFxKind

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="FxInterpreter"/>. 스크린 FX(FadeOut/FadeIn/Flash)만 인식하고
    /// 범위 밖 FX(카메라/Eye/Tint/매크로)는 IsValid=false로 스킵 위임하는지. 케이스/duration 파싱 포함.
    /// </summary>
    [TestFixture]
    public class FxInterpreterTests
    {
        [Test]
        public void FadeOut_Default_Duration_Unspecified()
        {
            var fx = FxInterpreter.ParseScreen("FadeOut");
            Assert.IsTrue(fx.IsValid);
            Assert.AreEqual(ScreenFxKind.FadeOut, fx.Kind);
            Assert.Less(fx.Duration, 0f); // -1 = 기본 위임
        }

        [Test]
        public void FadeIn_WithDuration()
        {
            var fx = FxInterpreter.ParseScreen("FadeIn:1.5");
            Assert.AreEqual(ScreenFxKind.FadeIn, fx.Kind);
            Assert.AreEqual(1.5f, fx.Duration, 1e-4f);
        }

        [Test]
        public void Flash_Parsed()
        {
            var fx = FxInterpreter.ParseScreen("Flash:0.2");
            Assert.AreEqual(ScreenFxKind.Flash, fx.Kind);
            Assert.AreEqual(0.2f, fx.Duration, 1e-4f);
        }

        [Test]
        public void CaseInsensitive()
        {
            Assert.AreEqual(ScreenFxKind.FadeOut, FxInterpreter.ParseScreen("fadeout").Kind);
            Assert.AreEqual(ScreenFxKind.FadeIn, FxInterpreter.ParseScreen("FADEIN").Kind);
            Assert.AreEqual(ScreenFxKind.Flash, FxInterpreter.ParseScreen("flash").Kind);
        }

        [Test]
        public void OutOfSlice_Fx_Is_Invalid()
        {
            // 카메라/Eye/Tint/흔들기/캐릭터/매크로 = 이번 슬라이스 밖 → 엔진이 스킵.
            Assert.IsFalse(FxInterpreter.ParseScreen("CamShake:0.3").IsValid);
            Assert.IsFalse(FxInterpreter.ParseScreen("EyeOpen").IsValid);
            Assert.IsFalse(FxInterpreter.ParseScreen("ColorTint:red:0.25").IsValid);
            Assert.IsFalse(FxInterpreter.ParseScreen("DayEnd").IsValid);
            Assert.IsFalse(FxInterpreter.ParseScreen("").IsValid);
            Assert.IsFalse(FxInterpreter.ParseScreen(null).IsValid);
        }
    }
}
