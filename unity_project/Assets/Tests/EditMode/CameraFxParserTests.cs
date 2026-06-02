using NUnit.Framework;
using LoveAlgo.Story;  // CameraFxParser, CameraFxIntent
using LoveAlgo.Events; // CameraFxKind

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="CameraFxParser"/>. CamZoom/CamPan/CamReset의 종류·배율·오프셋·지속
    /// 파싱과, 카메라 외 FX는 IsValid=false로 스킵 위임하는지. 시간 해석(동결값)은 엔진 몫이라 "미지정=음수"만 확인.
    /// </summary>
    [TestFixture]
    public class CameraFxParserTests
    {
        [Test]
        public void CamZoom_Scale_And_Duration()
        {
            var c = CameraFxParser.Parse("CamZoom:1.3:0.5");
            Assert.IsTrue(c.IsValid);
            Assert.AreEqual(CameraFxKind.Zoom, c.Kind);
            Assert.AreEqual(1.3f, c.ZoomScale, 1e-4f);
            Assert.AreEqual(0.5f, c.Duration, 1e-4f);
        }

        [Test]
        public void CamZoom_Bare_Defaults_To_Unit_Scale_Unspecified_Duration()
        {
            var c = CameraFxParser.Parse("CamZoom");
            Assert.AreEqual(CameraFxKind.Zoom, c.Kind);
            Assert.AreEqual(1f, c.ZoomScale, 1e-4f); // 배율 생략 = 1.0(줌 해제)
            Assert.Less(c.Duration, 0f);
        }

        [Test]
        public void CamPan_X_Y_Duration()
        {
            var c = CameraFxParser.Parse("CamPan:100:-50:0.4");
            Assert.AreEqual(CameraFxKind.Pan, c.Kind);
            Assert.AreEqual(100f, c.PanX, 1e-4f);
            Assert.AreEqual(-50f, c.PanY, 1e-4f);
            Assert.AreEqual(0.4f, c.Duration, 1e-4f);
        }

        [Test]
        public void CamPan_Origin_Return()
        {
            var c = CameraFxParser.Parse("CamPan:0:0");
            Assert.AreEqual(CameraFxKind.Pan, c.Kind);
            Assert.AreEqual(0f, c.PanX, 1e-4f);
            Assert.AreEqual(0f, c.PanY, 1e-4f);
            Assert.Less(c.Duration, 0f);
        }

        [Test]
        public void CamReset_Duration()
        {
            var c = CameraFxParser.Parse("CamReset:0.4");
            Assert.AreEqual(CameraFxKind.Reset, c.Kind);
            Assert.AreEqual(0.4f, c.Duration, 1e-4f);
        }

        [Test]
        public void CamReset_Bare_Unspecified_Duration()
        {
            var c = CameraFxParser.Parse("CamReset");
            Assert.AreEqual(CameraFxKind.Reset, c.Kind);
            Assert.Less(c.Duration, 0f);
        }

        [Test]
        public void CaseInsensitive()
        {
            Assert.AreEqual(CameraFxKind.Zoom, CameraFxParser.Parse("camzoom:1.5").Kind);
            Assert.AreEqual(CameraFxKind.Pan, CameraFxParser.Parse("CAMPAN:10:10").Kind);
            Assert.AreEqual(CameraFxKind.Reset, CameraFxParser.Parse("camRESET").Kind);
        }

        [Test]
        public void NonCamera_Fx_Is_Invalid()
        {
            Assert.IsFalse(CameraFxParser.Parse("FadeOut").IsValid);
            Assert.IsFalse(CameraFxParser.Parse("StageShake").IsValid);
            Assert.IsFalse(CameraFxParser.Parse("CamShake:0.3").IsValid); // 흔들기는 ShakeParser 소관
            Assert.IsFalse(CameraFxParser.Parse("").IsValid);
            Assert.IsFalse(CameraFxParser.Parse(null).IsValid);
        }
    }
}
