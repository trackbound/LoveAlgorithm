using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // EyeMaskCommand, EyeMaskAction, CompletionHandle, NarrativeFinishedEvent
using LoveAlgo.UI;     // EyeMaskView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 아이마스크 슬라이스2 PlayMode 검증: EyeMaskView가 OnEnable에서 EyeMaskCommand를 구독해 상/하 바를 보간하고,
    /// 감김 끝에 두 바가 중앙(y=0)에서 만나며 완료 핸들을 푸는지. Open은 후퇴+비활성, 종료 시 뜨기. 명령+핸들로만 검증.
    /// </summary>
    public class EyeMaskViewPlayModeTests
    {
        const float Half = 540f; // 부모 높이 1080의 절반.

        static EyeMaskView MakeView(out GameObject root, out RectTransform top, out RectTransform bottom)
        {
            root = new GameObject("EyeMaskView_PlayTest", typeof(RectTransform));
            var parentRt = root.GetComponent<RectTransform>();
            parentRt.sizeDelta = new Vector2(1920f, 1080f);

            top = new GameObject("Top", typeof(RectTransform)).GetComponent<RectTransform>();
            bottom = new GameObject("Bottom", typeof(RectTransform)).GetComponent<RectTransform>();
            top.SetParent(root.transform, false);
            bottom.SetParent(root.transform, false);

            var view = root.AddComponent<EyeMaskView>();
            view.TopBar = top;
            view.BottomBar = bottom;
            return view;
        }

        static IEnumerator WaitDone(CompletionHandle req, float timeout = 2f)
        {
            float t = 0f;
            while (!req.IsComplete && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Close_Bars_Meet_At_Center_And_Completes()
        {
            var view = MakeView(out var root, out var top, out var bottom);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.Close, 0.1f, 0.1f, 0f, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(0f, top.anchoredPosition.y, 1e-2f, "감김 끝: 상단 바 중앙(y=0).");
                Assert.AreEqual(0f, bottom.anchoredPosition.y, 1e-2f, "감김 끝: 하단 바 중앙(y=0).");
                Assert.IsTrue(top.gameObject.activeSelf, "감긴 상태에선 바가 활성.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator CloseImmediate_Snaps_Closed()
        {
            var view = MakeView(out var root, out var top, out var bottom);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.CloseImmediate, 0f, 0f, 0f, req));
                yield return null;

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(0f, top.anchoredPosition.y, 1e-2f);
                Assert.AreEqual(0f, bottom.anchoredPosition.y, 1e-2f);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Open_Retracts_And_Deactivates()
        {
            var view = MakeView(out var root, out var top, out var bottom);
            try
            {
                yield return null;
                // 먼저 즉시 감기.
                var closeReq = new CompletionHandle();
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.CloseImmediate, 0f, 0f, 0f, closeReq));
                yield return WaitDone(closeReq);

                var openReq = new CompletionHandle();
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.Open, 0.1f, 0.1f, 0f, openReq));
                yield return WaitDone(openReq);

                Assert.IsTrue(openReq.IsComplete);
                Assert.AreEqual(Half, top.anchoredPosition.y, 1f, "뜨면 상단 바는 화면 위로 후퇴.");
                Assert.IsFalse(top.gameObject.activeSelf, "뜬 뒤 바 비활성.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_Opens_Immediately()
        {
            var view = MakeView(out var root, out var top, out var bottom);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.CloseImmediate, 0f, 0f, 0f, req));
                yield return WaitDone(req);
                Assert.AreEqual(0f, top.anchoredPosition.y, 1e-2f);

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;

                Assert.AreEqual(Half, top.anchoredPosition.y, 1f, "종료 시 즉시 뜨기.");
                Assert.IsFalse(top.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
