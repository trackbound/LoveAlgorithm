using NUnit.Framework;
using LoveAlgo.Story; // SoundInterpreter, SoundIntent, SoundCategory

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="SoundInterpreter"/>. Sound Value 문법을 인텐트로 분해하는지
    /// (BGM/SFX/Voice 카테고리·Stop·Fade, 케이스 무시, 형식오류→IsValid=false).
    /// </summary>
    [TestFixture]
    public class SoundInterpreterTests
    {
        [Test]
        public void Bgm_Play_NoFade_Uses_Default()
        {
            var s = SoundInterpreter.Parse("BGM:white_noise");
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(SoundCategory.Bgm, s.Category);
            Assert.AreEqual("white_noise", s.Name);
            Assert.IsFalse(s.IsStop);
            Assert.Less(s.Fade, 0f);
        }

        [Test]
        public void Bgm_Play_WithFade()
        {
            var s = SoundInterpreter.Parse("BGM:theme:Fade:1.5");
            Assert.AreEqual("theme", s.Name);
            Assert.AreEqual(1.5f, s.Fade, 1e-4f);
        }

        [Test]
        public void Bgm_Stop_And_Stop_WithFade()
        {
            var stop = SoundInterpreter.Parse("BGM:Stop");
            Assert.IsTrue(stop.IsValid);
            Assert.AreEqual(SoundCategory.Bgm, stop.Category);
            Assert.IsTrue(stop.IsStop);
            Assert.IsNull(stop.Name);

            var stopFade = SoundInterpreter.Parse("bgm:stop:fade:2");
            Assert.IsTrue(stopFade.IsStop);
            Assert.AreEqual(2f, stopFade.Fade, 1e-4f);
        }

        [Test]
        public void Sfx_Plays_By_Name()
        {
            var s = SoundInterpreter.Parse("SFX:click");
            Assert.AreEqual(SoundCategory.Sfx, s.Category);
            Assert.AreEqual("click", s.Name);
            Assert.IsFalse(s.IsStop);
        }

        [Test]
        public void Sfx_Stop_Is_Invalid()
        {
            // SFX는 정지 개념이 없다.
            Assert.IsFalse(SoundInterpreter.Parse("SFX:Stop").IsValid);
        }

        [Test]
        public void Voice_Play_And_Stop()
        {
            var play = SoundInterpreter.Parse("Voice:roa_01");
            Assert.AreEqual(SoundCategory.Voice, play.Category);
            Assert.AreEqual("roa_01", play.Name);
            Assert.IsFalse(play.IsStop);

            var stop = SoundInterpreter.Parse("Voice:Stop");
            Assert.IsTrue(stop.IsStop);
        }

        [Test]
        public void CaseInsensitive_Category()
        {
            Assert.AreEqual(SoundCategory.Bgm, SoundInterpreter.Parse("bgm:x").Category);
            Assert.AreEqual(SoundCategory.Sfx, SoundInterpreter.Parse("sfx:x").Category);
            Assert.AreEqual(SoundCategory.Voice, SoundInterpreter.Parse("VOICE:x").Category);
        }

        [Test]
        public void Invalid_Inputs()
        {
            Assert.IsFalse(SoundInterpreter.Parse("").IsValid);
            Assert.IsFalse(SoundInterpreter.Parse(null).IsValid);
            Assert.IsFalse(SoundInterpreter.Parse("BGM").IsValid);       // 이름 없음
            Assert.IsFalse(SoundInterpreter.Parse("Music:x").IsValid);   // 미지원 카테고리
        }
    }
}
