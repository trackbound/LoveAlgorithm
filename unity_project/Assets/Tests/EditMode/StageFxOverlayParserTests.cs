using NUnit.Framework;
using LoveAlgo.Story; // StageFxOverlayParser, StageFxIntent

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 순수 <see cref="StageFxOverlayParser"/> 검증: <c>StageFx:파일명[:Loop]</c> 파싱과,
    /// 비-StageFx는 IsValid=false로 스킵 위임하는지. 기본 Loop=false·케이스무시·trim 확인.
    /// </summary>
    [TestFixture]
    public class StageFxOverlayParserTests
    {
        [Test]
        public void NameOnly_Defaults_NoLoop()
        {
            var v = StageFxOverlayParser.Parse("StageFx:서류정리");
            Assert.IsTrue(v.IsValid);
            Assert.AreEqual("서류정리", v.Name);
            Assert.IsFalse(v.Loop);
        }

        [Test]
        public void Loop_Flag_Sets_Loop()
        {
            var v = StageFxOverlayParser.Parse("StageFx:서류정리:Loop");
            Assert.IsTrue(v.IsValid);
            Assert.IsTrue(v.Loop);
        }

        [Test]
        public void CaseInsensitive_Command_And_Flag()
        {
            var v = StageFxOverlayParser.Parse("stagefx:Clip:loop");
            Assert.IsTrue(v.IsValid);
            Assert.AreEqual("Clip", v.Name);
            Assert.IsTrue(v.Loop);
        }

        [Test]
        public void Trims_Whitespace_Around_Name()
        {
            var v = StageFxOverlayParser.Parse("StageFx:  서류정리  ");
            Assert.AreEqual("서류정리", v.Name);
        }

        [Test]
        public void Missing_Name_Is_Invalid()
        {
            Assert.IsFalse(StageFxOverlayParser.Parse("StageFx").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse("StageFx:").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse("StageFx:   ").IsValid);
        }

        [Test]
        public void NonStageFx_Is_Invalid()
        {
            Assert.IsFalse(StageFxOverlayParser.Parse("Video:intro").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse("FadeOut").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse("").IsValid);
            Assert.IsFalse(StageFxOverlayParser.Parse(null).IsValid);
        }
    }
}
