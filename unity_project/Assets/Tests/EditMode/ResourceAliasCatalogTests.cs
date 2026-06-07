using System.Collections.Generic;
using LoveAlgo.Story;
using NUnit.Framework;
using UnityEngine;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 순수 별칭 해석(<see cref="ResourceAliasCatalogSO.Resolve(IReadOnlyList{ResourceAliasCatalogSO.Entry}, string)"/>)
    /// 검증: id/별칭 대소문자 무시 일치 → 정본 id, 미등록 → passthrough(trim).
    /// </summary>
    public class ResourceAliasCatalogTests
    {
        static ResourceAliasCatalogSO.Entry E(string id, params string[] aliases)
            => new ResourceAliasCatalogSO.Entry { id = id, aliases = aliases };

        static readonly List<ResourceAliasCatalogSO.Entry> Bg = new()
        {
            E("bg_00_00", "빈 화면"),
            E("bg_20_05", "공대 강의실 낮"),
        };

        [Test]
        public void Resolve_별칭_한글명을_코드ID로()
        {
            Assert.AreEqual("bg_20_05", ResourceAliasCatalogSO.Resolve(Bg, "공대 강의실 낮"));
            Assert.AreEqual("bg_00_00", ResourceAliasCatalogSO.Resolve(Bg, "빈 화면"));
        }

        [Test]
        public void Resolve_ID직접기입은_정본ID_케이스정규화()
        {
            var bgm = new List<ResourceAliasCatalogSO.Entry> { E("daily1", "일상1"), E("night") };
            Assert.AreEqual("daily1", ResourceAliasCatalogSO.Resolve(bgm, "Daily1")); // 케이스 무시 → 정본
            Assert.AreEqual("night", ResourceAliasCatalogSO.Resolve(bgm, "Night"));
            Assert.AreEqual("daily1", ResourceAliasCatalogSO.Resolve(bgm, "daily1"));
        }

        [Test]
        public void Resolve_미등록은_입력그대로_passthrough()
        {
            Assert.AreEqual("bg_60_01", ResourceAliasCatalogSO.Resolve(Bg, "bg_60_01"));
            Assert.AreEqual("없는 이름", ResourceAliasCatalogSO.Resolve(Bg, "없는 이름"));
        }

        [Test]
        public void Resolve_trim_및_빈입력()
        {
            Assert.AreEqual("bg_20_05", ResourceAliasCatalogSO.Resolve(Bg, " 공대 강의실 낮 "));
            Assert.IsNull(ResourceAliasCatalogSO.Resolve(Bg, null));
            Assert.AreEqual("", ResourceAliasCatalogSO.Resolve(Bg, ""));
            Assert.AreEqual("x", ResourceAliasCatalogSO.Resolve(null, "x")); // 목록 null = passthrough
        }

        [Test]
        public void Resolve_널엔트리_빈ID는_건너뜀()
        {
            var list = new List<ResourceAliasCatalogSO.Entry> { null, E(""), E("c01", "로아") };
            Assert.AreEqual("c01", ResourceAliasCatalogSO.Resolve(list, "로아"));
        }

        [Test]
        public void TryResolveCharacter_등록시만_true()
        {
            var so = ScriptableObject.CreateInstance<ResourceAliasCatalogSO>();
            try
            {
                // SO 인스턴스 경로는 빈 카탈로그 → 실패해야 함(passthrough와 구분).
                Assert.IsFalse(so.TryResolveCharacter("로아", out _));
                Assert.IsFalse(so.TryResolveCharacter("{{Player}}", out _));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void IsRegistered_별칭과_ID_모두()
        {
            Assert.IsTrue(ResourceAliasCatalogSO.IsRegistered(Bg, "공대 강의실 낮"));
            Assert.IsTrue(ResourceAliasCatalogSO.IsRegistered(Bg, "BG_20_05")); // 케이스 무시
            Assert.IsFalse(ResourceAliasCatalogSO.IsRegistered(Bg, "미등록"));
        }
    }
}
