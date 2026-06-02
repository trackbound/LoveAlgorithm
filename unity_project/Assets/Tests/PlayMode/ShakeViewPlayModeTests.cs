using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShakeCommand, ShakeTarget, ShakeProfile, CharSlot, CompletionHandle, NarrativeFinishedEvent
using LoveAlgo.UI;     // ShakeView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 흔들기 슬라이스2 PlayMode 검증: ShakeView가 OnEnable에서 ShakeCommand를 구독해 자기 대상(+Char 슬롯)이
    /// 일치할 때만 RectTransform을 감쇠 진동시키고, 끝나면 원위치 복귀 + 완료 핸들을 푸는지. 명령+핸들로만 검증.
    /// </summary>
    public class ShakeViewPlayModeTests
    {
        static readonly ShakeProfile Prof = new ShakeProfile(1.0f, 0.35f, 0.06f, 5.0f, 5.2f, 0.025f);

        static ShakeView MakeStageView(out GameObject root, out RectTransform body)
        {
            root = new GameObject("ShakeView_PlayTest", typeof(RectTransform));
            body = root.GetComponent<RectTransform>();
            var view = root.AddComponent<ShakeView>(); // OnEnable → 구독
            view.Target = ShakeTarget.Stage;
            view.Body = body;
            return view;
        }

        static IEnumerator WaitDone(CompletionHandle req, float timeout = 2f)
        {
            float t = 0f;
            while (!req.IsComplete && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator StageShake_Returns_To_Rest_And_Completes()
        {
            var view = MakeStageView(out var root, out var body);
            try
            {
                body.anchoredPosition = new Vector2(100f, 50f); // 기준점(0이 아니어도 원위치 복귀해야).
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ShakeCommand(ShakeTarget.Stage, CharSlot.C, 25f, 0.1f, Prof, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(100f, body.anchoredPosition.x, 1e-3f, "흔들기 후 원위치 X 복귀.");
                Assert.AreEqual(50f, body.anchoredPosition.y, 1e-3f, "흔들기 후 원위치 Y 복귀.");
                Assert.AreEqual(Quaternion.identity, body.localRotation);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Mismatched_Target_Is_Ignored()
        {
            var view = MakeStageView(out var root, out var body);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                // Dialogue 명령은 Stage 뷰가 무시 → 핸들 미완료.
                EventBus.Publish(new ShakeCommand(ShakeTarget.Dialogue, CharSlot.C, 25f, 0.1f, Prof, req));
                yield return null; yield return null;

                Assert.IsFalse(req.IsComplete, "대상이 다른 명령은 이 뷰가 완료시키지 않는다.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator CharShake_Routes_To_Slot()
        {
            var root = new GameObject("ShakeView_Char", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<ShakeView>();
                view.Target = ShakeTarget.Char;
                var l = new GameObject("L", typeof(RectTransform)).GetComponent<RectTransform>();
                var c = new GameObject("C", typeof(RectTransform)).GetComponent<RectTransform>();
                var r = new GameObject("R", typeof(RectTransform)).GetComponent<RectTransform>();
                l.SetParent(root.transform); c.SetParent(root.transform); r.SetParent(root.transform);
                view.SlotL = l; view.SlotC = c; view.SlotR = r;
                yield return null;

                var req = new CompletionHandle();
                EventBus.Publish(new ShakeCommand(ShakeTarget.Char, CharSlot.R, 18f, 0.1f, Prof, req));
                yield return WaitDone(req);

                Assert.IsTrue(req.IsComplete);
                Assert.AreEqual(Vector2.zero, r.anchoredPosition, "R 슬롯은 원위치 복귀.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator MissingTarget_Completes_Immediately()
        {
            // Char 대상인데 슬롯 미바인딩 → 엔진을 막지 않도록 즉시 완료.
            var root = new GameObject("ShakeView_Empty", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<ShakeView>();
                view.Target = ShakeTarget.Char; // 슬롯 참조 없음.
                yield return null;

                var req = new CompletionHandle();
                EventBus.Publish(new ShakeCommand(ShakeTarget.Char, CharSlot.L, 18f, 0.1f, Prof, req));
                yield return null;

                Assert.IsTrue(req.IsComplete, "대상 RectTransform이 없으면 핸들 즉시 완료.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_Resets_MidShake()
        {
            var view = MakeStageView(out var root, out var body);
            try
            {
                yield return null;
                var req = new CompletionHandle();
                EventBus.Publish(new ShakeCommand(ShakeTarget.Stage, CharSlot.C, 50f, 5f, Prof, req)); // 긴 흔들기.
                yield return null; // 진동 시작.

                EventBus.Publish(new NarrativeFinishedEvent("test"));
                yield return null;

                Assert.IsTrue(req.IsComplete, "종료 시 진행 중 핸들 해제.");
                Assert.AreEqual(Vector2.zero, body.anchoredPosition, "종료 시 원위치 복귀.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
