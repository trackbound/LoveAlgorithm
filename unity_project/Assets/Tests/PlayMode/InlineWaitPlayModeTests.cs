using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowDialogueCommand, CompletionHandle, InlinePause
using LoveAlgo.UI;     // DialogueView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 인라인 <wait> 슬라이스2 PlayMode 검증: DialogueView가 명령에 실린 <see cref="InlinePause"/>를 타이핑 중
    /// 적용해, 멈춤이 없을 때보다 완료까지 더 오래 걸리는지(핸들 완료 시각 비교). 표시 텍스트는 엔진이 이미 태그를
    /// 제거해 넘기므로 뷰는 그대로 타이핑한다.
    /// </summary>
    public class InlineWaitPlayModeTests
    {
        static DialogueView MakeView(out GameObject root)
        {
            root = new GameObject("DialogueView_WaitTest");
            var view = root.AddComponent<DialogueView>();
            var bodyGo = new GameObject("body");
            bodyGo.transform.SetParent(root.transform);
            view.BodyText = bodyGo.AddComponent<TextMeshProUGUI>();
            view.CharInterval = 0.001f; // 타이핑은 거의 즉시 — 멈춤 시간만 부각.
            return view;
        }

        static IEnumerator RunAndMeasure(DialogueView view, IReadOnlyList<InlinePause> pauses, System.Action<float> onDone)
        {
            var req = new CompletionHandle();
            float start = Time.time;
            EventBus.Publish(new ShowDialogueCommand("", "abcde", false, req, pauses));
            float t = 0f;
            while (!req.IsComplete && t < 3f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(req.IsComplete, "멈춤이 있어도 결국 완료되어야 한다(무한 대기 금지).");
            onDone(Time.time - start);
        }

        [UnityTest]
        public IEnumerator Wait_Pause_Delays_Completion()
        {
            var view = MakeView(out var root);
            try
            {
                yield return null;

                float noPause = -1f;
                yield return RunAndMeasure(view, null, t => noPause = t);

                float withPause = -1f;
                var pauses = new[] { new InlinePause(2, 0.4f) };
                yield return RunAndMeasure(view, pauses, t => withPause = t);

                // 멈춤(0.4s)이 적용되면 완료가 눈에 띄게 늦어진다.
                Assert.Greater(withPause, noPause + 0.25f,
                    $"<wait> 멈춤이 타이핑 완료를 지연시켜야 한다 (no={noPause:F3}s, with={withPause:F3}s).");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
