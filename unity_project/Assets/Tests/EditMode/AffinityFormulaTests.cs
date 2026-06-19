using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.Affinity;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M2 슬라이스1 검증 (REWRITE_FEATURE_INVENTORY §4·§5 공식 그대로 재현 증거).
    /// 호감도 총점·스탯/피로 보너스·엔딩 판정·Event3 재선택·스탯 API 클램프를 회귀한다.
    /// 순수 함수 + SO 인스턴스만 다루므로 EditMode로 충분(프로젝트 관행).
    /// </summary>
    [TestFixture]
    public class AffinityFormulaTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        // 정의표는 전역 static이므로 각 테스트 후 폴백으로 복원 → 테스트 격리.
        [TearDown]
        public void RestoreFallbackDefs() => AffinityFormula.ResetToFallback();

        /// <summary>private [SerializeField] heroines 리스트를 테스트용으로 주입.</summary>
        static GameBalanceSO MakeBalance(params GameBalanceSO.HeroineEntry[] entries)
        {
            var bal = ScriptableObject.CreateInstance<GameBalanceSO>();
            typeof(GameBalanceSO)
                .GetField("heroines", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(bal, new List<GameBalanceSO.HeroineEntry>(entries));
            return bal;
        }

        // ── 스탯 API (Core 보강) ───────────────────────────────

        [Test]
        public void SetStat_Clamps_To_0_100()
        {
            var gs = MakeState();
            gs.SetStat("Str", 150);
            Assert.AreEqual(100, gs.GetStat("Str"));
            gs.SetStat("Str", -5);
            Assert.AreEqual(0, gs.GetStat("Str"));
        }

        [Test]
        public void GetStat_Int_Maps_To_Intel_Field()
        {
            var gs = MakeState();
            gs.SetStat("Int", 42);
            Assert.AreEqual(42, gs.GetStat("Int"));
        }

        [Test]
        public void AddStat_Accumulates_And_Clamps()
        {
            var gs = MakeState();
            gs.AddStat("Per", 30);
            gs.AddStat("Per", 80); // 110 → 100
            Assert.AreEqual(100, gs.GetStat("Per"));
        }

        // ── 스탯 보너스 (로아 제외) ────────────────────────────

        [Test]
        public void StatBonus_SoloFirst_Plus3()
        {
            var gs = MakeState();
            gs.SetStat("Str", 10); // HaYeEun 선호 = Str, 단독 1등
            gs.SetStat("Int", 5);
            Assert.AreEqual(3, AffinityFormula.StatBonus(gs, AffinityFormula.IndexOf("HaYeEun")));
        }

        [Test]
        public void StatBonus_TiedFirst_Plus1()
        {
            var gs = MakeState();
            gs.SetStat("Str", 10); // 공동 1등 (Str=Int=10)
            gs.SetStat("Int", 10);
            Assert.AreEqual(1, AffinityFormula.StatBonus(gs, AffinityFormula.IndexOf("HaYeEun")));
        }

        [Test]
        public void StatBonus_SecondOrLower_Zero()
        {
            var gs = MakeState();
            gs.SetStat("Str", 5);  // 선호 Str이 2등
            gs.SetStat("Int", 10);
            Assert.AreEqual(0, AffinityFormula.StatBonus(gs, AffinityFormula.IndexOf("HaYeEun")));
        }

        [Test]
        public void StatBonus_PreferredZero_Zero()
        {
            var gs = MakeState(); // 모든 스탯 0
            Assert.AreEqual(0, AffinityFormula.StatBonus(gs, AffinityFormula.IndexOf("HaYeEun")));
        }

        // ── 로아 피로 보너스 (스탯 보너스 대신) ─────────────────

        [TestCase(69, 0)]
        [TestCase(70, 3)]
        [TestCase(79, 3)]
        [TestCase(80, 6)]
        [TestCase(89, 6)]
        [TestCase(90, 10)]
        [TestCase(100, 10)]
        public void RoaFatigueBonus_Tiers(int fatigue, int expected)
        {
            var gs = MakeState();
            gs.SetStat("Fatigue", fatigue);
            Assert.AreEqual(expected, AffinityFormula.RoaFatigueBonus(gs));
        }

        // ── 총점 = 기본 + 보너스 ───────────────────────────────

        [Test]
        public void TotalScore_NonRoa_AddsStatBonus()
        {
            var gs = MakeState();
            gs.SetStat("Str", 10); // HaYeEun 단독 1등 → +3
            AffinityFormula.AddPoint(gs, "HaYeEun", PointCategory.Event, 20);
            Assert.AreEqual(23, AffinityFormula.TotalScore(gs, "HaYeEun"));
        }

        [Test]
        public void TotalScore_Roa_AddsFatigueBonus_NotStatBonus()
        {
            var gs = MakeState();
            gs.SetStat("Str", 99);       // 로아는 스탯 보너스 무시
            gs.SetStat("Fatigue", 80);   // +6
            AffinityFormula.AddPoint(gs, "Roa", PointCategory.Event, 30);
            Assert.AreEqual(36, AffinityFormula.TotalScore(gs, "Roa"));
        }

        // ── 티어 ───────────────────────────────────────────────

        [TestCase(0, AffinityTier.Stranger)]
        [TestCase(9, AffinityTier.Stranger)]
        [TestCase(10, AffinityTier.Acquaintance)]
        [TestCase(20, AffinityTier.Friend)]
        [TestCase(30, AffinityTier.CloseFriend)]
        [TestCase(40, AffinityTier.Love)]
        [TestCase(99, AffinityTier.Love)]
        public void Tier_Boundaries(int score, AffinityTier expected)
        {
            Assert.AreEqual(expected, AffinityFormula.Tier(score));
        }

        // ── Event3 재선택 +2 ───────────────────────────────────

        [Test]
        public void RecordEventChoice_Event3_Reselect_Plus2()
        {
            var gs = MakeState();
            AffinityFormula.RecordEventChoice(gs, "HaYeEun", "Event1", 3);
            AffinityFormula.RecordEventChoice(gs, "HaYeEun", "Event3", 9);
            // 3 + 9 + 2(재선택) = 14
            Assert.AreEqual(14, AffinityFormula.BasePoints(gs, "HaYeEun"));
            Assert.AreEqual(2, AffinityFormula.EventSelections(gs, "HaYeEun"));
        }

        [Test]
        public void RecordEventChoice_Event3_Roa_NoPlus2()
        {
            var gs = MakeState();
            AffinityFormula.RecordEventChoice(gs, "Roa", "Event1", 3);
            AffinityFormula.RecordEventChoice(gs, "Roa", "Event3", 9);
            Assert.AreEqual(12, AffinityFormula.BasePoints(gs, "Roa")); // 보정 없음
        }

        [Test]
        public void RecordEventChoice_Event3_DifferentHeroine_NoPlus2()
        {
            var gs = MakeState();
            AffinityFormula.RecordEventChoice(gs, "HaYeEun", "Event1", 3);
            AffinityFormula.RecordEventChoice(gs, "SeoDaEun", "Event3", 9);
            Assert.AreEqual(9, AffinityFormula.BasePoints(gs, "SeoDaEun")); // 직전 선택 아님
        }

        // ── 엔딩 판정 ──────────────────────────────────────────

        [Test]
        public void DetermineEnding_RoaHidden_TakesPriority()
        {
            var gs = MakeState();
            gs.SetStat("Fatigue", 70);
            AffinityFormula.AddPoint(gs, "Roa", PointCategory.Event, 43); // 43+3=46 ≥ 46
            // 동시에 다른 히로인을 큰 마진으로 자격 부여해도 로아 우선
            AffinityFormula.RecordEventChoice(gs, "DoHeewon", "Event1", 80);
            Assert.AreEqual("Roa", AffinityFormula.DetermineEndingHeroine(gs));
        }

        [Test]
        public void DetermineEnding_PicksMaxMargin()
        {
            var gs = MakeState(); // 스탯 0 → 보너스 0, 총점 = 기본
            AffinityFormula.RecordEventChoice(gs, "HaYeEun", "Event1", 35);  // 35-32 = 3
            AffinityFormula.RecordEventChoice(gs, "SeoDaEun", "Event2", 40); // 40-35 = 5
            Assert.AreEqual("SeoDaEun", AffinityFormula.DetermineEndingHeroine(gs));
        }

        [Test]
        public void DetermineEnding_RequiresEventSelection()
        {
            var gs = MakeState();
            // 임계치는 넘지만 이벤트 선택 0회 → 제외
            AffinityFormula.AddPoint(gs, "HaYeEun", PointCategory.Dialogue, 40);
            Assert.IsNull(AffinityFormula.DetermineEndingHeroine(gs));
        }

        [Test]
        public void DetermineEnding_NobodyEligible_ReturnsNull()
        {
            var gs = MakeState();
            AffinityFormula.RecordEventChoice(gs, "LeeBom", "Event1", 10); // 10 < 39
            Assert.IsNull(AffinityFormula.DetermineEndingHeroine(gs));
        }

        [Test]
        public void IsHappyEnding_ByThreshold()
        {
            var gs = MakeState();
            AffinityFormula.AddPoint(gs, "LeeBom", PointCategory.Event, 39); // = 임계치 39
            Assert.IsTrue(AffinityFormula.IsHappyEnding(gs, "LeeBom"));
            AffinityFormula.AddPoint(gs, "LeeBom", PointCategory.Event, -1); // 38 < 39
            Assert.IsFalse(AffinityFormula.IsHappyEnding(gs, "LeeBom"));
        }

        // ── lovePoints 동기화 ──────────────────────────────────

        [Test]
        public void AddPoint_SyncsLovePoints_WithBonus()
        {
            var gs = MakeState();
            gs.SetStat("Str", 10); // HaYeEun +3
            AffinityFormula.AddPoint(gs, "HaYeEun", PointCategory.Event, 20);
            Assert.AreEqual(23, gs.GetLove("HaYeEun")); // 총점(보너스 포함)이 lovePoints에 반영
        }

        // ── Definition 연결 (M2 slice2: GameBalanceSO → 정의표 교체) ──

        [Test]
        public void Configure_ReplacesDefs_FromSO()
        {
            var bal = MakeBalance(
                new GameBalanceSO.HeroineEntry { id = "Roa",     endingThreshold = 11, preferredStat = "Fatigue" },
                new GameBalanceSO.HeroineEntry { id = "TestGirl", endingThreshold = 77, preferredStat = "Str" });
            AffinityFormula.Configure(bal);

            Assert.AreEqual(2, AffinityFormula.Count);
            Assert.AreEqual(77, AffinityFormula.ThresholdOf("TestGirl")); // SO 값이 폴백을 대체
            Assert.AreEqual(11, AffinityFormula.ThresholdOf("Roa"));      // 폴백 46이 아님
        }

        [Test]
        public void Configure_NullOrEmpty_KeepsFallback()
        {
            AffinityFormula.Configure(null);
            Assert.AreEqual(5, AffinityFormula.Count);
            Assert.AreEqual(46, AffinityFormula.ThresholdOf("Roa"));

            AffinityFormula.Configure(MakeBalance()); // 빈 리스트
            Assert.AreEqual(46, AffinityFormula.ThresholdOf("Roa"));
        }

        [Test]
        public void ResetToFallback_RestoresConstants()
        {
            AffinityFormula.Configure(MakeBalance(
                new GameBalanceSO.HeroineEntry { id = "X", endingThreshold = 1, preferredStat = "Str" }));
            AffinityFormula.ResetToFallback();

            Assert.AreEqual(5, AffinityFormula.Count);
            Assert.AreEqual(46, AffinityFormula.ThresholdOf("Roa"));
            Assert.AreEqual(32, AffinityFormula.ThresholdOf("HaYeEun"));
        }

        // 실제 GameBalance.asset 내용이 인벤토리 §4 임계치와 일치하는지(드리프트 검증).
        [Test]
        public void RealAsset_Thresholds_Match_Inventory()
        {
            var bal = Resources.Load<GameBalanceSO>("Data/GameBalance");
            Assert.IsNotNull(bal, "Resources/Data/GameBalance.asset 로드 실패");
            AffinityFormula.Configure(bal);

            Assert.AreEqual(46, AffinityFormula.ThresholdOf("Roa"));
            Assert.AreEqual(32, AffinityFormula.ThresholdOf("HaYeEun"));
            Assert.AreEqual(35, AffinityFormula.ThresholdOf("SeoDaEun"));
            Assert.AreEqual(39, AffinityFormula.ThresholdOf("LeeBom"));
            Assert.AreEqual(43, AffinityFormula.ThresholdOf("DoHeewon"));
            Assert.AreEqual("Roa", AffinityFormula.HeroineIdAt(0)); // 인덱스 0 = 로아(히든 루트) 유지
        }

        // ── 히로인 id 경계 정규화 (3-스킴 혼동 → silent-0 방지) ──

        [Test]
        public void NormalizeId_AllForms_ResolveToCanonical()
        {
            Assert.AreEqual("SeoDaEun", AffinityFormula.NormalizeId("서다은"));   // 한글
            Assert.AreEqual("SeoDaEun", AffinityFormula.NormalizeId("Daeun"));    // 짧은 로마자(에셋 폴더 id)
            Assert.AreEqual("SeoDaEun", AffinityFormula.NormalizeId("c02"));      // 친구 코드 id
            Assert.AreEqual("HaYeEun",  AffinityFormula.NormalizeId("hayeeun"));  // 대소문자
            Assert.AreEqual("Roa",      AffinityFormula.NormalizeId("roa"));      // 정본 케이스 보정
            Assert.AreEqual("DoHeewon", AffinityFormula.NormalizeId("DoHeewon")); // 이미 정본
        }

        [Test]
        public void NormalizeId_Unknown_PassesThroughTrimmed_NoLog()
        {
            // 미등록 오타는 그대로 통과(로그 없음 — loud 거부는 FlowCommandInterpreter.Fail의 몫).
            Assert.AreEqual("Yeun", AffinityFormula.NormalizeId(" Yeun "));
            Assert.IsNull(AffinityFormula.NormalizeId(null));
        }

        [Test]
        public void IndexOf_IsCaseAndAliasInsensitive()
        {
            Assert.AreEqual(AffinityFormula.IndexOf("Roa"),     AffinityFormula.IndexOf("roa"));
            Assert.AreEqual(AffinityFormula.IndexOf("HaYeEun"), AffinityFormula.IndexOf("Yeeun")); // 짧은형 별칭
            Assert.AreEqual(AffinityFormula.IndexOf("HaYeEun"), AffinityFormula.IndexOf("하예은")); // 한글
        }

        [Test]
        public void Love_WrittenAndReadViaDifferentForms_StayConsistent()
        {
            var gs = MakeState();
            AffinityFormula.AddPoint(gs, "Daeun", PointCategory.Dialogue, 12); // 짧은형으로 적립

            // 어떤 형태로 읽어도 동일 — 키 분열 없음
            Assert.AreEqual(12, AffinityFormula.BasePoints(gs, "SeoDaEun"));
            Assert.AreEqual(12, AffinityFormula.BasePoints(gs, "서다은"));
            Assert.AreEqual(12, AffinityFormula.BasePoints(gs, "c02"));

            // 저장 키는 정본(B)로 단일화. GameStateSO 자체는 정규화 안 함(정규화는 호감도 API 경계에서만).
            Assert.AreEqual(12, gs.GetLove("SeoDaEun"));
            Assert.AreEqual(0,  gs.GetLove("Daeun"));
        }
    }
}
