using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class GameStateRoaDeviceTests
    {
        [Test]
        public void StoryRoaDevice_Default_Empty()
        {
            var d = new GameStateData();
            Assert.AreEqual("", d.storyRoaDevice);
        }

        [Test]
        public void StoryRoaDevice_RoundTrips_Json()
        {
            var d = new GameStateData { storyRoaDevice = "모바일" };
            var json = JsonUtility.ToJson(d);
            var d2 = JsonUtility.FromJson<GameStateData>(json);
            Assert.AreEqual("모바일", d2.storyRoaDevice);
        }
    }
}
