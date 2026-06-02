using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowScreenFxCommand, ScreenFxKind, FxRequest, NarrativeFinishedEvent
using LoveAlgo.UI;     // ScreenFxView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 스크린 FX 슬라이스2 PlayMode 검증: ScreenFxView가 OnEnable에서 ShowScreenFxCommand를 구독해 오버레이
    /// 알파를 코루틴 lerp하고 완료 핸들을 푸는지(FadeOut→1, FadeIn→0, Flash→0). 명령+핸들로만 검증.
    /// </summary>
    public class ScreenFxViewPlayModeTests
    {
        static ScreenFxView MakeView(out GameObject root, out Image overlay)
        {
            root = new GameObject("ScreenFxView_PlayTest");
            var view = root.AddComponent<ScreenFxView>(); // OnEnable → 구독
            var imgGo = new GameObject("overlay");
            imgGo.transform.SetParent(root.transform);
            overlay = imgGo.AddComponent<Image>();
            view.Overlay = overlay;
            return view;
        }

        static IEnumerator WaitDone(FxRequest req, float timeout = 2f)
        {
            float t = 0f;
            while (!req.IsComplete && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator FadeOut_Ends_Opaque_And_Completes()
        {
            var view = MakeView(out var root, out var overlay);
            try
            {
                yield return null;
                var req = new FxRequest();
                EventBus.Publish(new ShowScreenFxCommand(ScreenFxKind.FadeOut, 0.05f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(1f, overlay.color.a, 1e-3f, "FadeOut은 검정 불투명으로 끝나야 한다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator FadeIn_Ends_Transparent()
        {
            var view = MakeView(out var root, out var overlay);
            try
            {
                yield return null;
                // 먼저 FadeOut으로 암전.
                var outReq = new FxRequest();
                EventBus.Publish(new ShowScreenFxCommand(ScreenFxKind.FadeOut, 0f, outReq));
                yield return WaitDone(outReq);
                Assert.AreEqual(1f, overlay.color.a, 1e-3f);

                var inReq = new FxRequest();
                EventBus.Publish(new ShowScreenFxCommand(ScreenFxKind.FadeIn, 0.05f, inReq));
                yield return WaitDone(inReq);

                Assert.IsTrue(inReq.IsComplete);
                Assert.AreEqual(0f, overlay.color.a, 1e-3f, "FadeIn은 투명으로 끝나야 한다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Flash_Returns_To_Transparent_And_Completes()
        {
            var view = MakeView(out var root, out var overlay);
            try
            {
                yield return null;
                var req = new FxRequest();
                EventBus.Publish(new ShowScreenFxCommand(ScreenFxKind.Flash, 0.06f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(0f, overlay.color.a, 1e-3f, "Flash는 0→1→0으로 투명 복귀.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_Resets_Overlay()
        {
            var view = MakeView(out var root, out var overlay);
            try
            {
                yield return null;
                var req = new FxRequest();
                EventBus.Publish(new ShowScreenFxCommand(ScreenFxKind.FadeOut, 0f, req));
                yield return WaitDone(req);
                Assert.AreEqual(1f, overlay.color.a, 1e-3f);

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;

                Assert.AreEqual(0f, overlay.color.a, 1e-3f, "내러티브 종료 시 잔여 암전 해제.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
