using System.Threading;
using UnityEngine;
using LoveAlgo.Modules.Audio;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;
using LoveAlgo.UI;
using LoveAlgo.Schedule;
using LoveAlgo.Core;
using LoveAlgo.Stage;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 게임 Phase 전환 전담 컨트롤러
    /// Title → Username → Prologue → DayLoop → Ending 흐름 관리
    /// </summary>
    public class GameFlowController
    {
        readonly GameManager _gm;

        /// <summary>비동기 전환 진행 중 재진입 방지 플래그</summary>
        bool _isTransitioning;

        public GameFlowController(GameManager gm)
        {
            _gm = gm;
        }

        #region Phase Entry

        /// <summary>
        /// 게임 상태 전환
        /// </summary>
        public void ChangePhase(GamePhase newPhase)
        {
            if (_gm.CurrentPhase == newPhase && newPhase != GamePhase.DayLoop && newPhase != GamePhase.Title)
            {
                Debug.LogWarning($"[GameManager] ChangePhase 중복 호출 무시: {newPhase}");
                return;
            }

            var prevPhase = _gm.CurrentPhase;
            _gm.SetCurrentPhase(newPhase);

            Debug.Log($"[GameManager] Phase: {prevPhase} → {newPhase}");

            // 타이틀에서 나갈 때 BGM 즉시 중단 (이후 CSV가 새 BGM 담당)
            if (prevPhase == GamePhase.Title && newPhase != GamePhase.Title)
            {
                AudioManager.Instance?.StopBGMImmediate();
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

        void EnterTitle()
        {
            UIManager.Instance?.ShowOnly(MainUIType.Title);
            UIManager.Instance?.TitlePanel?.PlayTitleBGM();
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
            }
        }

        void EnterDayLoop()
        {
            if (_gm.ShouldReturnToDemoEnd())
            {
                OnContentEnd();
                return;
            }

            var dayInfo = GameTimeline.GetDayInfo(_gm.CurrentDay);

            // ── 고백 이벤트 (Day 30) ──
            if (dayInfo?.Type == DayType.Confession)
            {
                ChangePhase(GamePhase.Ending);
                return;
            }

            // 이벤트 날 / 아침 컷씬 / 메신저 메시지는 데모에서 스킵 → 바로 스케줄 UI
            ShowScheduleUI();
        }

        /// <summary>
        /// 스케줄 UI 표시
        /// </summary>
        void ShowScheduleUI()
        {
            // 배경이 없으면 기본 배경 설정 (디버그 점프 등으로 배경이 클리어된 경우)
            var bg = StageModule.Instance?.Background;
            if (bg != null && string.IsNullOrEmpty(bg.CurrentBackground))
            {
                bg.ChangeBackgroundAsync("BG_Title", Story.BGTransition.Cut, 0f).Forget();
            }

            UIManager.Instance?.ShowOnly(MainUIType.Schedule);
            var scheduleUI = UIManager.Instance?.ScheduleUI;
            scheduleUI?.ShowAsync(_gm.OnScheduleSelected).Forget();
        }

        void EnterEnding()
        {
            // 회차 카운터 증가 (머신-와이드, PlayerPrefs). CSV에서 If:EndingCount>=N으로 분기 가능.
            GameState.IncrementEndingCount();

            string endingHeroine = _gm.DetermineEndingHeroine();
            string scriptName;

            if (string.IsNullOrEmpty(endingHeroine))
            {
                scriptName = "Ending_Normal";
            }
            else
            {
                // 해피/새드 분기: 포인트가 임계치 이상이면 해피
                string suffix = _gm.IsHappyEnding(endingHeroine) ? "Happy" : "Sad";
                scriptName = $"Ending_{endingHeroine}_{suffix}";
            }

            Debug.Log($"[GameManager] Ending started: {scriptName}");

            // 대화 UI로 엔딩 스크립트 재생
            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
            var dialogueUI = UIManager.Instance?.DialogueUI;
            dialogueUI?.Clear();
            dialogueUI?.HideImmediate();

            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                runner.OnScriptEnd -= OnEndingEnd;
                runner.OnScriptEnd += OnEndingEnd;
                runner.StartScript(scriptName).Forget();
            }
        }

        /// <summary>
        /// 프롤로그 종료 → DayLoop 진입
        /// </summary>
        public void OnPrologueEnd()
        {
            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                runner.OnScriptEnd -= OnPrologueEnd;
            }

            EnterDayLoopSimpleAsync().Forget();
        }

        /// <summary>
        /// 엔딩 스크립트 종료 후 타이틀 복귀
        /// </summary>
        void OnEndingEnd()
        {
            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                runner.OnScriptEnd -= OnEndingEnd;
            }

            OnContentEnd();
        }

        #endregion

        #region Async Transitions

        /// <summary>
        /// 타이틀 화면으로 이동 (페이드 전환 포함)
        /// </summary>
        public void GoToTitle()
        {
            GoToTitleAsync().Forget();
        }

        async UniTaskVoid GoToTitleAsync()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            try
            {
                var ct = _gm.GetCancellationTokenOnDestroy();
                var fx = ScreenFX.Instance;

                // 페이드 아웃
                if (fx != null && !fx.IsFadeBlack)
                    await fx.FadeOutAsync(0.5f, ct);

                ScriptRunner.Instance?.Stop();

                // 장면 정리 (캐릭터, 배경, 오버레이 등)
                _gm.CleanupStage();

                ChangePhase(GamePhase.Title);

                await UniTask.Yield(ct);

                // 페이드 인 (타이틀 표시)
                if (fx != null)
                    await fx.FadeInAsync(0.5f, ct);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 이름 입력 완료 (UsernameUI에서 호출)
        /// </summary>
        public void OnNameConfirmed(string playerName)
        {
            _gm.SetPlayerName(playerName);

            // GameState에도 동기화 (DialogueUI 변수 치환용)
            if (GameState.Instance != null)
            {
                GameState.Instance.SetPlayerName(playerName);
            }

            Debug.Log($"[GameManager] 플레이어 이름: {playerName}");
            TransitionToPrologueAsync().Forget();
        }

        /// <summary>
        /// 새 게임: 이름 입력 없이 곳장 프롬로그로 진입 (로딩 화면 + 페이드 포함).
        /// 이름은 프롬로그 CSV의 `Flow,,Username,>` 시점에 따로 받는다.
        /// </summary>
        public void StartPrologueFromNewGame()
        {
            TransitionToPrologueAsync().Forget();
        }

        /// <summary>
        /// Username → Prologue 전환 (로딩 화면 + 페이드)
        /// </summary>
        async UniTaskVoid TransitionToPrologueAsync()
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[GameManager] TransitionToPrologueAsync 중복 호출 무시");
                return;
            }
            _isTransitioning = true;
            try
            {
                var ct = _gm.GetCancellationTokenOnDestroy();
                var fx = ScreenFX.Instance;
                var loading = LoadingScreen.Instance;

                // 1) 페이드 아웃
                if (fx != null)
                    await fx.FadeOutAsync(0.6f, ct);
                else
                    await UniTask.Yield(ct);

                // 2) 로딩 화면 표시 (암전 위에)
                if (loading != null)
                    await loading.ShowAsync(ct);

                // 3) 페이드 해제 (로딩 화면이 부드럽게 드러남)
                if (fx != null)
                    await fx.FadeInAsync(0.5f, ct);

                // 4) UI 전환 + 프롤로그 UI 준비 (로딩 화면 뒤에서)
                ChangePhase(GamePhase.Prologue);

                // 5) 로딩 화면 표시 유지
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.8f), cancellationToken: ct);

                // 6) 페이드 아웃 (로딩 화면 위에 암전)
                if (fx != null)
                    await fx.FadeOutAsync(0.5f, ct);

                // 7) 로딩 화면 제거 (암전 상태라 안 보임)
                loading?.HideImmediate();

                // 8) 인게임 페이드 인 (로딩 후 부드러운 등장)
                if (fx != null)
                    await fx.FadeInAsync(2.0f, ct);

                // 10) 프롤로그 스크립트 실행 (전환 완료 후 시작)
                ScriptRunner.Instance?.StartScript(_gm.PrologueScript).Forget();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 프롤로그 → DayLoop 단순 전환 (데모용: 로딩 화면 없이 페이드만)
        /// </summary>
        async UniTaskVoid EnterDayLoopSimpleAsync()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;
            try
            {
                var ct = _gm.GetCancellationTokenOnDestroy();
                var fx = ScreenFX.Instance;

                if (fx != null)
                    await fx.FadeOutAsync(0.5f, ct);

                _gm.CleanupStage();

                // 데모 모드: 프롤로그 후 스케줄 없이 바로 종료 안내
                if (_gm.IsDemoMode)
                {
                    _gm.SetCurrentPhase(GamePhase.DayLoop);
                    await _gm.AutoSaveAsync();
                    _isTransitioning = false;
                    OnContentEnd();
                    return;
                }

                ChangePhase(GamePhase.DayLoop);
                await _gm.AutoSaveAsync();

                await UniTask.Yield(ct);

                if (fx != null)
                    await fx.FadeInAsync(0.5f, ct);
            }
            finally
            {
                _isTransitioning = false;
            }
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
            var ct = _gm.GetCancellationTokenOnDestroy();

            // 대화 UI 숨기기 (마지막 대사가 남아있을 수 있음)
            var dialogueUI = UIManager.Instance?.DialogueUI;
            dialogueUI?.HideImmediate();

            // 자동저장 (화면이 보이는 상태에서 스크린샷 캡처)
            await _gm.AutoSaveAsync();

            // 데모 종료 안내 (페이드 전 — ScreenFX가 PopupManager 위 레이어라 페이드 후엔 안 보임)
            if (UI.PopupManager.Instance != null)
                await UI.PopupManager.Instance.AlertAsync("데모 버전 플레이가 종료되었습니다.\n타이틀로 이동합니다.");
            else
                await UniTask.Delay(3000, cancellationToken: ct);

            // 페이드 아웃 → 타이틀 복귀
            if (ScreenFX.Instance != null)
                await ScreenFX.Instance.FadeOutAsync(0.5f, ct);

            GoToTitle();
        }

        #endregion
    }
}
