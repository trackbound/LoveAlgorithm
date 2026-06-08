using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // SetMonologueOverlayCommand, NarrativeFinishedEvent
using LoveAlgo.UI;     // MonologueOverlayView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 독백 오버레이 뷰 PlayMode 검증: MonologueOverlayView가 <see cref="SetMonologueOverlayCommand"/>를 구독해
    /// 오버레이 알파를 0↔shownAlpha로 토글하고, 내러티브 종료 시 해제하는지. 페이드는 0초(즉시)로 결정성 확보.
    /// </summary>
    public class MonologueOverlayViewPlayModeTests
    {
        static MonologueOverlayView MakeView(out GameObject root, out Image overlay)
        {
            root = new GameObject("MonologueOverlayView_PlayTest");
            var view = root.AddComponent<MonologueOverlayView>(); // OnEnable → 구독
            var imgGo = new GameObject("overlay");
            imgGo.transform.SetParent(root.transform);
            overlay = imgGo.AddComponent<Image>();
            view.Overlay = overlay;
            view.ShownAlpha = 1f;
            view.FadeDuration = 0f; // 즉시 토글
            SetAlpha(overlay, 0f);  // OnEnable 초기화(overlay 늦은 바인딩으로 스킵됨)를 수동 재현
            return view;
        }

        static void SetAlpha(Image img, float a) { var c = img.color; c.a = a; img.color = c; }

        [UnityTest]
        public IEnumerator Active_True_Shows_Then_False_Hides()
        {
            var view = MakeView(out var root, out var overlay);
            try
            {
                yield return null;

                EventBus.Publish(new SetMonologueOverlayCommand(true));
                yield return null;
                Assert.AreEqual(1f, overlay.color.a, 1e-3f, "독백 진입 → shownAlpha(1)로 표시");
                Assert.IsTrue(overlay.enabled);

                EventBus.Publish(new SetMonologueOverlayCommand(false));
                yield return null;
                Assert.AreEqual(0f, overlay.color.a, 1e-3f, "대사 복귀 → 알파 0으로 해제");
                Assert.IsFalse(overlay.enabled, "해제 완료 시 비활성");
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

                EventBus.Publish(new SetMonologueOverlayCommand(true));
                yield return null;
                Assert.Greater(overlay.color.a, 0f);

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;
                Assert.AreEqual(0f, overlay.color.a, 1e-3f, "내러티브 종료 시 독백 오버레이 해제");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
