using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // OverlayGate, PlayerNameFormat
using LoveAlgo.Events; // PlayScriptCommand, ShowDialogueCommand, OpenDialogueLogCommand, CompletionHandle
using LoveAlgo.UI;     // DialogueLogRecorder, DialogueLogView, DialogueLogEntrySlot, DialogueLogStore

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 로그 런타임 계약: ① Recorder가 EventBus(PlayScript+ShowDialogue)만으로 저장소를 채운다(스크립트 경계 그룹핑)
    /// ② View가 열기 명령에 종류별 슬롯(캐릭터/플레이어/나레이션)을 스폰하고 초상은 등록 화자만,
    /// 게이트 차단·공용 뒤로가기 닫기 왕복.
    /// </summary>
    public class DialogueLogPlayModeTests
    {
        GameObject _root;
        readonly List<IDisposable> _subs = new();

        [SetUp] public void SetUp() => DialogueLogStore.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            DialogueLogStore.Reset();
            OverlayGate.Reset();
        }

        static ShowDialogueCommand Line(string speaker, string text, string speakerId = null)
            => new ShowDialogueCommand(speaker, text, false, new CompletionHandle(), null, null, speakerId);

        [UnityTest]
        public IEnumerator Recorder_Collects_OneBoxPerAdvance()
        {
            // 상주 Game 씬의 Recorder와 이중 적재 방지 — 테스트 전용 인스턴스만 남긴다.
            foreach (var r in UnityEngine.Object.FindObjectsByType<DialogueLogRecorder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(r.gameObject);
            DialogueLogStore.Reset(); // Awake 리셋과 별개로 직전 상태 제거

            _root = new GameObject("Recorder");
            _root.AddComponent<DialogueLogRecorder>();
            yield return null;

            // 같은 화자 연속이라도 진행마다 새 박스(목업 박스2). 한 박스 안 여러 줄은 본문 \n으로(목업 박스1).
            EventBus.Publish(Line("로아", "이야기는 내일이야!\n같은 신이면 같은 박스에 들어갑니다.", "c01"));
            EventBus.Publish(Line("로아", "다음 진행이면 새 박스.", "c01"));
            EventBus.Publish(Line("", "독백 줄."));

            Assert.AreEqual(3, DialogueLogStore.Count, "진행 단위 분리(연속 로아도 새 박스)");
            Assert.AreEqual(2, DialogueLogStore.Entries[0].Text.Split('\n').Length, "한 행의 \\n = 같은 박스 여러 줄");
            Assert.AreEqual(DialogueLogKind.Character, DialogueLogStore.Entries[1].Kind);
            Assert.AreEqual(DialogueLogKind.Narration, DialogueLogStore.Entries[2].Kind);
        }

        DialogueLogView CreateView()
        {
            _root = new GameObject("LogRoot");
            _root.SetActive(false); // Awake 전 주입

            var viewGo = new GameObject("DialogueLogView", typeof(RectTransform), typeof(CanvasGroup));
            viewGo.transform.SetParent(_root.transform, false);
            var view = viewGo.AddComponent<DialogueLogView>();
            view.Group = viewGo.GetComponent<CanvasGroup>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(_root.transform, false);
            view.Content = content.transform;

            // 헤더 = 이름(+선택 초상), 버블 = 본문만. 뷰가 run 컨테이너에 조립한다.
            DialogueLogEntrySlot MkHeader(string name, bool withPortrait)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(_root.transform, false);
                var slot = go.AddComponent<DialogueLogEntrySlot>();
                var nameTxt = new GameObject("Name", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
                nameTxt.transform.SetParent(go.transform, false);
                slot.NameText = nameTxt;
                if (withPortrait)
                {
                    var pr = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                    pr.transform.SetParent(go.transform, false);
                    slot.PortraitRoot = pr;
                    slot.PortraitImage = pr.GetComponent<Image>();
                }
                return slot;
            }
            DialogueLogEntrySlot MkBubble(string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(_root.transform, false);
                var slot = go.AddComponent<DialogueLogEntrySlot>();
                var body = new GameObject("Body", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
                body.transform.SetParent(go.transform, false);
                slot.BodyText = body;
                return slot;
            }

            view.SpeakerHeaderPrefab = MkHeader("SpeakerHeader", withPortrait: true);
            view.PlayerHeaderPrefab = MkHeader("PlayerHeader", withPortrait: false);
            view.CharacterBubblePrefab = MkBubble("CharBubble");
            view.PlayerBubblePrefab = MkBubble("PlayerBubble");
            view.NarrationBubblePrefab = MkBubble("NarrBubble");

            var portrait = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            view.Portraits.Add(new DialogueLogView.PortraitPair { speakerId = "c01", sprite = portrait });

            _root.SetActive(true);
            return view;
        }

        [UnityTest]
        public IEnumerator View_GroupsRuns_HeaderOncePerRun_AndRoundtrip()
        {
            DialogueLogStore.Append("로아", "c01", "줄1");                              // run0: 로아 연속 2줄
            DialogueLogStore.Append("로아", "c01", "줄2");
            DialogueLogStore.Append("철수", PlayerNameFormat.PlayerSpeakerId, "플레이어 줄"); // run1
            DialogueLogStore.Append("", null, "독백 줄");                                // run2: 헤더 없음

            var view = CreateView();
            yield return null;

            Assert.IsFalse(view.IsVisible, "부팅 숨김");
            EventBus.Publish(new OpenDialogueLogCommand());
            Assert.IsTrue(view.IsVisible, "열기 명령 → 표시");
            Assert.IsTrue(OverlayGate.IsBlocked, "표시 중 게임플레이 차단(오토 정지)");

            var runs = new List<Transform>();
            foreach (Transform c in view.Content) runs.Add(c);
            Assert.AreEqual(3, runs.Count, "연속 동일 화자 = 한 run(3개)");

            // run0(로아): 헤더 1개(초상 표시·이름 '로아') + 버블 2개(줄1/줄2).
            var r0 = runs[0].GetComponentsInChildren<DialogueLogEntrySlot>(true);
            var header0 = r0.First(s => s.NameText != null);
            var bubbles0 = r0.Where(s => s.BodyText != null).ToList();
            Assert.AreEqual(1, r0.Count(s => s.NameText != null), "이름표는 run당 1회");
            Assert.IsTrue(header0.PortraitRoot.activeSelf, "히로인 = 초상 표시");
            Assert.AreEqual("로아", header0.NameText.text);
            Assert.AreEqual(2, bubbles0.Count, "진행마다 버블(2줄)");
            CollectionAssert.AreEquivalent(new[] { "줄1", "줄2" }, bubbles0.Select(b => b.BodyText.text).ToArray());

            // run1(플레이어): 헤더 이름 '철수'.
            var header1 = runs[1].GetComponentsInChildren<DialogueLogEntrySlot>(true).First(s => s.NameText != null);
            Assert.AreEqual("철수", header1.NameText.text, "플레이어 = 입력 이름 표시");

            // run2(독백): 헤더 없음 + 버블 본문.
            var r2 = runs[2].GetComponentsInChildren<DialogueLogEntrySlot>(true);
            Assert.IsFalse(r2.Any(s => s.NameText != null), "독백 = 화자 헤더 없음");
            Assert.AreEqual("독백 줄", r2.First(s => s.BodyText != null).BodyText.text);

            Assert.IsTrue(OverlayGate.CloseTop(), "공용 뒤로가기 → 닫기");
            Assert.IsFalse(view.IsVisible);
            Assert.IsFalse(OverlayGate.IsBlocked);
        }
    }
}
