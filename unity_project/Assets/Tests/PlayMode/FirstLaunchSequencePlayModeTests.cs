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

        [UnityTest]
        public IEnumerator WarnShake_Moves_WithinAmplitude_AndRestoresOnDisable()
        {
            const float Amp = 6f;
            var go = new GameObject("Warn", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(10f, 20f);
            var shake = go.AddComponent<LoveAlgo.UI.WarnWidgetShake>();
            SetPrivate(shake, "amplitude", Amp);
            // target은 Awake에서 self로 자동 바인딩, _base=(10,20) 캡처됨
            try
            {
                float maxDev = 0f;
                for (int i = 0; i < 20; i++)
                {
                    yield return null;
                    float dev = (rt.anchoredPosition - new Vector2(10f, 20f)).magnitude;
                    maxDev = Mathf.Max(maxDev, dev);
                    Assert.LessOrEqual(dev, Amp * 1.5f + 1e-3f, "흔들림은 진폭 범위 내.");
                }
                Assert.Greater(maxDev, 1e-2f, "흔들려서 위치가 변해야 한다.");

                shake.enabled = false;
                yield return null;
                Assert.AreEqual(new Vector2(10f, 20f), rt.anchoredPosition, "OnDisable에 기준 위치 복원.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Bridge_PublishesStartNewGame_Once_AndSelfDestructs()
        {
            var go = new GameObject("Bridge", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var bridge = go.AddComponent<LoveAlgo.UI.FirstLaunchTransitionBridge>();
            SetPrivate(bridge, "group", go.GetComponent<CanvasGroup>());
            SetPrivate(bridge, "blackIn", 0.05f);
            SetPrivate(bridge, "postLoadHold", 0.05f);
            SetPrivate(bridge, "blackOut", 0.05f);

            int count = 0;
            var sub = LoveAlgo.Common.EventBus.Subscribe<LoveAlgo.Events.StartNewGameCommand>(_ => count++);
            try
            {
                bridge.Begin();
                bridge.Begin(); // 중복 호출 무시돼야 한다
                yield return new WaitForSeconds(0.5f);
                Assert.AreEqual(1, count, "StartNewGameCommand 정확히 1회 발행.");
                Assert.IsTrue(go == null, "페이드아웃 후 자기 파괴.");
            }
            finally { sub.Dispose(); if (go != null) Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Director_NoMessages_NoCatcher_AutoAdvances_Once()
        {
            var go = new GameObject("Director");
            var dir = go.AddComponent<LoveAlgo.UI.FirstLaunchDirector>();
            // messages=null, clickCatcher=null, bridgePrefab=null → 완료 후 대기 → 캐처 없으면 자동 진행(폴백)
            SetPrivate(dir, "fadeIn", 0f);
            SetPrivate(dir, "clickEnableDelay", 0.05f);

            int count = 0;
            var sub = LoveAlgo.Common.EventBus.Subscribe<LoveAlgo.Events.StartNewGameCommand>(_ => count++);
            try
            {
                yield return null; // Start → Run → 메시지 없음 → 즉시 완료 → 대기 → 자동 진행
                yield return new WaitForSeconds(0.3f);
                Assert.AreEqual(1, count, "캐처 없을 때 폴백으로 StartNewGameCommand 1회.");
            }
            finally { sub.Dispose(); Object.DestroyImmediate(go); }
        }

        [Test]
        public void ClickCatcher_FiresClicked_OnlyWhenArmed()
        {
            var go = new GameObject("Catcher");
            var catcher = go.AddComponent<LoveAlgo.UI.ClickAdvanceCatcher>();
            int clicks = 0;
            catcher.Clicked += () => clicks++;
            try
            {
                catcher.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));
                Assert.AreEqual(0, clicks, "무장 전 클릭은 무시.");

                catcher.Arm();
                catcher.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));
                Assert.AreEqual(1, clicks, "무장 후 클릭은 Clicked 발화.");

                catcher.Disarm();
                catcher.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));
                Assert.AreEqual(1, clicks, "해제 후 클릭은 다시 무시.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Director_WaitsForClick_AfterDelay_ThenPublishesOnce()
        {
            var catcherGo = new GameObject("Catcher");
            var catcher = catcherGo.AddComponent<LoveAlgo.UI.ClickAdvanceCatcher>();
            // GO를 비활성으로 만든 뒤 필드 주입 → 활성화: OnEnable이 clickCatcher 구독을 보장(직렬화 프리팹과 동일 순서).
            var go = new GameObject("Director");
            go.SetActive(false);
            var dir = go.AddComponent<LoveAlgo.UI.FirstLaunchDirector>();
            // messages=null → 즉시 완료. clickCatcher 바인딩, bridgePrefab=null → 클릭 시 폴백 발행.
            SetPrivate(dir, "fadeIn", 0f);
            SetPrivate(dir, "clickEnableDelay", 0.2f);
            SetPrivate(dir, "clickCatcher", catcher);
            go.SetActive(true);

            int count = 0;
            var sub = LoveAlgo.Common.EventBus.Subscribe<LoveAlgo.Events.StartNewGameCommand>(_ => count++);
            try
            {
                yield return null; // Start → 완료 → 대기 시작(아직 무장 전)
                Assert.IsFalse(catcher.Armed, "대기 동안엔 무장 전.");
                catcher.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));
                Assert.AreEqual(0, count, "무장 전 클릭은 진행시키지 않음.");

                yield return new WaitForSeconds(0.3f); // clickEnableDelay 경과 → 무장
                Assert.IsTrue(catcher.Armed, "대기 후 무장됨.");
                Assert.AreEqual(0, count, "클릭 전엔 자동 진행하지 않음(자동 넘어감 제거).");

                catcher.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));
                catcher.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null)); // 중복
                yield return null;
                Assert.AreEqual(1, count, "무장 후 클릭 → StartNewGameCommand 1회만.");
                Assert.IsFalse(catcher.Armed, "진행 후 해제(이후 클릭 무시).");
            }
            finally { sub.Dispose(); Object.DestroyImmediate(go); Object.DestroyImmediate(catcherGo); }
        }
    }
}
