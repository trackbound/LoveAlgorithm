using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;
using LoveAlgo.Events;
using LoveAlgo.Gacha;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 가챠 화면 PlayMode — 열기(보드 30칸 생성+보유 반영+카운터)/닫기, 추첨 결과 연출 후 칸 안착·카운터
    /// 갱신, 완성 시 전체화면 자동 전환. 연출 수치는 0으로 주입해 결정적으로 검증(감독 튜닝 영역 미검증).
    /// </summary>
    public class GachaViewPlayModeTests
    {
        GameStateSO _gs;
        GachaTuningSO _tuning;

        [SetUp]
        public void SetUp()
        {
            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _tuning = ScriptableObject.CreateInstance<GachaTuningSO>();
            // 연출 0초 — 코루틴이 수 프레임 안에 끝나도록(결정적 테스트).
            _tuning.ticketShakeDuration = 0f;
            _tuning.pieceFlyDuration = 0f;
            _tuning.completeLineFadeDuration = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tuning);
            Object.DestroyImmediate(_gs);
        }

        static GachaPieceSlot MakeSlotPrefab()
        {
            var go = new GameObject("PieceSlot", typeof(RectTransform));
            var slot = go.AddComponent<GachaPieceSlot>();

            var empty = new GameObject("Empty", typeof(RectTransform));
            empty.transform.SetParent(go.transform);
            slot.EmptyRoot = empty;

            var reveal = new GameObject("Reveal", typeof(RectTransform));
            reveal.transform.SetParent(go.transform);
            slot.RevealRoot = reveal;

            var ph = new GameObject("Placeholder", typeof(RectTransform));
            ph.transform.SetParent(reveal.transform);
            slot.PlaceholderImage = ph.AddComponent<Image>();

            var label = new GameObject("Index", typeof(RectTransform));
            label.transform.SetParent(reveal.transform);
            slot.IndexLabel = label.AddComponent<TextMeshProUGUI>();
            return slot;
        }

        GachaView Build(out GameObject go)
        {
            go = new GameObject("GachaView", typeof(RectTransform));
            go.SetActive(false);
            var view = go.AddComponent<GachaView>();
            view.State = _gs;
            view.Tuning = _tuning;

            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(go.transform);
            root.SetActive(false);
            view.Root = root;

            var board = new GameObject("Board", typeof(RectTransform), typeof(GridLayoutGroup));
            board.transform.SetParent(root.transform);
            view.BoardContainer = (RectTransform)board.transform;
            view.PieceSlotPrefab = MakeSlotPrefab();

            var counter = new GameObject("Counter", typeof(RectTransform));
            counter.transform.SetParent(root.transform);
            view.CounterText = counter.AddComponent<TextMeshProUGUI>();

            var fly = new GameObject("Fly", typeof(RectTransform));
            fly.transform.SetParent(root.transform);
            fly.SetActive(false);
            view.PieceFlyImage = (RectTransform)fly.transform;

            var fullscreen = new GameObject("Fullscreen", typeof(RectTransform));
            fullscreen.transform.SetParent(root.transform);
            fullscreen.SetActive(false);
            view.FullscreenRoot = fullscreen;
            return view;
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, int maxFrames = 3000)
        {
            int guard = 0;
            while (!condition() && guard++ < maxFrames) yield return null;
            Assert.IsTrue(condition(), $"{maxFrames}프레임 내 조건 충족 실패");
        }

        [UnityTest]
        public IEnumerator Open_Builds_Board_And_Reflects_Owned()
        {
            GachaPuzzleService.Own(_gs, _tuning, 3);
            GachaPuzzleService.Own(_gs, _tuning, 7);

            var view = Build(out var go);
            go.SetActive(true);
            yield return null;

            try
            {
                EventBus.Publish(new OpenGachaCommand(fromPurchase: false)); // 현황 보기
                yield return null;

                Assert.IsTrue(view.Root.activeSelf, "열기 명령으로 루트 활성");
                var slots = view.BoardContainer.GetComponentsInChildren<GachaPieceSlot>(true);
                Assert.AreEqual(30, slots.Length, "퍼즐판 30칸(6×5)");
                Assert.AreEqual("2/30", view.CounterText.text, "보유 수 카운터");

                int ownedShown = 0;
                foreach (var s in slots) if (s.IsOwned) ownedShown++;
                Assert.AreEqual(2, ownedShown, "보유 칸만 공개");

                EventBus.Publish(new CloseGachaCommand());
                Assert.IsFalse(view.Root.activeSelf, "닫기 명령으로 루트 비활성");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator DrawResult_Reveals_Piece_And_Updates_Counter()
        {
            GachaPuzzleService.Own(_gs, _tuning, 5); // 컨트롤러가 이미 확정한 셈

            var view = Build(out var go);
            go.SetActive(true);
            yield return null;

            try
            {
                EventBus.Publish(new OpenGachaCommand(fromPurchase: true));
                var slots = view.BoardContainer.GetComponentsInChildren<GachaPieceSlot>(true);

                EventBus.Publish(new GachaDrawResultEvent(5, isBonus: false, ownedCount: 1, isComplete: false));
                Assert.IsFalse(slots[5].IsOwned, "연출 시작 — 비행 동안 해당 칸은 잠시 숨김");

                yield return WaitUntil(() => slots[5].IsOwned); // 연출(플립 0.3s) 종료 = 안착
                Assert.AreEqual("1/30", view.CounterText.text);
                Assert.IsFalse(view.FullscreenRoot.activeSelf, "미완성 — 전체화면 자동 전환 없음");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Complete_Result_Auto_Opens_Fullscreen()
        {
            for (int i = 0; i < 30; i++) GachaPuzzleService.Own(_gs, _tuning, i);

            var view = Build(out var go);
            go.SetActive(true);
            yield return null;

            try
            {
                EventBus.Publish(new OpenGachaCommand(fromPurchase: true));
                EventBus.Publish(new GachaDrawResultEvent(29, isBonus: false, ownedCount: 30, isComplete: true));
                yield return WaitUntil(() => view.FullscreenRoot.activeSelf); // 연출+컨페티 종료 대기

                Assert.AreEqual("30/30", view.CounterText.text);
                Assert.IsTrue(view.FullscreenRoot.activeSelf, "완성 — 전체화면 자동 전환(기획 p49)");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
