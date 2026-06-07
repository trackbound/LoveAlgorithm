using NUnit.Framework;
using LoveAlgo.Story; // VideoParser, VideoIntent

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 순수 <see cref="VideoParser"/> 검증: <c>Video:파일명[:Loop][:Skippable|:NoSkip]</c> 파싱과,
    /// 비-Video는 IsValid=false로 스킵 위임하는지. 동결 기본(Loop=false·Skippable=true)·순서무관·케이스무시 확인.
    /// </summary>
    [TestFixture]
    public class VideoParserTests
    {
        [Test]
        public void NameOnly_Defaults_Skippable_NoLoop()
        {
            var v = VideoParser.Parse("Video:roa_CG01_intro");
            Assert.IsTrue(v.IsValid);
            Assert.AreEqual("roa_CG01_intro", v.Name);
            Assert.IsFalse(v.Loop);
            Assert.IsTrue(v.Skippable, "동결 기본: 스킵 가능.");
        }

        [Test]
        public void Loop_Flag_Sets_Loop()
        {
            var v = VideoParser.Parse("Video:intro:Loop");
            Assert.IsTrue(v.Loop);
            Assert.IsTrue(v.Skippable);
        }

        [Test]
        public void NoSkip_Flag_Disables_Skippable()
        {
            var v = VideoParser.Parse("Video:intro:NoSkip");
            Assert.IsFalse(v.Skippable);
            Assert.IsFalse(v.Loop);
        }

        [Test]
        public void Flags_Are_Order_Insensitive()
        {
            var a = VideoParser.Parse("Video:intro:Loop:NoSkip");
            var b = VideoParser.Parse("Video:intro:NoSkip:Loop");
            Assert.IsTrue(a.Loop && !a.Skippable);
            Assert.IsTrue(b.Loop && !b.Skippable);
        }

        [Test]
        public void CaseInsensitive_Command_And_Flags()
        {
            var v = VideoParser.Parse("video:Clip:loop:noskip");
            Assert.IsTrue(v.IsValid);
            Assert.AreEqual("Clip", v.Name);
            Assert.IsTrue(v.Loop);
            Assert.IsFalse(v.Skippable);
        }

        [Test]
        public void Trims_Whitespace_Around_Name()
        {
            var v = VideoParser.Parse("Video:  intro_clip  ");
            Assert.AreEqual("intro_clip", v.Name);
        }

        [Test]
        public void Missing_Name_Is_Invalid()
        {
            Assert.IsFalse(VideoParser.Parse("Video").IsValid);
            Assert.IsFalse(VideoParser.Parse("Video:").IsValid);
            Assert.IsFalse(VideoParser.Parse("Video:   ").IsValid);
        }

        [Test]
        public void NonVideo_Fx_Is_Invalid()
        {
            Assert.IsFalse(VideoParser.Parse("FadeOut").IsValid);
            Assert.IsFalse(VideoParser.Parse("ColorTint:Blue").IsValid);
            Assert.IsFalse(VideoParser.Parse("").IsValid);
            Assert.IsFalse(VideoParser.Parse(null).IsValid);
        }
    }
}
