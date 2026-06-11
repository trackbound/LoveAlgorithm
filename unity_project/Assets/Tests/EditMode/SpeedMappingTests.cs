using NUnit.Framework;
using LoveAlgo.UI; // DialogueView.MapSpeed

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// DialogueView 속도 매핑(정규화 0=느림~1=빠름 → 초) 순수 테스트.
    /// </summary>
    public class SpeedMappingTests
    {
        const float Slow = 0.08f, Fast = 0.004f;

        [Test]
        public void MapSpeed_Endpoints_AndClamp()
        {
            Assert.AreEqual(Slow, DialogueView.MapSpeed(0f, Slow, Fast), 1e-5f);   // 느림 끝
            Assert.AreEqual(Fast, DialogueView.MapSpeed(1f, Slow, Fast), 1e-5f);   // 빠름 끝
            Assert.AreEqual(0.042f, DialogueView.MapSpeed(0.5f, Slow, Fast), 1e-5f); // 중간
            Assert.AreEqual(Slow, DialogueView.MapSpeed(-1f, Slow, Fast), 1e-5f);  // 하한 클램프
            Assert.AreEqual(Fast, DialogueView.MapSpeed(2f, Slow, Fast), 1e-5f);   // 상한 클램프
        }

        [Test]
        public void MapSpeed_HigherT_IsFaster()
        {
            Assert.Less(DialogueView.MapSpeed(0.8f, Slow, Fast), DialogueView.MapSpeed(0.2f, Slow, Fast)); // t↑ → 초↓(빠름)
        }
    }
}
