using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;
using LoveAlgo.Events;
using LoveAlgo.Tutorial;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 튜토리얼 오버레이 PlayMode — 시작 명령→스텝 표시(치환), 클릭 진행, 강제 클릭 게이트
    /// (앵커 영역만 패스스루 + 실제 버튼 실행), 자동 스텝, 종료(루트 닫힘+통지), 컨트롤러 1회 발동.
    /// 시퀀스는 prefsKey 빈 값(PlayerPrefs 무오염)·지연 0으로 주입해 결정적으로 검증.
    /// </summary>
    public class TutorialViewPlayModeTests
    {
        GameStateSO _gs;
        TutorialSequenceSO _seq;

        [SetUp]
        public void SetUp()
        {
            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.Data.playerName = "감독";

            _seq = ScriptableObject.CreateInstance<TutorialSequenceSO>();
            _seq.prefsKey = ""; // 빈 키 = 기록 안 함(테스트 격리)
            _seq.SetSteps(new List<TutorialSequenceSO.Step>
            {
                new() { text = "안녕, {{Player}}!" },
                new()
                {
                    // 긴 다행 대사 — 말풍선 가변 크기 검증(목업: 길이 따라 가로+세로 증가)
                    text = "여기는 길게 이어지는 안내 문장이야. 줄을 바꿔서\n두 번째 줄, 그리고\n세 번째 줄까지 이어지는 설명!",
                    requiredClickAnchor = "TestShop"
                },
                new() { text = "끝!", autoAdvanceSeconds = 0.05f },
            });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_seq);
            UnityEngine.Object.DestroyImmediate(_gs);
        }

        TutorialView Build(out GameObject go)
        {
            go = new GameObject("Tutorial", typeof(RectTransform));
            go.SetActive(false);
            var view = go.AddComponent<TutorialView>();
            view.State = _gs;
            view.Sequence = _seq;

            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(go.transform);
            root.SetActive(false);
            view.Root = root;

            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(root.transform);
            view.DimImage = dim.AddComponent<Image>();

            var group = new GameObject("RoaGroup", typeof(RectTransform));
            group.transform.SetParent(root.transform);
            view.RoaGroup = (RectTransform)group.transform;

            var bubble = new GameObject("Bubble", typeof(RectTransform));
            bubble.transform.SetParent(group.transform);
            view.BubbleRect = (RectTransform)bubble.transform;

            var text = new GameObject("Text", typeof(RectTransform));
            text.transform.SetParent(bubble.transform);
            view.BubbleText = text.AddComponent<TextMeshProUGUI>();
            return view;
        }

        static PointerEventData ClickAt(Vector2 screenPos)
            => new(EventSystem.current) { position = screenPos };

        static IEnumerator WaitUntil(Func<bool> condition, int maxFrames = 3000)
        {
            int guard = 0;
            while (!condition() && guard++ < maxFrames) yield return null;
            Assert.IsTrue(condition(), $"{maxFrames}프레임 내 조건 충족 실패");
        }

        [UnityTest]
        public IEnumerator Full_Flow_Click_Gate_Auto_And_Finish()
        {
            var view = Build(out var go);
            go.SetActive(true);
            yield return null;

            bool finished = false;
            using var sub = EventBus.Subscribe<TutorialFinishedEvent>(_ => finished = true);

            // 강제 클릭 대상 가짜 앵커(원점 100x100) + 버튼 실행 추적
            var anchorGo = new GameObject("Fake_TestShop", typeof(RectTransform), typeof(Button));
            var anchor = anchorGo.AddComponent<TutorialAnchor>();
            anchor.Id = "TestShop";
            anchor.Button = anchorGo.GetComponent<Button>();
            ((RectTransform)anchorGo.transform).sizeDelta = new Vector2(100, 100);
            bool buttonFired = false;
            anchor.Button.onClick.AddListener(() => buttonFired = true);

            try
            {
                EventBus.Publish(new StartTutorialCommand());
                yield return WaitUntil(() => view.CurrentStep == 0 && view.RoaGroup.gameObject.activeSelf);
                Assert.IsTrue(view.Root.activeSelf, "시작 명령으로 오버레이 열림");
                Assert.AreEqual("안녕, 감독!", view.BubbleText.text, "{{Player}} 치환");
                Vector2 shortSize = view.BubbleRect.sizeDelta;

                view.HandleClick(ClickAt(new Vector2(9999, 9999)));
                yield return WaitUntil(() => view.CurrentStep == 1, 60);

                // 말풍선 가변 크기(목업) — 긴 다행 대사에서 가로·세로 모두 증가
                Vector2 longSize = view.BubbleRect.sizeDelta;
                Assert.Greater(longSize.x, shortSize.x, "긴 줄 = 말풍선 가로 증가");
                Assert.Greater(longSize.y, shortSize.y, "여러 줄 = 말풍선 세로 증가");

                // 강제 클릭 스텝 — 영역 밖 클릭은 흡수, 앵커 위 클릭만 패스스루+진행
                view.HandleClick(ClickAt(new Vector2(9999, 9999)));
                yield return null;
                Assert.AreEqual(1, view.CurrentStep, "영역 밖 클릭은 무시(기획: 버튼만 누를 수 있게 제한)");
                Assert.IsFalse(buttonFired);

                view.HandleClick(ClickAt(Vector2.zero)); // 앵커(원점) 위 클릭
                yield return WaitUntil(() => view.CurrentStep == 2, 60);
                Assert.IsTrue(buttonFired, "지정 버튼 실제 실행(패스스루)");

                // 마지막 스텝 자동 진행(0.05s) → 종료
                yield return WaitUntil(() => finished);
                Assert.IsFalse(view.Root.activeSelf, "종료 시 오버레이 닫힘");
                Assert.AreEqual(-1, view.CurrentStep);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(anchorGo);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator Controller_Fires_Once_On_First_Schedule_Entry()
        {
            var go = new GameObject("TutorialController");
            go.SetActive(false);
            var controller = go.AddComponent<TutorialController>();
            controller.Sequence = _seq; // prefsKey 빈 값 = 항상 미완료 취급
            go.SetActive(true);
            yield return null;

            int started = 0;
            using var sub = EventBus.Subscribe<StartTutorialCommand>(_ => started++);
            try
            {
                EventBus.Publish(new ScreenPhaseChangedEvent(ScreenPhase.Story, ScreenPhase.Schedule));
                Assert.AreEqual(1, started, "스케줄 첫 진입 → 발동");

                EventBus.Publish(new ScreenPhaseChangedEvent(ScreenPhase.Story, ScreenPhase.Schedule));
                Assert.AreEqual(1, started, "같은 세션 재진입은 무시(1회)");

                EventBus.Publish(new ScreenPhaseChangedEvent(ScreenPhase.Schedule, ScreenPhase.Story));
                Assert.AreEqual(1, started, "다른 페이즈 전환 무반응");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
