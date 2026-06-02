using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;                 // EventBus
using LoveAlgo.Core;                   // GameStateSO
using LoveAlgo.Events;                 // 내러티브/UI/Affinity 이벤트
using LoveAlgo.Affinity;               // AffinityFormula (결정적 폴백)
using LoveAlgo.Story.StoryEngine;      // NarrativePlayer
using LoveAlgo.Story.StoryEngine.Flow; // FlowCommandRouter

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// M3 slice1 엔드투엔드: <see cref="NarrativePlayer"/>가 CSV를 받아 대사→선택지→효과→점프→종료까지
    /// 코루틴으로 구동하는지. 뷰 없이 명령 이벤트를 구독해 완료 핸들을 즉시 채우는 방식으로 결정적으로 검증한다
    /// (대사=Complete, 선택지=Select). 호감도는 같은 state에 붙인 FlowCommandRouter가 실제 적용한다.
    /// </summary>
    public class NarrativePlayerPlayModeTests
    {
        const string Csv =
            "LineID,Type,Speaker,Value,Next\n" +
            ",Text,로아,안녕,click\n" +
            ",Choice,,,>\n" +
            ",Option,,선택A|done|Love:HaYeEun:3,>\n" +
            ",Option,,선택B|other|Stat:Int:5,>\n" +
            "done,Text,,끝,click\n" +
            ",Flow,,End,>\n" +
            "other,Text,,다른길,click\n";

        GameStateSO _gs;
        GameObject _routerGo;
        GameObject _playerGo;
        readonly List<IDisposable> _subs = new();

        // 캡처
        readonly List<string> _dialogues = new();
        readonly List<UIGroup> _groups = new();
        int _choiceCount;
        string _affHero;
        int _affScore = int.MinValue;
        bool _finished;
        string _finishedName;

        NarrativePlayer SetUp(int selectIndex)
        {
            // NUnit은 픽스처 인스턴스를 테스트 간 공유한다 — 캡처 상태를 매 테스트 초기화(누수 방지).
            _dialogues.Clear();
            _groups.Clear();
            _choiceCount = 0;
            _affHero = null;
            _affScore = int.MinValue;
            _finished = false;
            _finishedName = null;

            AffinityFormula.ResetToFallback(); // 스탯 0 → 보너스 0 → Dialogue 3점 = 3

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            // 구독은 PlayScriptCommand 발행 전에 — 첫 ShowUiGroupCommand(Narrative)까지 캡처.
            _subs.Add(EventBus.Subscribe<ShowUiGroupCommand>(e => _groups.Add(e.Group)));
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { _dialogues.Add(e.Text); e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<ShowChoiceCommand>(e => { _choiceCount++; e.Handle.Select(selectIndex); }));
            _subs.Add(EventBus.Subscribe<AffinityChangedEvent>(e => { _affHero = e.HeroineId; _affScore = e.NewScore; }));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(e => { _finished = true; _finishedName = e.ScriptName; }));

            _routerGo = new GameObject("Router");
            _routerGo.AddComponent<FlowCommandRouter>().State = _gs;

            _playerGo = new GameObject("Player");
            var player = _playerGo.AddComponent<NarrativePlayer>();
            player.State = _gs;
            return player;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_playerGo != null) UnityEngine.Object.DestroyImmediate(_playerGo);
            if (_routerGo != null) UnityEngine.Object.DestroyImmediate(_routerGo);
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        static IEnumerator WaitUntilDone(NarrativePlayer player)
        {
            int guard = 0;
            while (player.IsRunning && guard++ < 600) yield return null;
            Assert.IsFalse(player.IsRunning, "스크립트가 600프레임 내 종료되어야 함");
        }

        [UnityTest]
        public IEnumerator Run_PathA_Choice_Applies_Affinity_Jumps_And_Finishes()
        {
            var player = SetUp(selectIndex: 0);
            yield return null; // OnEnable 구독 정착

            EventBus.Publish(new PlayScriptCommand(Csv, "slice1"));
            yield return WaitUntilDone(player);

            CollectionAssert.AreEqual(new[] { "안녕", "끝" }, _dialogues, "안녕 → 선택A(done 점프) → 끝");
            Assert.AreEqual(1, _choiceCount, "선택지 1회 표시");

            Assert.AreEqual("HaYeEun", _affHero, "Love 효과 → Affinity 카테고리로 위임 → Router가 통지");
            Assert.AreEqual(3, _affScore, "Dialogue 3점(폴백, 보너스 0)");
            Assert.AreEqual(3, _gs.GetLove("HaYeEun"), "공식 동기화 총점이 lovePoints에 반영");

            Assert.IsTrue(_finished, "NarrativeFinishedEvent 발행");
            Assert.AreEqual("slice1", _finishedName);

            Assert.AreEqual(UIGroup.Narrative, _groups[0], "시작 시 Narrative 그룹");
            Assert.AreEqual(UIGroup.Simulation, _groups[_groups.Count - 1], "종료 시 Simulation 복귀");
        }

        [UnityTest]
        public IEnumerator Run_PathB_Choice_Applies_Stat_And_Jumps_Other()
        {
            var player = SetUp(selectIndex: 1);
            yield return null;

            EventBus.Publish(new PlayScriptCommand(Csv, "slice1"));
            yield return WaitUntilDone(player);

            CollectionAssert.AreEqual(new[] { "안녕", "다른길" }, _dialogues, "안녕 → 선택B(other 점프) → 다른길");
            Assert.AreEqual(5, _gs.GetStat("Int"), "Stat:Int:5 즉시 적용");
            Assert.AreEqual(int.MinValue, _affScore, "Path B는 호감도 변경 없음");
            Assert.IsTrue(_finished);
        }
    }
}
