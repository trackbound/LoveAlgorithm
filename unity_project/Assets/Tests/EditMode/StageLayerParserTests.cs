using NUnit.Framework;
using LoveAlgo.Story;  // StageLayerParser, StageLayerIntent
using LoveAlgo.Events; // LayerTransition

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="StageLayerParser"/>(CG/SD/Overlay 공통). 표시(이름·전환·지속)·종료 키워드
    /// 파싱. 종류(Kind)는 LineType에서 엔진이 정하므로 파서엔 없다. fade 동결값 해석은 엔진 몫(미지정=음수).
    /// </summary>
    [TestFixture]
    public class StageLayerParserTests
    {
        [Test]
        public void Show_Name_Transition_Duration()
        {
            var s = StageLayerParser.Parse("cg_c01_01:Fade:4.0");
            Assert.IsTrue(s.IsValid);
            Assert.IsFalse(s.IsClose);
            Assert.AreEqual("cg_c01_01", s.Name);
            Assert.AreEqual(LayerTransition.Fade, s.Transition);
            Assert.AreEqual(4.0f, s.Duration, 1e-4f);
        }

        [Test]
        public void Show_Name_Only_Defaults_Fade_Unspecified_Duration()
        {
            var s = StageLayerParser.Parse("sd_c04_01");
            Assert.IsFalse(s.IsClose);
            Assert.AreEqual("sd_c04_01", s.Name);
            Assert.AreEqual(LayerTransition.Fade, s.Transition); // 기본 Fade
            Assert.Less(s.Duration, 0f);
        }

        [Test]
        public void Show_Cut_Transition()
        {
            var s = StageLayerParser.Parse("cg_c03_01:Cut");
            Assert.AreEqual(LayerTransition.Cut, s.Transition);
            Assert.AreEqual("cg_c03_01", s.Name);
        }

        [Test]
        public void Close_Keyword()
        {
            var s = StageLayerParser.Parse("Close");
            Assert.IsTrue(s.IsValid);
            Assert.IsTrue(s.IsClose);
        }

        [Test]
        public void Close_Aliases_And_Duration()
        {
            Assert.IsTrue(StageLayerParser.Parse("Exit").IsClose);
            Assert.IsTrue(StageLayerParser.Parse("Hide").IsClose);
            var fo = StageLayerParser.Parse("FadeOut:0.3");
            Assert.IsTrue(fo.IsClose);
            Assert.AreEqual(0.3f, fo.Duration, 1e-4f);
        }

        [Test]
        public void CaseInsensitive_Close()
        {
            Assert.IsTrue(StageLayerParser.Parse("close").IsClose);
            Assert.IsTrue(StageLayerParser.Parse("CLOSE").IsClose);
        }

        [Test]
        public void Empty_Is_Invalid()
        {
            Assert.IsFalse(StageLayerParser.Parse("").IsValid);
            Assert.IsFalse(StageLayerParser.Parse(null).IsValid);
        }
    }
}
