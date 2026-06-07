using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Core;              // GameStateSO
using LoveAlgo.Events;            // ShowBackgroundCommand, ShowCharacterCommand, ShowDialogueCommand, PlayBgmCommand
using LoveAlgo.Story;             // ResourceAliasCatalogSO
using LoveAlgo.Story.StoryEngine; // NarrativeController
using LoveAlgo.Game;              // PhaseController (격리 청소용)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 별칭 카탈로그 해석 배선 검증: NarrativeController가 명령 발행 **전** 작가 한글명→코드ID로 해석하는지
    /// (BG/Char+기본표정/BGM/화자 SpeakerId). 뷰 없이 명령 이벤트를 구독해 결정적으로 검증 — 뷰는 카탈로그 무지.
    /// </summary>
    public class AliasResolutionPlayModeTests
    {
        GameStateSO _gs;
        GameObject _playerGo;
        ResourceAliasCatalogSO _catalog;
        readonly List<IDisposable> _subs = new();

        NarrativeController SetUp()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(pc.gameObject);

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            _catalog = ScriptableObject.CreateInstance<ResourceAliasCatalogSO>();
            // 테스트 카탈로그(런타임 구성 — 인스펙터 편집과 동치). 실 에셋과 무관하게 결정적.
            SetList(_catalog, "bg", E("bg_20_05", "공대 강의실 낮"));
            SetList(_catalog, "characters", E("c01", "로아"));
            SetList(_catalog, "emotes", E("41", "깜짝"));
            SetList(_catalog, "bgm", E("daily1", "일상1"));

            _playerGo = new GameObject("Player");
            var player = _playerGo.AddComponent<NarrativeController>();
            player.State = _gs;
            player.AliasCatalog = _catalog;
            return player;
        }

        static ResourceAliasCatalogSO.Entry E(string id, params string[] aliases)
            => new ResourceAliasCatalogSO.Entry { id = id, aliases = aliases };

        // 직렬화 필드 주입(테스트 전용 — SerializedObject 없이 리플렉션으로 인스펙터 편집을 모사).
        static void SetList(ResourceAliasCatalogSO so, string field, params ResourceAliasCatalogSO.Entry[] entries)
        {
            var f = typeof(ResourceAliasCatalogSO).GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.SetValue(so, new List<ResourceAliasCatalogSO.Entry>(entries));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_playerGo != null) UnityEngine.Object.DestroyImmediate(_playerGo);
            if (_catalog != null) UnityEngine.Object.DestroyImmediate(_catalog);
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        static IEnumerator WaitUntilDone(NarrativeController player)
        {
            int guard = 0;
            while (player.IsRunning && guard++ < 600) yield return null;
            Assert.IsFalse(player.IsRunning, "스크립트가 600프레임 내 종료되어야 함");
        }

        [UnityTest]
        public IEnumerator Bg_Char_Bgm_Speaker_Aliases_Resolved_Before_Publish()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",BG,,공대 강의실 낮:Cut,>\n" +
                ",Sound,,BGM:일상1,>\n" +
                ",Char,,C:Enter:로아,>\n" +          // 표정 생략 → 기본표정 00 보정
                ",Text,로아,안녕,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            string bg = null, bgm = null, ch = null, em = null, speaker = null, speakerId = null;
            _subs.Add(EventBus.Subscribe<ShowBackgroundCommand>(e => { bg = e.Name; e.Handle?.Complete(); }));
            _subs.Add(EventBus.Subscribe<PlayBgmCommand>(e => bgm = e.Name));
            _subs.Add(EventBus.Subscribe<ShowCharacterCommand>(e => { ch = e.Character; em = e.Emote; e.Handle?.Complete(); }));
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { speaker = e.Speaker; speakerId = e.SpeakerId; e.Handle.Complete(); }));

            EventBus.Publish(new PlayScriptCommand(csv, "alias"));
            yield return WaitUntilDone(player);

            Assert.AreEqual("bg_20_05", bg, "BG 한글명 → 코드ID");
            Assert.AreEqual("daily1", bgm, "BGM 한글명 → 코드ID");
            Assert.AreEqual("c01", ch, "Char 한글명 → 코드ID");
            Assert.AreEqual("00", em, "Enter 표정 생략 → 기본표정 보정");
            Assert.AreEqual("로아", speaker, "표시 화자는 원문 유지");
            Assert.AreEqual("c01", speakerId, "SpeakerId = 해석된 코드ID");
        }

        [UnityTest]
        public IEnumerator Unregistered_Names_Pass_Through_And_Null_Catalog_Safe()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",BG,,bg_60_01:Cut,>\n" +          // 미등록 코드명 → 그대로
                ",Text,{{Player}},독백,click\n" +  // 미등록 화자 → SpeakerId null
                ",Flow,,End,>\n";

            var player = SetUp();
            yield return null;

            string bg = null, speakerId = "sentinel";
            _subs.Add(EventBus.Subscribe<ShowBackgroundCommand>(e => { bg = e.Name; e.Handle?.Complete(); }));
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { speakerId = e.SpeakerId; e.Handle.Complete(); }));

            EventBus.Publish(new PlayScriptCommand(csv, "alias"));
            yield return WaitUntilDone(player);

            Assert.AreEqual("bg_60_01", bg, "미등록 이름 passthrough");
            Assert.IsNull(speakerId, "미등록 화자 SpeakerId=null(뷰는 Speaker 폴백)");

            // 카탈로그 미바인딩이어도 안전(전부 passthrough).
            player.AliasCatalog = null;
            bg = null;
            EventBus.Publish(new PlayScriptCommand(csv, "alias2"));
            yield return WaitUntilDone(player);
            Assert.AreEqual("bg_60_01", bg, "카탈로그 null → 원문 그대로");
        }
    }
}
