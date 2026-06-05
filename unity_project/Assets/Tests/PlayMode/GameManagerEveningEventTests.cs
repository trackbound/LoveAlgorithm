using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo;          // EventScriptCatalogSO, GameConstants
using LoveAlgo.Core;     // GameStateSO, DayLoop, GameTimeline, NarrativeFlowGate
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Events;   // DayEndRequested/DayChanged/PlayScript/NarrativeFinished
using LoveAlgo.Story;    // StoryAssetLoader
using GameManager = LoveAlgo.Game.GameManager;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 저녁 이벤트 씨임(StreamingAssets 로딩 + 흐름 게이트) PlayMode 검증: 이벤트 날이고 카탈로그에 경로가
    /// 매핑돼 있으면 GameManager가 StoryAssetLoader로 파일을 읽어 <c>PlayScriptCommand</c> 발행 + <c>NarrativeFlowGate</c>
    /// 잠금, <c>NarrativeFinishedEvent</c>까지 대기 후 DayChanged·게이트 해제. 비이벤트/카탈로그 null은 즉시 동기 전환.
    /// 임시 CSV는 StreamingAssets/Story에 쓰고 TearDown에서 삭제.
    /// </summary>
    public class GameManagerEveningEventTests
    {
        readonly List<string> _tempFiles = new();

        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            DayLoop.BeginRun(so);
            return so;
        }

        static bool TryFindEventDay(out int day, out string tag)
        {
            for (int d = 1; d < GameConstants.MaxDay; d++)
            {
                var info = GameTimeline.GetDayInfo(d);
                if (info != null && !string.IsNullOrEmpty(info.EventTag)) { day = d; tag = info.EventTag; return true; }
            }
            day = -1; tag = null; return false;
        }

        static EventScriptCatalogSO MakeCatalog(string tag, string csvPath)
        {
            var cat = ScriptableObject.CreateInstance<EventScriptCatalogSO>();
            cat.SetEntries(new List<EventScriptCatalogSO.Entry>
            {
                new EventScriptCatalogSO.Entry { eventTag = tag, csvPath = csvPath }
            });
            return cat;
        }

        string WriteTempStory(string name, string csv)
        {
            StoryAssetLoader.Write(name, csv);
            _tempFiles.Add(name);
            return name;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var rel in _tempFiles)
            {
                var path = Path.Combine(Application.streamingAssetsPath, "Story", rel);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
            }
            _tempFiles.Clear();
            NarrativeFlowGate.Reset(); // 안전(테스트 간 누수 방지)
        }

        [UnityTest]
        public IEnumerator EventDay_Plays_Script_Locks_Gate_And_Awaits_Before_DayChange()
        {
            Assert.IsTrue(TryFindEventDay(out int day, out string tag), "타임라인에 (엔딩 경계 아닌) 이벤트 날 존재");

            var so = MakeState();
            so.Day = day;
            string relPath = WriteTempStory($"__evening_{tag}.csv", "LineID,Type,Speaker,Value,Next\n,Text,,test,>");
            var catalog = MakeCatalog(tag, relPath);

            var go = new GameObject("GM_Evening");
            var gm = go.AddComponent<GameManager>();
            gm.State = so;
            gm.EventScripts = catalog;

            bool played = false; string playedName = null; bool changed = false;
            var sPlay = EventBus.Subscribe<PlayScriptCommand>(e => { played = true; playedName = e.Name; });
            var sChange = EventBus.Subscribe<DayChangedEvent>(_ => changed = true);
            try
            {
                yield return null;

                EventBus.Publish(new DayEndRequestedEvent(so.Day)); // 코루틴: 파일 읽기 → PlayScript 발행 → 게이트 잠금 → WaitUntil
                yield return null;
                Assert.IsTrue(played, "이벤트 날: 하루 전환 전 PlayScriptCommand 발행");
                Assert.AreEqual(relPath, playedName, "발행 이름=매핑된 CSV 경로");
                Assert.IsTrue(NarrativeFlowGate.IsLocked, "씨임 대기 중 흐름 게이트 잠김(도구 Apply 차단)");
                Assert.IsFalse(changed, "내러티브 완료 전엔 하루 전환 안 됨");

                EventBus.Publish(new NarrativeFinishedEvent(relPath));
                yield return null;
                Assert.IsTrue(changed, "내러티브 완료 후 하루 전환(DayChanged)");
                Assert.IsFalse(NarrativeFlowGate.IsLocked, "완료 후 게이트 해제");
                Assert.AreEqual(day + 1, so.Day, "일차 +1");
            }
            finally
            {
                sPlay.Dispose(); sChange.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
                Object.DestroyImmediate(catalog);
            }
        }

        [UnityTest]
        public IEnumerator NonEventDay_Advances_Synchronously_Without_Script()
        {
            var so = MakeState(); // Day 1 — 이벤트 아님
            Assume.That(GameTimeline.GetDayInfo(so.Day)?.EventTag, Is.Null.Or.Empty, "Day1은 이벤트 날 아님");

            var catalog = MakeCatalog("Event1", "Event1.csv"); // 매핑 있으나 오늘이 이벤트 날 아님(파일 불필요)

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
                Assert.IsFalse(NarrativeFlowGate.IsLocked, "게이트 잠기지 않음");
                Assert.AreEqual(2, so.Day, "일차 +1");
            }
            finally
            {
                sPlay.Dispose(); sChange.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(so);
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
