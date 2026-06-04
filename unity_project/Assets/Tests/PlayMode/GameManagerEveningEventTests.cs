using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo;          // EventScriptCatalogSO, GameConstants
using LoveAlgo.Core;     // GameStateSO, DayLoop, GameTimeline
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Events;   // DayEndRequested/DayChanged/PlayScript/NarrativeFinished
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 저녁 이벤트 씨임(M3 내러티브 ↔ 하루 루프 연결) PlayMode 검증: 오늘이 타임라인 이벤트 날이고 카탈로그에
    /// 스크립트가 매핑돼 있으면 GameManager가 하루 전환 전 <c>PlayScriptCommand</c>를 발행하고
    /// <c>NarrativeFinishedEvent</c>까지 대기한 뒤 <c>DayChangedEvent</c>를 발행한다. 이벤트가 없거나 카탈로그
    /// 미바인딩이면 즉시(동기) 전환(기존 계약 보존). 실제 NarrativeController 없이 씨임 거동만 검증 —
    /// 완료 통지는 테스트가 직접 발행한다.
    /// </summary>
    public class GameManagerEveningEventTests
    {
        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            DayLoop.BeginRun(so);
            return so;
        }

        // 타임라인에서 엔딩 경계가 아닌 첫 이벤트 날(태그 있음)을 찾는다 — 하드코딩 회피, 콘텐츠 변화에 견고.
        static bool TryFindEventDay(out int day, out string tag)
        {
            for (int d = 1; d < GameConstants.MaxDay; d++)
            {
                var info = GameTimeline.GetDayInfo(d);
                if (info != null && !string.IsNullOrEmpty(info.EventTag)) { day = d; tag = info.EventTag; return true; }
            }
            day = -1; tag = null; return false;
        }

        static EventScriptCatalogSO MakeCatalog(string tag, TextAsset script)
        {
            var cat = ScriptableObject.CreateInstance<EventScriptCatalogSO>();
            cat.SetEntries(new List<EventScriptCatalogSO.Entry>
            {
                new EventScriptCatalogSO.Entry { eventTag = tag, script = script }
            });
            return cat;
        }

        [UnityTest]
        public IEnumerator EventDay_Plays_Script_And_Awaits_Before_DayChange()
        {
            Assert.IsTrue(TryFindEventDay(out int day, out string tag), "타임라인에 (엔딩 경계 아닌) 이벤트 날 존재");

            var so = MakeState();
            so.Day = day;
            var dummy = new TextAsset("LineID,Type,Speaker,Value,Next\n,Text,,test,>") { name = "EveningTest_" + tag };
            var catalog = MakeCatalog(tag, dummy);

            var go = new GameObject("GM_Evening");
            var gm = go.AddComponent<GameManager>();
            gm.State = so;
            gm.EventScripts = catalog;

            bool played = false; string playedName = null; bool changed = false;
            var sPlay = EventBus.Subscribe<PlayScriptCommand>(e => { played = true; playedName = e.Name; });
            var sChange = EventBus.Subscribe<DayChangedEvent>(_ => changed = true);
            try
            {
                yield return null; // OnEnable 구독

                EventBus.Publish(new DayEndRequestedEvent(so.Day)); // 코루틴 시작 → PlayScript 발행 → WaitUntil 정지
                Assert.IsTrue(played, "이벤트 날: 하루 전환 전 PlayScriptCommand 발행");
                Assert.AreEqual(dummy.name, playedName, "발행된 스크립트가 매핑된 스크립트");
                Assert.IsFalse(changed, "내러티브 완료 전엔 하루 전환 안 됨");

                EventBus.Publish(new NarrativeFinishedEvent(dummy.name)); // 완료 통지
                yield return null; // WaitUntil 만족 → AdvanceDay
                Assert.IsTrue(changed, "내러티브 완료 후 하루 전환(DayChanged)");
                Assert.AreEqual(day + 1, so.Day, "일차 +1");
            }
            finally
            {
                sPlay.Dispose(); sChange.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(dummy);
            }
        }

        [UnityTest]
        public IEnumerator NonEventDay_Advances_Synchronously_Without_Script()
        {
            var so = MakeState(); // Day 1 — 이벤트 아님
            Assume.That(GameTimeline.GetDayInfo(so.Day)?.EventTag, Is.Null.Or.Empty, "Day1은 이벤트 날 아님");

            var dummy = new TextAsset("x") { name = "ShouldNotPlay" };
            var catalog = MakeCatalog("Event1", dummy); // 매핑은 있으나 오늘이 이벤트 날 아님

            var go = new GameObject("GM_NonEvent");
            var gm = go.AddComponent<GameManager>();
            gm.State = so;
            gm.EventScripts = catalog;

            bool played = false, changed = false;
            var sPlay = EventBus.Subscribe<PlayScriptCommand>(_ => played = true);
            var sChange = EventBus.Subscribe<DayChangedEvent>(_ => changed = true);
            try
            {
                yield return null;
                EventBus.Publish(new DayEndRequestedEvent(so.Day));
                Assert.IsFalse(played, "비이벤트 날: 스크립트 미발행");
                Assert.IsTrue(changed, "비이벤트 날: 즉시(동기) 하루 전환");
                Assert.AreEqual(2, so.Day, "일차 +1");
            }
            finally
            {
                sPlay.Dispose(); sChange.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(dummy);
            }
        }
    }
}
