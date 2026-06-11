using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;    // JsonSaveStore
using LoveAlgo.Events;  // StartNewGameCommand, ContinueGameCommand, QuitGameCommand, ShowModalCommand, ModalRequest, PlayBgmCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 타이틀 화면 뷰(*View). 메뉴 버튼 클릭 → EventBus 의도 발행(ADR-007: 표시만, 상태·씬·종료는 구독자 몫).
    /// 별도 Title 씬의 <c>_UI</c> 캔버스에 배치. 버튼은 인스펙터 바인딩(미바인딩 시 조용히 무시).
    ///
    /// 동작 범위:
    /// - New Game(Start) → <see cref="StartNewGameCommand"/>, Continue → <see cref="ContinueGameCommand"/>
    ///   (둘 다 SceneFlowController가 받아 게임 씬 로드). Continue는 오토세이브가 없으면 비활성(interactable=false).
    /// - Exit → 확인 모달(<see cref="ShowModalCommand"/>) → "예"일 때만 <see cref="QuitGameCommand"/> 발행
    ///   (SceneFlowController가 받아 종료). 모달 뷰(ModalView)는 Title 씬에 배선해야 한다(없으면 콜백 미수신=종료 불가).
    /// - Settings(Config) → <see cref="ShowSettingsCommand"/>, Load → <see cref="ShowSaveLoadCommand"/>(Overlay 팝업; 각각
    ///   SettingsView/SaveLoadView가 구독·표시). Extra → 목적지 미존재로 안내 로그만(후속). 필요한 것만 배선(과설계 게이트).
    ///
    /// 진입 연출: <see cref="Start"/>에서 타이틀 BGM 재생 명령(<see cref="PlayBgmCommand"/>)을 발행한다 —
    /// Title 씬의 AudioManager가 구독·재생(ADR-007). titleBgm을 비우면 발행하지 않는다.
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        [SerializeField] Button newGameButton; // Start
        [SerializeField] Button continueButton;
        [SerializeField] Button settingsButton; // Config — 안내 로그만, 후속 연결
        [SerializeField] Button loadButton;      // 불러오기 — 안내 로그만, 후속 연결
        [SerializeField] Button extraButton;     // 부가 콘텐츠 — 안내 로그만, 후속 연결
        [SerializeField] Button exitButton;      // 게임 종료

        [Tooltip("타이틀 진입 시 재생할 BGM 이름(Resources/Audio/BGM/{name}). 비우면 재생 안 함.")]
        [SerializeField] string titleBgm = "title";

        [Header("종료 확인 모달")]
        [SerializeField] string exitConfirmTitle = "게임 종료";
        [SerializeField] string exitConfirmMessage = "정말 종료하시겠습니까?";
        [Tooltip("버튼 0 = 종료(예), 버튼 1 = 취소(아니오).")]
        [SerializeField] string exitConfirmYes = "예";
        [SerializeField] string exitConfirmNo = "아니오";

        public Button NewGameButton { get => newGameButton; set => newGameButton = value; }
        public Button ContinueButton { get => continueButton; set => continueButton = value; }
        public Button SettingsButton { get => settingsButton; set => settingsButton = value; }
        public Button LoadButton { get => loadButton; set => loadButton = value; }
        public Button ExtraButton { get => extraButton; set => extraButton = value; }
        public Button ExitButton { get => exitButton; set => exitButton = value; }

        void Awake()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinue);
                // 오토세이브가 없으면 이어하기 비활성(ScheduleSlot 패턴: 미바인딩 버튼은 무시).
                continueButton.interactable = JsonSaveStore.Exists(JsonSaveStore.AutoSaveSlot);
            }
            if (exitButton != null) exitButton.onClick.AddListener(OnExit);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (loadButton != null) loadButton.onClick.AddListener(OnLoad);
            if (extraButton != null) extraButton.onClick.AddListener(OnExtra);
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(titleBgm))
                EventBus.Publish(new PlayBgmCommand(titleBgm));
        }

        void OnNewGame() => EventBus.Publish(new StartNewGameCommand());
        void OnContinue() => EventBus.Publish(new ContinueGameCommand());
        // Exit은 즉시 종료하지 않고 확인 모달을 띄운다 — 아니오(좌, No)·예(우, Yes) 순서, "예"(index 1)일 때만
        // QuitGameCommand 발행(ADR-007: 표시·아트는 ModalView/프리팹, 종료 동작은 콜백→구독자). 첫 범용 모달 소비처.
        void OnExit() => EventBus.Publish(new ShowModalCommand(
            exitConfirmTitle, exitConfirmMessage,
            new[]
            {
                new ModalButton(exitConfirmNo, ModalButtonKind.No),
                new ModalButton(exitConfirmYes, ModalButtonKind.Yes),
            },
            new ModalRequest(i => { if (i == 1) EventBus.Publish(new QuitGameCommand()); })));

        // Settings/Load는 Overlay 팝업 커맨드 발행(SettingsView/SaveLoadView가 구독·표시). Extra는 목적지 미존재 — 안내 로그만.
        void OnSettings() => EventBus.Publish(new ShowSettingsCommand());
        void OnLoad() => EventBus.Publish(new ShowSaveLoadCommand(SaveLoadMode.Load));
        void OnExtra() => LogTodo("부가 콘텐츠(Extra)");
        static void LogTodo(string what) => Log.Info($"[Title] {what} 화면은 준비 중입니다(후속 마일스톤).");

        void OnDestroy()
        {
            if (newGameButton != null) newGameButton.onClick.RemoveListener(OnNewGame);
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
            if (exitButton != null) exitButton.onClick.RemoveListener(OnExit);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
            if (loadButton != null) loadButton.onClick.RemoveListener(OnLoad);
            if (extraButton != null) extraButton.onClick.RemoveListener(OnExtra);
        }
    }
}
