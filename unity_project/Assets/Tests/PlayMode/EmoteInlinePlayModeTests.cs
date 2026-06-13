using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowDialogueCommand, ShowSpeakerEmoteCommand, ShowCharacterCommand, CompletionHandle, InlineEmote, CharSlot, CharAction
using LoveAlgo.UI;     // DialogueView, StageView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 인라인 &lt;emote&gt; 라이프사이클 검증(M3): (1) DialogueView가 타이핑 중 emote 지점에서
    /// <c>ShowSpeakerEmoteCommand</c>를 화자+표정으로 발행하는지, (2) StageView가 Char Enter로 슬롯→캐릭터를
    /// 추적해 화자→슬롯 조회가 일치하는지. 스프라이트 에셋 없이 발행/추적 거동만 결정적으로 검증.
    /// </summary>
    public class EmoteInlinePlayModeTests
    {
        [UnityTest]
        public IEnumerator DialogueView_Fires_SpeakerEmote_With_Speaker_And_Emote()
        {
            // 잔존 Game 씬의 DialogueView 제거 — 부팅 내러티브가 켜져 있으면 같은 명령을 같이 처리해
            // 발행 수가 이중 계상된다(HANDOFF PlayMode 격리 주의, DialogueEndMark Build 가드 미러).
            foreach (var v in Object.FindObjectsByType<DialogueView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(v.gameObject);

            var go = new GameObject("DialogueView");
            go.SetActive(false);
            var view = go.AddComponent<DialogueView>();
            go.SetActive(true);     // Awake/OnEnable → ShowDialogueCommand 구독
            // OnEnable의 ApplyFromSettings()가 영속 속도로 덮어쓰므로 활성화 후 주입(즉시 표시 → 최종-발행 경로).
            view.CharInterval = 0f;
            yield return null;

            string gotSpeaker = null, gotEmote = null;
            int count = 0;
            var sub = EventBus.Subscribe<ShowSpeakerEmoteCommand>(e => { gotSpeaker = e.Speaker; gotEmote = e.Emote; count++; });
            try
            {
                var emotes = new List<InlineEmote> { new InlineEmote(0, "활짝웃음") };
                EventBus.Publish(new ShowDialogueCommand("로아", "안녕", false, new CompletionHandle(), null, emotes));
                yield return null; // 타이핑 코루틴 진행
                yield return null;

                Assert.AreEqual(1, count, "emote 1회 발행");
                Assert.AreEqual("로아", gotSpeaker, "화자로 발행");
                Assert.AreEqual("활짝웃음", gotEmote, "표정 키로 발행");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator StageView_Tracks_SlotCharacter_From_Enter_And_Clears()
        {
            var go = new GameObject("StageView");
            go.SetActive(false);
            var view = go.AddComponent<StageView>();
            var slotGo = new GameObject("SlotC", typeof(RectTransform), typeof(CanvasGroup));
            var img = slotGo.AddComponent<Image>();
            view.SlotC = new StageView.SlotBinding { image = img, group = slotGo.GetComponent<CanvasGroup>() };
            go.SetActive(true); // OnEnable → ShowCharacterCommand/ShowSpeakerEmoteCommand 구독
            yield return null;

            try
            {
                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Enter, "로아", "기본", 0f, new CompletionHandle()));
                yield return null;
                Assert.AreEqual((int)CharSlot.C, view.SlotIndexForSpeaker("로아"), "Enter로 슬롯C에 로아 추적");
                Assert.AreEqual(-1, view.SlotIndexForSpeaker("하예은"), "무대에 없는 화자는 -1");

                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Clear, null, null, 0f, new CompletionHandle()));
                yield return null;
                Assert.AreEqual(-1, view.SlotIndexForSpeaker("로아"), "Clear 후 추적 해제");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(slotGo);
            }
        }
    }
}
