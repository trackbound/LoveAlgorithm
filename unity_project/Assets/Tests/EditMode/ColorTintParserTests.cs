using NUnit.Framework;
using LoveAlgo.Story; // ColorTintParser, ColorTintIntent, TintPreset

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="ColorTintParser"/>. 프리셋·Clear·알파·지속 파싱과, 틴트 외 FX는
    /// IsValid=false로 스킵 위임하는지. 색 RGB/기본값 해석은 엔진(SO) 몫이라 여기선 프리셋 식별·"미지정=음수"만 확인.
    /// </summary>
    [TestFixture]
    public class ColorTintParserTests
    {
        [Test]
        public void Preset_Alpha_Duration()
        {
            var c = ColorTintParser.Parse("ColorTint:Sepia:0.3:0.5");
            Assert.IsTrue(c.IsValid);
            Assert.IsFalse(c.IsClear);
            Assert.AreEqual(TintPreset.Sepia, c.Preset);
            Assert.AreEqual(0.3f, c.Alpha, 1e-4f);
            Assert.AreEqual(0.5f, c.Duration, 1e-4f);
        }

        [Test]
        public void Preset_Only_Defaults_Unspecified()
        {
            var c = ColorTintParser.Parse("ColorTint:Blue");
            Assert.AreEqual(TintPreset.Blue, c.Preset);
            Assert.IsFalse(c.IsClear);
            Assert.Less(c.Alpha, 0f);    // 알파 미지정 → 엔진 동결값
            Assert.Less(c.Duration, 0f);
        }

        [Test]
        public void Clear_Sets_IsClear()
        {
            var c = ColorTintParser.Parse("ColorTint:Clear");
            Assert.IsTrue(c.IsValid);
            Assert.IsTrue(c.IsClear);
        }

        [Test]
        public void Clear_With_Duration_DoubleColon()
        {
            // 구 문법: ColorTint:Clear::dur (alpha 자리 비움).
            var c = ColorTintParser.Parse("ColorTint:Clear::0.3");
            Assert.IsTrue(c.IsClear);
            Assert.AreEqual(0.3f, c.Duration, 1e-4f);
        }

        [Test]
        public void Unknown_Preset_Treated_As_Clear()
        {
            // 구 ParseTintColor: 미지정 프리셋 = Color.clear → 해제로 간주.
            var c = ColorTintParser.Parse("ColorTint:Magenta");
            Assert.IsTrue(c.IsValid);
            Assert.IsTrue(c.IsClear);
        }

        [Test]
        public void AllPresets_Recognized_CaseInsensitive()
        {
            Assert.AreEqual(TintPreset.Sepia, ColorTintParser.Parse("colortint:sepia").Preset);
            Assert.AreEqual(TintPreset.Red, ColorTintParser.Parse("COLORTINT:RED").Preset);
            Assert.AreEqual(TintPreset.Pink, ColorTintParser.Parse("ColorTint:Pink").Preset);
            Assert.AreEqual(TintPreset.Green, ColorTintParser.Parse("ColorTint:green").Preset);
            Assert.AreEqual(TintPreset.Sunset, ColorTintParser.Parse("ColorTint:Sunset").Preset);
        }

        [Test]
        public void NonTint_Fx_Is_Invalid()
        {
            Assert.IsFalse(ColorTintParser.Parse("FadeOut").IsValid);
            Assert.IsFalse(ColorTintParser.Parse("CamZoom:1.5").IsValid);
            Assert.IsFalse(ColorTintParser.Parse("StageShake").IsValid);
            Assert.IsFalse(ColorTintParser.Parse("").IsValid);
            Assert.IsFalse(ColorTintParser.Parse(null).IsValid);
        }
    }
}
