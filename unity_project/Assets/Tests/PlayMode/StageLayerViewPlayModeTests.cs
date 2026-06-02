using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowStageLayerCommand, StageLayerKind, LayerTransition, SetCgModeCommand, CompletionHandle, NarrativeFinishedEvent
using LoveAlgo.UI;     // StageLayerView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 스테이지 레이어 슬라이스2 PlayMode 검증: StageLayerView가 ShowStageLayerCommand를 구독해 종류별 Image 알파를
    /// lerp(표시→1·종료→0)하고 완료 핸들을 푸는지. CG 진입/종료 시 SetCgModeCommand를 발행하는지. 명령+핸들로만 검증.
    /// </summary>
    public class StageLayerViewPlayModeTests
    {
        static StageLayerView MakeView(out GameObject root, out Image cg, out Image sd, out Image overlay)
        {
            root = new GameObject("StageLayerView_PlayTest");
            var view = root.AddComponent<StageLayerView>();
            cg = new GameObject("cg").AddComponent<Image>();
            sd = new GameObject("sd").AddComponent<Image>();
            overlay = new GameObject("overlay").AddComponent<Image>();
            cg.transform.SetParent(root.transform);
            sd.transform.SetParent(root.transform);
            overlay.transform.SetParent(root.transform);
            view.CgImage = cg; view.SdImage = sd; view.OverlayImage = overlay;
            return view;
        }

        static IEnumerator WaitDone(CompletionHandle req, float timeout = 2f)
        {
            float t = 0f;
            while (!req.IsComplete && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Overlay_Show_Fades_In_And_Completes()
        {
            var view = MakeView(out var root, out var cg, out var sd, out var overlay);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.Overlay, false, "overlay_c01_pc_default", LayerTransition.Fade, 0.1f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(1f, overlay.color.a, 1e-2f, "표시 후 알파 1.");
                Assert.IsTrue(overlay.enabled);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator SD_Close_Fades_Out_And_Disables()
        {
            var view = MakeView(out var root, out var cg, out var sd, out var overlay);
            try
            {
                yield return null;
                var showReq = new CompletionHandle();
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.SD, false, "sd_c02_01", LayerTransition.Cut, 0f, showReq));
                yield return WaitDone(showReq);
                Assert.IsTrue(sd.enabled);

                var closeReq = new CompletionHandle();
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.SD, true, null, LayerTransition.Fade, 0.1f, closeReq));
                yield return WaitDone(closeReq);

                Assert.IsTrue(closeReq.IsComplete);
                Assert.AreEqual(0f, sd.color.a, 1e-2f, "종료 후 알파 0.");
                Assert.IsFalse(sd.enabled);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator CG_Show_And_Close_Publish_CgMode()
        {
            var view = MakeView(out var root, out var cg, out var sd, out var overlay);
            bool? lastCgMode = null;
            var cgSub = EventBus.Subscribe<SetCgModeCommand>(e => lastCgMode = e.Active);
            try
            {
                yield return null;
                var showReq = new CompletionHandle();
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.CG, false, "cg_c01_01", LayerTransition.Cut, 0f, showReq));
                yield return WaitDone(showReq);
                Assert.AreEqual(true, lastCgMode, "CG 진입 시 CgMode(true) 발행.");

                var closeReq = new CompletionHandle();
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.CG, true, null, LayerTransition.Cut, 0f, closeReq));
                yield return WaitDone(closeReq);
                Assert.AreEqual(false, lastCgMode, "CG 종료 시 CgMode(false) 발행.");
            }
            finally { cgSub.Dispose(); Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_Clears_All_And_Releases_CgMode()
        {
            var view = MakeView(out var root, out var cg, out var sd, out var overlay);
            bool? lastCgMode = null;
            var cgSub = EventBus.Subscribe<SetCgModeCommand>(e => lastCgMode = e.Active);
            try
            {
                yield return null;
                var showReq = new CompletionHandle();
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.CG, false, "cg_c01_01", LayerTransition.Cut, 0f, showReq));
                yield return WaitDone(showReq);

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;

                Assert.AreEqual(false, lastCgMode, "종료 시 CgMode(false) 복원.");
                Assert.IsFalse(cg.enabled, "종료 시 CG 숨김.");
            }
            finally { cgSub.Dispose(); Object.DestroyImmediate(root); }
        }
    }
}
