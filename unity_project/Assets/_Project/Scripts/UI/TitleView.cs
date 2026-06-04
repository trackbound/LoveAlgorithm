using LoveAlgo.Common; // EventBus
using LoveAlgo.Events;  // StartNewGameCommand, PlayBgmCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 타이틀 화면 뷰(*View). New Game/Continue/Settings 버튼 클릭 → EventBus 의도 발행(ADR-007: 표시만,
    /// 상태·씬 변경은 구독자 몫). 별도 Title 씬의 <c>_UI</c> 캔버스에 배치.
    ///
    /// 슬라이스1 범위: New Game만 실동작 — <see cref="StartNewGameCommand"/> 발행, SceneFlowController가 받아
    /// 게임 씬을 로드한다. Continue/Settings 버튼은 배치만(리스너 미연결) — 후속 슬라이스에서 명령을 연결한다.
    /// 버튼은 인스펙터 바인딩(미바인딩 시 조용히 무시 — ScheduleSlot 패턴).
    ///
    /// 진입 연출: <see cref="Start"/>에서 타이틀 BGM 재생 명령(<see cref="PlayBgmCommand"/>)을 발행한다 —
    /// Title 씬의 AudioManager가 구독·재생(ADR-007, 발행/재생 분리). titleBgm을 비우면 발행하지 않는다.
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        [SerializeField] Button newGameButton;
        [SerializeField] Button continueButton; // 슬라이스1: 배치만, 후속 연결
        [SerializeField] Button settingsButton; // 슬라이스1: 배치만, 후속 연결

        [Tooltip("타이틀 진입 시 재생할 BGM 이름(Resources/Audio/BGM/{name}). 비우면 재생 안 함.")]
        [SerializeField] string titleBgm = "title";

        public Button NewGameButton { get => newGameButton; set => newGameButton = value; }
        public Button ContinueButton { get => continueButton; set => continueButton = value; }
        public Button SettingsButton { get => settingsButton; set => settingsButton = value; }

        void Awake()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(titleBgm))
                EventBus.Publish(new PlayBgmCommand(titleBgm));
        }

        void OnNewGame() => EventBus.Publish(new StartNewGameCommand());

        void OnDestroy()
        {
            if (newGameButton != null) newGameButton.onClick.RemoveListener(OnNewGame);
        }
    }
}
