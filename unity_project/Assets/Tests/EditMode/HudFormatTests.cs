using NUnit.Framework;
using LoveAlgo.UI; // HudFormat

namespace LoveAlgo.Tests.Editor
{
    /// <summary>HUD 슬라이스1 검증: 순수 <see cref="HudFormat"/> 표시 문자열 포맷.</summary>
    [TestFixture]
    public class HudFormatTests
    {
        [Test] public void Day_Formats() => Assert.AreEqual("Day 5", HudFormat.Day(5));

        [Test] public void Affinity_Formats() => Assert.AreEqual("HaYeEun ♥ 12", HudFormat.Affinity("HaYeEun", 12));

        [Test] public void Stat_Formats() => Assert.AreEqual("Str 7", HudFormat.Stat("Str", 7));

        [Test]
        public void SaveStatus_Formats()
        {
            Assert.AreEqual("저장됨", HudFormat.SaveStatus(true));
            Assert.AreEqual("저장 실패", HudFormat.SaveStatus(false));
        }

        [Test]
        public void Bgm_Formats()
        {
            Assert.AreEqual("♪ white_noise", HudFormat.Bgm("white_noise"));
            Assert.AreEqual("♪ —", HudFormat.Bgm(null));
            Assert.AreEqual("♪ —", HudFormat.Bgm(""));
        }
    }
}
