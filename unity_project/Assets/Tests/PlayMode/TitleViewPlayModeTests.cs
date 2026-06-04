using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;    // JsonSaveStore
using LoveAlgo.Events;  // StartNewGameCommand, ContinueGameCommand, PlayBgmCommand
using LoveAlgo.UI;      // TitleView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 타이틀 뷰 PlayMode: 얇은 TitleView가 버튼 클릭/진입 시 EventBus 의도를 발행하고(ADR-007 표시만),
    /// Continue는 오토세이브 존재 여부로 interactable을 결정하는지. Awake에서 리스너/상태를 거므로
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

        [UnityTest]
        public IEnumerator ContinueButton_Click_Publishes_ContinueGameCommand()
        {
            var btnGo = new GameObject("ContinueButton", typeof(RectTransform), typeof(Button));
            var go = new GameObject("TitleView");
            go.SetActive(false);
            var view = go.AddComponent<TitleView>();
            view.ContinueButton = btnGo.GetComponent<Button>();
            go.SetActive(true); // Awake → onClick.AddListener(OnContinue)
            yield return null;

            bool published = false;
            var sub = EventBus.Subscribe<ContinueGameCommand>(_ => published = true);
            try
            {
                view.ContinueButton.onClick.Invoke();
                Assert.IsTrue(published, "Continue 버튼 클릭 → ContinueGameCommand 발행");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(btnGo);
            }
        }

        [UnityTest]
        public IEnumerator ExitButton_Click_Publishes_QuitGameCommand()
        {
            var btnGo = new GameObject("ExitButton", typeof(RectTransform), typeof(Button));
            var go = new GameObject("TitleView");
            go.SetActive(false);
            var view = go.AddComponent<TitleView>();
            view.ExitButton = btnGo.GetComponent<Button>();
            go.SetActive(true); // Awake → onClick.AddListener(OnExit)
            yield return null;

            bool published = false;
            var sub = EventBus.Subscribe<QuitGameCommand>(_ => published = true);
            try
            {
                view.ExitButton.onClick.Invoke();
                Assert.IsTrue(published, "Exit 버튼 클릭 → QuitGameCommand 발행");
            }
            finally
            {
                sub.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(btnGo);
            }
        }

        [UnityTest]
        public IEnumerator ContinueButton_Interactable_Matches_SaveExistence()
        {
            // Continue는 오토세이브가 있을 때만 활성(세이브 유무와 무관하게 일치 검증).
            var btnGo = new GameObject("ContinueButton", typeof(RectTransform), typeof(Button));
            var go = new GameObject("TitleView");
            go.SetActive(false);
            var view = go.AddComponent<TitleView>();
            view.ContinueButton = btnGo.GetComponent<Button>();
            go.SetActive(true); // Awake → interactable = Exists(AutoSaveSlot)
            yield return null;

            try
            {
                Assert.AreEqual(
                    JsonSaveStore.Exists(JsonSaveStore.AutoSaveSlot),
                    view.ContinueButton.interactable,
                    "Continue interactable은 오토세이브 존재 여부와 일치");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(btnGo);
            }
        }
    }
}
