using NUnit.Framework;
using LoveAlgo.Story;  // ShakeParser, ShakeIntent, ShakeStrength
using LoveAlgo.Events; // ShakeTarget, CharSlot

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="ShakeParser"/>. 흔들기 3종(Stage/Dialogue/Char)의 대상·강도(프리셋/숫자)·
    /// 지속·슬롯 파싱과, 흔들기 외 FX는 IsValid=false로 스킵 위임하는지. 매직넘버 해석은 엔진(SO) 몫이라 여기선
    /// "미지정=음수" 표식만 확인한다.
    /// </summary>
    [TestFixture]
    public class ShakeParserTests
    {
        [Test]
        public void StageShake_Bare_Defaults_To_MediumPreset_Unspecified()
        {
            var s = ShakeParser.Parse("StageShake");
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(ShakeTarget.Stage, s.Target);
            Assert.Less(s.StrengthPx, 0f);                  // 프리셋 사용(엔진이 해석)
            Assert.AreEqual(ShakeStrength.Medium, s.Preset);
            Assert.Less(s.Duration, 0f);                    // 미지정
        }

        [Test]
        public void DialogueShake_Preset_In_Pos1()
        {
            var s = ShakeParser.Parse("DialogueShake:Strong");
            Assert.AreEqual(ShakeTarget.Dialogue, s.Target);
            Assert.AreEqual(ShakeStrength.Strong, s.Preset);
            Assert.Less(s.StrengthPx, 0f);
            Assert.Less(s.Duration, 0f); // 프리셋만 줬으면 지속은 기본
        }

        [Test]
        public void StageShake_Pos1_Number_Is_Duration()
        {
            // 구 ParseShakeArgs 의미: pos1이 숫자면 지속(강도 아님).
            var s = ShakeParser.Parse("StageShake:0.5");
            Assert.AreEqual(0.5f, s.Duration, 1e-4f);
            Assert.AreEqual(ShakeStrength.Medium, s.Preset);
            Assert.Less(s.StrengthPx, 0f);
        }

        [Test]
        public void StageShake_Duration_Then_Preset()
        {
            var s = ShakeParser.Parse("StageShake:0.5:Strong");
            Assert.AreEqual(0.5f, s.Duration, 1e-4f);
            Assert.AreEqual(ShakeStrength.Strong, s.Preset);
        }

        [Test]
        public void StageShake_Duration_Then_NumericStrength()
        {
            var s = ShakeParser.Parse("StageShake:0.5:40");
            Assert.AreEqual(0.5f, s.Duration, 1e-4f);
            Assert.AreEqual(40f, s.StrengthPx, 1e-4f); // 숫자 강도 직접 지정
        }

        [Test]
        public void CharShake_Slot_Strength_Duration()
        {
            var s = ShakeParser.Parse("CharShake:L:30:0.4");
            Assert.AreEqual(ShakeTarget.Char, s.Target);
            Assert.AreEqual(CharSlot.L, s.Slot);
            Assert.AreEqual(30f, s.StrengthPx, 1e-4f);
            Assert.AreEqual(0.4f, s.Duration, 1e-4f);
        }

        [Test]
        public void CharShake_Bare_Defaults_CenterSlot_Unspecified()
        {
            var s = ShakeParser.Parse("CharShake");
            Assert.AreEqual(ShakeTarget.Char, s.Target);
            Assert.AreEqual(CharSlot.C, s.Slot);
            Assert.Less(s.StrengthPx, 0f); // 캐릭터 기본강도(엔진 해석)
            Assert.Less(s.Duration, 0f);
        }

        [Test]
        public void CharShake_SlotOmitted_NumberIsStrength()
        {
            // 슬롯 토큰이 아니면 바로 강도(숫자)로 해석, 슬롯은 기본 C.
            var s = ShakeParser.Parse("CharShake:30");
            Assert.AreEqual(CharSlot.C, s.Slot);
            Assert.AreEqual(30f, s.StrengthPx, 1e-4f);
        }

        [Test]
        public void CaseInsensitive_Heads_And_Tokens()
        {
            Assert.AreEqual(ShakeTarget.Stage, ShakeParser.Parse("stageshake").Target);
            Assert.AreEqual(ShakeStrength.Weak, ShakeParser.Parse("DIALOGUESHAKE:weak").Preset);
            Assert.AreEqual(CharSlot.R, ShakeParser.Parse("charshake:right").Slot);
        }

        [Test]
        public void CamShake_Maps_To_Stage()
        {
            // UI 무대엔 월드 카메라가 없으므로 CamShake = StageShake(콘텐츠 래퍼).
            var s = ShakeParser.Parse("CamShake:Strong");
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(ShakeTarget.Stage, s.Target);
            Assert.AreEqual(ShakeStrength.Strong, s.Preset);
        }

        [Test]
        public void NonShake_Fx_Is_Invalid()
        {
            Assert.IsFalse(ShakeParser.Parse("FadeOut").IsValid);
            Assert.IsFalse(ShakeParser.Parse("CamZoom:1.5").IsValid); // 카메라 줌/팬은 CameraParser 소관
            Assert.IsFalse(ShakeParser.Parse("ColorTint:red").IsValid);
            Assert.IsFalse(ShakeParser.Parse("").IsValid);
            Assert.IsFalse(ShakeParser.Parse(null).IsValid);
        }
    }
}
