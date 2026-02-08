using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Story;
using LoveAlgo.UI;
using LoveAlgo.Schedule;

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

        /// <summary>
        /// 다음 날로 진행 (스크립트 매크로용)
        /// </summary>
        public void AdvanceDay(int actions = 3)
        {
            CurrentDay++;
            RemainingActions = actions;
            Debug.Log($"[GameManager] {CurrentDay}일차 시작 (actions={actions})");
        }

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

            // 타이틀에서 나갈 때 BGM 페이드아웃 (기본 3초)
            if (prevPhase == GamePhase.Title && newPhase != GamePhase.Title)
            {
                AudioManager.Instance?.StopBGMAsync().Forget();
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

            // 대화창을 숨김 상태로 시작 (첨 대사 시점에 자동 표시)
            var dialogueUI = UIManager.Instance?.DialogueUI;
            dialogueUI?.Clear();
            dialogueUI?.HideImmediate();

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

            // ScheduleUI에 콜백 연결
            var scheduleUI = UIManager.Instance?.ScheduleUI;
            scheduleUI?.ShowAsync(OnScheduleSelected).Forget();
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
            // Stop 전에 자동저장 (스크립트 위치 보존)
            AutoSave();
            ScriptRunner.Instance?.Stop();

            // 장면 정리 (캐릭터, 배경, 오버레이 등)
            CleanupStage();

            ChangePhase(GamePhase.Title);
        }

        /// <summary>
        /// 새 게임 시작 (Title에서 호출)
        /// </summary>
        public void StartNewGame()
        {
            // 이전 BGM 정리 (페이드아웃)
            Story.AudioManager.Instance?.StopBGMAsync().Forget();
            ScriptRunner.Instance?.Stop();

            // 장면 정리
            CleanupStage();

            // 게임 상태 초기화
            GameState.Instance?.ResetAll();

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
        /// 콘텐츠(데모) 분량 종료 시 호출
        /// 페이드아웃 → 저장 팝업 → 타이틀 복귀
        /// </summary>
        public void OnContentEnd()
        {
            ShowEndOfContentAsync().Forget();
        }

        async UniTaskVoid ShowEndOfContentAsync()
        {
            // 페이드 아웃
            await ScreenFX.Instance.FadeOutAsync(2f);

            // 저장 확인
            bool save = await PopupManager.Instance.ConfirmAsync("저장하시겠습니까?");
            if (save)
            {
                AutoSave();
                PopupManager.Instance?.Toast("저장", "저장되었습니다.");
                await UniTask.Delay(1000);
            }

            // 타이틀로 복귀
            GoToTitle();
        }

        /// <summary>
        /// 스케줄 선택 완료 → 스탯 적용 → 행동 소모
        /// </summary>
        void OnScheduleSelected(ScheduleType type)
        {
            var effect = ScheduleTable.Get(type);
            var gs = GameState.Instance;

            if (gs != null)
            {
                gs.AddStat("Str", effect.strengthChange);
                gs.AddStat("Int", effect.intelligenceChange);
                gs.AddStat("Soc", effect.socialChange);
                gs.AddStat("Per", effect.perseveranceChange);
                gs.AddStat("Fatigue", effect.fatigueChange);
                gs.AddMoney(effect.moneyChange);
            }

            Debug.Log($"[GameManager] 스케줄 완료: {effect.displayName}");
            OnScheduleCompleted();
        }

        /// <summary>
        /// 스케줄 수행 완료 (행동 소모 처리)
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
                // 스케줄 UI 다시 표시 (ScheduleUI.OnScheduleClick에서 HideAsync 호출됨)
                UIManager.Instance?.ShowOnly(MainUIType.Schedule);
                var scheduleUI = UIManager.Instance?.ScheduleUI;
                scheduleUI?.ShowAsync(OnScheduleSelected).Forget();
            }
        }

        /// <summary>
        /// 하루 종료 (블랙 페이드 연출 포함)
        /// </summary>
        void EndDay()
        {
            EndDayAsync().Forget();
        }

        async UniTaskVoid EndDayAsync()
        {
            // 페이드 아웃 (2.5초)
            await ScreenFX.Instance.FadeOutAsync(2.5f);

            CurrentDay++;
            RemainingActions = 3;

            Debug.Log($"[GameManager] {CurrentDay}일차 시작");

            AutoSave();

            // 잠시 블랙 유지
            await UniTask.Delay(500);

            ChangePhase(GamePhase.DayLoop);

            // 페이드 인 (2초)
            await ScreenFX.Instance.FadeInAsync(2.0f);
        }

        /// <summary>
        /// 이어하기 (Continue 버튼)
        /// </summary>
        public void ContinueGame()
        {
            // 자동저장 우선
            if (SaveManager.Exists(SaveManager.AutoSaveSlot))
            {
                LoadGame(SaveManager.AutoSaveSlot);
                return;
            }

            // 자동저장 없으면 최근 수동 저장 찾기
            var saves = SaveManager.GetAllUserSaves();
            if (saves.Count > 0)
            {
                saves.Sort((a, b) => b.data.SaveTime.CompareTo(a.data.SaveTime));
                LoadGame(saves[0].slot);
                return;
            }

            Debug.LogWarning("[GameManager] 세이브 데이터 없음");
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

            LoadFromSaveData(data).Forget();
        }

        async UniTaskVoid LoadFromSaveData(SaveData data)
        {
            // 이전 BGM 정리 (페이드아웃 완료 대기)
            if (Story.AudioManager.Instance != null)
            {
                Story.AudioManager.Instance.StopBGMImmediate();
            }
            ScriptRunner.Instance?.Stop();

            // 이전 장면 정리
            CleanupStage();

            PlayerName = data.PlayerName;
            CurrentDay = data.CurrentDay;
            RemainingActions = data.RemainingActions;

            // GameState 전체 복원 (스탯, 호감도, 플래그, 돈 등)
            SaveManager.ApplyToGameState(data);

            // 스크립트 위치 복원 (스토리 실행 중이었던 경우)
            if (!string.IsNullOrEmpty(data.ScriptName) &&
                (data.Phase == GamePhase.Prologue || data.Phase == GamePhase.DayLoop))
            {
                // Phase를 먼저 설정해야 입력 핸들러(DebugRemoteUI 등)가 클릭을 처리함
                CurrentPhase = data.Phase;

                UIManager.Instance?.ShowOnly(MainUIType.Dialogue);

                // 대화창을 숨김 상태로 시작 (첨 대사 시점에 자동 표시)
                var dialogueUI = UIManager.Instance?.DialogueUI;
                dialogueUI?.Clear();
                dialogueUI?.HideImmediate();

                // 장면 상태 복원 (배경, 캐릭터, BGM)
                await RestoreStageState(data);

                var runner = ScriptRunner.Instance;
                if (runner != null)
                {
                    // 프롤로그면 종료 이벤트 연결
                    if (data.Phase == GamePhase.Prologue)
                    {
                        runner.OnScriptEnd -= OnPrologueEnd;
                        runner.OnScriptEnd += OnPrologueEnd;
                    }

                    await runner.StartScriptFrom(data.ScriptName, data.LineId, data.LineIndex);
                }

                return;
            }

            // 스크립트가 없으면 Phase로 복귀
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
            // 스크립트 위치 정보 (실행 중일 때만 저장)
            var runner = ScriptRunner.Instance;
            string scriptName = "";
            string lineId = "";
            int lineIndex = 0;

            if (runner != null && runner.IsRunning)
            {
                scriptName = runner.CurrentScriptName ?? "";
                lineId = runner.CurrentLine?.LineID ?? "";
                lineIndex = runner.CurrentIndex;
            }

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

            // 스크린샷 캡처
            SaveManager.CaptureScreenshot(slot);
        }

        #endregion

        #region Stage State

        /// <summary>
        /// 장면 정리 (타이틀 복귀 / 로드 시)
        /// </summary>
        void CleanupStage()
        {
            // 레이어 정리
            StageManager.Instance?.Character?.ClearAll();
            StageManager.Instance?.Background?.Clear();
            StageManager.Instance?.VirtualBG?.HideImmediate();
            StageManager.Instance?.CG?.Clear();
            StageManager.Instance?.MonologueDim?.HideImmediate();

            // 화면 효과 정리
            if (ScreenFX.Instance != null)
            {
                ScreenFX.Instance.SetClear();
                ScreenFX.Instance.EyeOpenImmediate();
            }

            // 오디오 정리
            Story.AudioManager.Instance?.StopVoice();

            // DOTween 동즁 트윈 정리 (Safe Mode로 사용 중이므로 KillAll 대신 유휴 트윈만 정리)
            DOTween.KillAll();
        }

        /// <summary>
        /// 세이브 데이터에서 장면 상태 복원 (배경, 캐릭터, BGM, CG, 오버레이, 딤, FX)
        /// </summary>
        async UniTask RestoreStageState(SaveData data)
        {
            // 배경 복원 (즉시 전환)
            if (!string.IsNullOrEmpty(data.CurrentBG))
            {
                var bg = StageManager.Instance?.Background;
                if (bg != null)
                {
                    await bg.ChangeBackgroundAsync(data.CurrentBG, Story.BGTransition.Cut, 0f);
                }
            }

            // 캐릭터 복원
            if (data.Characters != null && data.Characters.Count > 0)
            {
                var charLayer = StageManager.Instance?.Character;
                if (charLayer != null)
                {
                    foreach (var charInfo in data.Characters)
                    {
                        Story.SlotPosition pos;
                        switch (charInfo.Slot)
                        {
                            case "L": pos = Story.SlotPosition.L; break;
                            case "R": pos = Story.SlotPosition.R; break;
                            default:  pos = Story.SlotPosition.C; break;
                        }
                        var slot = charLayer.GetSlot(pos);
                        if (slot != null)
                        {
                            await slot.EnterAsync(charInfo.Character, charInfo.Emote);
                        }
                    }
                }
            }

            // CG 복원
            if (!string.IsNullOrEmpty(data.CurrentCG))
            {
                var cg = StageManager.Instance?.CG;
                if (cg != null)
                {
                    await cg.ShowAsync(data.CurrentCG, 0f);  // 즉시 표시
                }
            }

            // VirtualBG 오버레이 복원
            if (!string.IsNullOrEmpty(data.CurrentOverlay))
            {
                var overlay = StageManager.Instance?.VirtualBG;
                if (overlay != null)
                {
                    await overlay.ShowAsync(data.CurrentOverlay, 0f);  // 즉시 표시
                }
            }

            // 독백 딤 복원
            if (data.IsMonologueDimShowing)
            {
                StageManager.Instance?.MonologueDim?.ShowImmediate();
            }

            // 화면 효과 복원
            var fx = ScreenFX.Instance;
            if (fx != null)
            {
                if (data.IsEyeClosed)
                    fx.EyeCloseImmediate();
                else if (data.IsFadeBlack)
                    fx.SetBlack();
                else
                    fx.SetClear();
            }

            // BGM 복원
            if (!string.IsNullOrEmpty(data.CurrentBGM) && Story.AudioManager.Instance != null)
            {
                await Story.AudioManager.Instance.PlayBGMAsync(data.CurrentBGM, 0.5f);
            }
        }

        #endregion
    }
}
