using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ColorTintCommand, CompletionHandle, NarrativeFinishedEvent
using LoveAlgo.UI;     // ColorTintView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 색 틴트 슬라이스2 PlayMode 검증: ColorTintView가 OnEnable에서 ColorTintCommand를 구독해 오버레이 색을
    /// lerp하고, 끝나면 목표 색에 안착 + 완료 핸들을 푸는지. Clear는 알파 0, 종료 시 해제. 명령+핸들로만 검증.
    /// </summary>
    public class ColorTintViewPlayModeTests
    {
        static ColorTintView MakeView(out GameObject root, out Image overlay)
        {
            root = new GameObject("ColorTintView_PlayTest");
            var view = root.AddComponent<ColorTintView>(); // OnEnable → 구독
            var imgGo = new GameObject("overlay");
            imgGo.transform.SetParent(root.transform);
            overlay = imgGo.AddComponent<Image>();
            view.Overlay = overlay;
            return view;
        }

        static IEnumerator WaitDone(CompletionHandle req, float timeout = 2f)
        {
            float t = 0f;
            while (!req.IsComplete && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Tint_Ends_At_Target_Color_And_Alpha()
        {
            var view = MakeView(out var root, out var overlay);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ColorTintCommand(0.5f, 0.05f, 0.05f, 0.25f, 0.1f, false, req)); // Red 0.25
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(0.5f, overlay.color.r, 1e-2f);
                Assert.AreEqual(0.25f, overlay.color.a, 1e-2f);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Clear_Fades_Alpha_To_Zero()
        {
            var view = MakeView(out var root, out var overlay);
            try
            {
                yield return null;
                var setReq = new CompletionHandle();
                EventBus.Publish(new ColorTintCommand(0.1f, 0.15f, 0.4f, 0.3f, 0f, false, setReq)); // Blue 즉시
                yield return WaitDone(setReq);
                Assert.AreEqual(0.3f, overlay.color.a, 1e-2f);

                var clearReq = new CompletionHandle();
                EventBus.Publish(new ColorTintCommand(0f, 0f, 0f, 0f, 0.1f, true, clearReq));
                yield return WaitDone(clearReq);

                Assert.IsTrue(clearReq.IsComplete);
                Assert.AreEqual(0f, overlay.color.a, 1e-3f, "Clear는 알파 0으로 해제.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_Resets_Tint()
        {
            var view = MakeView(out var root, out var overlay);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ColorTintCommand(0.6f, 0.2f, 0.35f, 0.4f, 0f, false, req)); // Pink 즉시
                yield return WaitDone(req);
                Assert.Greater(overlay.color.a, 0f);

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;

                Assert.AreEqual(0f, overlay.color.a, 1e-3f, "내러티브 종료 시 틴트 해제.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
