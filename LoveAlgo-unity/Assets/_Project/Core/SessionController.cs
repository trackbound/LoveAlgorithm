using System.Threading;
using UnityEngine;
using LoveAlgo.Modules.Affinity;
using LoveAlgo.Modules.Audio;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;
using LoveAlgo.Story.SaveSystem;
using LoveAlgo.UI;
using LoveAlgo.Schedule;
using LoveAlgo.Core;
using LoveAlgo.Stage;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 저장/로드/세션 관리 전담 컨트롤러
    /// 새 게임, 이어하기, 세이브 데이터 적용, 장면 복원
    /// </summary>
    public class SessionController
    {
        readonly GameManager _gm;

        /// <summary>로드 진행 중 재진입 방지 플래그</summary>
        bool _isLoading;

        public SessionController(GameManager gm)
        {
            _gm = gm;
        }

        /// <summary>
        /// 새 게임 시작 (Title에서 호출)
        /// </summary>
        public void StartNewGame()
        {
            // 이전 BGM 즉시 정리
            AudioManager.Instance?.StopBGMImmediate();
            ScriptRunner.Instance?.Stop();

            // 장면 정리
            _gm.CleanupStage();

            // 게임 상태 초기화
            GameState.Instance?.ResetAll();
            DayEventTable.ResetFired();
            HeroinePointTracker.Reset();
            Phone.MessengerManager.Reset();

            _gm.SetPlayerName("");
            _gm.CurrentDay = 1;
            _gm.RemainingActions = GameConstants.ActionsPerDay;
            // 새 게임은 곧바로 프롬로그로 진입한다 (로딩 화면 + 페이드 + 스크립트 시작).
            // 이름 입력은 프롬로그 CSV 중간의 `Flow,,Username,>` 명령으로 호출된다.
            GameManager.Instance.Flow?.StartPrologueFromNewGame();
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
            if (_isLoading)
            {
                Debug.LogWarning("[GameManager] LoadFromSaveData 중복 호출 무시");
                return;
            }
            _isLoading = true;
            try
            {
                // 로드 시작 전 팝업 강제 정리 (CloseModalAsync 실패 시 안전장치)
                PopupManager.Instance?.CloseAll();

                // 진행 중이던 선택지 UI 잔상 제거 (스케줄/팝업과 함께 저장된 케이스)
                UIManager.Instance?.ChoicePopup?.ResetImmediate();

                // 이전 BGM 정리 (페이드아웃 완료 대기)
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StopBGMImmediate();
                }
                LoveAlgo.Story.StageSyncLog.Section("LoadFromSave", "Stop ScriptRunner");
                ScriptRunner.Instance?.Stop();

                // ── Race 방어 ──
                LoveAlgo.Story.StageSyncLog.Section("LoadFromSave", "Yield 1 frame for cancellation propagation");
                await UniTask.Yield();

                // 이전 장면 정리 (1차)
                LoveAlgo.Story.StageSyncLog.Section("LoadFromSave", "Cleanup stage (1차)");
                _gm.CleanupStage();

                // 2차 명시 정리 — race 잔여 작업이 또 무대를 건드렸을 수도 있으므로
                LoveAlgo.Story.StageSyncLog.Section("LoadFromSave", "Cleanup stage (2차)");
                await UniTask.Yield();
                StageModule.Instance?.CG?.Clear();
                StageModule.Instance?.SDCutscene?.Clear();
                StageModule.Instance?.VirtualBG?.HideImmediate();
                StageModule.Instance?.MonologueDim?.HideImmediate();

                _gm.SetPlayerName(data.PlayerName);
                _gm.CurrentDay = data.CurrentDay;
                _gm.RemainingActions = data.RemainingActions;

                // GameState 전체 복원 (스탯, 호감도, 플래그, 돈 등)
                SaveManager.ApplyToGameState(data);

                // 스크립트 위치 복원 (스토리 실행 중이었던 경우)
                if (!string.IsNullOrEmpty(data.ScriptName) &&
                    (data.Phase == GamePhase.Prologue || data.Phase == GamePhase.DayLoop))
                {
                    // Phase를 먼저 설정해야 입력 핸들러(StoryInputHandler)가 클릭을 처리함
                    _gm.SetCurrentPhase(data.Phase);

                    UIManager.Instance?.ShowOnly(MainUIType.Dialogue);

                    // 대화창을 숨김 상태로 시작 (첨 대사 시점에 자동 표시)
                    var dialogueUI = UIManager.Instance?.DialogueUI;
                    dialogueUI?.Clear();
                    dialogueUI?.ClearLog();  // 이전 세션 로그 제거 (중복 방지)
                    dialogueUI?.HideImmediate();

                    // 장면 상태 복원 (배경, 캐릭터, BGM)
                    LoveAlgo.Story.StageSyncLog.Section("LoadFromSave", "Restore stage from SaveData");
                    await RestoreStageState(data);

                    // 안전장치: 저장 시 암전이었으면 페이드 인 (전환 중 저장된 경우)
                    if (data.IsFadeBlack)
                    {
                        var loadFx = ScreenFX.Instance;
                        if (loadFx != null)
                        {
                            loadFx.SetClear();
                            Debug.Log("[GameManager] 로드 시 IsFadeBlack 해제");
                        }
                    }

                    var runner = ScriptRunner.Instance;
                    if (runner != null)
                    {
                        // 프롤로그면 종료 이벤트 연결
                        if (data.Phase == GamePhase.Prologue)
                        {
                            var flow = GameManager.Instance.Flow;
                            if (flow != null)
                            {
                                runner.OnScriptEnd -= flow.OnPrologueEnd;
                                runner.OnScriptEnd += flow.OnPrologueEnd;
                            }
                        }

                        // 로드 완료 — 스크립트 실행 전에 플래그 해제 (실행 중 재로드 허용)
                        _isLoading = false;
                        LoveAlgo.Story.StageSyncLog.Section("LoadFromSave",
                            $"StartScriptFrom script={data.ScriptName} lineId={data.LineId} lineIdx={data.LineIndex}");
                        await runner.StartScriptFrom(data.ScriptName, data.LineId, data.LineIndex);
                    }

                    return;
                }

                // 스크립트가 없으면 Phase로 복귀
                GameManager.Instance.Flow?.ChangePhase(data.Phase);
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// 자동저장 (슬롯 0) — 비동기: UI 숨김 → 1프레임 대기 → 스크린샷 → 저장
        /// </summary>
        public async UniTask AutoSaveAsync()
        {
            await SaveThumbnailManager.CapturePendingScreenshotAsync();
            Save(SaveManager.AutoSaveSlot, usePendingThumbnail: true);
            Debug.Log("[GameManager] 자동저장 완료");
        }

        /// <summary>
        /// 수동저장 (슬롯 1~29).
        /// customLabel: 사용자가 직접 입력한 슬롯 이름. null/공백이면 자동값(GetSaveChapterName) 사용.
        /// </summary>
        public void Save(int slot, bool usePendingThumbnail = true, string customLabel = null)
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

            string chapterName = !string.IsNullOrWhiteSpace(customLabel)
                ? customLabel
                : GetSaveChapterName(scriptName);

            SaveManager.Save(
                slot,
                _gm.CurrentPhase,
                _gm.CurrentDay,
                _gm.RemainingActions,
                scriptName,
                lineId,
                lineIndex,
                chapterName
            );

            // 스크린샷 저장
            // - 수동 저장 팝업: ShowSave에서 미리 캡처한 pending 썸네일 우선 사용
            //   (commit 실패 시 즉시 재캡처는 SaveLoadPopup/Confirm/딤이 찍힐 위험이 있어 생략)
            // - 자동저장/기타: pending 미사용 시 즉시 캡처
            if (usePendingThumbnail)
            {
                if (!SaveManager.TryCommitPendingScreenshot(slot))
                {
                    Debug.LogWarning($"[SessionController] 슬롯 {slot} pending 썸네일 commit 실패 — 기존 썸네일 유지");
                }
            }
            else
            {
                SaveManager.CaptureScreenshot(slot);
            }
        }

        /// <summary>
        /// 세이브 슬롯 표시용 챕터명
        /// 임시 룰: Prologue phase는 항상 "프롤로그"로 고정
        /// </summary>
        string GetSaveChapterName(string scriptName)
        {
            if (_gm.CurrentPhase == GamePhase.Prologue)
                return "프롤로그";

            return $"Day {_gm.CurrentDay}";
        }

        /// <summary>
        /// 장면 정리 (타이틀 복귀 / 로드 시)
        /// </summary>
        public void CleanupStage()
        {
            // 레이어 정리
            StageModule.Instance?.Character?.SetVisibleImmediate(true);  // SD 숨김 상태 복원
            StageModule.Instance?.Character?.ClearAll();
            StageModule.Instance?.Background?.Clear();
            StageModule.Instance?.VirtualBG?.HideImmediate();
            StageModule.Instance?.CG?.Clear();
            StageModule.Instance?.SDCutscene?.Clear();
            StageModule.Instance?.MonologueDim?.HideImmediate();

            // 화면 효과 정리
            if (ScreenFX.Instance != null)
            {
                ScreenFX.Instance.SetClear();
                ScreenFX.Instance.EyeOpenImmediate();
            }

            // 로딩 화면 정리
            LoadingScreen.Instance?.HideImmediate();

            // 오디오 정리
            AudioManager.Instance?.StopBGMImmediate();
            AudioManager.Instance?.StopVoice();

            // 캐릭터 스프라이트 캐시 정리
            CharacterSlot.ClearSpriteCache();

            // 미사용 에셋 메모리 해제
            Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 세이브 데이터에서 장면 상태 복원 (배경, 캐릭터, BGM, CG, 오버레이, 딤, FX).
        /// 실제 로직은 StageRestorer.RestoreAsync로 추출됨 — 디버그 점프 등에서도 공유 사용.
        /// </summary>
        UniTask RestoreStageState(SaveData data) => StageRestorer.RestoreAsync(data);

        /// <summary>
        /// 테스트용: 프롤로그 스킵하고 DayLoop 직행
        /// </summary>
        public void SkipToDayLoop()
        {
            AudioManager.Instance?.StopBGMImmediate();
            ScriptRunner.Instance?.Stop();
            CleanupStage();

            GameState.Instance?.ResetAll();
            DayEventTable.ResetFired();
            HeroinePointTracker.Reset();
            Phone.MessengerManager.Reset();

            _gm.SetPlayerName("테스터");
            GameState.Instance?.SetPlayerName(_gm.PlayerName);
            _gm.CurrentDay = 1;
            _gm.RemainingActions = GameConstants.ActionsPerDay;
            GameState.Instance?.AddMoney(100000);

            var flow = GameManager.Instance.Flow;
            flow?.ChangePhase(GamePhase.DayLoop);
        }
    }
}
