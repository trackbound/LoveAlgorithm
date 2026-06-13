using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // OverlayGate
using LoveAlgo.Events; // ShowSaveLoadCommand, ShowSettingsCommand, ReturnToTitleCommand, ShowModalCommand, QuitGameCommand
using LoveAlgo.UI;     // QuickMenuView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// QuickMenuView 런타임: 토글 펼침/접힘, 항목 클릭 → 커맨드 발행(저장/불러오기/설정/타이틀),
    /// 종료 → 확인 모달("예"만 QuitGameCommand), 뒤로가기 → OverlayGate.CloseTop(최상단 팝업 닫기).
    /// SceneFlowController 미존재 환경이라 ReturnToTitle/Quit 발행은 캡처만(씬 로드 없음 — 안전).
    /// </summary>
    public class QuickMenuViewPlayModeTests
    {
        GameObject _root;
        readonly List<IDisposable> _subs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            OverlayGate.Reset();
        }

        QuickMenuView CreateView()
        {
            // 잔존 Game 씬의 SceneFlowController 무력화 — 이 픽스처는 ReturnToTitle/Quit을 실발행한다
            // (씬 로드/에디터 정지로 런이 끊기는 것 방지, HANDOFF PlayMode 격리 주의).
            ResidentSceneGuard.DisableSceneFlowControllers();

            _root = new GameObject("QuickMenu");
            _root.SetActive(false); // Awake 전 버튼 주입(SaveLoadView 테스트 패턴)
            var view = _root.AddComponent<QuickMenuView>();

            Button Mk(string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(_root.transform);
                return go.AddComponent<Button>();
            }

            view.ToggleButton = Mk("Toggle");
            var menuRoot = new GameObject("MenuRoot", typeof(RectTransform));
            menuRoot.transform.SetParent(_root.transform);
            view.MenuRoot = menuRoot;
            view.TitleButton = Mk("Title");
            view.MessengerButton = Mk("Messenger");
            view.SaveButton = Mk("Save");
            view.LoadButton = Mk("Load");
            view.SettingsButton = Mk("Settings");
            view.QuitButton = Mk("Quit");
            view.CollapseButton = Mk("Collapse");
            view.BackButton = Mk("Back");

            _root.SetActive(true); // Awake: 리스너 바인딩 + 부팅 접힘
            return view;
        }

        [UnityTest]
        public IEnumerator Toggle_ExpandsAndCollapses_BootsCollapsed()
        {
            var view = CreateView();
            yield return null;

            Assert.IsFalse(view.IsExpanded, "부팅 접힘(기획서)");
            view.ToggleButton.onClick.Invoke();
            Assert.IsTrue(view.IsExpanded, "호출 버튼 → 펼침");
            view.ToggleButton.onClick.Invoke();
            Assert.IsFalse(view.IsExpanded, "한번 더 → 접힘");

            view.ToggleButton.onClick.Invoke();
            view.CollapseButton.onClick.Invoke();
            Assert.IsFalse(view.IsExpanded, "메뉴 접히기 항목 → 접힘");
        }

        [UnityTest]
        public IEnumerator MenuButtons_PublishCommands()
        {
            var view = CreateView();
            yield return null;

            var saveLoads = new List<ShowSaveLoadCommand>();
            int settings = 0, toTitle = 0, messenger = 0;
            _subs.Add(EventBus.Subscribe<ShowSaveLoadCommand>(e => saveLoads.Add(e)));
            _subs.Add(EventBus.Subscribe<ShowSettingsCommand>(_ => settings++));
            _subs.Add(EventBus.Subscribe<ReturnToTitleCommand>(_ => toTitle++));
            _subs.Add(EventBus.Subscribe<OpenMessengerCommand>(e =>
            {
                messenger++;
                Assert.IsTrue(string.IsNullOrEmpty(e.RoomId), "빠른메뉴 진입 = 기본 화면(친구 탭)");
            }));

            view.SaveButton.onClick.Invoke();
            view.LoadButton.onClick.Invoke();
            view.SettingsButton.onClick.Invoke();
            view.TitleButton.onClick.Invoke();
            view.MessengerButton.onClick.Invoke();

            Assert.AreEqual(2, saveLoads.Count);
            Assert.AreEqual(SaveLoadMode.Save, saveLoads[0].Mode, "저장하기 → Save 모드");
            Assert.AreEqual(SaveLoadMode.Load, saveLoads[1].Mode, "불러오기 → Load 모드");
            Assert.AreEqual(1, settings, "환경설정 → ShowSettingsCommand");
            Assert.AreEqual(1, toTitle, "메인 타이틀 → ReturnToTitleCommand(확인 팝업 없음 — 기획서)");
            Assert.AreEqual(1, messenger, "메신저 → OpenMessengerCommand");
        }

        [UnityTest]
        public IEnumerator Quit_ShowsConfirmModal_YesOnlyQuits()
        {
            var view = CreateView();
            yield return null;

            ShowModalCommand modal = default;
            int modals = 0, quits = 0;
            _subs.Add(EventBus.Subscribe<ShowModalCommand>(e => { modal = e; modals++; }));
            _subs.Add(EventBus.Subscribe<QuitGameCommand>(_ => quits++));

            // "아니오"(index 0) → 종료 없음
            view.QuitButton.onClick.Invoke();
            Assert.AreEqual(1, modals, "종료는 즉시가 아니라 확인 모달");
            Assert.AreEqual(2, modal.Buttons.Count);
            modal.Handle.Select(0);
            Assert.AreEqual(0, quits, "아니오 → QuitGameCommand 미발행");

            // "예"(index 1) → 종료 발행
            view.QuitButton.onClick.Invoke();
            modal.Handle.Select(1);
            Assert.AreEqual(1, quits, "예 → QuitGameCommand 발행");
        }

        [UnityTest]
        public IEnumerator Visibility_FollowsPhase_AndMessengerOpen()
        {
            var view = CreateView();
            var state = ScriptableObject.CreateInstance<GameStateSO>();
            var group = _root.AddComponent<CanvasGroup>();
            view.State = state;   // OnEnable 이후 주입 — 직후 이벤트로 재평가시킨다
            view.Group = group;
            yield return null;

            try
            {
                // Story = 숨김(서브메뉴 기획서: 스탯/자유행동·메신저에서만), Schedule = 표시
                state.Phase = ScreenPhase.Story;
                EventBus.Publish(new ScreenPhaseChangedEvent(ScreenPhase.Schedule, ScreenPhase.Story));
                Assert.IsFalse(view.IsShown, "Story 페이즈 → 숨김");

                state.Phase = ScreenPhase.Schedule;
                EventBus.Publish(new ScreenPhaseChangedEvent(ScreenPhase.Story, ScreenPhase.Schedule));
                Assert.IsTrue(view.IsShown, "Schedule 페이즈 → 표시");

                // Story 중에도 메신저 열림이면 표시(기획서: 메신저 화면 우측)
                state.Phase = ScreenPhase.Story;
                EventBus.Publish(new ScreenPhaseChangedEvent(ScreenPhase.Schedule, ScreenPhase.Story));
                EventBus.Publish(new OpenMessengerCommand());
                Assert.IsTrue(view.IsShown, "메신저 열림 → 표시");

                view.ToggleButton.onClick.Invoke();
                Assert.IsTrue(view.IsExpanded, "표시 중 펼침 가능");
                EventBus.Publish(new CloseMessengerCommand());
                Assert.IsFalse(view.IsShown, "메신저 닫힘+Story → 숨김");
                Assert.IsFalse(view.IsExpanded, "숨김 전환 시 펼침 잔존 방지");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(state);
            }
        }

        [UnityTest]
        public IEnumerator Back_ClosesTopOverlay_NoopWhenEmpty()
        {
            var view = CreateView();
            yield return null;

            view.BackButton.onClick.Invoke(); // 팝업 없음 → 무동작(예외 없음)

            int closed = 0;
            IDisposable token = null;
            token = OverlayGate.Push(() => { closed++; token.Dispose(); });
            view.BackButton.onClick.Invoke();
            Assert.AreEqual(1, closed, "뒤로가기 → 최상단 팝업 닫기 요청");
            Assert.IsFalse(OverlayGate.IsBlocked);
        }
    }
}
