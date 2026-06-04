using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events;  // StartNewGameCommand
using LoveAlgo.UI;      // TitleView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 타이틀 슬라이스1: 얇은 TitleView가 New Game 버튼 클릭 시 StartNewGameCommand를 발행하는지
    /// (ADR-007 표시만 — 씬 전환은 SceneFlowController 몫). Awake에서 리스너를 걸므로
    /// inactive GO에 컴포넌트를 붙이고 버튼 주입 후 활성화해 Awake 타이밍을 맞춘다.
    /// </summary>
    public class TitleViewPlayModeTests
    {
        [UnityTest]
        public IEnumerator NewGameButton_Click_Publishes_StartNewGameCommand()
        {
            var btnGo = new GameObject("NewGameButton", typeof(RectTransform), typeof(Button));
            var go = new GameObject("TitleView");
            go.SetActive(false);
            var view = go.AddComponent<TitleView>();
            view.NewGameButton = btnGo.GetComponent<Button>();
            go.SetActive(true); // Awake → onClick.AddListener(OnNewGame)
            yield return null;

            bool published = false;
            var sub = EventBus.Subscribe<StartNewGameCommand>(_ => published = true);
            try
            {
                view.NewGameButton.onClick.Invoke();
                Assert.IsTrue(published, "New Game 버튼 클릭 → StartNewGameCommand 발행");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(btnGo);
            }
        }
    }
}
