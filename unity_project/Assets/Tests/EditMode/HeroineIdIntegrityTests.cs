using System.Linq;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo;          // GameBalanceSO
using LoveAlgo.Affinity; // AffinityFormula
using LoveAlgo.Story;    // ResourceAliasCatalogSO

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 히로인 id 3-스킴 드리프트 가드. 같은 5명이 도메인마다 다른 키로 불린다
    /// (에셋/캐릭터=짧은 로마자 A, 호감도=긴 로마자 B, 친구=c0X). 이들이 서로 어긋나면 호감도가
    /// silent 0으로 새거나(키 분열) 스플래시/아트가 안 뜬다. 아래 한 표(Rows)를 SSOT로 두고:
    ///   ① GameBalance.asset heroine id = 긴 로마자(B) 5종과 집합 일치
    ///   ② AffinityFormula.NormalizeId가 모든 형태(한글/짧은/긴/c0X/대소문자) → B (AliasToCanonical↔GameBalance 드리프트 차단)
    ///   ③ Resources/UI/Loading 스플래시가 B별로 존재 (LoadingScene:키 매칭 보장)
    ///   ④ ResourceAliasCatalog가 모든 형태 → 짧은 로마자(A, 아트 폴더 id)
    /// 5명/이름 변경 시 이 표와 모든 소스를 같이 고치라는 의도된 마찰. (참조: heroine-id-schemes 메모)
    /// </summary>
    public class HeroineIdIntegrityTests
    {
        struct Forms { public string Korean, ShortA, LongB, Code; }

        static readonly Forms[] Rows =
        {
            new Forms { Korean = "로아",   ShortA = "Roa",    LongB = "Roa",      Code = "c01" },
            new Forms { Korean = "서다은", ShortA = "Daeun",  LongB = "SeoDaEun", Code = "c02" },
            new Forms { Korean = "하예은", ShortA = "Yeeun",  LongB = "HaYeEun",  Code = "c03" },
            new Forms { Korean = "도희원", ShortA = "Heewon", LongB = "DoHeewon", Code = "c04" },
            new Forms { Korean = "이봄",   ShortA = "Bom",    LongB = "LeeBom",   Code = "c05" },
        };

        // Configure가 전역 static 정의표를 바꾸므로 각 테스트 후 폴백 복원(격리).
        [TearDown] public void RestoreFallbackDefs() => AffinityFormula.ResetToFallback();

        [Test]
        public void GameBalance_HeroineIds_Match_CanonicalB_Set()
        {
            var bal = Resources.Load<GameBalanceSO>("Data/GameBalance");
            Assert.IsNotNull(bal, "Resources/Data/GameBalance.asset 존재");

            CollectionAssert.AreEquivalent(
                Rows.Select(r => r.LongB).ToList(),
                bal.Heroines.Select(h => h.id).ToList(),
                "GameBalance heroine id = 호감도 정본(긴 로마자) 5종과 집합 일치해야 함");
        }

        [Test]
        public void NormalizeId_EveryForm_Resolves_To_CanonicalB()
        {
            var bal = Resources.Load<GameBalanceSO>("Data/GameBalance");
            Assert.IsNotNull(bal);
            AffinityFormula.Configure(bal); // 실제 정의표 기준 정규화 검증(폴백 아님)

            foreach (var r in Rows)
            {
                Assert.AreEqual(r.LongB, AffinityFormula.NormalizeId(r.Korean), $"{r.Korean}(한글) → {r.LongB}");
                Assert.AreEqual(r.LongB, AffinityFormula.NormalizeId(r.ShortA), $"{r.ShortA}(짧은 로마자) → {r.LongB}");
                Assert.AreEqual(r.LongB, AffinityFormula.NormalizeId(r.Code),   $"{r.Code}(친구 id) → {r.LongB}");
                Assert.AreEqual(r.LongB, AffinityFormula.NormalizeId(r.LongB),  "이미 정본");
                Assert.AreEqual(r.LongB, AffinityFormula.NormalizeId(r.LongB.ToLowerInvariant()), "대소문자 보정");
                Assert.GreaterOrEqual(AffinityFormula.IndexOf(r.LongB), 0, $"{r.LongB} 정의표에 존재");
            }
        }

        [Test]
        public void Splash_Exists_For_Each_CanonicalB()
        {
            var splashes = Resources.LoadAll<Sprite>("UI/Loading");
            Assert.IsNotNull(splashes);
            Assert.Greater(splashes.Length, 0, "Resources/UI/Loading 스플래시 존재");

            foreach (var r in Rows)
            {
                bool has = splashes.Any(s => s != null &&
                    s.name.IndexOf(r.LongB, System.StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.IsTrue(has, $"Load_{r.LongB}_* 스플래시가 있어야 LoadingScene:{r.LongB}가 매칭");
            }
        }

        [Test]
        public void AliasCatalog_Resolves_AllForms_To_ShortA()
        {
            var cat = Resources.Load<ResourceAliasCatalogSO>("Data/ResourceAliasCatalog");
            Assert.IsNotNull(cat, "Resources/Data/ResourceAliasCatalog.asset 존재");

            foreach (var r in Rows)
            {
                Assert.AreEqual(r.ShortA, cat.ResolveCharacter(r.Korean), $"{r.Korean}(한글) → {r.ShortA}(아트 폴더 id)");
                Assert.AreEqual(r.ShortA, cat.ResolveCharacter(r.LongB),  $"{r.LongB}(긴 로마자) → {r.ShortA}");
                Assert.AreEqual(r.ShortA, cat.ResolveCharacter(r.Code),   $"{r.Code}(친구 id) → {r.ShortA}");
                Assert.AreEqual(r.ShortA, cat.ResolveCharacter(r.ShortA), "이미 A 정본");
            }
        }
    }
}
