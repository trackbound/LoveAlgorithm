using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowDialogueCommand, DialogueRequest, SetAutoModeCommand
using LoveAlgo.UI;     // DialogueView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 오토 모드 슬라이스2 PlayMode 검증: DialogueView가 <see cref="SetAutoModeCommand"/>를 구독해, 오토 ON이면
    /// 클릭 없이 타이핑 완료 후 지연을 두고 자동 진행하고(핸들 완료), OFF면 클릭(Advance)까지 대기하는지.
    /// bodyText 미바인딩이면 타이핑은 즉시 끝나므로 대기 로직만 순수하게 검증된다.
    /// </summary>
    public class AutoModePlayModeTests
    {
        static DialogueView MakeView(out GameObject root)
        {
            root = new GameObject("DialogueView_AutoTest");
            return root.AddComponent<DialogueView>(); // OnEnable → 구독
        }

        [UnityTest]
        public IEnumerator AutoMode_Advances_Without_Click()
        {
            var view = MakeView(out var root);
            try
            {
                view.AutoAdvanceDelay = 0.05f;
                yield return null;

                EventBus.Publish(new SetAutoModeCommand(true));

                var req = new DialogueRequest();
                EventBus.Publish(new ShowDialogueCommand("로아", "안녕!", true, req));

                // 클릭 없이도 지연 후 자동 완료되어야 한다.
                float t = 0f;
                while (!req.IsComplete && t < 1f) { t += Time.deltaTime; yield return null; }

                Assert.IsTrue(req.IsComplete, "오토 모드는 클릭 없이 자동 진행해야 한다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Manual_Waits_For_Click()
        {
            var view = MakeView(out var root);
            try
            {
                view.AutoAdvanceDelay = 0.05f; // 오토라면 곧 끝날 값 — 수동에선 영향 없어야.
                yield return null;

                EventBus.Publish(new SetAutoModeCommand(false));

                var req = new DialogueRequest();
                EventBus.Publish(new ShowDialogueCommand("로아", "안녕!", true, req));

                // 클릭 전: 충분히 기다려도 완료되지 않아야(클릭 대기).
                float t = 0f;
                while (t < 0.3f) { t += Time.deltaTime; yield return null; }
                Assert.IsFalse(req.IsComplete, "수동 모드는 클릭 전 진행하면 안 된다.");

                // 클릭 → 즉시 완료.
                view.Advance();
                yield return null;
                Assert.IsTrue(req.IsComplete, "클릭(Advance) 시 진행해야 한다.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
