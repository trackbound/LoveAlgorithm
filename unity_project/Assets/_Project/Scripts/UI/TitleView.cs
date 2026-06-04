using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;    // JsonSaveStore
using LoveAlgo.Events;  // StartNewGameCommand, ContinueGameCommand, PlayBgmCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 타이틀 화면 뷰(*View). New Game/Continue/Settings 버튼 클릭 → EventBus 의도 발행(ADR-007: 표시만,
    /// 상태·씬 변경은 구독자 몫). 별도 Title 씬의 <c>_UI</c> 캔버스에 배치.
    ///
    /// 동작 범위: New Game→<see cref="StartNewGameCommand"/>, Continue→<see cref="ContinueGameCommand"/>
    /// (둘 다 SceneFlowController가 받아 게임 씬 로드). Continue는 오토세이브가 없으면 비활성(interactable=false).
    /// Settings 버튼은 배치만(리스너 미연결) — 후속 마일스톤에서 연결. 버튼은 인스펙터 바인딩(미바인딩 시 조용히 무시).
    ///
    /// 진입 연출: <see cref="Start"/>에서 타이틀 BGM 재생 명령(<see cref="PlayBgmCommand"/>)을 발행한다 —
    /// Title 씬의 AudioManager가 구독·재생(ADR-007). titleBgm을 비우면 발행하지 않는다.
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        [SerializeField] Button newGameButton;
        [SerializeField] Button continueButton;
        [SerializeField] Button settingsButton; // 배치만, 후속 연결

        [Tooltip("타이틀 진입 시 재생할 BGM 이름(Resources/Audio/BGM/{name}). 비우면 재생 안 함.")]
        [SerializeField] string titleBgm = "title";

        public Button NewGameButton { get => newGameButton; set => newGameButton = value; }
        public Button ContinueButton { get => continueButton; set => continueButton = value; }
        public Button SettingsButton { get => settingsButton; set => settingsButton = value; }

        void Awake()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinue);
                // 오토세이브가 없으면 이어하기 비활성(ScheduleSlot 패턴: 미바인딩 버튼은 무시).
                continueButton.interactable = JsonSaveStore.Exists(JsonSaveStore.AutoSaveSlot);
            }
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(titleBgm))
                EventBus.Publish(new PlayBgmCommand(titleBgm));
        }

        void OnNewGame() => EventBus.Publish(new StartNewGameCommand());
        void OnContinue() => EventBus.Publish(new ContinueGameCommand());

        void OnDestroy()
        {
            if (newGameButton != null) newGameButton.onClick.RemoveListener(OnNewGame);
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
        }
    }
}
