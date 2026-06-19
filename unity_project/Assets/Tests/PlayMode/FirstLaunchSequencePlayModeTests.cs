using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.MessageStack;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>첫실행 연출 부품 검증(MessageStack 이벤트 / Director 핸드오프 / Bridge).</summary>
    public class FirstLaunchSequencePlayModeTests
    {
        static void SetPrivate(object o, string name, object val)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"private 필드를 찾지 못함: {name}");
            f.SetValue(o, val);
        }

        static MessageStackController BuildController(int lineCount, out GameObject root)
        {
            root = new GameObject("FLSeqTest_Root", typeof(RectTransform), typeof(Canvas));
            var stackGo = new GameObject("Stack", typeof(RectTransform));
            ((RectTransform)stackGo.transform).SetParent(root.transform, false);

            var cardGo = new GameObject("CardTemplate", typeof(RectTransform), typeof(CanvasGroup));
            var card = cardGo.AddComponent<MessageCardView>();
            cardGo.transform.SetParent(root.transform, false);

            var seq = ScriptableObject.CreateInstance<MessageSequenceSO>();
            SetPrivate(seq, "senderName", "ROA");
            SetPrivate(seq, "startDelay", 0.05f);
            var lines = new List<MessageSequenceSO.Line>();
            for (int i = 0; i < lineCount; i++) lines.Add(new MessageSequenceSO.Line { text = "m" + i, delay = 0.05f });
            SetPrivate(seq, "lines", lines);

            var ctrlGo = new GameObject("FLSeqTest_Ctrl");
            ctrlGo.transform.SetParent(root.transform, false);
            var ctrl = ctrlGo.AddComponent<MessageStackController>();
            SetPrivate(ctrl, "cardPrefab", card);
            SetPrivate(ctrl, "cardParent", (RectTransform)stackGo.transform);
            SetPrivate(ctrl, "sequence", seq);
            SetPrivate(ctrl, "riseDuration", 0.02f);
            SetPrivate(ctrl, "shiftDuration", 0.02f);
            SetPrivate(ctrl, "playOnStart", false);
            return ctrl;
        }

        [UnityTest]
        public IEnumerator Events_Spawned_PerLine_And_Completed_Once()
        {
            var ctrl = BuildController(3, out var root);
            int spawned = 0, completed = 0;
            ctrl.MessageSpawned += () => spawned++;
            ctrl.Completed += () => completed++;
            try
            {
                yield return null;        // Awake/Start
                ctrl.Play();
                yield return new WaitForSeconds(1f); // 3줄(0.05s 간격) + 정착
                Assert.AreEqual(3, spawned, "줄마다 MessageSpawned 1회씩.");
                Assert.AreEqual(1, completed, "시퀀스 종료 시 Completed 정확히 1회.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
