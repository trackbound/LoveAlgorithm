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

        [UnityTest]
        public IEnumerator Start_Publishes_PlayBgmCommand_With_TitleBgm()
        {
            // titleBgm 기본값("title")이 Start에서 PlayBgmCommand로 발행되는지 (AudioManager 구독 전제).
            var go = new GameObject("TitleView");
            go.SetActive(false);
            go.AddComponent<TitleView>();

            string received = null;
            var sub = EventBus.Subscribe<PlayBgmCommand>(e => received = e.Name);
            try
            {
                go.SetActive(true); // Awake → (yield 후) Start → PlayBgmCommand 발행
                yield return null;
                Assert.AreEqual("title", received, "Start 시 타이틀 BGM 재생 명령(PlayBgmCommand) 발행");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
            }
        }
    }
}
