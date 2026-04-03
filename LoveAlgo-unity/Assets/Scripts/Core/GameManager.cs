using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Story;
using LoveAlgo.Schedule;

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

        public GamePhase CurrentPhase { get; set; } = GamePhase.Title;
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
            Flow.ChangePhase(GamePhase.Title);
        }

        void RestoreResolution()
        {
            int resIdx = PlayerPrefs.GetInt("ResolutionIndex", -1);
            if (resIdx < 0) return;
            bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            var resolutions = GameConstants.Resolutions;
            resIdx = Mathf.Clamp(resIdx, 0, resolutions.Length - 1);
            var res = resolutions[resIdx];
            Screen.SetResolution(res.w, res.h, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            Debug.Log($"[GameManager] 해상도 복원: {res.w}x{res.h}, 전체화면: {fullscreen}");
        }

        const string DemoSingleScheduleCompleteFlag = "Demo_SingleScheduleComplete";
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

        // ── 내부 상태 setter (컨트롤러 전용) ──
        public void SetCurrentPhase(GamePhase phase) => CurrentPhase = phase;
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
        public UniTask AutoSaveAsync() => Session.AutoSaveAsync();
        public void Save(int slot, bool usePendingThumbnail = true) => Session.Save(slot, usePendingThumbnail);
    }
}
