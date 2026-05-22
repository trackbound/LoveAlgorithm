using LoveAlgo.Contracts;
using System.Threading;
using UnityEngine;
using LoveAlgo.Modules.Affinity;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using LoveAlgo.Modules.Stats;
using LoveAlgo.Story;
using LoveAlgo.UI;
using LoveAlgo.Schedule;
using LoveAlgo.Core;

namespace LoveAlgo.Core
{
    /// <summary>
    /// Day 루프 전담 컨트롤러
    /// 스케줄 선택, 행동 소모, 하루 종료, 데이 이벤트 처리
    /// </summary>
    public class DayLoopController
    {
        readonly GameManager _gm;

        /// <summary>비동기 전환 진행 중 재진입 방지 플래그</summary>
        bool _isTransitioning;

        /// <summary>인라인 스케줄 완료 시그널 (Flow,,Schedule,await 용)</summary>
        UniTaskCompletionSource _inlineScheduleTcs;

        public DayLoopController(GameManager gm)
        {
            _gm = gm;
        }

        /// <summary>
        /// 스케줄 선택 완료 -> 스탯 적용 -> 행동 소모
        /// </summary>
        public void OnScheduleSelected(ScheduleType type)
        {
            var effect = ScheduleTable.Get(type);
            var gs = GameState.Instance;

            if (gs != null)
            {
                if (type == ScheduleType.Invest)
                {
                    int currentMoney = gs.Money;
                    float multiplier = UnityEngine.Random.Range(-0.5f, 1.0f);
                    int change = Mathf.RoundToInt(currentMoney * multiplier);
                    gs.AddMoney(change);

                    string resultText = change >= 0
                        ? $"{MoneyFormat.SignedCurrency(change)} (수익!)"
                        : $"{MoneyFormat.SignedCurrency(change)} (손실...)";
                    PopupManager.Instance?.Toast("투자 결과", resultText, 3f);
                    Debug.Log($"[GameManager] 투자: {MoneyFormat.Currency(currentMoney)} x {multiplier:P0} = {MoneyFormat.SignedCurrency(change)}");
                }
                else
                {
                    var stats = Services.TryGet<IStats>();
                    AddStat(stats, gs, "Str", effect.strengthChange);
                    AddStat(stats, gs, "Int", effect.intelligenceChange);
                    AddStat(stats, gs, "Soc", effect.socialChange);
                    AddStat(stats, gs, "Per", effect.perseveranceChange);
                    AddStat(stats, gs, "Fatigue", effect.fatigueChange);
                    gs.AddMoney(effect.moneyChange);

                    var (buffStat, buffBonus, subStat, subValue) = Shop.ItemEffectSystem.ConsumeSessionBuff();
                    if (buffStat != null && buffBonus > 0)
                    {
                        AddStat(stats, gs, buffStat, buffBonus);
                        Debug.Log($"[GameManager] 세션 버프 적용: {buffStat} +{buffBonus}");
                    }
                    // 보조 효과 (무릎담요: 피로-2, 노트북 거치대: 지성+1 등)
                    if (subStat != null && subValue != 0)
                    {
                        AddStat(stats, gs, subStat, subValue);
                        Debug.Log($"[GameManager] 세션 보조 버프 적용: {subStat} {subValue:+#;-#;0}");
                    }

                    var feedbackItems = BuildScheduleFeedbackList(effect);
                    if (buffStat != null && buffBonus > 0)
                    {
                        string buffText = $"버프: {buffStat} +{buffBonus}";
                        if (subStat != null && subValue != 0)
                            buffText += $", {subStat} {subValue:+#;-#;0}";
                        feedbackItems.Add($"<color=#FFD700>{buffText}</color>");
                    }
                    PopupManager.Instance?.ToastSequence(effect.displayName, feedbackItems, 0.8f);
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
            var parts = BuildScheduleFeedbackList(effect);
            return parts.Count > 0 ? string.Join("\n", parts) : "변화 없음";
        }

        /// <summary>
        /// 스케줄 효과를 개별 항목 리스트로 반환 (순차 토스트용)
        /// </summary>
        System.Collections.Generic.List<string> BuildScheduleFeedbackList(ScheduleEffect effect)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (effect.strengthChange != 0) parts.Add($"체력 {FormatChange(effect.strengthChange)}");
            if (effect.intelligenceChange != 0) parts.Add($"지성 {FormatChange(effect.intelligenceChange)}");
            if (effect.socialChange != 0) parts.Add($"사교 {FormatChange(effect.socialChange)}");
            if (effect.perseveranceChange != 0) parts.Add($"끈기 {FormatChange(effect.perseveranceChange)}");
            if (effect.fatigueChange != 0) parts.Add($"피로 {FormatChange(effect.fatigueChange)}");
            if (effect.moneyChange != 0) parts.Add($"금액 {MoneyFormat.SignedCurrency(effect.moneyChange)}");
            return parts;
        }

        string FormatChange(int value) => value > 0 ? $"+{value}" : value.ToString();

        /// <summary>
        /// IStats 모듈 경유로 스탯 증감. 모듈 미등록 시 GameState 직접 변경 폴백.
        /// 모듈 경유 시 StatChangedEvent 발행 → UI 등 다른 모듈 반응 가능.
        /// </summary>
        static void AddStat(IStats stats, GameState gs, string statId, int delta)
        {
            if (delta == 0) return;
            if (stats != null) stats.Add(statId, delta);
            else if (gs != null) gs.AddStat(statId, delta);
        }

        /// <summary>인라인 스케줄 진행 중인지 (Flow,,Schedule,await)</summary>
        public bool IsInlineSchedule => _inlineScheduleTcs != null;

        /// <summary>
        /// 스케줄 수행 완료 (행동 소모 처리)
        /// </summary>
        public void OnScheduleCompleted()
        {
            _gm.RemainingActions--;

            // 인라인 모드: 결과 토스트만 표시, 뒤로가기로 스토리 복귀
            if (_inlineScheduleTcs != null)
                return;

            if (_gm.ShouldEndDemoAfterSchedule())
            {
                _gm.MarkDemoScheduleComplete();
            }

            if (_gm.RemainingActions <= 0)
            {
                // 데모 모드: 행동 리셋하여 스케줄 테스트 지속 (뒤로가기로만 종료)
                if (_gm.ShouldReturnToDemoEnd())
                {
                    _gm.RemainingActions = GameConstants.ActionsPerDay;
                    return;
                }

                EndDay();
            }
            // remaining > 0: 스케줄 UI가 이미 열려있으므로 재표시 불필요
        }

        /// <summary>
        /// 인라인 스케줄 완료 시그널 (퀵메뉴 뒤로가기에서 호출)
        /// </summary>
        public void CompleteInlineSchedule()
        {
            if (_inlineScheduleTcs == null) return;
            var tcs = _inlineScheduleTcs;
            _inlineScheduleTcs = null;
            tcs.TrySetResult();
        }

        /// <summary>
        /// 인라인 스케줄 — 뒤로가기까지 대기 (Flow,,Schedule,await 용)
        /// </summary>
        public async UniTask WaitForInlineScheduleAsync(CancellationToken ct)
        {
            _inlineScheduleTcs = new UniTaskCompletionSource();

            var scheduleUI = UIManager.Instance?.ScheduleUI;
            scheduleUI?.ShowAsync(OnScheduleSelected).Forget();

            using (ct.Register(() => _inlineScheduleTcs?.TrySetCanceled()))
            {
                await _inlineScheduleTcs.Task;
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
            if (_isTransitioning)
            {
                Debug.LogWarning("[GameManager] EndDayAsync 중복 호출 무시");
                return;
            }
            _isTransitioning = true;
            try
            {
                var ct = _gm.GetCancellationTokenOnDestroy();

                var eveningEvent = DayEventTable.GetEvent(_gm.CurrentDay, DayTiming.Evening);
                if (eveningEvent != null)
                {
                    await RunDayEventInline(eveningEvent, ct);
                }

                var fx = ScreenFX.Instance;

                // FadeOut → Loading 표시 → FadeIn (0.6 / 0.4)
                if (fx != null)
                    await fx.EnterLoadingAsync(0.6f, 0.4f, ct);

                _gm.CurrentDay++;
                _gm.RemainingActions = GameConstants.ActionsPerDay;
                UIManager.Instance?.ScheduleUI?.ResetDailyLimits();
                Debug.Log($"[GameManager] {_gm.CurrentDay}일차 시작");

                if (_gm.CurrentDay > GameConstants.MaxDay)
                {
                    // Ending 진입 — 마지막 FadeIn은 ending 흐름이 책임
                    if (fx != null) await fx.ExitLoadingAsync(0.5f, fadeInDuration: 0f, ct);
                    _gm.ChangePhase(GamePhase.Ending);
                    return;
                }

                await _gm.AutoSaveAsync("day-end");

                await UniTask.Delay(700, cancellationToken: ct);

                // FadeOut → Loading 제거 (Phase 전환 사이에) → 마지막 FadeIn은 ChangePhase 이후로 분리
                if (fx != null) await fx.ExitLoadingAsync(0.5f, fadeInDuration: 0f, ct);
                _gm.ChangePhase(GamePhase.DayLoop);
                await UniTask.Yield(ct);

                // 로딩 화면 후 부드러운 등장 (배경/UI 모두 3초 페이드)
                if (fx != null)
                    await fx.FadeInAsync(3.0f, ct);
            }
            finally
            {
                _isTransitioning = false;
            }
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
        /// 엔딩 히로인 결정 - AffinityCalculator에 위임
        /// </summary>
        public string DetermineEndingHeroine() => AffinityCalculator.DetermineEndingHeroine();

        /// <summary>
        /// 해피/새드 엔딩 분기 판정 - AffinityCalculator에 위임
        /// </summary>
        public bool IsHappyEnding(string heroineId) => AffinityCalculator.IsHappyEnding(heroineId);
    }
}
