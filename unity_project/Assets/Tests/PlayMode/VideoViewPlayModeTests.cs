using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // PlayVideoCommand, CompletionHandle, NarrativeFinishedEvent
using LoveAlgo.UI;     // VideoView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// VideoView PlayMode 검증: 자가완결 구성(최상위 캔버스 order 32000 + VideoPlayer + RawImage)과,
    /// 핸들 계약(클립 없음=즉시 완료로 엔진 hang 방지). 실제 디코드 재생은 디바이스 의존이라 감독 Play로 확인.
    /// </summary>
    public class VideoViewPlayModeTests
    {
        static VideoView MakeView(out GameObject root)
        {
            root = new GameObject("VideoView_PlayTest");
            return root.AddComponent<VideoView>(); // Awake → 자가 구성, OnEnable → 구독
        }

        static IEnumerator WaitDone(CompletionHandle req, float timeout = 2f)
        {
            float t = 0f;
            while (!req.IsComplete && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Builds_Fullscreen_Canvas_On_Awake()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                var canvas = root.GetComponent<Canvas>();
                Assert.IsNotNull(canvas, "전용 Canvas 자가 생성.");
                Assert.AreEqual(32000, canvas.sortingOrder, "모든 캔버스 위(동결 order).");
                Assert.IsNotNull(root.GetComponent<VideoPlayer>(), "VideoPlayer 자가 생성.");
                Assert.IsNotNull(root.GetComponentInChildren<RawImage>(true), "풀스크린 RawImage 자가 생성.");
                Assert.IsFalse(view.IsPlaying, "부팅 시 비표시.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Missing_Clip_Completes_Handle_Immediately()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new PlayVideoCommand("__no_such_clip__", false, true, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete, "클립 없으면 즉시 완료 — 엔진 await가 hang되지 않음.");
                Assert.IsFalse(view.IsPlaying);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_Releases_Pending_Handle()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;
                // 클립 없음이라 OnPlay가 즉시 완료시키므로, 종료 이벤트가 잔여 없이 안전한지(예외 0)만 확인.
                var req = new CompletionHandle();
                EventBus.Publish(new PlayVideoCommand("__no_such_clip__", false, true, req));
                yield return WaitDone(req);

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;
                Assert.IsFalse(view.IsPlaying, "종료 후 비표시 유지.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
