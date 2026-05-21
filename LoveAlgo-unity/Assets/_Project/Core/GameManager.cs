using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Common;
using LoveAlgo.Story;
using LoveAlgo.Schedule;
using LoveAlgo.Core;

namespace LoveAlgo.Core
{
    /// <summary>게임 전체 진입점 — 상태 관리 + 컨트롤러 조합</summary>
    public class GameManager : SingletonMonoBehaviour<GameManager>
    {
        [Header("Settings")]
        [SerializeField] string prologueScript = "Prologue";
        [SerializeField] bool limitDemoToSingleSchedule = true;

        [Header("DOTween Settings")]
        [SerializeField] int tweenersCapacity = 500;
        [SerializeField] int sequencesCapacity = 125;

        GamePhase _currentPhase = GamePhase.Title;
        /// <summary>현재 phase. 변경은 ChangePhase(...) 또는 컨트롤러 전용 SetCurrentPhase 경유 — 외부 직접 set 금지.</summary>
        public GamePhase CurrentPhase => _currentPhase;
        public int CurrentDay { get; set; } = 1;
        public int RemainingActions { get; set; }
        public string PlayerName { get; set; } = "";

        public GameFlowController Flow { get; private set; }
        public DayLoopController DayLoop { get; private set; }
        public SessionController Session { get; private set; }

        bool isTransitioning;
        bool isLoading;

        public bool IsTransitioning { get => isTransitioning; set => isTransitioning = value; }
        public bool IsLoading { get => isLoading; set => isLoading = value; }
        public string PrologueScript => prologueScript;

        /// <summary>다음 날로 진행 (스크립트 매크로용)</summary>
        public void AdvanceDay(int actions = -1)
        {
            CurrentDay++;
            RemainingActions = actions < 0 ? GameConstants.ActionsPerDay : actions;
            Debug.Log($"[GameManager] {CurrentDay}일차 시작 (actions={actions})");
        }

        protected override void OnSingletonAwake()
        {
#if !UNITY_EDITOR
            Debug.unityLogger.filterLogType = LogType.Warning;
            ClearStaleSaves();
#endif
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(tweenersCapacity, sequencesCapacity);
            Debug.Log($"[GameManager] DOTween initialized: Tweeners={tweenersCapacity}, Sequences={sequencesCapacity}");
            RestoreResolution();
            RemainingActions = GameConstants.ActionsPerDay;
        }

        void Start()
        {
            Flow = new GameFlowController(this);
            DayLoop = new DayLoopController(this);
            Session = new SessionController(this);

            // 게임 설치 후 최초 1회 진입: EntryRouter가 LockScreen GameStart 띄우는 중 → Title 전환 보류.
            // LockScreen Outro Blackout 시점에 EntryRouter가 ChangePhase(Title)을 호출한다.
            var ls = LoveAlgo.Common.Services.TryGet<LoveAlgo.LockScreen.ILockScreen>();
            if (ls != null && !ls.IsPasswordSet)
            {
                Debug.Log("[GameManager] 첫 진입(LockScreen 흐름) — Title 전환 보류");
                return;
            }

            Flow.ChangePhase(GamePhase.Title);
        }

        void RestoreResolution()
        {
            // 해상도/전체화면 저장소는 SettingsModule이 마스터. SettingsModule(-450)이
            // 이미 Awake에서 Load + ApplyResolution 자체 호출 흐름을 갖춰두지 않으므로,
            // GameManager 시작 시 한 번 위임 호출해 화면에 반영한다.
            var settings = LoveAlgo.Common.Services.TryGet<LoveAlgo.Settings.ISettings>();
            settings?.ApplyResolution();
        }

        const string DemoSingleScheduleCompleteFlag = "Demo_SingleScheduleComplete";
        public bool IsDemoMode => limitDemoToSingleSchedule;
        public bool ShouldEndDemoAfterSchedule() => limitDemoToSingleSchedule && CurrentPhase == GamePhase.DayLoop && !IsDemoScheduleComplete();
        public bool ShouldReturnToDemoEnd() => limitDemoToSingleSchedule && CurrentPhase == GamePhase.DayLoop && IsDemoScheduleComplete();
        bool IsDemoScheduleComplete() => GameState.Instance?.GetFlag(DemoSingleScheduleCompleteFlag) ?? false;
        public void MarkDemoScheduleComplete() => GameState.Instance?.SetFlag(DemoSingleScheduleCompleteFlag, true);

        /// <summary>빌드 변경 감지 시 에디터/이전 빌드 잔여 세이브 삭제</summary>
        void ClearStaleSaves()
        {
            const string key = "LastClearedBuild";
            string currentBuild = Application.buildGUID;
            if (string.IsNullOrEmpty(currentBuild)) currentBuild = Application.version;
            string prev = PlayerPrefs.GetString(key, "");
            if (prev == currentBuild) return;
            SaveManager.DeleteAll();
            PlayerPrefs.SetString(key, currentBuild);
            PlayerPrefs.Save();
        }

        // ── 내부 상태 setter (컨트롤러 전용 — 같은 어셈블리 내 Flow/Session/Debug에서만 호출) ──
        /// <summary>
        /// phase를 직접 갱신하고 GamePhaseChangedEvent를 발행한다. 같은 phase로의 재설정은 no-op.
        /// 외부 진입점은 ChangePhase(...) 사용 — 이 메서드는 FlowController/SessionController의
        /// 내부 흐름 전용으로, 정상 전환에 필요한 사전·사후 처리(페이드/Stage 정리 등)는 호출자가 책임.
        /// </summary>
        internal void SetCurrentPhase(GamePhase phase)
        {
            if (_currentPhase == phase) return;
            var prev = _currentPhase;
            _currentPhase = phase;
            EventBus.Publish(new GamePhaseChangedEvent(prev, phase));
        }
        public void SetPlayerName(string name) => PlayerName = name;
        public void CleanupStage() => Session.CleanupStage();
        public void OnScheduleSelected(ScheduleType type) => DayLoop.OnScheduleSelected(type);
        public string DetermineEndingHeroine() => DayLoop.DetermineEndingHeroine();
        public bool IsHappyEnding(string heroineId) => DayLoop.IsHappyEnding(heroineId);

        // ── 외부 진입점 → 컨트롤러 위임 ──
        public void ChangePhase(GamePhase phase) => Flow.ChangePhase(phase);
        public void GoToTitle() => Flow.GoToTitle();
        public void StartNewGame() => Session.StartNewGame();
        public void OnNameConfirmed(string name) => Flow.OnNameConfirmed(name);
        public void ContinueGame() => Session.ContinueGame();
        public void LoadGame(int slot) => Session.LoadGame(slot);
        public void OnContentEnd() => Flow.OnContentEnd();
        public void OnScheduleCompleted() => DayLoop.OnScheduleCompleted();
        public void SkipToDayLoop() => Session.SkipToDayLoop();
        public UniTask AutoSaveAsync(string reason = "unspecified") => Session.AutoSaveAsync(reason);
        public void Save(int slot, bool usePendingThumbnail = true, string customLabel = null)
            => Session.Save(slot, usePendingThumbnail, customLabel);
    }
}
