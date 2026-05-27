using NUnit.Framework;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D4 엔딩 매니페스트 — endings.csv 매핑이 정상 로드되고 ResolveScriptName이
    /// 기대 경로를 반환하는지 검증. 매니페스트가 비어있으면 ResolveScriptName은 null →
    /// GameFlowController의 프로시져 폴백이 동작.
    /// </summary>
    [TestFixture]
    public class EndingTableCsvTests
    {
        [SetUp]
        public void SetUp() => EndingTable.ReloadForTests();

        [Test]
        public void NormalSlot_EmptyHeroine_ReturnsEndingNormal()
        {
            string s = EndingTable.ResolveScriptName(null, isHappy: false);
            Assert.AreEqual("Ending_Normal", s,
                "빈 HeroineId 행이 'Ending_Normal'로 매핑돼야 함 (노 고백 엔딩)");
        }

        [Test]
        public void Roa_Happy_OverridesToMeriBad()
        {
            // 프로시져 폴백은 "Ending_Roa_Happy"이지만 매니페스트가 MeriBad로 override해야 함.
            string s = EndingTable.ResolveScriptName("Roa", isHappy: true);
            Assert.AreEqual("Ending_Roa_MeriBad", s,
                "Roa 해피는 매니페스트에서 MeriBad 전용 스크립트로 override돼야 함");
        }

        [Test]
        public void StandardHeroine_HappyAndSad_BothMapped()
        {
            Assert.AreEqual("Ending_HaYeEun_Happy", EndingTable.ResolveScriptName("HaYeEun", true));
            Assert.AreEqual("Ending_HaYeEun_Sad",   EndingTable.ResolveScriptName("HaYeEun", false));
            Assert.AreEqual("Ending_SeoDaEun_Happy", EndingTable.ResolveScriptName("SeoDaEun", true));
            Assert.AreEqual("Ending_LeeBom_Sad",     EndingTable.ResolveScriptName("LeeBom", false));
            Assert.AreEqual("Ending_DoHeewon_Happy", EndingTable.ResolveScriptName("DoHeewon", true));
        }

        [Test]
        public void UnknownHeroine_ReturnsNull_TriggersProceduralFallback()
        {
            string s = EndingTable.ResolveScriptName("NoSuchHeroine", isHappy: true);
            Assert.IsNull(s, "매니페스트에 없는 히로인은 null 반환 → 호출자가 폴백 사용");
        }

        [Test]
        public void Manifest_HasAtLeastFiveHeroineEntries()
        {
            // 5 히로인 × (Happy/Sad) ≈ 9~10개 슬롯 + 노 고백 → 최소 8개는 있어야 함.
            Assert.GreaterOrEqual(EndingTable.Count, 8,
                $"endings.csv가 너무 적게 로드됨 ({EndingTable.Count}). 기획서 기준 9개 정상.");
        }
    }
}
