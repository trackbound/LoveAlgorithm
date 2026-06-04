using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo; // EventScriptCatalogSO

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// EventScriptCatalogSO 순수 룩업(Resolve) 검증: 태그→스크립트 매핑 적중, 미매핑/빈 태그/null=null,
    /// 대소문자 구분(코드 정의 태그와 정확 일치), 인스턴스 Resolve가 정적 룩업에 위임. 더미 TextAsset으로 결정적.
    /// </summary>
    public class EventScriptCatalogTests
    {
        readonly List<Object> _tracked = new();

        TextAsset Dummy(string name)
        {
            var t = new TextAsset("dummy") { name = name };
            _tracked.Add(t);
            return t;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _tracked) if (o != null) Object.DestroyImmediate(o);
            _tracked.Clear();
        }

        static EventScriptCatalogSO.Entry E(string tag, TextAsset s) =>
            new EventScriptCatalogSO.Entry { eventTag = tag, script = s };

        [Test]
        public void Resolve_Returns_Mapped_Script()
        {
            var ev1 = Dummy("Event1");
            var list = new List<EventScriptCatalogSO.Entry> { E("Event1", ev1), E("Event2", Dummy("Event2")) };
            Assert.AreSame(ev1, EventScriptCatalogSO.Resolve(list, "Event1"));
        }

        [Test]
        public void Resolve_Unmapped_Tag_Returns_Null()
        {
            var list = new List<EventScriptCatalogSO.Entry> { E("Event1", Dummy("Event1")) };
            Assert.IsNull(EventScriptCatalogSO.Resolve(list, "Festival"));
        }

        [Test]
        public void Resolve_Null_Or_Empty_Tag_Returns_Null()
        {
            var list = new List<EventScriptCatalogSO.Entry> { E("Event1", Dummy("Event1")) };
            Assert.IsNull(EventScriptCatalogSO.Resolve(list, null));
            Assert.IsNull(EventScriptCatalogSO.Resolve(list, ""));
        }

        [Test]
        public void Resolve_Is_Case_Sensitive()
        {
            var list = new List<EventScriptCatalogSO.Entry> { E("Event1", Dummy("Event1")) };
            Assert.IsNull(EventScriptCatalogSO.Resolve(list, "event1"));
        }

        [Test]
        public void Instance_Resolve_Delegates_To_Static()
        {
            var ev1 = Dummy("Event1");
            var so = ScriptableObject.CreateInstance<EventScriptCatalogSO>();
            so.SetEntries(new List<EventScriptCatalogSO.Entry> { E("Event1", ev1) });
            try
            {
                Assert.AreSame(ev1, so.Resolve("Event1"));
                Assert.IsNull(so.Resolve("Nope"));
            }
            finally { Object.DestroyImmediate(so); }
        }
    }
}
