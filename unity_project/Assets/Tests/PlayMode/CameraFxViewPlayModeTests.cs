using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // CameraFxCommand, CameraFxKind, CompletionHandle, NarrativeFinishedEvent
using LoveAlgo.UI;     // CameraFxView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 카메라 FX 슬라이스2 PlayMode 검증: CameraFxView가 OnEnable에서 CameraFxCommand를 구독해 콘텐츠 래퍼의
    /// localScale(줌)·anchoredPosition(팬)을 lerp하고, 끝나면 목표값에 안착 + 완료 핸들을 푸는지. 명령+핸들로만 검증.
    /// </summary>
    public class CameraFxViewPlayModeTests
    {
        static CameraFxView MakeView(out GameObject root, out RectTransform body)
        {
            root = new GameObject("CameraFxView_PlayTest", typeof(RectTransform));
            body = root.GetComponent<RectTransform>();
            var view = root.AddComponent<CameraFxView>(); // OnEnable → 구독
            view.Body = body;
            return view;
        }

        static IEnumerator WaitDone(CompletionHandle req, float timeout = 2f)
        {
            float t = 0f;
            while (!req.IsComplete && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Zoom_Ends_At_Target_Scale()
        {
            var view = MakeView(out var root, out var body);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new CameraFxCommand(CameraFxKind.Zoom, 1.5f, 0f, 0f, 0.1f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(1.5f, body.localScale.x, 1e-3f);
                Assert.AreEqual(1.5f, body.localScale.y, 1e-3f);
                Assert.AreEqual(Vector2.zero, body.anchoredPosition, "줌은 위치를 건드리지 않는다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Pan_Ends_At_Target_Offset()
        {
            var view = MakeView(out var root, out var body);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new CameraFxCommand(CameraFxKind.Pan, 1f, 120f, -40f, 0.1f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(120f, body.anchoredPosition.x, 1e-2f);
                Assert.AreEqual(-40f, body.anchoredPosition.y, 1e-2f);
                Assert.AreEqual(1f, body.localScale.x, 1e-3f, "팬은 스케일을 건드리지 않는다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Reset_Returns_Scale_And_Pos_To_Origin()
        {
            var view = MakeView(out var root, out var body);
            try
            {
                // 줌+팬 상태로 만든 뒤 리셋.
                body.localScale = Vector3.one * 1.8f;
                body.anchoredPosition = new Vector2(200f, 100f);
                yield return null;

                var req = new CompletionHandle();
                EventBus.Publish(new CameraFxCommand(CameraFxKind.Reset, 1f, 0f, 0f, 0.1f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(1f, body.localScale.x, 1e-3f);
                Assert.AreEqual(Vector2.zero, body.anchoredPosition);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator ZeroDuration_Snaps_Immediately()
        {
            var view = MakeView(out var root, out var body);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new CameraFxCommand(CameraFxKind.Zoom, 2f, 0f, 0f, 0f, req));
                yield return null;

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(2f, body.localScale.x, 1e-3f);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_Resets_Camera()
        {
            var view = MakeView(out var root, out var body);
            try
            {
                body.localScale = Vector3.one * 1.5f;
                body.anchoredPosition = new Vector2(80f, 0f);
                yield return null;

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;

                Assert.AreEqual(1f, body.localScale.x, 1e-3f, "종료 시 줌 원복.");
                Assert.AreEqual(Vector2.zero, body.anchoredPosition, "종료 시 팬 원복.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
