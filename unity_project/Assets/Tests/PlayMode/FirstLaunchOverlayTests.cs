using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI; // FirstLaunchDirector

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 첫실행 오버레이 프리팹 구조 검증: 자체 Canvas(최상단 sortingOrder) + Director(무입력 자동 연출) 바인딩.
    /// 탭→넘김 폐기로 FirstLaunchOverlayView 의존 제거(Director가 흐름 소유).
    /// </summary>
    public class FirstLaunchOverlayTests
    {
        [Test]
        public void OverlayPrefab_Loads_WithCanvasAndDirector()
        {
            var prefab = Resources.Load<GameObject>("UI/FirstLaunchOverlay");
            Assert.IsNotNull(prefab, "Resources/UI/FirstLaunchOverlay 프리팹 로드(직렬화 정상).");
            var canvas = prefab.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "자체 Canvas 보유 → 어느 씬 위든 단독 렌더.");
            Assert.AreEqual(200, canvas.sortingOrder, "최상단 sortingOrder.");
            Assert.IsNotNull(prefab.GetComponentInChildren<FirstLaunchDirector>(true), "Director 바인딩(무입력 자동 연출).");
            Assert.IsNotNull(prefab.GetComponentInChildren<Image>(true), "배경/HUD Image 보유.");
        }
    }
}
