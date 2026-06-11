using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common;    // EventBus
using LoveAlgo.Core;      // GameStateSO
using LoveAlgo.Events;    // Messenger 명령/통지, FlowCommandRequestedEvent, CompletionHandle
using LoveAlgo.Messenger;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 메신저 슬라이스2 PlayMode — 컨트롤러(도착 명령/일자 자동/읽힘 대기 핸들)와
    /// 뷰(열기·닫기·탭 기본, 채팅창 말풍선+선택지+효과 발행+읽음 처리) 라이프사이클 검증.
    /// 리그는 ModalViewPlayModeTests 패턴(코드 구성 + inactive 바인딩 후 활성화).
    /// </summary>
    public class MessengerPlayModeTests
    {
        const string DemoCsv =
            "Msg,roa,갈래?\n" +
            "Option,,좋아!|Love:roa:1\n" +
            "Option,,글쎄…\n" +
            "Me,,기대된다";

        GameStateSO _state;
        MessengerScriptCatalogSO _catalog;

        [SetUp]
        public void SetUp()
        {
            _state = ScriptableObject.CreateInstance<GameStateSO>();
            _catalog = ScriptableObject.CreateInstance<MessengerScriptCatalogSO>();
            _catalog.SetEntries(new List<MessengerScriptCatalogSO.Entry>
            {
                new() { sequenceId = "Seq1", roomId = "roa", csvPath = "Seq1.csv", deliverDay = 0 },
                new() { sequenceId = "DaySeq", roomId = "c01", csvPath = "DaySeq.csv", deliverDay = 7 },
            });
            MessengerScriptStore.Preload("Seq1.csv", DemoCsv);
            MessengerScriptStore.Preload("DaySeq.csv", "Msg,c01,오늘이다!");
        }

        [TearDown]
        public void TearDown()
        {
            MessengerScriptStore.ClearCache();
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_state);
        }

        MessengerController MakeController(out GameObject go)
        {
            go = new GameObject("MessengerController");
            go.SetActive(false);
            var controller = go.AddComponent<MessengerController>();
            controller.State = _state;
            controller.Catalog = _catalog;
            go.SetActive(true); // OnEnable → 구독
            return controller;
        }

        [UnityTest]
        public IEnumerator Controller_Delivers_RecordsState_And_PublishesArrived()
        {
            MakeController(out var go);
            yield return null;

            var arrived = new List<MessengerMessageArrivedEvent>();
            using var sub = EventBus.Subscribe<MessengerMessageArrivedEvent>(e => arrived.Add(e));
            try
            {
                EventBus.Publish(new DeliverMessengerSequenceCommand("Seq1"));

                Assert.IsTrue(MessengerService.IsDelivered(_state, "Seq1"), "상태에 도착 기록");
                Assert.AreEqual(1, arrived.Count, "도착 통지 발행");
                Assert.AreEqual("roa", arrived[0].RoomId, "카탈로그의 방으로 통지");

                EventBus.Publish(new DeliverMessengerSequenceCommand("Seq1"));
                Assert.AreEqual(1, arrived.Count, "중복 도착은 통지 없음");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Controller_AutoDelivers_On_DayChanged()
        {
            MakeController(out var go);
            yield return null;

            try
            {
                EventBus.Publish(new DayChangedEvent(6, 7));
                Assert.IsTrue(MessengerService.IsDelivered(_state, "DaySeq"), "자동 도착일 매칭 시퀀스 도착");
                Assert.IsFalse(MessengerService.IsDelivered(_state, "Seq1"), "도착일 0(스토리 전용)은 자동 도착 안 함");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Controller_OnRead_Handle_Completes_On_ReadEvent_And_FailOpen()
        {
            MakeController(out var go);
            yield return null;

            try
            {
                // 미등록 시퀀스 — hang 방지 즉시 완료(fail-open).
                var missing = new CompletionHandle();
                EventBus.Publish(new DeliverMessengerSequenceCommand("없는시퀀스", missing));
                Assert.IsTrue(missing.IsComplete, "미등록은 즉시 완료");

                // 정상 — 읽힘 이벤트로 완료.
                var handle = new CompletionHandle();
                EventBus.Publish(new DeliverMessengerSequenceCommand("Seq1", handle));
                Assert.IsFalse(handle.IsComplete, "읽기 전 대기");

                EventBus.Publish(new MessengerSequenceReadEvent("Seq1"));
                Assert.IsTrue(handle.IsComplete, "읽힘 통지로 완료");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── 뷰 ──

        static MessengerView BuildMessengerView(out GameObject viewGo)
        {
            viewGo = new GameObject("MessengerView");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<MessengerView>();

            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(viewGo.transform);
            view.Root = root;

            var friendPanel = new GameObject("FriendPanel", typeof(RectTransform));
            friendPanel.transform.SetParent(root.transform);
            view.FriendPanel = friendPanel;

            var chatPanel = new GameObject("ChatPanel", typeof(RectTransform));
            chatPanel.transform.SetParent(root.transform);
            view.ChatPanel = chatPanel;

            root.SetActive(false); // 부팅 시 닫힘
            return view;
        }

        [UnityTest]
        public IEnumerator MessengerView_Opens_DefaultFriendTab_And_Closes()
        {
            var view = BuildMessengerView(out var viewGo);
            viewGo.SetActive(true);
            yield return null;

            try
            {
                EventBus.Publish(new OpenMessengerCommand());
                Assert.IsTrue(view.Root.activeSelf, "열기 명령으로 루트 활성");
                Assert.IsTrue(view.FriendPanel.activeSelf, "기본 = 친구 탭(기획서)");
                Assert.IsFalse(view.ChatPanel.activeSelf);

                view.ShowChatTab();
                Assert.IsFalse(view.FriendPanel.activeSelf);
                Assert.IsTrue(view.ChatPanel.activeSelf);

                EventBus.Publish(new CloseMessengerCommand());
                Assert.IsFalse(view.Root.activeSelf, "닫기 명령으로 루트 비활성");
            }
            finally { Object.DestroyImmediate(viewGo); }
        }

        static MessengerBubble MakeBubblePrefab(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var bubble = go.AddComponent<MessengerBubble>();
            var label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(go.transform);
            bubble.MessageText = label.AddComponent<TextMeshProUGUI>();
            return bubble;
        }

        static MessengerOptionSlot MakeOptionPrefab()
        {
            var go = new GameObject("Option", typeof(RectTransform), typeof(Button));
            var slot = go.AddComponent<MessengerOptionSlot>();
            slot.Button = go.GetComponent<Button>();
            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(go.transform);
            slot.LabelText = label.AddComponent<TextMeshProUGUI>();
            return slot;
        }

        ChatRoomView BuildChatRoom(out GameObject viewGo, out Transform bubbles, out Transform options)
        {
            viewGo = new GameObject("ChatRoomView");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<ChatRoomView>();

            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(viewGo.transform);
            view.Root = root;

            var bubbleGo = new GameObject("Bubbles", typeof(RectTransform));
            bubbleGo.transform.SetParent(root.transform);
            bubbles = bubbleGo.transform;
            view.BubbleContainer = bubbles;

            var optionGo = new GameObject("Options", typeof(RectTransform));
            optionGo.transform.SetParent(root.transform);
            options = optionGo.transform;
            view.OptionContainer = options;

            view.BubbleInPrefab = MakeBubblePrefab("BubbleIn");
            view.BubbleOutPrefab = MakeBubblePrefab("BubbleOut");
            view.OptionPrefab = MakeOptionPrefab();
            view.State = _state;
            view.Catalog = _catalog;
            return view;
        }

        [UnityTest]
        public IEnumerator ChatRoom_Renders_Choice_Applies_Effect_And_MarksRead()
        {
            MessengerService.Deliver(_state, "Seq1", "roa", 1);

            var view = BuildChatRoom(out var viewGo, out var bubbles, out var options);
            viewGo.SetActive(true);
            yield return null;

            var flow = new List<string>();
            var read = new List<string>();
            using var sub1 = EventBus.Subscribe<FlowCommandRequestedEvent>(e => flow.Add(e.Command));
            using var sub2 = EventBus.Subscribe<MessengerSequenceReadEvent>(e => read.Add(e.SequenceId));
            try
            {
                view.Show("roa");
                yield return null;

                Assert.AreEqual(1, bubbles.GetComponentsInChildren<MessengerBubble>().Length, "미답 선택지 전까지 말풍선 1개");
                var slots = options.GetComponentsInChildren<MessengerOptionSlot>();
                Assert.AreEqual(2, slots.Length, "선택지 2개 표시");

                slots[0].Button.onClick.Invoke(); // "좋아!" 선택
                yield return null;

                CollectionAssert.AreEqual(new[] { "Affinity:Point:roa:Dialogue:1" }, flow, "Love→Dialogue 위임 발행");
                CollectionAssert.AreEqual(new[] { 0 }, MessengerService.FindRecord(_state, "Seq1").choices, "선택 기록");
                Assert.AreEqual(3, bubbles.GetComponentsInChildren<MessengerBubble>().Length, "선택 반영 + 이어지는 메시지까지 렌더");
                Assert.AreEqual(0, options.GetComponentsInChildren<MessengerOptionSlot>().Length, "선택지 정리");
                Assert.IsTrue(MessengerService.FindRecord(_state, "Seq1").read, "완주 시퀀스 읽음 처리");
                CollectionAssert.AreEqual(new[] { "Seq1" }, read, "읽힘 통지 발행");
            }
            finally { Object.DestroyImmediate(viewGo); }
        }
    }
}
