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

            DialogueLogEntrySlot MkSlot(string name, bool withName, bool withPortrait)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(_root.transform, false);
                var slot = go.AddComponent<DialogueLogEntrySlot>();
                var body = new GameObject("Body", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
                body.transform.SetParent(go.transform, false);
                slot.BodyText = body;
                if (withName)
                {
                    var nameTxt = new GameObject("Name", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
                    nameTxt.transform.SetParent(go.transform, false);
                    slot.NameText = nameTxt;
                }
                if (withPortrait)
                {
                    var pr = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                    pr.transform.SetParent(go.transform, false);
                    slot.PortraitRoot = pr;
                    slot.PortraitImage = pr.GetComponent<Image>();
                }
                return slot;
            }

            view.CharacterSlotPrefab = MkSlot("CharSlot", true, true);
            view.PlayerSlotPrefab = MkSlot("PlayerSlot", true, false);
            view.NarrationSlotPrefab = MkSlot("NarrSlot", false, false);

            var portrait = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            view.Portraits.Add(new DialogueLogView.PortraitPair { speakerId = "c01", sprite = portrait });

            _root.SetActive(true);
            return view;
        }

        [UnityTest]
        public IEnumerator View_SpawnsKindSlots_GateAndBackRoundtrip()
        {
            DialogueLogStore.Append("로아", "c01", "히로인 줄");          // 캐릭터(초상 등록)
            DialogueLogStore.Append("교수님", null, "엑스트라 줄");        // 캐릭터(초상 미등록)
            DialogueLogStore.Append("철수", PlayerNameFormat.PlayerSpeakerId, "플레이어 줄");
            DialogueLogStore.Append("", null, "독백 줄");

            var view = CreateView();
            yield return null;

            Assert.IsFalse(view.IsVisible, "부팅 숨김");
            EventBus.Publish(new OpenDialogueLogCommand());
            Assert.IsTrue(view.IsVisible, "열기 명령 → 표시");
            Assert.IsTrue(OverlayGate.IsBlocked, "표시 중 게임플레이 차단(오토 정지)");

            var slots = view.Content.GetComponentsInChildren<DialogueLogEntrySlot>(true);
            Assert.AreEqual(4, slots.Length, "박스 4개 스폰");
            Assert.IsTrue(slots[0].PortraitRoot.activeSelf, "히로인 = 초상 표시");
            Assert.AreEqual("로아", slots[0].NameText.text);
            Assert.IsFalse(slots[1].PortraitRoot.activeSelf, "엑스트라 = 초상 숨김(미등록)");
            Assert.AreEqual("철수", slots[2].NameText.text, "플레이어 = 입력 이름 표시");
            Assert.AreEqual("독백 줄", slots[3].BodyText.text);

            Assert.IsTrue(OverlayGate.CloseTop(), "공용 뒤로가기 → 닫기");
            Assert.IsFalse(view.IsVisible);
            Assert.IsFalse(OverlayGate.IsBlocked);
        }
    }
}
