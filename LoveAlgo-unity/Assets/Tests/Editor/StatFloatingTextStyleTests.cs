using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Modules.Stats;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D6 스탯 floating text 스타일 — 색 매핑이 의도대로 동작하는지 검증.
    /// gain/loss 분기, override 우선, alpha=0 → default 폴백.
    /// (실제 풀링/visual은 PlayMode + 씬 의존이므로 여기선 SO 로직만.)
    /// </summary>
    [TestFixture]
    public class StatFloatingTextStyleTests
    {
        StatFloatingTextStyleSO _so;

        [SetUp]
        public void SetUp()
        {
            _so = ScriptableObject.CreateInstance<StatFloatingTextStyleSO>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_so != null) Object.DestroyImmediate(_so);
        }

        [Test]
        public void ResolveColor_PositiveDelta_UsesDefaultGainColor()
        {
            _so.defaultGainColor = new Color(0.1f, 0.9f, 0.2f);
            var c = _so.ResolveColor("Str", 5);
            Assert.AreEqual(_so.defaultGainColor, c);
        }

        [Test]
        public void ResolveColor_NegativeDelta_UsesDefaultLossColor()
        {
            _so.defaultLossColor = new Color(0.9f, 0.1f, 0.1f);
            var c = _so.ResolveColor("Int", -3);
            Assert.AreEqual(_so.defaultLossColor, c);
        }

        [Test]
        public void ResolveColor_OverrideMatches_UsesOverride()
        {
            var blue = new Color(0.2f, 0.4f, 1f, 1f);
            _so.overrides.Add(new StatFloatingTextStyleSO.Override
            {
                statId = "Fatigue",
                gainColor = blue,
                lossColor = new Color(0.5f, 0.5f, 0.1f, 1f),
            });

            var c = _so.ResolveColor("Fatigue", 5);
            Assert.AreEqual(blue, c, "Fatigue gain override가 적용돼야 함");
        }

        [Test]
        public void ResolveColor_OverrideIsCaseInsensitive()
        {
            _so.overrides.Add(new StatFloatingTextStyleSO.Override
            {
                statId = "Fatigue",
                gainColor = new Color(1, 1, 0, 1),
                lossColor = new Color(0, 1, 1, 1),
            });
            // 소문자/대문자 둘 다 매칭
            Assert.AreEqual(new Color(1, 1, 0, 1), _so.ResolveColor("fatigue", 3));
            Assert.AreEqual(new Color(1, 1, 0, 1), _so.ResolveColor("FATIGUE", 3));
        }

        [Test]
        public void ResolveColor_OverrideAlphaZero_FallsBackToDefault()
        {
            _so.defaultGainColor = new Color(0.3f, 0.9f, 0.3f, 1f);
            _so.overrides.Add(new StatFloatingTextStyleSO.Override
            {
                statId = "Str",
                gainColor = new Color(1, 0, 0, 0f), // alpha 0 = 사용 안 함 신호
                lossColor = new Color(0, 1, 0, 1f),
            });

            var c = _so.ResolveColor("Str", 5);
            Assert.AreEqual(_so.defaultGainColor, c,
                "alpha=0 override는 default로 폴백해야 함");
        }

        [Test]
        public void ResolveColor_UnknownStat_UsesDefault()
        {
            _so.overrides.Add(new StatFloatingTextStyleSO.Override
            {
                statId = "Str",
                gainColor = new Color(1, 0, 0, 1),
                lossColor = new Color(0, 1, 0, 1),
            });
            // 미등록 스탯은 default 사용
            var c = _so.ResolveColor("UnknownStat", 5);
            Assert.AreEqual(_so.defaultGainColor, c);
        }

        [Test]
        public void DefaultsAreSane_GainGreenish_LossReddish()
        {
            // 기획자가 SO를 안 만든 상태에서도 +는 녹색계열, -는 적색계열이어야 함
            Assert.Greater(_so.defaultGainColor.g, _so.defaultGainColor.r,
                "default gain은 녹>적이라야 +가 녹색으로 보임");
            Assert.Greater(_so.defaultLossColor.r, _so.defaultLossColor.g,
                "default loss는 적>녹이라야 -가 적색으로 보임");
        }
    }
}
