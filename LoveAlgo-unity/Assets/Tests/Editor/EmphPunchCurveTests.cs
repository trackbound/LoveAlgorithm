using NUnit.Framework;
using LoveAlgo.Story;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D11 Emph 펀치 곡선 — 순수 함수 검증.
    /// 펄스 모양(시작=1, 피크 t=0.5, 끝=1), intensity 비례, 경계 안전성.
    /// "돌다리" 라운드의 핵심 — 잘못된 곡선이 잘못된 시각으로 이어짐.
    /// </summary>
    [TestFixture]
    public class EmphPunchCurveTests
    {
        const float Duration = 0.25f;
        const float Tolerance = 0.0001f;

        [Test]
        public void Punch_AtStart_NoEffect()
        {
            // elapsed=0 → 곡선의 시작점, scale=1, yOffset=0 — 시각적 변화 없음
            var (scale, yOff) = DialogueEffectsRenderer.ComputeEmphPunch(0f, Duration, 1f);
            Assert.AreEqual(1f, scale, Tolerance);
            Assert.AreEqual(0f, yOff, Tolerance);
        }

        [Test]
        public void Punch_AtEnd_NoEffect()
        {
            // elapsed=Duration → 끝, sin(π)=0, scale=1, yOffset=0
            var (scale, yOff) = DialogueEffectsRenderer.ComputeEmphPunch(Duration, Duration, 1f);
            Assert.AreEqual(1f, scale, Tolerance);
            Assert.AreEqual(0f, yOff, Tolerance);
        }

        [Test]
        public void Punch_PastEnd_NoEffect()
        {
            // 지난 시점 — no-op (펄스 끝)
            var (scale, yOff) = DialogueEffectsRenderer.ComputeEmphPunch(Duration * 2f, Duration, 1f);
            Assert.AreEqual(1f, scale, Tolerance);
            Assert.AreEqual(0f, yOff, Tolerance);
        }

        [Test]
        public void Punch_AtPeak_MaxValues()
        {
            // t=0.5 → sin(π/2)=1, peak scale & yOffset
            var (scale, yOff) = DialogueEffectsRenderer.ComputeEmphPunch(Duration * 0.5f, Duration, 1f);
            Assert.Greater(scale, 1.17f, "intensity=1에서 peak scale ≈ 1.18");
            Assert.Less(scale, 1.19f);
            Assert.Greater(yOff, 2.4f, "peak yOffset ≈ 2.5");
            Assert.Less(yOff, 2.6f);
        }

        [Test]
        public void Punch_IntensityScalesPeak()
        {
            var (s1, y1) = DialogueEffectsRenderer.ComputeEmphPunch(Duration * 0.5f, Duration, 1f);
            var (s2, y2) = DialogueEffectsRenderer.ComputeEmphPunch(Duration * 0.5f, Duration, 2f);
            // intensity 2배 → punch 효과 2배 (envelope은 그대로, 상수만 곱)
            Assert.AreEqual((s2 - 1f), (s1 - 1f) * 2f, 0.001f, "scale 추가분이 intensity에 비례");
            Assert.AreEqual(y2, y1 * 2f, 0.01f, "yOffset이 intensity에 비례");
        }

        [Test]
        public void Punch_NegativeElapsed_NoEffect()
        {
            // 잘못된 입력 — 안전 폴백
            var (scale, yOff) = DialogueEffectsRenderer.ComputeEmphPunch(-0.1f, Duration, 1f);
            Assert.AreEqual(1f, scale);
            Assert.AreEqual(0f, yOff);
        }

        [Test]
        public void Punch_ZeroDuration_NoEffect()
        {
            // duration=0 — 무한 즉시 — no-op로 안전 처리
            var (scale, yOff) = DialogueEffectsRenderer.ComputeEmphPunch(0.1f, 0f, 1f);
            Assert.AreEqual(1f, scale);
            Assert.AreEqual(0f, yOff);
        }

        [Test]
        public void Punch_NegativeDuration_NoEffect()
        {
            var (scale, yOff) = DialogueEffectsRenderer.ComputeEmphPunch(0.1f, -0.1f, 1f);
            Assert.AreEqual(1f, scale);
            Assert.AreEqual(0f, yOff);
        }

        [Test]
        public void Punch_ZeroIntensity_FallsBackToOne()
        {
            // intensity 0/음수 → 1로 폴백 (관대 정책)
            var (s, _) = DialogueEffectsRenderer.ComputeEmphPunch(Duration * 0.5f, Duration, 0f);
            Assert.Greater(s, 1f, "intensity=0이라도 폴백 1로 처리되어 peak는 보임");
        }

        [Test]
        public void Punch_SymmetricalAroundPeak()
        {
            // t=0.25, t=0.75 같은 envelope 값
            var (sA, yA) = DialogueEffectsRenderer.ComputeEmphPunch(Duration * 0.25f, Duration, 1f);
            var (sB, yB) = DialogueEffectsRenderer.ComputeEmphPunch(Duration * 0.75f, Duration, 1f);
            Assert.AreEqual(sA, sB, 0.0001f, "sin(π·t) 곡선은 t=0.25, 0.75에서 같은 값");
            Assert.AreEqual(yA, yB, 0.0001f);
        }

        [Test]
        public void Punch_MonotonicRiseToPeak()
        {
            // 0 → 0.25 → 0.5 까지 단조증가
            var (s0, _) = DialogueEffectsRenderer.ComputeEmphPunch(0f, Duration, 1f);
            var (s1, _) = DialogueEffectsRenderer.ComputeEmphPunch(Duration * 0.25f, Duration, 1f);
            var (s2, _) = DialogueEffectsRenderer.ComputeEmphPunch(Duration * 0.5f, Duration, 1f);
            Assert.Less(s0, s1);
            Assert.Less(s1, s2);
        }
    }
}
