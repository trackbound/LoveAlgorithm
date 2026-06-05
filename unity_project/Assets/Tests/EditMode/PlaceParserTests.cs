using NUnit.Framework;
using LoveAlgo.Story; // PlaceParser, PlaceIntent

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 위치 배너 순수 파서 검증: <see cref="PlaceParser"/>. "제목 | 장소" 분리·트림, '|' 없으면 전체=장소,
    /// 빈 값=무효.
    /// </summary>
    [TestFixture]
    public class PlaceParserTests
    {
        [Test]
        public void Title_And_Place_Split_On_Bar()
        {
            var p = PlaceParser.Parse("[새 학기 첫날] | 침대 위");
            Assert.IsTrue(p.IsValid);
            Assert.AreEqual("[새 학기 첫날]", p.Title);
            Assert.AreEqual("침대 위", p.Place);
        }

        [Test]
        public void Trims_Whitespace_Around_Bar()
        {
            var p = PlaceParser.Parse("첫 전공수업  |  공대 강의실");
            Assert.AreEqual("첫 전공수업", p.Title);
            Assert.AreEqual("공대 강의실", p.Place);
        }

        [Test]
        public void No_Bar_Whole_Is_Place()
        {
            var p = PlaceParser.Parse("그냥 장소");
            Assert.IsTrue(p.IsValid);
            Assert.IsTrue(string.IsNullOrEmpty(p.Title));
            Assert.AreEqual("그냥 장소", p.Place);
        }

        [Test]
        public void Empty_Is_Invalid()
        {
            Assert.IsFalse(PlaceParser.Parse("").IsValid);
            Assert.IsFalse(PlaceParser.Parse(null).IsValid);
        }
    }
}
