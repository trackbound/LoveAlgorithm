using NUnit.Framework;
using LoveAlgo.Story; // SceneFxParser, SceneFxIntent, SceneFxKind

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// FX 매크로 순수 파서 검증: <see cref="SceneFxParser"/>. SceneStart(bg·EyeClose 플래그 추출, 순서/케이스),
    /// SceneEnd(지속 생략→-1·명시), 비-Scene head→IsValid=false.
    /// </summary>
    [TestFixture]
    public class SceneFxParserTests
    {
        [Test]
        public void SceneEnd_No_Arg_Defaults_Duration()
        {
            var s = SceneFxParser.Parse("SceneEnd");
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(SceneFxKind.End, s.Kind);
            Assert.Less(s.Duration, 0f); // -1 = 기본 위임
        }

        [Test]
        public void SceneEnd_With_Duration()
        {
            var s = SceneFxParser.Parse("SceneEnd:0.8");
            Assert.AreEqual(SceneFxKind.End, s.Kind);
            Assert.AreEqual(0.8f, s.Duration, 1e-4f);
        }

        [Test]
        public void SceneStart_Plain_No_Bg_No_EyeClose()
        {
            var s = SceneFxParser.Parse("SceneStart");
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(SceneFxKind.Start, s.Kind);
            Assert.IsNull(s.Bg);
            Assert.IsFalse(s.EyeClose);
        }

        [Test]
        public void SceneStart_With_Bg_Only()
        {
            var s = SceneFxParser.Parse("SceneStart:bg_40_05");
            Assert.AreEqual("bg_40_05", s.Bg);
            Assert.IsFalse(s.EyeClose);
        }

        [Test]
        public void SceneStart_With_Bg_And_EyeClose()
        {
            var s = SceneFxParser.Parse("SceneStart:bg_10_04:EyeClose");
            Assert.AreEqual("bg_10_04", s.Bg);
            Assert.IsTrue(s.EyeClose);
        }

        [Test]
        public void SceneStart_EyeClose_Without_Bg()
        {
            var s = SceneFxParser.Parse("SceneStart:EyeClose");
            Assert.IsNull(s.Bg);
            Assert.IsTrue(s.EyeClose);
        }

        [Test]
        public void SceneStart_Case_Insensitive_Head_And_Flag()
        {
            var s = SceneFxParser.Parse("scenestart:BG1:eyeclose");
            Assert.AreEqual(SceneFxKind.Start, s.Kind);
            Assert.AreEqual("BG1", s.Bg);
            Assert.IsTrue(s.EyeClose);
        }

        [Test]
        public void Non_Scene_Head_Is_Invalid()
        {
            Assert.IsFalse(SceneFxParser.Parse("Setup:BG=x").IsValid);
            Assert.IsFalse(SceneFxParser.Parse("Wait").IsValid);
            Assert.IsFalse(SceneFxParser.Parse("").IsValid);
        }
    }
}
