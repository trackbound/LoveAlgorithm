using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // OverlayGate, GameStateSO, ScreenPhase
using LoveAlgo.Events; // ShowSaveLoadCommand, ShowSettingsCommand, OpenMessengerCommand, ReturnToTitleCommand, ShowModalCommand, QuitGameCommand, ScreenPhaseChangedEvent
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 인게임 공용 빠른메뉴(*View, 서브메뉴 기획서). 메뉴 호출 버튼(클릭마다 펼침/접힘) + 세로 버튼 스택
    /// (메인 타이틀/메신저/저장/불러오기/환경설정/게임 종료/메뉴 접히기)과 공용 뒤로가기 버튼.
    /// 표시·발행만(ADR-007): 팝업 열기는 Show* 커맨드(SaveLoadView/SettingsView가 구독), 메신저는
    /// <see cref="OpenMessengerCommand"/>(기본 화면 = 친구 탭), 타이틀 복귀는
    /// <see cref="ReturnToTitleCommand"/>(기획서 — 확인 팝업 없음), 종료는 확인 모달(<c>TitleView.OnExit</c> 미러)
    /// → "예"일 때만 <see cref="QuitGameCommand"/>. 뒤로가기는 <see cref="OverlayGate.CloseTop"/>
    /// (가장 마지막에 출력된 팝업 닫기 — 기획서 규칙. 팝업이 없으면 무동작).
    ///
    /// 노출 규칙(서브메뉴 기획서: "메신저, 스탯/자유행동 UI 우측에 뜹니다"): <see cref="ScreenPhase.Schedule"/>
    /// 또는 메신저 열림 중에만 표시 — Story/Ending에선 숨김(폰 버튼이 Story 진입 담당, PhoneButtonView 미러).
    /// 상점은 Schedule 페이즈 위 오버레이라 자동 포함. GO 비활성 대신 CanvasGroup으로 숨겨 구독을 유지한다.
    /// ⚠️ 씬 배선: 팝업 캔버스보다 높은 sortingOrder에 둘 것 — 팝업이 떠 있어도 뒤로가기/메뉴가 눌려야
    /// 기획서의 "팝업 두 개 이상" 스택 시나리오가 성립한다(모달 캔버스는 이보다 더 위).
    /// </summary>
    public class QuickMenuView : MonoBehaviour
    {
        [Header("노출(페이즈 연동)")]
        [Tooltip("페이즈 판정용 상태 SO(GameState_Main). 미바인딩 시 항상 표시(dev 씬 폴백).")]
        [SerializeField] GameStateSO state;
        [Tooltip("노출 토글용 캔버스그룹(GO 비활성 금지 — 구독 유지).")]
        [SerializeField] CanvasGroup canvasGroup;

        [Header("호출/접힘")]
        [Tooltip("메뉴 호출 버튼 — 클릭마다 빠른메뉴 펼침/접힘 토글.")]
        [SerializeField] Button toggleButton;
        [Tooltip("펼침 상태 루트(세로 버튼 스택). 부팅 시 접힘.")]
        [SerializeField] GameObject menuRoot;

        [Header("메뉴 항목 (위→아래)")]
        [SerializeField] Button titleButton;     // 메인 타이틀 화면 출력
        [SerializeField] Button messengerButton; // 메신저 화면 출력(준비 중 — 안내 로그)
        [SerializeField] Button saveButton;      // 저장하기: 저장 화면 출력
        [SerializeField] Button loadButton;      // 불러오기 화면 출력
        [SerializeField] Button settingsButton;  // 환경설정 팝업 출력
        [SerializeField] Button quitButton;      // 게임 종료: 확인 팝업 출력
        [SerializeField] Button collapseButton;  // 메뉴 접히기

        [Header("공용 뒤로가기")]
        [Tooltip("가장 마지막에 출력된 팝업 닫기. 팝업이 없으면 무동작.")]
        [SerializeField] Button backButton;

        [Header("종료 확인 모달 (TitleView 미러)")]
        [SerializeField] string quitConfirmTitle = "게임 종료";
        [SerializeField] string quitConfirmMessage = "정말 종료하시겠습니까?";
        [Tooltip("좌=아니오(취소), 우=예(종료). \"예\"일 때만 QuitGameCommand 발행.")]
        [SerializeField] string quitConfirmYes = "예";
        [SerializeField] string quitConfirmNo = "아니오";

        // 테스트/배선 주입용(SaveLoadView 패턴 — Awake 전 세팅).
        public GameStateSO State { get => state; set => state = value; }
        public CanvasGroup Group { get => canvasGroup; set => canvasGroup = value; }
        public Button ToggleButton { get => toggleButton; set => toggleButton = value; }
        public GameObject MenuRoot { get => menuRoot; set => menuRoot = value; }
        public Button TitleButton { get => titleButton; set => titleButton = value; }
        public Button MessengerButton { get => messengerButton; set => messengerButton = value; }
        public Button SaveButton { get => saveButton; set => saveButton = value; }
        public Button LoadButton { get => loadButton; set => loadButton = value; }
        public Button SettingsButton { get => settingsButton; set => settingsButton = value; }
        public Button QuitButton { get => quitButton; set => quitButton = value; }
        public Button CollapseButton { get => collapseButton; set => collapseButton = value; }
        public Button BackButton { get => backButton; set => backButton = value; }

        /// <summary>빠른메뉴 펼침 여부(menuRoot activeSelf).</summary>
        public bool IsExpanded => menuRoot != null && menuRoot.activeSelf;

        /// <summary>현재 노출 여부(canvasGroup 미바인딩 시 항상 true).</summary>
        public bool IsShown => canvasGroup == null || canvasGroup.alpha > 0.5f;

        readonly List<IDisposable> _subs = new();
        bool _messengerOpen;

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<ScreenPhaseChangedEvent>(_ => ApplyVisibility()));
            _subs.Add(EventBus.Subscribe<OpenMessengerCommand>(_ => { _messengerOpen = true; ApplyVisibility(); }));
            _subs.Add(EventBus.Subscribe<CloseMessengerCommand>(_ => { _messengerOpen = false; ApplyVisibility(); }));
            ApplyVisibility();
        }

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
        }

        // 시뮬 직행 부팅은 페이즈 이벤트 없이 GameBootstrap.Start가 상태만 세팅한다(씬 초기 active=초기 화면) —
        // Start 순서 비의존을 위해 1프레임 뒤 재평가(부팅 한정, 이후 변화는 이벤트가 구동).
        IEnumerator Start()
        {
            yield return null;
            ApplyVisibility();
        }

        void ApplyVisibility()
        {
            // 미바인딩(state/canvasGroup) = 항상 표시 — dev 씬·단위 테스트 폴백.
            bool visible = state == null || state.Phase == ScreenPhase.Schedule || _messengerOpen;
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            if (!visible) SetExpanded(false); // 숨김 전환 시 펼침 잔존 방지
        }

        void Awake()
        {
            Click(toggleButton, Toggle);
            Click(collapseButton, () => SetExpanded(false));
            Click(titleButton, () => EventBus.Publish(new ReturnToTitleCommand()));
            Click(messengerButton, () => EventBus.Publish(new OpenMessengerCommand()));
            Click(saveButton, () => EventBus.Publish(new ShowSaveLoadCommand(SaveLoadMode.Save)));
            Click(loadButton, () => EventBus.Publish(new ShowSaveLoadCommand(SaveLoadMode.Load)));
            Click(settingsButton, () => EventBus.Publish(new ShowSettingsCommand()));
            Click(quitButton, OnQuit);
            Click(backButton, () => OverlayGate.CloseTop());
            SetExpanded(false); // 부팅 접힘(기획서: 호출 버튼 클릭 시 출력)
        }

        static void Click(Button b, UnityAction a) { if (b != null) b.onClick.AddListener(a); }

        /// <summary>메뉴 호출 버튼 — 펼침/접힘 토글.</summary>
        public void Toggle() => SetExpanded(!IsExpanded);

        public void SetExpanded(bool expanded)
        {
            if (menuRoot != null) menuRoot.SetActive(expanded);
        }

        // 종료는 즉시가 아니라 확인 모달 — 아니오(좌)·예(우), "예"(index 1)일 때만 QuitGameCommand.
        void OnQuit() => EventBus.Publish(new ShowModalCommand(
            quitConfirmTitle, quitConfirmMessage,
            new[]
            {
                new ModalButton(quitConfirmNo, ModalButtonKind.No),
                new ModalButton(quitConfirmYes, ModalButtonKind.Yes),
            },
            new ModalRequest(i => { if (i == 1) EventBus.Publish(new QuitGameCommand()); })));
    }
}
