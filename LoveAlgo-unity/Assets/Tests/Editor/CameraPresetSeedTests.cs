using System.Collections.Generic;
using NUnit.Framework;
using LoveAlgo.Stage;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D14 카메라 프리셋 시드 — 시드와 D5 폴백의 동기성 검증.
    /// 시드가 폴백의 어떤 id를 빠뜨리면, 디자이너가 자산 생성 후 그 id가 사라짐 → 회귀.
    /// </summary>
    [TestFixture]
    public class CameraPresetSeedTests
    {
        [Test]
        public void Seed_ContainsAllFallbackIds()
        {
            var entries = CameraPresetSeed.BuildSeedEntries();
            var ids = new HashSet<string>();
            foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.id)) ids.Add(e.id);

            foreach (var required in CameraPresetSeed.FallbackIds)
            {
                Assert.IsTrue(ids.Contains(required),
                    $"시드가 폴백 id '{required}'을 빠뜨리면 사용자가 자산 생성 후 회귀.");
            }
        }

        [Test]
        public void Seed_AllEntries_HaveNonEmptyIdAndAtLeastOneStep()
        {
            var entries = CameraPresetSeed.BuildSeedEntries();
            Assert.IsNotEmpty(entries);
            foreach (var e in entries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.id), "시드 entry id가 비어 있음");
                Assert.IsNotEmpty(e.steps, $"'{e.id}' steps 비어 있음 — 의미 없는 프리셋");
                foreach (var s in e.steps)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(s.command),
                        $"'{e.id}'의 step 중 command 비어 있음");
            }
        }

        [Test]
        public void Seed_IdsAreUnique()
        {
            var entries = CameraPresetSeed.BuildSeedEntries();
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                Assert.IsTrue(seen.Add(e.id), $"중복 id '{e.id}' — 시드에 같은 이름 두 번 정의됨");
            }
        }

        [Test]
        public void Seed_AddsExtras_BeyondFallback()
        {
            // D14 추가 — 폴백 6 + 추가 3 = 9
            var entries = CameraPresetSeed.BuildSeedEntries();
            Assert.GreaterOrEqual(entries.Count, CameraPresetSeed.FallbackIds.Length + 1,
                "시드는 폴백 외에 추가 프리셋을 포함해야 D14의 의미가 있음");
        }

        [Test]
        public void Seed_ResolvesViaTable_AfterReload()
        {
            // 시드의 모든 id가 CameraPresetTable에서 lookup 가능해야 함 (폴백 또는 SO 경로).
            // 이 테스트에선 SO 없음 → 폴백만 lookup 가능 → 폴백 id 만 확인.
            CameraPresetTable.ReloadForTests();
            foreach (var id in CameraPresetSeed.FallbackIds)
            {
                Assert.IsNotNull(CameraPresetTable.Resolve(id),
                    $"폴백 id '{id}'를 Table이 해석하지 못함 — 시드/폴백 ID 불일치");
            }
        }
    }
}
