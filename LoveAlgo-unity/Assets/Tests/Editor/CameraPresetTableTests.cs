using System.Linq;
using NUnit.Framework;
using LoveAlgo.Stage;
using LoveAlgo.Story.StoryEngine;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D5 카메라 프리셋 — 내장 폴백 프리셋이 정상 등록되는지, 이름으로 lookup 가능한지,
    /// FXCommandSignatures가 CamPreset을 인식하는지 검증.
    /// 사용자 SO 추가 후엔 새 id가 폴백을 override해야 하지만 SO 미생성 상태에선 폴백이 베이스라인.
    /// </summary>
    [TestFixture]
    public class CameraPresetTableTests
    {
        [SetUp]
        public void SetUp() => CameraPresetTable.ReloadForTests();

        [Test]
        public void Fallback_ContainsCoreSixPresets()
        {
            // 폴백 6종이 항상 사용 가능해야 함 — SO 미작성이어도 CSV가 즉시 동작.
            string[] expected = { "ZoomIn-Soft", "ZoomIn-Hard", "ZoomOut", "PunchHit", "DramaticReveal", "Heartbeat" };
            foreach (var id in expected)
            {
                Assert.IsNotNull(CameraPresetTable.Resolve(id), $"폴백 프리셋 '{id}'이(가) 등록돼 있어야 함");
            }
        }

        [Test]
        public void Resolve_IsCaseInsensitive()
        {
            Assert.IsNotNull(CameraPresetTable.Resolve("ZoomIn-Soft"));
            Assert.IsNotNull(CameraPresetTable.Resolve("zoomin-soft"));
            Assert.IsNotNull(CameraPresetTable.Resolve("ZOOMIN-SOFT"));
        }

        [Test]
        public void Resolve_TrimsWhitespace()
        {
            Assert.IsNotNull(CameraPresetTable.Resolve("  ZoomOut  "));
        }

        [Test]
        public void Resolve_UnknownReturnsNull()
        {
            Assert.IsNull(CameraPresetTable.Resolve("NoSuchPreset"));
            Assert.IsNull(CameraPresetTable.Resolve(""));
            Assert.IsNull(CameraPresetTable.Resolve(null));
        }

        [Test]
        public void PunchHit_FlashStepIsNonBlocking()
        {
            // PunchHit는 Flash가 fire-and-forget이어야 다음 Shake와 시각적으로 겹침
            var entry = CameraPresetTable.Resolve("PunchHit");
            Assert.IsNotNull(entry);
            Assert.AreEqual(2, entry.steps.Count);
            Assert.IsTrue(entry.steps[0].command.StartsWith("Flash"),
                "PunchHit 첫 step은 Flash");
            Assert.IsFalse(entry.steps[0].waitForCompletion,
                "Flash step은 fire-and-forget이라야 Shake와 시각적으로 겹침");
        }

        [Test]
        public void FXSignature_CamPresetRequiresExactlyOneArg()
        {
            // CamPreset:이름 — 인자 1개 강제 (0개도 2개도 안 됨)
            Assert.IsTrue(FXCommandSignatures.TryValidate("CamPreset", argCount: 1, out _, out _));
            Assert.IsFalse(FXCommandSignatures.TryValidate("CamPreset", argCount: 0, out _, out var errZero));
            Assert.IsFalse(FXCommandSignatures.TryValidate("CamPreset", argCount: 2, out _, out var errTwo));
            Assert.IsNotNull(errZero);
            Assert.IsNotNull(errTwo);
        }

        [Test]
        public void Alias_PresetMapsToCamPreset()
        {
            Assert.AreEqual("CamPreset", CommandAliases.NormalizeFX("Preset"));
            Assert.AreEqual("CamPreset", CommandAliases.NormalizeFX("preset"));
            Assert.IsTrue(CommandAliases.IsKnownFX("Preset"));
        }
    }
}
