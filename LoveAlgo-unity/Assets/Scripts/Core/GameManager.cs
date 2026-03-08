using System.Threading;
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
    public class GameManager : SingletonMonoBehaviour<GameManager>
    {

        [Header("Settings")]
        [SerializeField] string prologueScript = "Prologue";  // 프롤로그 CSV 이름

        [Header("DOTween Settings")]
        [SerializeField] int tweenersCapacity = 500;   // 동시 트위너 수
        [SerializeField] int sequencesCapacity = 125;  // 동시 시퀀스 수

        // 현재 상태
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Title;
        public int CurrentDay { get; private set; } = 1;
        public int RemainingActions { get; private set; } = GameConstants.ActionsPerDay;  // 하루 남은 행동 수 (기획서: 2회 - 낮/밤)
        public string PlayerName { get; private set; } = "";

        /// <summary>
        /// 다음 날로 진행 (스크립트 매크로용)
        /// </summary>
        public void AdvanceDay(int actions = GameConstants.ActionsPerDay)
        {
            CurrentDay++;
            RemainingActions = actions;
            Debug.Log($"[GameManager] {CurrentDay}일차 시작 (actions={actions})");
        }

        protected override void OnSingletonAwake()
        {
            // ── 빌드 시 Debug.Log 비활성화 (Warning/Error는 유지) ──
#if !UNITY_EDITOR
            Debug.unityLogger.filterLogType = LogType.Warning;
#endif

            // DOTween 초기화 - 용량 설정으로 IndexOutOfRangeException 방지
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(tweenersCapacity, sequencesCapacity);
            Debug.Log($"[GameManager] DOTween initialized: Tweeners={tweenersCapacity}, Sequences={sequencesCapacity}");

            // 저장된 해상도/전체화면 설정 복원
            RestoreResolution();
        }

        void Start()
        {
            ChangePhase(GamePhase.Title);
        }

        /// <summary>
        /// PlayerPrefs에서 해상도/전체화면 설정 복원
        /// </summary>
        void RestoreResolution()
        {
            int resIdx = PlayerPrefs.GetInt("ResolutionIndex", -1);
            if (resIdx < 0) return;  // 저장된 설정 없으면 Unity 기본값 사용

            bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            var resolutions = GameConstants.Resolutions;
            resIdx = Mathf.Clamp(resIdx, 0, resolutions.Length - 1);
            var res = resolutions[resIdx];
            Screen.SetResolution(res.w, res.h, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            Debug.Log($"[GameManager] 해상도 복원: {res.w}x{res.h}, 전체화면: {fullscreen}");
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
            UIManager.Instance?.TitleUI?.PlayTitleBGM();
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
            var dayInfo = GameTimeline.GetDayInfo(CurrentDay);

            // ── 메신저 일차별 메시지 발송 ──
            Phone.MessengerManager.TriggerDayMessages(CurrentDay);

            // ── 고백 이벤트 (Day 30) ──
            if (dayInfo?.Type == DayType.Confession)
            {
                ChangePhase(GamePhase.Ending);
                return;
            }

            // ── 이벤트 날 (자유행동 없음) ──
            if (dayInfo != null && (dayInfo.Type == DayType.PersonalEvent || dayInfo.Type == DayType.GroupEvent))
            {
                EnterEventDay(dayInfo);
                return;
            }

            // ── 자유행동 날 ──
            // 아침 이벤트 체크 (DayEventTable — 일차별 컷씬)
            var morningEvent = DayEventTable.GetEvent(CurrentDay, DayTiming.Morning);
            if (morningEvent != null)
            {
                RunDayEventAsync(morningEvent, showScheduleAfter: true).Forget();
                return;
            }

            // 이벤트 없으면 스케줄 UI 직접 표시
            ShowScheduleUI();
        }

        /// <summary>
        /// 이벤트 날 진입 (개인/단체 이벤트)
        /// CSV 스크립트를 실행하고, 스크립트 내에서 히로인 선택 처리
        /// </summary>
        void EnterEventDay(DayInfo dayInfo)
        {
            Debug.Log($"[GameManager] 이벤트 날 진입: Day {dayInfo.Day} - {dayInfo.EventTag} ({dayInfo.Type})");

            // 이벤트 스크립트 실행
            string scriptName = dayInfo.EventTag;
            if (string.IsNullOrEmpty(scriptName))
            {
                Debug.LogWarning($"[GameManager] Day {dayInfo.Day}: 이벤트 스크립트 미지정, 다음 날로 넘김");
                EndDay();
                return;
            }

            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
            var dialogueUI = UIManager.Instance?.DialogueUI;
            dialogueUI?.Clear();
            dialogueUI?.HideImmediate();

            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                runner.OnScriptEnd -= OnEventDayEnd;
                runner.OnScriptEnd += OnEventDayEnd;
                runner.StartScript(scriptName).Forget();
            }
        }

        /// <summary>
        /// 이벤트 날 스크립트 종료 후 → 하루 종료
        /// </summary>
        void OnEventDayEnd()
        {
            var runner = ScriptRunner.Instance;
            if (runner != null)
                runner.OnScriptEnd -= OnEventDayEnd;

            EndDay();
        }

        /// <summary>
        /// 스케줄 UI 표시
        /// </summary>
        void ShowScheduleUI()
        {
            UIManager.Instance?.ShowOnly(MainUIType.Schedule);
            var scheduleUI = UIManager.Instance?.ScheduleUI;
            scheduleUI?.ShowAsync(OnScheduleSelected).Forget();
        }

        /// <summary>
        /// 데이 이벤트 CSV 실행
        /// </summary>
        async UniTaskVoid RunDayEventAsync(DayEvent evt, bool showScheduleAfter)
        {
            Debug.Log($"[GameManager] 데이 이벤트 실행: {evt.ScriptName}");
            DayEventTable.MarkFired(evt.ScriptName);

            // 대화 UI로 스크립트 재생
            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
            var dialogueUI = UIManager.Instance?.DialogueUI;
            dialogueUI?.Clear();
            dialogueUI?.HideImmediate();

            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                await runner.StartScript(evt.ScriptName);
            }

            // 이벤트 종료 후 처리
            if (showScheduleAfter)
            {
                ShowScheduleUI();
            }
        }

        void EnterEnding()
        {
            string endingHeroine = DetermineEndingHeroine();
            string scriptName;

            if (string.IsNullOrEmpty(endingHeroine))
            {
                scriptName = "Ending_Normal";
            }
            else
            {
                // 해피/새드 분기: 포인트가 임계치 이상이면 해피
                string suffix = IsHappyEnding(endingHeroine) ? "Happy" : "Sad";
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
        /// 엔딩 히로인 결정 (기획서 기준)
        /// 포인트 = 이벤트 + 대화 + 선물 + 미니게임 + 스탯보정 + 피로보정(로아)
        /// 총 포인트 ≥ 히로인별 임계치 → 해피엔딩, 미달 → 새드엔딩
        /// 
        /// 조건: 해당 히로인 이벤트 최소 1회 이상 선택 필수
        /// 로아: 모든 이벤트에서 로아만 선택 + 피로 ≥70
        /// </summary>
        string DetermineEndingHeroine()
        {
            var gs = GameState.Instance;
            if (gs == null) return null;

            // 히든 루트: 로아 (피로 ≥70 + 포인트 ≥ 46)
            int roaPoints = HeroinePointTracker.GetTotalPoint("Roa") + GetRoaFatigueBonus(gs);
            if (gs.GetStat("Fatigue") >= 70 && roaPoints >= GameConstants.EndingThresholds[0])
                return "Roa";

            // 나머지 히로인 (Yeun=1, Daeun=2, Bom=3, Heewon=4)
            string best = null;
            int bestMargin = -1;

            for (int i = 1; i < GameConstants.HeroineCount; i++)
            {
                string id = GameConstants.HeroineIds[i];

                // 기획서: 최소 1회 이상 이벤트 참여 필수
                if (HeroinePointTracker.GetEventSelectionCount(id) < 1)
                    continue;

                int total = HeroinePointTracker.GetTotalPoint(id) + CalcStatBonus(gs, i);
                int threshold = GameConstants.EndingThresholds[i];

                if (total >= threshold)
                {
                    int margin = total - threshold;
                    if (margin > bestMargin)
                    {
                        bestMargin = margin;
                        best = id;
                    }
                }
            }

            return best; // null → 노멀 엔딩
        }

        /// <summary>
        /// 해피/새드 엔딩 분기 판정
        /// best 히로인이 있으면 해피, 임계치 미달이면 새드
        /// </summary>
        bool IsHappyEnding(string heroineId)
        {
            if (string.IsNullOrEmpty(heroineId)) return false;

            int idx = System.Array.IndexOf(GameConstants.HeroineIds, heroineId);
            if (idx < 0) return false;

            var gs = GameState.Instance;
            int total = HeroinePointTracker.GetTotalPoint(heroineId);
            if (heroineId == "Roa")
                total += GetRoaFatigueBonus(gs);
            else
                total += CalcStatBonus(gs, idx);

            return total >= GameConstants.EndingThresholds[idx];
        }

        /// <summary>
        /// 스탯 보정 계산 (로아 제외)
        /// 선호스탯이 최고스탯이면 +3, 공동1등이면 +1
        /// </summary>
        int CalcStatBonus(GameState gs, int heroineIndex)
        {
            string preferredStat = GameConstants.HeroinePreferredStat[heroineIndex];
            int preferredValue = gs.GetStat(preferredStat);
            if (preferredValue <= 0) return 0;

            // 모든 스탯 중 최고값 찾기 (피로 제외)
            int maxValue = 0;
            int maxCount = 0;
            foreach (var statId in new[] { "Str", "Int", "Soc", "Per" })
            {
                int val = gs.GetStat(statId);
                if (val > maxValue)
                {
                    maxValue = val;
                    maxCount = 1;
                }
                else if (val == maxValue && val > 0)
                {
                    maxCount++;
                }
            }

            if (preferredValue < maxValue) return 0;   // 2등 이하
            if (maxCount == 1) return 3;                // 단독 1등
            return 1;                                    // 공동 1등
        }

        /// <summary>
        /// 로아 피로 보정 (기획서: 70~79:+3 / 80~89:+6 / 90~100:+10)
        /// </summary>
        int GetRoaFatigueBonus(GameState gs)
        {
            int fatigue = gs.GetStat("Fatigue");
            if (fatigue >= 90) return 10;
            if (fatigue >= 80) return 6;
            if (fatigue >= 70) return 3;
            return 0;
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
            DayEventTable.ResetFired();
            HeroinePointTracker.Reset();
            Phone.MessengerManager.Reset();

            PlayerName = "";
            CurrentDay = 1;
            RemainingActions = GameConstants.ActionsPerDay;
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
            TransitionToPrologueAsync().Forget();
        }

        /// <summary>
        /// Username → Prologue 전환 (로딩 화면 + 페이드)
        /// </summary>
        async UniTaskVoid TransitionToPrologueAsync()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            var fx = ScreenFX.Instance;
            var loading = LoadingScreen.Instance;

            // 1) 여유 있게 페이드 아웃
            if (fx != null)
                await fx.FadeOutAsync(0.8f, ct);
            else
                await UniTask.Yield(ct);

            // 2) 암전 상태에서 잠시 머무름 (호흡)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.4f), cancellationToken: ct);

            // 3) 로딩 화면 표시 (암전 위에)
            if (loading != null)
                await loading.ShowAsync(ct);

            // 4) 페이드 해제 (로딩 화면이 부드럽게 드러남)
            if (fx != null)
                await fx.FadeInAsync(0.6f, ct);

            // 5) UI 전환 + 프롤로그 준비 (로딩 화면 뒤에서)
            ChangePhase(GamePhase.Prologue);

            // 6) 로딩 화면 충분히 보여줌 + 프롤로그 초기화 대기
            await UniTask.Delay(System.TimeSpan.FromSeconds(1.5f), cancellationToken: ct);

            // 7) 부드럽게 페이드 아웃 (로딩 화면 위에 암전)
            if (fx != null)
                await fx.FadeOutAsync(0.6f, ct);

            // 8) 프롤로그 첫 BG 세팅 완료 대기 (빈 화면 방지)
            await UniTask.DelayFrame(3, cancellationToken: ct);

            // 9) 로딩 화면 제거
            loading?.HideImmediate();

            // 9) 인게임 페이드 인
            if (fx != null)
                await fx.FadeInAsync(0.8f, ct);
        }

        /// <summary>
        /// 프롤로그 종료 (ScriptRunner OnScriptEnd에서 호출)
        /// 현재 컨텐츠 종료 지점이므로 저장 팝업 후 타이틀 복귀
        /// </summary>
        void OnPrologueEnd()
        {
            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                runner.OnScriptEnd -= OnPrologueEnd;
            }

            // 페이드아웃 → 저장 팝업 → 타이틀 복귀
            OnContentEnd();
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
            var ct = this.GetCancellationTokenOnDestroy();

            // 페이드 아웃
            if (ScreenFX.Instance != null)
                await ScreenFX.Instance.FadeOutAsync(2f, ct);

            // 자동저장
            AutoSave();

            // 데모 종료 안내 (사용자가 확인 후 진행)
            if (UI.PopupManager.Instance != null)
                await UI.PopupManager.Instance.AlertAsync("데모 버전 플레이가 종료되었습니다.\n자동 저장되었습니다.");
            else
                await UniTask.Delay(3000, cancellationToken: ct);

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
                // 투자: 기획서 기준 ±50~100% 랜덤
                if (type == ScheduleType.Invest)
                {
                    int currentMoney = gs.Money;
                    // -50% ~ +100% 범위의 랜덤 배율
                    float multiplier = UnityEngine.Random.Range(-0.5f, 1.0f);
                    int change = Mathf.RoundToInt(currentMoney * multiplier);
                    gs.AddMoney(change);

                    string resultText = change >= 0
                        ? $"+{change:N0}원 (수익!)"
                        : $"{change:N0}원 (손실...)";
                    PopupManager.Instance?.Toast("투자 결과", resultText, 3f);
                    Debug.Log($"[GameManager] 투자: {currentMoney:N0} × {multiplier:P0} = {change:N0}");
                }
                else
                {
                    gs.AddStat("Str", effect.strengthChange);
                    gs.AddStat("Int", effect.intelligenceChange);
                    gs.AddStat("Soc", effect.socialChange);
                    gs.AddStat("Per", effect.perseveranceChange);
                    gs.AddStat("Fatigue", effect.fatigueChange);
                    gs.AddMoney(effect.moneyChange);

                    // 스탯 변화 피드백 토스트
                    string feedback = BuildScheduleFeedback(effect);
                    PopupManager.Instance?.Toast(effect.displayName, feedback, 2.5f);
                }
            }

            Debug.Log($"[GameManager] 스케줄 완료: {effect.displayName}");
            OnScheduleCompleted();
        }

        /// <summary>
        /// 스케줄 효과 피드백 문자열 생성
        /// </summary>
        string BuildScheduleFeedback(ScheduleEffect effect)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (effect.strengthChange != 0) parts.Add($"체력 {FormatChange(effect.strengthChange)}");
            if (effect.intelligenceChange != 0) parts.Add($"지성 {FormatChange(effect.intelligenceChange)}");
            if (effect.socialChange != 0) parts.Add($"사교 {FormatChange(effect.socialChange)}");
            if (effect.perseveranceChange != 0) parts.Add($"끈기 {FormatChange(effect.perseveranceChange)}");
            if (effect.fatigueChange != 0) parts.Add($"피로 {FormatChange(effect.fatigueChange)}");
            if (effect.moneyChange != 0) parts.Add($"금액 {FormatChange(effect.moneyChange)}");
            return parts.Count > 0 ? string.Join(" / ", parts) : "변화 없음";
        }

        string FormatChange(int value) => value > 0 ? $"+{value}" : value.ToString();

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
            var ct = this.GetCancellationTokenOnDestroy();

            // 저녁 이벤트 체크
            var eveningEvent = DayEventTable.GetEvent(CurrentDay, DayTiming.Evening);
            if (eveningEvent != null)
            {
                await RunDayEventInline(eveningEvent, ct);
            }

            var fx = ScreenFX.Instance;
            var loading = LoadingScreen.Instance;

            // ── 1. 페이드 아웃 ──
            if (fx != null)
                await fx.FadeOutAsync(0.8f, ct);

            // ── 2. 로딩 화면 준비 (암전 뒤에서) ──
            if (loading != null)
                await loading.ShowAsync(ct);

            // ── 3. 페이드 해제 → 로딩 화면 드러남 ──
            if (fx != null)
                await fx.FadeInAsync(0.5f, ct);

            // ── 4. 날짜 처리 + 자동저장 ──
            CurrentDay++;
            RemainingActions = GameConstants.ActionsPerDay;
            UIManager.Instance?.ScheduleUI?.ResetDailyLimits();
            Debug.Log($"[GameManager] {CurrentDay}일차 시작");

            // 최대 일차 초과 시 엔딩 진입
            if (CurrentDay > GameConstants.MaxDay)
            {
                if (fx != null) await fx.FadeOutAsync(0.5f, ct);
                loading?.HideImmediate();
                ChangePhase(GamePhase.Ending);
                return;
            }

            AutoSave();

            // ── 5. 로딩 화면 유지 ──
            await UniTask.Delay(1200, cancellationToken: ct);

            // ── 6. 페이드로 로딩 가림 ──
            if (fx != null)
                await fx.FadeOutAsync(0.5f, ct);

            // ── 7. 로딩 제거 + 다음 Phase 준비 (암전 뒤) ──
            loading?.HideImmediate();
            ChangePhase(GamePhase.DayLoop);

            // ── 8. 1프레임 대기 (UI 레이아웃 정리) ──
            await UniTask.Yield(ct);

            // ── 9. 페이드 인 (새 하루 시작) ──
            if (fx != null)
                await fx.FadeInAsync(0.8f, ct);
        }

        /// <summary>
        /// 데이 이벤트를 EndDay 흐름 내에서 실행 (저녁 이벤트용)
        /// </summary>
        async UniTask RunDayEventInline(DayEvent evt, CancellationToken ct)
        {
            Debug.Log($"[GameManager] 저녁 이벤트 실행: {evt.ScriptName}");
            DayEventTable.MarkFired(evt.ScriptName);

            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
            var dialogueUI = UIManager.Instance?.DialogueUI;
            dialogueUI?.Clear();
            dialogueUI?.HideImmediate();

            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                await runner.StartScript(evt.ScriptName);
            }
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
                // Phase를 먼저 설정해야 입력 핸들러(StoryInputHandler)가 클릭을 처리함
                CurrentPhase = data.Phase;

                UIManager.Instance?.ShowOnly(MainUIType.Dialogue);

                // 대화창을 숨김 상태로 시작 (첨 대사 시점에 자동 표시)
                var dialogueUI = UIManager.Instance?.DialogueUI;
                dialogueUI?.Clear();
                dialogueUI?.ClearLog();  // 이전 세션 로그 제거 (중복 방지)
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
            Save(SaveManager.AutoSaveSlot, usePendingThumbnail: false);
            Debug.Log("[GameManager] 자동저장 완료");
        }

        /// <summary>
        /// 수동저장 (슬롯 1~29)
        /// </summary>
        public void Save(int slot, bool usePendingThumbnail = true)
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
                GetSaveChapterName(scriptName)
            );

            // 스크린샷 저장
            // - 수동 저장 팝업: ShowSave에서 미리 캡처한 pending 썸네일 우선 사용
            // - 자동저장/기타: pending 미사용 또는 부재 시 즉시 캡처
            if (!usePendingThumbnail || !SaveManager.TryCommitPendingScreenshot(slot))
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
            if (CurrentPhase == GamePhase.Prologue)
                return "프롤로그";

            return $"Day {CurrentDay}";
        }

        #endregion

        #region Stage State

        /// <summary>
        /// 장면 정리 (타이틀 복귀 / 로드 시)
        /// </summary>
        void CleanupStage()
        {
            // 레이어 정리
            StageManager.Instance?.Character?.SetVisibleImmediate(true);  // SD 숨김 상태 복원
            StageManager.Instance?.Character?.ClearAll();
            StageManager.Instance?.Background?.Clear();
            StageManager.Instance?.VirtualBG?.HideImmediate();
            StageManager.Instance?.CG?.Clear();
            StageManager.Instance?.SDCutscene?.Clear();
            StageManager.Instance?.MonologueDim?.HideImmediate();

            // 화면 효과 정리
            if (ScreenFX.Instance != null)
            {
                ScreenFX.Instance.SetClear();
                ScreenFX.Instance.EyeOpenImmediate();
            }

            // 로딩 화면 정리
            LoadingScreen.Instance?.HideImmediate();

            // 오디오 정리
            Story.AudioManager.Instance?.StopBGMImmediate();
            Story.AudioManager.Instance?.StopVoice();

            // DOTween 트윈 정리 — KillAll()은 팝업 애니메이션 등 전역 트윈을 파괴할 수 있으므로
            // 완료되지 않은 트윈만 안전하게 정리
            DOTween.KillAll(complete: false);

            // 캐릭터 스프라이트 캐시 정리
            CharacterSlot.ClearSpriteCache();

            // 미사용 에셋 메모리 해제
            Resources.UnloadUnusedAssets();
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

            // SD 컷씬 복원
            if (!string.IsNullOrEmpty(data.CurrentSD))
            {
                var sd = StageManager.Instance?.SDCutscene;
                if (sd != null)
                {
                    // SD 표시 중이면 캐릭터 레이어 즉시 숨김
                    StageManager.Instance?.Character?.SetVisibleImmediate(false);
                    await sd.ShowAsync(data.CurrentSD, 0f);  // 즉시 표시
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
