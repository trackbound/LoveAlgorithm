using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowBackgroundCommand, ShowCharacterCommand, CompletionHandle, NarrativeFinishedEvent
using LoveAlgo.UI;     // StageView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 스테이지 슬라이스2 PlayMode 검증: StageView가 OnEnable에서 BG/Char 명령을 구독해 코루틴 전환을
    /// 수행하고 완료 핸들을 풀어주는지(실 Resources <c>BG/bg_00_00</c>·<c>Characters/c01_00</c>로 로드).
    /// 슬라이스1과 동일하게 명령 이벤트 + 완료 핸들로만 검증(뷰 직접 참조 없음).
    /// </summary>
    public class StageViewPlayModeTests
    {
        const string BgKey = "bg_00_00";       // Resources/BG/bg_00_00
        const string CharId = "c01";           // Resources/Characters/c01_00
        const string Emote = "00";

        static Image MakeImage(string name, out CanvasGroup group)
        {
            var go = new GameObject(name);
            var img = go.AddComponent<Image>();
            group = go.AddComponent<CanvasGroup>();
            return img;
        }

        static StageView MakeView(out GameObject root)
        {
            root = new GameObject("StageView_PlayTest");
            var view = root.AddComponent<StageView>(); // OnEnable → 구독

            var bgFront = MakeImage("bgFront", out var bgFrontGroup);
            var bgBack = MakeImage("bgBack", out var bgBackGroup);
            var cImg = MakeImage("slotC", out var cGroup);
            bgFront.transform.SetParent(root.transform);
            bgBack.transform.SetParent(root.transform);
            cImg.transform.SetParent(root.transform);

            view.BgFront = bgFront; view.BgFrontGroup = bgFrontGroup;
            view.BgBack = bgBack; view.BgBackGroup = bgBackGroup;
            view.SlotC = new StageView.SlotBinding { image = cImg, group = cGroup };
            return view;
        }

        static IEnumerator WaitDone(CompletionHandle req, float timeout = 2f)
        {
            float t = 0f;
            while (!req.IsComplete && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Bg_Cut_Sets_Sprite_And_Completes()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ShowBackgroundCommand(BgKey, BgTransition.Cut, 0f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete, "Cut은 즉시 완료되어야 한다.");
                Assert.IsNotNull(view.BgFront.sprite, "BG 스프라이트가 설정되어야 한다.");
                Assert.IsTrue(view.BgFront.enabled);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Bg_Cross_Animates_And_Completes()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ShowBackgroundCommand(BgKey, BgTransition.Cross, 0.05f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete, "Cross 전환이 핸들을 완료해야 한다.");
                Assert.IsNotNull(view.BgFront.sprite);
                Assert.AreEqual(1f, view.BgFrontGroup.alpha, 1e-3f, "전환 후 front는 완전 노출.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Bg_MissingSprite_Still_Completes()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                LogAssert.ignoreFailingMessages = true; // 경고 로그 허용
                var req = new CompletionHandle();
                EventBus.Publish(new ShowBackgroundCommand("__nonexistent__", BgTransition.Cut, 0f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete, "스프라이트 없어도 핸들은 완료되어 엔진이 멈추지 않아야 한다.");
            }
            finally { LogAssert.ignoreFailingMessages = false; Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Char_Enter_Then_Exit_Toggles_Slot()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;

                var enterReq = new CompletionHandle();
                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Enter, CharId, Emote, 0.05f, enterReq));
                yield return WaitDone(enterReq);

                Assert.IsTrue(enterReq.IsComplete);
                Assert.IsNotNull(view.SlotC.image.sprite, "캐릭터 스프라이트가 설정되어야 한다.");
                Assert.IsTrue(view.SlotC.image.enabled);
                Assert.AreEqual(1f, view.SlotC.group.alpha, 1e-3f);

                var exitReq = new CompletionHandle();
                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Exit, null, "", 0.05f, exitReq));
                yield return WaitDone(exitReq);

                Assert.IsTrue(exitReq.IsComplete);
                Assert.IsFalse(view.SlotC.image.enabled, "Exit 후 슬롯은 비활성.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_Clears_Stage()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ShowCharacterCommand(CharSlot.C, CharAction.Enter, CharId, Emote, 0f, req));
                yield return WaitDone(req);
                Assert.IsTrue(view.SlotC.image.enabled);

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;

                Assert.IsFalse(view.SlotC.image.enabled, "내러티브 종료 시 스테이지가 정리되어야 한다.");
                Assert.IsNull(view.BgFront.sprite);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
