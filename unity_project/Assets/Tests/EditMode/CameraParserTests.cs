using NUnit.Framework;
using LoveAlgo.Story;  // CameraParser, CameraIntent
using LoveAlgo.Events; // CameraKind

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="CameraParser"/>. CamZoom/CamPan/CamReset의 종류·배율·오프셋·지속
    /// 파싱과, 카메라 외 FX는 IsValid=false로 스킵 위임하는지. 시간 해석(동결값)은 엔진 몫이라 "미지정=음수"만 확인.
    /// </summary>
    [TestFixture]
    public class CameraParserTests
    {
        [Test]
        public void CamZoom_Scale_And_Duration()
        {
            var c = CameraParser.Parse("CamZoom:1.3:0.5");
            Assert.IsTrue(c.IsValid);
            Assert.AreEqual(CameraKind.Zoom, c.Kind);
            Assert.AreEqual(1.3f, c.ZoomScale, 1e-4f);
            Assert.AreEqual(0.5f, c.Duration, 1e-4f);
        }

        [Test]
        public void CamZoom_Bare_Defaults_To_Unit_Scale_Unspecified_Duration()
        {
            var c = CameraParser.Parse("CamZoom");
            Assert.AreEqual(CameraKind.Zoom, c.Kind);
            Assert.AreEqual(1f, c.ZoomScale, 1e-4f); // 배율 생략 = 1.0(줌 해제)
            Assert.Less(c.Duration, 0f);
        }

        [Test]
        public void CamPan_X_Y_Duration()
        {
            var c = CameraParser.Parse("CamPan:100:-50:0.4");
            Assert.AreEqual(CameraKind.Pan, c.Kind);
            Assert.AreEqual(100f, c.PanX, 1e-4f);
            Assert.AreEqual(-50f, c.PanY, 1e-4f);
            Assert.AreEqual(0.4f, c.Duration, 1e-4f);
        }

        [Test]
        public void CamPan_Origin_Return()
        {
            var c = CameraParser.Parse("CamPan:0:0");
            Assert.AreEqual(CameraKind.Pan, c.Kind);
            Assert.AreEqual(0f, c.PanX, 1e-4f);
            Assert.AreEqual(0f, c.PanY, 1e-4f);
            Assert.Less(c.Duration, 0f);
        }

        [Test]
        public void CamReset_Duration()
        {
            var c = CameraParser.Parse("CamReset:0.4");
            Assert.AreEqual(CameraKind.Reset, c.Kind);
            Assert.AreEqual(0.4f, c.Duration, 1e-4f);
        }

        [Test]
        public void CamReset_Bare_Unspecified_Duration()
        {
            var c = CameraParser.Parse("CamReset");
            Assert.AreEqual(CameraKind.Reset, c.Kind);
            Assert.Less(c.Duration, 0f);
        }

        [Test]
        public void CaseInsensitive()
        {
            Assert.AreEqual(CameraKind.Zoom, CameraParser.Parse("camzoom:1.5").Kind);
            Assert.AreEqual(CameraKind.Pan, CameraParser.Parse("CAMPAN:10:10").Kind);
            Assert.AreEqual(CameraKind.Reset, CameraParser.Parse("camRESET").Kind);
        }

        [Test]
        public void NonCamera_Fx_Is_Invalid()
        {
            Assert.IsFalse(CameraParser.Parse("FadeOut").IsValid);
            Assert.IsFalse(CameraParser.Parse("StageShake").IsValid);
            Assert.IsFalse(CameraParser.Parse("CamShake:0.3").IsValid); // 흔들기는 ShakeParser 소관
            Assert.IsFalse(CameraParser.Parse("").IsValid);
            Assert.IsFalse(CameraParser.Parse(null).IsValid);
        }
    }
}
