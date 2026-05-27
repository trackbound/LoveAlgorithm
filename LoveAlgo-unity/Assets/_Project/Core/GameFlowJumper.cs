using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Common;
using LoveAlgo.Contracts;
using LoveAlgo.Modules.Audio;
using LoveAlgo.Stage;
using LoveAlgo.Story;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 통합 점프 시스템 — 어디서 점프하든 일관된 흐름 보장.
    ///
    /// 흐름: 페이드아웃 → 완전 초기화(UI/무대/오디오) → 세팅(Phase/UI/Stage/Script) → 페이드인.
    ///
    /// 사용:
    ///   GameFlowJumper.JumpToScriptByLineIdAsync("Prologue", "DBG_NIGHT")    // 디버그 패널
    ///   GameFlowJumper.JumpToScriptAsync("Prologue", 142, GamePhase.Prologue) // Mark/Day 점프
    ///   GameFlowJumper.JumpToMemoryAsync(csv, "Prologue", 142)                // 편집기 in-memory
    ///
    /// 호출자(DebugPanel, ScenarioEditor)는 Phase/UI 스왑 로직을 직접 다루지 않음 — 모두 위임.
    /// </summary>
    public static class GameFlowJumper
    {
        const string Tag = "FlowJump";

        // ══════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════

        /// <summary>지정 스크립트 라인 인덱스로 점프 (디스크 CSV 로드).</summary>
        public static UniTask JumpToScriptAsync(string scriptName, int lineIndex,
            GamePhase targetPhase = GamePhase.Prologue, bool withFade = true)
            => RunJumpAsync(scriptName, csv: null, lineIndex, lineId: null, targetPhase, withFade);

        /// <summary>지정 스크립트 LineID로 점프.</summary>
        public static UniTask JumpToScriptByLineIdAsync(string scriptName, string lineId,
            GamePhase targetPhase = GamePhase.Prologue, bool withFade = true)
            => RunJumpAsync(scriptName, csv: null, lineIndex: -1, lineId, targetPhase, withFade);

        /// <summary>편집기 ▶ 여기로 점프 — 메모리 CSV (디스크 미반영).</summary>
        public static UniTask JumpToMemoryAsync(string csv, string scriptName, int lineIndex,
            GamePhase targetPhase = GamePhase.Prologue, bool withFade = true)
            => RunJumpAsync(scriptName, csv, lineIndex, lineId: null, targetPhase, withFade);

        /// <summary>스크립트명 → 페이즈 추론. "Prologue*"면 Prologue, 그 외 DayLoop.</summary>
        public static GamePhase InferPhaseFromScript(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName)) return GamePhase.Prologue;
            return scriptName.StartsWith("Prologue", StringComparison.OrdinalIgnoreCase)
                ? GamePhase.Prologue
                : GamePhase.DayLoop;
        }

        // ══════════════════════════════════════════════
        //  Core
        // ══════════════════════════════════════════════

        static async UniTask RunJumpAsync(string scriptName, string csv,
            int lineIndex, string lineId, GamePhase targetPhase, bool withFade)
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                StageSyncLog.Warn(Tag, "GameManager.Instance == null");
                return;
            }
            if (gm.CurrentPhase == GamePhase.Transitioning)
            {
                StageSyncLog.Warn(Tag, "Phase=Transitioning — 점프 보류");
                return;
            }
            if (string.IsNullOrEmpty(scriptName))
            {
                StageSyncLog.Warn(Tag, "scriptName 비어있음");
                return;
            }

            StageSyncLog.Info(Tag,
                $"target script={scriptName} line={(lineIndex >= 0 ? lineIndex.ToString() : "?")} " +
                $"lineId={lineId ?? "-"} targetPhase={targetPhase} (from {gm.CurrentPhase})");

            // [B0] ScreenFX 잔재 정리 (Eye/Fade 등) — fade 직전이라 SetClear 안전
            var fx = ScreenFX.Instance;
            fx?.EyeOpenImmediate();
            // SetClear 호출 안 함 — 이미 검정이면 다음 단계 fade-out이 합리적 시작

            // [B1] 전체화면 비디오 즉시 강제 정지 — VideoLayer는 sortingOrder=32000으로
            // ScreenFX fade-out 검정보다 위에 그려지므로, 여기서 먼저 끊지 않으면
            // 다음 단계 fade-out 동안 영상이 계속 보임. PlayAsync await loop도
            // player.isPlaying=false로 곧 탈출되어 호출자 cancel 전파됨.
            StopVideoLayer();

            // [A] 페이드 아웃 — 사용자에겐 검은 화면만
            if (withFade && fx != null && !fx.IsFadeBlack)
            {
                StageSyncLog.Section(Tag, "fade-out 0.25s");
                await fx.FadeOutAsync(0.25f);
            }

            // [B] 완전 초기화
            StageSyncLog.Section(Tag, "tear-down ALL");
            await TearDownEverythingAsync();

            // [C] 세팅
            StageSyncLog.Section(Tag, "setup new context");
            EnsurePlayerName(gm);
            gm.SetCurrentPhase(targetPhase);
            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);

            // CSV 확보 (메모리 or 디스크)
            if (csv == null)
            {
                csv = await StoryAssetLoader.LoadCsvAsync(scriptName);
                if (string.IsNullOrEmpty(csv))
                {
                    StageSyncLog.Warn(Tag, $"CSV 없음: {scriptName}");
                    if (withFade && fx != null) await fx.FadeInAsync(0.35f);
                    return;
                }
            }

            var runner = ScriptRunner.Instance;
            runner.LoadScript(csv, scriptName);
            HookScriptEndForPhase(runner, gm, targetPhase);

            // LineID → 인덱스 변환 (필요 시)
            int targetIdx = lineIndex;
            if (targetIdx < 0 && !string.IsNullOrEmpty(lineId))
            {
                targetIdx = ScriptParser.FindLineIndex(GetLines(runner), lineId);
                if (targetIdx < 0)
                {
                    StageSyncLog.Warn(Tag, $"LineID '{lineId}' 찾지 못함 — line 0부터 시작");
                    targetIdx = 0;
                }
            }

            // 합성·복원 + 실행 시작 (fade는 외부에서 처리하므로 내부 fade=false)
            await runner.JumpWithStateSyncAsync(targetIdx, withFade: false);

            // [D] 페이드 인
            if (withFade && fx != null)
            {
                StageSyncLog.Section(Tag, "fade-in 0.35s");
                await fx.FadeInAsync(0.35f);
            }

            StageSyncLog.Info(Tag, $"complete — phase={gm.CurrentPhase}, line={targetIdx}");
        }

        // ══════════════════════════════════════════════
        //  Internal helpers
        // ══════════════════════════════════════════════

        /// <summary>모든 UI·무대·오디오 흔적 제거.</summary>
        static async UniTask TearDownEverythingAsync()
        {
            // 1. 팝업/모달 정리
            PopupManager.Instance?.CloseAll();

            // 2. LockScreen 강제 닫기 (활성 시)
            var lsModule = UnityEngine.Object.FindAnyObjectByType<LoveAlgo.LockScreen.LockScreenModule>();
            var lsPanel = lsModule?.Panel;
            if (lsPanel != null && lsPanel.gameObject.activeSelf)
            {
                StageSyncLog.Section(Tag, "LockScreen 강제 닫음");
                lsPanel.Close();
            }

            // 2-b. Phone 모듈 강제 닫기 — CloseAll의 Hide는 fade 트윈 진행 중이라 즉시 사라지지 않음.
            //      ForceCloseImmediate는 트윈 무시 + openStack 동기화까지 처리.
            var phoneModule = UnityEngine.Object.FindAnyObjectByType<LoveAlgo.Phone.PhoneModule>();
            if (phoneModule != null && phoneModule.IsOpen)
            {
                StageSyncLog.Section(Tag, "Phone 강제 닫음");
                phoneModule.ForceCloseImmediate();
            }

            // 3. UI 전체 정리 — Title/Schedule/Shop/Username/Ending 등 모든 메인 UI
            UIManager.Instance?.HideAll();

            // 4. DialogueUI 초기화
            var dui = Services.TryGet<INarrative>()?.DialogueUI;
            dui?.Clear();
            dui?.ClearLog();
            dui?.HideImmediate();

            // 5. ScriptRunner 정지 (race 방어 핵심)
            ScriptRunner.Instance?.Stop();
            await UniTask.Yield();  // cancellation 전파

            // 6. 무대 1차 정리
            GameManager.Instance?.CleanupStage();

            // 7. 오디오 명시 정리
            var audio = Services.TryGet<IAudio>();
            audio?.StopBGMImmediate();
            audio?.StopVoice();

            // 8. 2차 yield + 재정리 (in-flight async 잔여)
            await UniTask.Yield();
            StageModule.Instance?.CG?.Clear();
            StageModule.Instance?.SDCutscene?.Clear();
            StageModule.Instance?.VirtualBG?.HideImmediate();
            StageModule.Instance?.MonologueDim?.HideImmediate();

            // 9. 비디오/애니메이션 정리 — 전체 화면 비디오, DOTween 진행 중 트윈 모두 정리
            StopVideoLayer();
            KillAllTweens();

            // 10. 로딩 화면 정리
            LoadingScreen.Instance?.HideImmediate();
            // ScreenFX는 [A]에서 검정이므로 SetClear 호출 안 함 — 페이드인에서 정상화
        }

        /// <summary>전체 화면 비디오 (FX,,Video:...) 즉시 중단 + 표시 제거.</summary>
        static void StopVideoLayer()
        {
            // VideoLayer는 SingletonMonoBehaviour — 인스턴스 없으면 그냥 무시
            var vl = VideoLayer.Instance;
            if (vl != null)
            {
                StageSyncLog.Section(Tag, "VideoLayer.Stop");
                vl.Stop();
            }
        }

        /// <summary>
        /// 진행 중인 모든 DOTween 트윈/시퀀스 강제 종료.
        /// 캐릭터 페이드인, BGM 크로스페이드, ScreenFX, LockScreen 인트로/아웃트로,
        /// Popup 페이드 등 모두 안전하게 중단.
        ///
        /// **호출 시점 중요**: [A] fade-out이 await 완료된 직후, [D] fade-in 시작 전에만.
        ///   - [A] fade-out 트윈은 이미 완료 → KillAll이 영향 없음
        ///   - [C] StageRestorer의 BGM 크로스페이드는 KillAll 후 시작 → 영향 없음
        ///   - [D] fade-in 트윈은 KillAll 이후 새로 시작 → 정상 진행
        /// </summary>
        static void KillAllTweens()
        {
            StageSyncLog.Section(Tag, "DOTween.KillAll");
            DOTween.KillAll();
            // 진행 중 BGM 페이드도 죽으므로 직후 StopBGMImmediate 한 번 더 (이중 안전).
            Services.TryGet<IAudio>()?.StopBGMImmediate();
        }

        static void EnsurePlayerName(GameManager gm)
        {
            if (string.IsNullOrEmpty(gm.PlayerName))
            {
                gm.SetPlayerName("테스트");
                GameState.Instance?.SetPlayerName(gm.PlayerName);
                StageSyncLog.Section(Tag, "PlayerName 폴백 '테스트' 적용");
            }
        }

        static void HookScriptEndForPhase(ScriptRunner runner, GameManager gm, GamePhase phase)
        {
            if (runner == null || gm.Flow == null) return;
            if (phase == GamePhase.Prologue)
            {
                runner.OnScriptEnd -= gm.Flow.OnPrologueEnd;
                runner.OnScriptEnd += gm.Flow.OnPrologueEnd;
            }
            // DayLoop은 Schedule UI가 OnScriptEnd를 자체 처리하므로 hook 안 함
        }

        // ScriptRunner.lines 접근 — public API로 노출되지 않아서 reflection 대신 GetLine 사용
        static System.Collections.Generic.List<ScriptLine> GetLines(ScriptRunner runner)
        {
            // ScriptParser.FindLineIndex가 List<ScriptLine>을 요구하므로 임시 빌드
            var list = new System.Collections.Generic.List<ScriptLine>(runner.LineCount);
            for (int i = 0; i < runner.LineCount; i++)
                list.Add(runner.GetLine(i));
            return list;
        }
    }
}
