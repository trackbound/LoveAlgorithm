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
using LoveAlgo.Story.StoryEngine;      // NarrativeController
using LoveAlgo.Story.StoryEngine.Flow; // FlowCommandController
using LoveAlgo.Game;                   // PhaseController (격리 청소용)

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// M3 slice1 엔드투엔드: <see cref="NarrativeController"/>가 CSV를 받아 대사→선택지→효과→점프→종료까지
    /// 코루틴으로 구동하는지. 뷰 없이 명령 이벤트를 구독해 완료 핸들을 즉시 채우는 방식으로 결정적으로 검증한다
    /// (대사=Complete, 선택지=Select). 호감도는 같은 state에 붙인 FlowCommandController가 실제 적용한다.
    /// </summary>
    public class NarrativeControllerPlayModeTests
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
        readonly List<ScreenPhase> _phases = new();
        int _choiceCount;
        List<string> _lastChoiceTexts;
        string _affHero;
        int _affScore = int.MinValue;
        bool _finished;
        string _finishedName;

        NarrativeController SetUp(int selectIndex)
        {
            // 격리: GameScene 테스트가 Game.unity를 Single 로드한 채 언로드하지 않아, 그 씬의
            // NarrativeController/FlowCommandController가 남아 있을 수 있다. 남으면 PlayScriptCommand/
            // FlowCommandRequestedEvent를 중복 처리(대사 2배·호감도 2배)하므로 먼저 제거한다.
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var r in UnityEngine.Object.FindObjectsByType<FlowCommandController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(r.gameObject);
            foreach (var pc in UnityEngine.Object.FindObjectsByType<PhaseController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(pc.gameObject);

            // NUnit은 픽스처 인스턴스를 테스트 간 공유한다 — 캡처 상태를 매 테스트 초기화(누수 방지).
            _dialogues.Clear();
            _phases.Clear();
            _choiceCount = 0;
            _lastChoiceTexts = null;
            _affHero = null;
            _affScore = int.MinValue;
            _finished = false;
            _finishedName = null;

            AffinityFormula.ResetToFallback(); // 스탯 0 → 보너스 0 → Dialogue 3점 = 3

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();

            // 구독은 PlayScriptCommand 발행 전에 — 첫 RequestPhaseCommand(Story)까지 캡처.
            _subs.Add(EventBus.Subscribe<RequestPhaseCommand>(e => _phases.Add(e.Target)));
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => { _dialogues.Add(e.Text); e.Handle.Complete(); }));
            _subs.Add(EventBus.Subscribe<ShowChoiceCommand>(e => { _choiceCount++; _lastChoiceTexts = new List<string>(e.OptionTexts); e.Handle.Select(selectIndex); }));
            _subs.Add(EventBus.Subscribe<AffinityChangedEvent>(e => { _affHero = e.HeroineId; _affScore = e.NewScore; }));
            _subs.Add(EventBus.Subscribe<NarrativeFinishedEvent>(e => { _finished = true; _finishedName = e.ScriptName; }));

            _routerGo = new GameObject("Router");
            _routerGo.AddComponent<FlowCommandController>().State = _gs;

            _playerGo = new GameObject("Player");
            var player = _playerGo.AddComponent<NarrativeController>();
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

        static IEnumerator WaitUntilDone(NarrativeController player)
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

            Assert.AreEqual(ScreenPhase.Story, _phases[0], "시작 시 Story 페이즈 요청");
            Assert.AreEqual(ScreenPhase.Schedule, _phases[_phases.Count - 1], "종료 시 Schedule 복귀 요청");
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

        [UnityTest]
        public IEnumerator Mark_Recorded_On_Choice_Then_If_Chose_Branches()
        {
            var player = SetUp(selectIndex: 0); // 옵션0 = mark:met_roa 선택
            yield return null;

            const string markCsv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Choice,,,>\n" +
                ",Option,,로아 선택|met|mark:met_roa,>\n" +
                ",Option,,다른 선택|met,>\n" +
                "met,Text,,로아루트,click\n" +
                ",Flow,,If:Chose:met_roa:gotit,>\n" +
                ",Text,,놓침,click\n" +
                ",Flow,,End,>\n" +
                "gotit,Text,,기억함,click\n" +
                ",Flow,,End,>\n";

            EventBus.Publish(new PlayScriptCommand(markCsv, "marktest"));
            yield return WaitUntilDone(player);

            Assert.IsTrue(_gs.HasChosen("met_roa"), "선택 확정 시 마커 기록");
            CollectionAssert.AreEqual(new[] { "로아루트", "기억함" }, _dialogues,
                "If:Chose:met_roa 참 → gotit 점프(놓침 미실행)");
        }

        // ── A·B·C 슬라이스: 조건 분기 런타임(If 점프 / 선택지 필터 / Flag 쓰기) ──

        [UnityTest]
        public IEnumerator Flag_Write_Then_If_True_Jumps()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,Flag:met_roa,>\n" +           // C: 플래그 쓰기(무통지)
                ",Text,,시작,click\n" +
                ",Flow,,If:Flag:met_roa:after,>\n" +  // A: 참 → after 점프
                ",Text,,건너뜀,click\n" +             // 점프로 스킵돼야 함
                "after,Text,,도착,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp(selectIndex: 0);
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "branch"));
            yield return WaitUntilDone(player);

            CollectionAssert.AreEqual(new[] { "시작", "도착" }, _dialogues, "Flag set → If 참 → after 점프('건너뜀' 스킵)");
            Assert.IsTrue(_gs.GetFlag("met_roa"), "Flow Flag 라인이 플래그를 set");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator If_False_Falls_Through_To_Next_Line()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Text,,시작,click\n" +
                ",Flow,,If:Flag:없는플래그:after,>\n" + // 거짓 → 통과(점프 안 함)
                ",Text,,통과,click\n" +
                "after,Text,,도착,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp(selectIndex: 0);
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "branch"));
            yield return WaitUntilDone(player);

            CollectionAssert.AreEqual(new[] { "시작", "통과", "도착" }, _dialogues, "If 거짓 → 점프 안 함 → 다음 라인 진행");
            Assert.IsTrue(_finished);
        }

        [UnityTest]
        public IEnumerator Choice_Condition_Filters_Then_Selects_Visible()
        {
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Flow,,Flag:vip,>\n" +                  // vip set
                ",Choice,,,>\n" +
                ",Option,,항상|a,>\n" +                  // 무조건 → 표시
                ",Option,,VIP전용|b|if:Flag:vip,>\n" +   // vip=true → 표시
                ",Option,,숨김|c|if:Flag:없음,>\n" +     // 미설정 → 숨김
                "a,Text,,경로A,click\n" +
                ",Flow,,End,>\n" +
                "b,Text,,경로B,click\n" +
                ",Flow,,End,>\n" +
                "c,Text,,경로C,click\n" +
                ",Flow,,End,>\n";

            var player = SetUp(selectIndex: 1); // 표시된 것 중 2번째 = VIP전용
            yield return null;

            EventBus.Publish(new PlayScriptCommand(csv, "branch"));
            yield return WaitUntilDone(player);

            CollectionAssert.AreEqual(new[] { "항상", "VIP전용" }, _lastChoiceTexts, "if:조건 만족 선택지만 표시(숨김 제외)");
            CollectionAssert.AreEqual(new[] { "경로B" }, _dialogues, "표시 목록 기준 index1 = VIP전용 → 경로B 점프");
            Assert.IsTrue(_finished);
        }
    }
}
