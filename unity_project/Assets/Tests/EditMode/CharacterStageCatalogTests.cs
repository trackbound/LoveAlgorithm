using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.UI;
using Entry = LoveAlgo.UI.CharacterStageCatalogSO.Entry;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// CharacterStageCatalogSO 순수 결정층 단위테스트(SO 인스턴스 불필요): ID 조회(대소문자·미등록·null) +
    /// 슬롯 기본 위 합성(ScaleFrom/PositionFrom). 적용(StageView 슬롯 RectTransform 반영)은 뷰 책임.
    /// </summary>
    public class CharacterStageCatalogTests
    {
        static List<Entry> Make() => new List<Entry>
        {
            new Entry { characterId = "c01", scale = 0.5f, offset = new Vector2(10, -5) },
            new Entry { characterId = "c02", scale = 0.8f, offset = new Vector2(-20, 0) },
        };

        [Test]
        public void Resolve_RegisteredId_ReturnsEntryPlacement()
        {
            var p = CharacterStageCatalogSO.Resolve(Make(), "c01");
            Assert.AreEqual(0.5f, p.Scale);
            Assert.AreEqual(new Vector2(10, -5), p.Offset);
        }

        [Test]
        public void Resolve_IsCaseInsensitive()
        {
            Assert.AreEqual(0.5f, CharacterStageCatalogSO.Resolve(Make(), "C01").Scale);
        }

        [Test]
        public void Resolve_UnknownOrNull_ReturnsIdentity()
        {
            Assert.AreEqual(1f, CharacterStageCatalogSO.Resolve(Make(), "c99").Scale);
            Assert.AreEqual(Vector2.zero, CharacterStageCatalogSO.Resolve(Make(), "c99").Offset);
            Assert.AreEqual(1f, CharacterStageCatalogSO.Resolve(null, "c01").Scale);
            Assert.AreEqual(1f, CharacterStageCatalogSO.Resolve(Make(), null).Scale);
        }

        [Test]
        public void Placement_ComposesOnSlotBase()
        {
            var p = CharacterStageCatalogSO.Resolve(Make(), "c01"); // scale .5, offset (10,-5)
            Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.5f), p.ScaleFrom(Vector3.one));
            Assert.AreEqual(new Vector2(-510, -5), p.PositionFrom(new Vector2(-520, 0)));
        }

        [Test]
        public void Identity_LeavesSlotBaseUnchanged()
        {
            var p = StagePlacement.Identity;
            Assert.AreEqual(new Vector3(2, 2, 2), p.ScaleFrom(new Vector3(2, 2, 2)));
            Assert.AreEqual(new Vector2(-520, 0), p.PositionFrom(new Vector2(-520, 0)));
        }
    }
}
