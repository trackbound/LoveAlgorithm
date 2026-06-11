using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using LoveAlgo.MessageStack;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 연출용 메시지 스택 PlayMode 검증. 자동 타이머로 6줄을 스폰하면:
    /// (1) 최대 maxVisible(4)겹만 유지 — 초과분 파괴, (2) 카드 크기는 변하지 않음(scale==1),
    /// (3) 오래된 카드일수록 위로(+stepY), (4) 오래될수록 더 투명(alpha 감소).
    /// 컨트롤러의 private 직렬화 필드는 리플렉션으로 주입한다(프로덕션 코드에 테스트 전용 훅을 두지 않기 위함).
    /// </summary>
    public class MessageStackPlayModeTests
    {
        static void SetPrivate(object o, string name, object val)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"private 필드를 찾지 못함: {name}");
            f.SetValue(o, val);
        }

        [UnityTest]
        public IEnumerator Stack_KeepsSize_MovesUp_Fades_AndCapsAtMax()
        {
            const int Max = 4;
            const float StepY = 28f;
            const float AnchorY = -200f;
            var alpha = new[] { 1f, 0.55f, 0.3f, 0.15f, 0f };

            // Canvas + 스택 컨테이너
            var canvasGo = new GameObject("MsgStackTest_Canvas", typeof(RectTransform), typeof(Canvas));
            var stackGo = new GameObject("Stack", typeof(RectTransform));
            var stackRT = (RectTransform)stackGo.transform;
            stackRT.SetParent(canvasGo.transform, false);

            // 인메모리 카드 프리팹(라벨 없이 포즈만 검증 — SetContent은 null 라벨을 안전 처리)
            var cardGo = new GameObject("CardTemplate", typeof(RectTransform), typeof(CanvasGroup));
            var cardView = cardGo.AddComponent<MessageCardView>();

            // 빠른 데모 시퀀스(6줄) — private 필드 주입
            var seq = ScriptableObject.CreateInstance<MessageSequenceSO>();
            SetPrivate(seq, "senderName", "ROA");
            SetPrivate(seq, "startDelay", 0.1f);
            var lines = new List<MessageSequenceSO.Line>();
            for (int i = 0; i < 6; i++) lines.Add(new MessageSequenceSO.Line { text = "m" + i, delay = 0.1f });
            SetPrivate(seq, "lines", lines);

            // 컨트롤러 주입
            var ctrlGo = new GameObject("MsgStackTest_Controller");
            var ctrl = ctrlGo.AddComponent<MessageStackController>();
            SetPrivate(ctrl, "cardPrefab", cardView);
            SetPrivate(ctrl, "cardParent", stackRT);
            SetPrivate(ctrl, "sequence", seq);
            SetPrivate(ctrl, "maxVisible", Max);
            SetPrivate(ctrl, "stepY", StepY);
            SetPrivate(ctrl, "alphaBySlot", alpha);
            SetPrivate(ctrl, "riseDuration", 0.05f);
            SetPrivate(ctrl, "shiftDuration", 0.05f);
            SetPrivate(ctrl, "incomingDropY", 100f);
            SetPrivate(ctrl, "anchorPos", new Vector2(0f, AnchorY));
            SetPrivate(ctrl, "playOnStart", false);

            try
            {
                yield return null;       // Awake/Start (playOnStart=false → 자동재생 안 함)
                ctrl.Play();
                yield return new WaitForSeconds(1.5f); // 6 스폰(0.1s 간격) + 정착

                // 스택 컨테이너에 살아있는 카드 수집
                var cards = new List<RectTransform>();
                foreach (Transform ch in stackRT) cards.Add((RectTransform)ch);

                Assert.AreEqual(Max, cards.Count, "최대 4겹만 유지되어야 한다(초과분 파괴).");

                // 최신(낮은 Y) → 오래됨(높은 Y) 순 정렬 = 슬롯 0..3
                cards.Sort((a, b) => a.anchoredPosition.y.CompareTo(b.anchoredPosition.y));

                float prevAlpha = float.PositiveInfinity;
                for (int i = 0; i < cards.Count; i++)
                {
                    var c = cards[i];
                    Assert.AreEqual(1f, c.localScale.x, 1e-3f, $"슬롯 {i}: 크기 불변(scale.x==1).");
                    Assert.AreEqual(1f, c.localScale.y, 1e-3f, $"슬롯 {i}: 크기 불변(scale.y==1).");
                    Assert.AreEqual(AnchorY + i * StepY, c.anchoredPosition.y, 0.5f, $"슬롯 {i}: 위로 {StepY}px씩 이동.");

                    var cg = c.GetComponent<CanvasGroup>();
                    Assert.AreEqual(alpha[i], cg.alpha, 2e-2f, $"슬롯 {i}: 알파 단계.");
                    Assert.Less(cg.alpha, prevAlpha + 1e-3f, $"슬롯 {i}: 오래될수록 알파가 더 작아야 한다.");
                    prevAlpha = cg.alpha;
                }
            }
            finally
            {
                Object.DestroyImmediate(ctrlGo);
                Object.DestroyImmediate(canvasGo);
                Object.DestroyImmediate(cardGo);
                Object.DestroyImmediate(seq);
            }
        }
    }
}
