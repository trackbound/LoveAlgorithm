using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // StartNewGameCommand
using LoveAlgo.UI;     // FirstLaunchOverlayView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 첫실행 오버레이 검증: 탭(Advance) 시 StartNewGameCommand를 1회만 발행(중복 가드)하는지 + 손수 작성한
    /// Resources 프리팹이 자체 Canvas(최상단 sortingOrder)와 View를 바인딩한 채 로드되는지(직렬화·스크립트 바인딩).
    /// </summary>
    public class FirstLaunchOverlayTests
    {
        [UnityTest]
        public IEnumerator Advance_PublishesStartNewGame_Once()
        {
            var go = new GameObject("Overlay", typeof(RectTransform));
            var view = go.AddComponent<FirstLaunchOverlayView>();
            yield return null;

            int count = 0;
            var sub = EventBus.Subscribe<StartNewGameCommand>(_ => count++);
            try
            {
                view.Advance();
                view.Advance(); // 씬 전환 직전 중복 탭은 무시돼야 한다
                Assert.AreEqual(1, count, "탭 → StartNewGameCommand 1회만(1회 소비 가드)");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OverlayPrefab_Loads_WithCanvasAndView()
        {
            var prefab = Resources.Load<GameObject>("UI/FirstLaunchOverlay");
            Assert.IsNotNull(prefab, "Resources/UI/FirstLaunchOverlay 프리팹 로드(직렬화 정상).");
            var canvas = prefab.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "자체 Canvas 보유 → 어느 씬 위든 단독 렌더.");
            Assert.AreEqual(200, canvas.sortingOrder, "최상단 sortingOrder.");
            Assert.IsNotNull(prefab.GetComponentInChildren<FirstLaunchOverlayView>(true), "View 바인딩(스크립트 미싱 아님).");
            Assert.IsNotNull(prefab.GetComponentInChildren<Image>(true), "풀스크린 Image(레이캐스트 타깃) 보유.");
        }
    }
}
