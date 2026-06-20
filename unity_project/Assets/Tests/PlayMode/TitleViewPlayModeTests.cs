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
    /// Continue는 항상 활성(호버 작동)이고 클릭 시 오토세이브 유무로 이어하기/안내 모달을 가르는지. Awake에서
    /// 리스너를 거므로 inactive GO에 컴포넌트를 붙이고 버튼 주입 후 활성화해 Awake 타이밍을 맞춘다.
    /// </summary>
    public class TitleViewPlayModeTests
    {
        // 잔존 Game 씬의 SceneFlowController 무력화 — 이 픽스처는 Start/Continue/Quit 의도를 실발행한다
        // (씬 로드/에디터 정지로 런이 끊기는 것 방지, HANDOFF PlayMode 격리 주의).
        [SetUp]
        public void NeutralizeResidentSceneFlow() => ResidentSceneGuard.DisableSceneFlowControllers();

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
        public IEnumerator ContinueButton_WithSave_Click_Publishes_ContinueGameCommand()
        {
            var backup = JsonSaveStore.Load(JsonSaveStore.AutoSaveSlot); // 유저 세이브 보호
            var btnGo = new GameObject("ContinueButton", typeof(RectTransform), typeof(Button));
            var go = new GameObject("TitleView");
            go.SetActive(false);
            var view = go.AddComponent<TitleView>();
            view.ContinueButton = btnGo.GetComponent<Button>();
            go.SetActive(true); // Awake → onClick.AddListener(OnContinue)
            yield return null;

            JsonSaveStore.Save(JsonSaveStore.AutoSaveSlot, new SaveData()); // 세이브 존재 보장
            bool published = false, gotModal = false;
            var sub = EventBus.Subscribe<ContinueGameCommand>(_ => published = true);
            var modalSub = EventBus.Subscribe<ShowModalCommand>(_ => gotModal = true);
            try
            {
                view.ContinueButton.onClick.Invoke();
                Assert.IsTrue(published, "세이브 있음 + Continue 클릭 → ContinueGameCommand 발행");
                Assert.IsFalse(gotModal, "세이브 있으면 안내 모달 안 띄움");
            }
            finally
            {
                sub.Dispose();
                modalSub.Dispose();
                if (backup != null) JsonSaveStore.Save(JsonSaveStore.AutoSaveSlot, backup);
                else JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(btnGo);
            }
        }

        [UnityTest]
        public IEnumerator ExitButton_Click_ShowsConfirmModal_YesQuits()
        {
            // Exit은 즉시 종료하지 않고 확인 모달(ShowModalCommand)을 띄운다. "예"(index 0)일 때만 QuitGameCommand.
            var btnGo = new GameObject("ExitButton", typeof(RectTransform), typeof(Button));
            var go = new GameObject("TitleView");
            go.SetActive(false);
            var view = go.AddComponent<TitleView>();
            view.ExitButton = btnGo.GetComponent<Button>();
            go.SetActive(true); // Awake → onClick.AddListener(OnExit)
            yield return null;

            ShowModalCommand captured = default;
            bool gotModal = false, quit = false;
            var modalSub = EventBus.Subscribe<ShowModalCommand>(e => { captured = e; gotModal = true; });
            var quitSub = EventBus.Subscribe<QuitGameCommand>(_ => quit = true);
            try
            {
                view.ExitButton.onClick.Invoke();
                Assert.IsTrue(gotModal, "Exit 클릭 → 확인 모달 발행");
                Assert.AreEqual(2, captured.Buttons.Count, "아니오/예 2버튼");
                Assert.IsFalse(quit, "모달만 떠선 종료 안 함");

                captured.Handle.Select(1); // "예"(우, index 1)
                Assert.IsTrue(quit, "예 선택 → QuitGameCommand 발행");
            }
            finally
            {
                modalSub.Dispose();
                quitSub.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(btnGo);
            }
        }

        [UnityTest]
        public IEnumerator ExitConfirmModal_No_DoesNotQuit()
        {
            var btnGo = new GameObject("ExitButton", typeof(RectTransform), typeof(Button));
            var go = new GameObject("TitleView");
            go.SetActive(false);
            var view = go.AddComponent<TitleView>();
            view.ExitButton = btnGo.GetComponent<Button>();
            go.SetActive(true);
            yield return null;

            ShowModalCommand captured = default;
            bool gotModal = false, quit = false;
            var modalSub = EventBus.Subscribe<ShowModalCommand>(e => { captured = e; gotModal = true; });
            var quitSub = EventBus.Subscribe<QuitGameCommand>(_ => quit = true);
            try
            {
                view.ExitButton.onClick.Invoke();
                Assert.IsTrue(gotModal, "Exit 클릭 → 확인 모달 발행");

                captured.Handle.Select(0); // "아니오"(좌, index 0)
                Assert.IsFalse(quit, "아니오 선택 → 종료 안 함");
            }
            finally
            {
                modalSub.Dispose();
                quitSub.Dispose();
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(btnGo);
            }
        }

        [UnityTest]
        public IEnumerator ContinueButton_NoSave_Click_ShowsNotice_StaysInteractable()
        {
            // Continue는 항상 활성(호버 작동). 세이브가 없으면 클릭 시 이어하기 대신 확인(Yes) 1버튼 안내 모달.
            var backup = JsonSaveStore.Load(JsonSaveStore.AutoSaveSlot); // 유저 세이브 보호
            var btnGo = new GameObject("ContinueButton", typeof(RectTransform), typeof(Button));
            var go = new GameObject("TitleView");
            go.SetActive(false);
            var view = go.AddComponent<TitleView>();
            view.ContinueButton = btnGo.GetComponent<Button>();
            go.SetActive(true);
            yield return null;

            JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot); // 세이브 없음 보장
            ShowModalCommand captured = default;
            bool gotModal = false, continued = false;
            var modalSub = EventBus.Subscribe<ShowModalCommand>(e => { captured = e; gotModal = true; });
            var contSub = EventBus.Subscribe<ContinueGameCommand>(_ => continued = true);
            try
            {
                Assert.IsTrue(view.ContinueButton.interactable, "Continue는 항상 활성(호버 작동)");

                view.ContinueButton.onClick.Invoke();
                Assert.IsTrue(gotModal, "세이브 없음 + 클릭 → 안내 모달 발행");
                Assert.AreEqual(1, captured.Buttons.Count, "확인(Yes) 1버튼");
                Assert.IsFalse(continued, "세이브 없으면 이어하기 발행 안 함");
            }
            finally
            {
                modalSub.Dispose();
                contSub.Dispose();
                if (backup != null) JsonSaveStore.Save(JsonSaveStore.AutoSaveSlot, backup);
                else JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(btnGo);
            }
        }
    }
}
