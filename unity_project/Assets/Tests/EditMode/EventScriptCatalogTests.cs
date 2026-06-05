using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo; // EventScriptCatalogSO

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// EventScriptCatalogSO 순수 룩업(Resolve) 검증: 태그→CSV 상대경로 매핑 적중, 미매핑/빈 태그/null=null,
    /// 대소문자 구분, 인스턴스 Resolve가 정적 룩업에 위임.
    /// </summary>
    public class EventScriptCatalogTests
    {
        static EventScriptCatalogSO.Entry E(string tag, string path) =>
            new EventScriptCatalogSO.Entry { eventTag = tag, csvPath = path };

        [Test]
        public void Resolve_Returns_Mapped_Path()
        {
            var list = new List<EventScriptCatalogSO.Entry> { E("Event1", "Event1.csv"), E("Event2", "Event2.csv") };
            Assert.AreEqual("Event1.csv", EventScriptCatalogSO.Resolve(list, "Event1"));
        }

        [Test]
        public void Resolve_Unmapped_Tag_Returns_Null()
        {
            var list = new List<EventScriptCatalogSO.Entry> { E("Event1", "Event1.csv") };
            Assert.IsNull(EventScriptCatalogSO.Resolve(list, "Festival"));
        }

        [Test]
        public void Resolve_Null_Or_Empty_Tag_Returns_Null()
        {
            var list = new List<EventScriptCatalogSO.Entry> { E("Event1", "Event1.csv") };
            Assert.IsNull(EventScriptCatalogSO.Resolve(list, null));
            Assert.IsNull(EventScriptCatalogSO.Resolve(list, ""));
        }

        [Test]
        public void Resolve_Is_Case_Sensitive()
        {
            var list = new List<EventScriptCatalogSO.Entry> { E("Event1", "Event1.csv") };
            Assert.IsNull(EventScriptCatalogSO.Resolve(list, "event1"));
        }

        [Test]
        public void Instance_Resolve_Delegates_To_Static()
        {
            var so = ScriptableObject.CreateInstance<EventScriptCatalogSO>();
            so.SetEntries(new List<EventScriptCatalogSO.Entry> { E("Event1", "Event1.csv") });
            try
            {
                Assert.AreEqual("Event1.csv", so.Resolve("Event1"));
                Assert.IsNull(so.Resolve("Nope"));
            }
            finally { Object.DestroyImmediate(so); }
        }
    }
}
