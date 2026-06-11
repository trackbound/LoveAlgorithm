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
            int settings = 0, toTitle = 0;
            _subs.Add(EventBus.Subscribe<ShowSaveLoadCommand>(e => saveLoads.Add(e)));
            _subs.Add(EventBus.Subscribe<ShowSettingsCommand>(_ => settings++));
            _subs.Add(EventBus.Subscribe<ReturnToTitleCommand>(_ => toTitle++));

            view.SaveButton.onClick.Invoke();
            view.LoadButton.onClick.Invoke();
            view.SettingsButton.onClick.Invoke();
            view.TitleButton.onClick.Invoke();
            view.MessengerButton.onClick.Invoke(); // 준비 중 — 안내 로그만(발행 없음·예외 없음)

            Assert.AreEqual(2, saveLoads.Count);
            Assert.AreEqual(SaveLoadMode.Save, saveLoads[0].Mode, "저장하기 → Save 모드");
            Assert.AreEqual(SaveLoadMode.Load, saveLoads[1].Mode, "불러오기 → Load 모드");
            Assert.AreEqual(1, settings, "환경설정 → ShowSettingsCommand");
            Assert.AreEqual(1, toTitle, "메인 타이틀 → ReturnToTitleCommand(확인 팝업 없음 — 기획서)");
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
