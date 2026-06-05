using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowCharacterCommand, ResetNarrativeViewsCommand, CharSlot, CharAction, CompletionHandle
using LoveAlgo.UI;     // StageView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 화면 정리(ResetNarrativeViewsCommand) 검증: 연출 뷰가 NarrativeFinishedEvent와 동일하게 이 명령에도
    /// 리셋되는지 — 기획 도구가 Apply 전 잔여 연출을 청소하는 토대. StageView로 대표 검증(슬롯 추적 해제 관찰).
    /// </summary>
    public class NarrativeViewResetPlayModeTests
    {
        [UnityTest]
        public IEnumerator StageView_Resets_On_ResetNarrativeViewsCommand()
        {
            var go = new GameObject("StageView");
            go.SetActive(false);
            var view = go.AddComponent<StageView>();
            var slotGo = new GameObject("SlotC", typeof(RectTransform), typeof(CanvasGroup));
            var img = slotGo.AddComponent<Image>();
            view.SlotC = new StageView.SlotBinding { image = img, group = slotGo.GetComponent<CanvasGroup>() };
            go.SetActive(true); // OnEnable → ShowCharacterCommand/ResetNarrativeViewsCommand 구독
            yield return null;

            try
            {
                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Enter, "로아", "기본", 0f, new CompletionHandle()));
                yield return null;
                Assert.AreEqual((int)CharSlot.C, view.SlotIndexForSpeaker("로아"), "Enter로 슬롯 추적");

                EventBus.Publish(new ResetNarrativeViewsCommand());
                yield return null;
                Assert.AreEqual(-1, view.SlotIndexForSpeaker("로아"), "ResetNarrativeViewsCommand → ClearAll로 슬롯 추적 해제");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(slotGo);
            }
        }
    }
}
