using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Story;
using LoveAlgo.UI;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 게임 전체 흐름 관리
    /// Title → Username → Prologue → DayLoop → Ending
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] string prologueScript = "Prologue";  // 프롤로그 CSV 이름

        [Header("DOTween Settings")]
        [SerializeField] int tweenersCapacity = 500;   // 동시 트위너 수
        [SerializeField] int sequencesCapacity = 125;  // 동시 시퀀스 수

        // 현재 상태
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Title;
        public int CurrentDay { get; private set; } = 1;
        public int RemainingActions { get; private set; } = 3;  // 하루 남은 행동 수
        public string PlayerName { get; private set; } = "";

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // DontDestroyOnLoad(gameObject);  // 데모: 단일 씬

            // DOTween 초기화 - 용량 설정으로 IndexOutOfRangeException 방지
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(tweenersCapacity, sequencesCapacity);
            Debug.Log($"[GameManager] DOTween initialized: Tweeners={tweenersCapacity}, Sequences={sequencesCapacity}");
        }

        void Start()
        {
            ChangePhase(GamePhase.Title);
        }

        /// <summary>
        /// 게임 상태 전환
        /// </summary>
        public void ChangePhase(GamePhase newPhase)
        {
            var prevPhase = CurrentPhase;
            CurrentPhase = newPhase;

            Debug.Log($"[GameManager] Phase: {prevPhase} → {newPhase}");

            // 타이틀에서 나갈 때 BGM 페이드아웃
            if (prevPhase == GamePhase.Title && newPhase != GamePhase.Title)
            {
                AudioManager.Instance?.StopBGMAsync(1f).Forget();
            }

            switch (newPhase)
            {
                case GamePhase.Title:
                    EnterTitle();
                    break;
                case GamePhase.Username:
                    EnterUsername();
                    break;
                case GamePhase.Prologue:
                    EnterPrologue();
                    break;
                case GamePhase.DayLoop:
                    EnterDayLoop();
                    break;
                case GamePhase.Ending:
                    EnterEnding();
                    break;
            }
        }

        #region Phase Entry

        void EnterTitle()
        {
            UIManager.Instance?.ShowOnly(MainUIType.Title);
        }

        void EnterUsername()
        {
            UIManager.Instance?.ShowOnly(MainUIType.Username);
        }

        void EnterPrologue()
        {
            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);

            // 프롤로그 스크립트 실행
            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                runner.OnScriptEnd -= OnPrologueEnd;
                runner.OnScriptEnd += OnPrologueEnd;
                runner.StartScript(prologueScript).Forget();
            }
        }

        void EnterDayLoop()
        {
            // 스케줄 UI 표시
            UIManager.Instance?.ShowOnly(MainUIType.Schedule);
        }

        void EnterEnding()
        {
            // TODO: 엔딩 처리
            Debug.Log("[GameManager] Ending reached!");
        }

        #endregion

        #region Game Flow

        /// <summary>
        /// 타이틀 화면으로 이동
        /// </summary>
        public void GoToTitle()
        {
            ChangePhase(GamePhase.Title);
        }

        /// <summary>
        /// 새 게임 시작 (Title에서 호출)
        /// </summary>
        public void StartNewGame()
        {
            // 이전 BGM 정리
            Story.AudioManager.Instance?.StopBGMImmediate();
            
            PlayerName = "";
            CurrentDay = 1;
            RemainingActions = 3;
            ChangePhase(GamePhase.Username);
        }

        /// <summary>
        /// 이름 입력 완료 (UsernameUI에서 호출)
        /// </summary>
        public void OnNameConfirmed(string playerName)
        {
            PlayerName = playerName;
            
            // GameState에도 동기화 (DialogueUI 변수 치환용)
            if (GameState.Instance != null)
            {
                GameState.Instance.SetPlayerName(playerName);
            }
            
            Debug.Log($"[GameManager] 플레이어 이름: {playerName}");
            ChangePhase(GamePhase.Prologue);
        }

        /// <summary>
        /// 프롤로그 종료 (ScriptRunner OnScriptEnd에서 호출)
        /// </summary>
        void OnPrologueEnd()
        {
            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                runner.OnScriptEnd -= OnPrologueEnd;
            }

            // 자동저장 후 DayLoop 진입
            AutoSave();
            ChangePhase(GamePhase.DayLoop);
        }

        /// <summary>
        /// 스케줄 수행 완료 (ScheduleUI에서 호출)
        /// </summary>
        public void OnScheduleCompleted()
        {
            RemainingActions--;

            if (RemainingActions <= 0)
            {
                EndDay();
            }
            else
            {
                // 스케줄 UI 유지
                UIManager.Instance?.ShowOnly(MainUIType.Schedule);
            }
        }

        /// <summary>
        /// 하루 종료
        /// </summary>
        void EndDay()
        {
            CurrentDay++;
            RemainingActions = 3;

            // TODO: 일과 종료 이벤트, 저녁 스토리 등
            Debug.Log($"[GameManager] {CurrentDay}일차 시작");

            AutoSave();
            ChangePhase(GamePhase.DayLoop);
        }

        /// <summary>
        /// 이어하기 (Continue 버튼)
        /// </summary>
        public void ContinueGame()
        {
            var data = SaveManager.Load(SaveManager.AutoSaveSlot);
            if (data == null)
            {
                Debug.LogWarning("[GameManager] 자동저장 없음");
                return;
            }

            LoadFromSaveData(data);
        }

        /// <summary>
        /// 특정 슬롯에서 로드
        /// </summary>
        public void LoadGame(int slot)
        {
            var data = SaveManager.Load(slot);
            if (data == null)
            {
                Debug.LogWarning($"[GameManager] 슬롯 {slot} 데이터 없음");
                return;
            }

            LoadFromSaveData(data);
        }

        void LoadFromSaveData(SaveData data)
        {
            // 이전 BGM 정리
            Story.AudioManager.Instance?.StopBGMImmediate();
            
            PlayerName = data.PlayerName;
            CurrentDay = data.CurrentDay;
            RemainingActions = data.RemainingActions;

            // GameState 전체 복원 (스탯, 호감도, 플래그, 돈 등)
            SaveManager.ApplyToGameState(data);

            // TODO: 스크립트 위치 복원
            // if (!string.IsNullOrEmpty(data.ScriptName))
            // {
            //     await ScriptRunner.Instance?.StartScript(data.ScriptName);
            //     // JumpToIndex 메서드 필요
            // }

            // Phase로 복귀
            ChangePhase(data.Phase);
        }

        #endregion

        #region Save

        /// <summary>
        /// 자동저장 (슬롯 0)
        /// </summary>
        public void AutoSave()
        {
            Save(SaveManager.AutoSaveSlot);
            Debug.Log("[GameManager] 자동저장 완료");
        }

        /// <summary>
        /// 수동저장 (슬롯 1~29)
        /// </summary>
        public void Save(int slot)
        {
            // 스크립트 위치 정보
            var runner = ScriptRunner.Instance;
            string scriptName = runner?.CurrentScriptName ?? "";
            string lineId = runner?.CurrentLine?.LineID ?? "";
            int lineIndex = runner?.CurrentIndex ?? 0;

            SaveManager.Save(
                slot,
                CurrentPhase,
                CurrentDay,
                RemainingActions,
                scriptName,
                lineId,
                lineIndex,
                $"Day {CurrentDay}"
            );
        }

        #endregion
    }
}
