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
                ScriptRunner.Instance?.Stop();

                // 이전 장면 정리
                _gm.CleanupStage();

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
        /// 자동저장 (슬롯 0) — 비동기: UI 숨김 → 1프레임 대기 → 스크린샷 → 저장.
        ///
        /// 호출 정책 (모든 진입점이 reason을 전달):
        /// - "day-end"   : DayLoopController.EndDayAsync, day 증가 직후
        /// - "phase:X"   : GameFlowController가 Phase 전환 직후 (X = 대상 phase)
        /// - "macro:X"   : 매크로(DayEnd 등)가 호출
        /// - "scripted"  : 스크립트가 직접 트리거한 명시적 저장점
        /// 어디서 자동저장이 일어나는지 콘솔에서 추적 가능. 향후 정책 변경(빈도 제한·
        /// 진행률 임계치 등)이 필요하면 이 reason을 분기 키로 활용.
        /// </summary>
        public async UniTask AutoSaveAsync(string reason = "unspecified")
        {
            await SaveThumbnailManager.CapturePendingScreenshotAsync();
            Save(SaveManager.AutoSaveSlot, usePendingThumbnail: true);
            Debug.Log($"[GameManager] 자동저장 완료 (reason={reason})");
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

            // 썸네일을 먼저 commit한 뒤에만 JSON 저장 — 데이터/썸네일 불일치 방지.
            // pending 우선(수동저장 팝업): commit 실패 시 옛 슬롯 유지(데이터 저장도 보류) →
            // 사용자가 잘못된 시각화 + 새 데이터 조합을 보지 않도록 보수적으로 차단.
            // 자동저장: 즉시 캡처(CaptureScreenshot) — 캡처 실패는 내부에서 LogWarning만 하고
            // 데이터 저장은 계속 (자동저장은 데이터 보존 우선, 썸네일은 best-effort).
            if (usePendingThumbnail)
            {
                if (!SaveManager.TryCommitPendingScreenshot(slot))
                {
                    Debug.LogError($"[SessionController] 슬롯 {slot} pending 썸네일 commit 실패 — 데이터 저장도 보류 (옛 슬롯 유지)");
                    return;
                }
            }
            else
            {
                SaveManager.CaptureScreenshot(slot);
            }

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
        /// 장면 정리 (타이틀 복귀 / 로드 시). Resources.UnloadUnusedAssets는 fire-and-forget —
        /// AsyncOperation을 그대로 무시하므로 백그라운드에서 진행되며 호출 자체는 비-블로킹.
        /// 정리 완료를 await하려면 <see cref="CleanupStageAsync"/>.
        /// </summary>
        public void CleanupStage()
        {
            CleanupStageSyncPart();
            _ = Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// CleanupStage의 비동기 버전 — Resources.UnloadUnusedAssets 완료까지 대기.
        /// 씬 전환·로드 직후 메모리 안정성이 필요할 때만 사용 (일반 정리는 CleanupStage로 충분).
        /// </summary>
        public async UniTask CleanupStageAsync(CancellationToken ct = default)
        {
            CleanupStageSyncPart();
            await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: ct);
        }

        void CleanupStageSyncPart()
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
        }

        /// <summary>
        /// 세이브 데이터에서 장면 상태 복원 (배경, 캐릭터, BGM, CG, 오버레이, 딤, FX)
        /// </summary>
        async UniTask RestoreStageState(SaveData data)
        {
            // 배경 복원 (즉시 전환)
            if (!string.IsNullOrEmpty(data.CurrentBG))
            {
                var bg = StageModule.Instance?.Background;
                if (bg != null)
                {
                    await bg.ChangeBackgroundAsync(data.CurrentBG, Story.BGTransition.Cut, 0f);
                }
            }

            // 캐릭터 복원
            if (data.Characters != null && data.Characters.Count > 0)
            {
                var charLayer = StageModule.Instance?.Character;
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
                var cg = StageModule.Instance?.CG;
                if (cg != null)
                {
                    await cg.ShowAsync(data.CurrentCG, 0f);
                }
            }

            // SD 컷씬 복원
            if (!string.IsNullOrEmpty(data.CurrentSD))
            {
                var sd = StageModule.Instance?.SDCutscene;
                if (sd != null)
                {
                    StageModule.Instance?.Character?.SetVisibleImmediate(false);
                    await sd.ShowAsync(data.CurrentSD, 0f);
                }
            }

            // VirtualBG 오버레이 복원
            if (!string.IsNullOrEmpty(data.CurrentOverlay))
            {
                var overlay = StageModule.Instance?.VirtualBG;
                if (overlay != null)
                {
                    await overlay.ShowAsync(data.CurrentOverlay, 0f);
                }
            }

            // 독백 딤 복원
            if (data.IsMonologueDimShowing)
            {
                StageModule.Instance?.MonologueDim?.ShowImmediate();
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
            if (!string.IsNullOrEmpty(data.CurrentBGM) && AudioManager.Instance != null)
            {
                await AudioManager.Instance.PlayBGMAsync(data.CurrentBGM, 0.5f);
            }
        }

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
