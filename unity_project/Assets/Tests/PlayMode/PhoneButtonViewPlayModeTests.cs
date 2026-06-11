using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common;    // EventBus
using LoveAlgo.Core;      // GameStateSO, ScreenPhase
using LoveAlgo.Events;
using LoveAlgo.Messenger;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 폰 버튼 PlayMode — 노출 규칙(Story에서만 + CG/메신저 열림 중 숨김), 미읽음 배지 갱신,
    /// 클릭 → 메신저 열기 명령 발행. 진동/슬라이드 애니메이션 수치는 감독 튜닝 영역이라 미검증.
    /// </summary>
    public class PhoneButtonViewPlayModeTests
    {
        GameStateSO _gs;

        [SetUp]
        public void SetUp() => _gs = ScriptableObject.CreateInstance<GameStateSO>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_gs);

        PhoneButtonView Build(out GameObject go)
        {
            go = new GameObject("PhoneButton", typeof(RectTransform));
            go.SetActive(false);
            var view = go.AddComponent<PhoneButtonView>();
            view.Group = go.AddComponent<CanvasGroup>();
            view.Button = go.AddComponent<Button>();
            view.State = _gs;

            var badge = new GameObject("Badge", typeof(RectTransform));
            badge.transform.SetParent(go.transform);
            view.Badge = badge;
            return view;
        }

        [UnityTest]
        public IEnumerator Visible_Only_In_Story_And_Hidden_During_Cg_Or_Messenger()
        {
            var view = Build(out var go);
            go.SetActive(true); // 기본 페이즈 = Schedule → 숨김
            yield return null;

            try
            {
                Assert.IsFalse(view.IsShown, "Schedule 페이즈(스탯/행동창)에선 숨김 — 진입은 빠른 메뉴 담당");

                _gs.Phase = ScreenPhase.Story;
                EventBus.Publish(new ScreenPhaseChangedEvent(ScreenPhase.Schedule, ScreenPhase.Story));
                Assert.IsTrue(view.IsShown, "Story 페이즈에서 표시");

                EventBus.Publish(new SetCgModeCommand(true));
                Assert.IsFalse(view.IsShown, "CG 모드 중 숨김(연출 보호)");
                EventBus.Publish(new SetCgModeCommand(false));
                Assert.IsTrue(view.IsShown);

                EventBus.Publish(new OpenMessengerCommand());
                Assert.IsFalse(view.IsShown, "메신저 열림 중 숨김(기획서)");
                EventBus.Publish(new CloseMessengerCommand());
                Assert.IsTrue(view.IsShown);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Badge_Tracks_Unread_And_Click_Publishes_Open()
        {
            var view = Build(out var go);
            _gs.Phase = ScreenPhase.Story;
            go.SetActive(true);
            yield return null;

            var opened = new List<OpenMessengerCommand>();
            using var sub = EventBus.Subscribe<OpenMessengerCommand>(e => opened.Add(e));
            try
            {
                Assert.IsFalse(view.Badge.activeSelf, "미읽음 없음 = 배지 꺼짐");

                MessengerService.Deliver(_gs, "Seq1", "roa", 1);
                EventBus.Publish(new MessengerMessageArrivedEvent("roa", "Seq1"));
                Assert.IsTrue(view.Badge.activeSelf, "도착 통지로 배지 켜짐");

                MessengerService.MarkRead(_gs, "Seq1");
                EventBus.Publish(new MessengerSequenceReadEvent("Seq1"));
                Assert.IsFalse(view.Badge.activeSelf, "읽힘 통지로 배지 꺼짐");

                view.Button.onClick.Invoke();
                Assert.AreEqual(1, opened.Count, "클릭 → OpenMessengerCommand 발행");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
