using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;
using LoveAlgo.UI;
using LoveAlgo.Schedule;

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
                    gs.AddStat("Str", effect.strengthChange);
                    gs.AddStat("Int", effect.intelligenceChange);
                    gs.AddStat("Soc", effect.socialChange);
                    gs.AddStat("Per", effect.perseveranceChange);
                    gs.AddStat("Fatigue", effect.fatigueChange);
                    gs.AddMoney(effect.moneyChange);

                    var (buffStat, buffBonus) = Shop.ItemEffectSystem.ConsumeSessionBuff();
                    if (buffStat != null && buffBonus > 0)
                    {
                        gs.AddStat(buffStat, buffBonus);
                        Debug.Log($"[GameManager] 세션 버프 적용: {buffStat} +{buffBonus}");
                    }

                    string feedback = BuildScheduleFeedback(effect);
                    if (buffStat != null && buffBonus > 0)
                        feedback += $"\n<color=#FFD700>버프: {buffStat} +{buffBonus}</color>";
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
            if (effect.moneyChange != 0) parts.Add($"금액 {MoneyFormat.SignedCurrency(effect.moneyChange)}");
            return parts.Count > 0 ? string.Join(" / ", parts) : "변화 없음";
        }

        string FormatChange(int value) => value > 0 ? $"+{value}" : value.ToString();

        /// <summary>
        /// 스케줄 수행 완료 (행동 소모 처리)
        /// </summary>
        public void OnScheduleCompleted()
        {
            _gm.RemainingActions--;

            if (_gm.ShouldEndDemoAfterSchedule())
            {
                _gm.MarkDemoScheduleComplete();
                _gm.OnContentEnd();
                return;
            }

            if (_gm.RemainingActions <= 0)
            {
                EndDay();
            }
            else
            {
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
                var loading = LoadingScreen.Instance;

                if (fx != null)
                    await fx.FadeOutAsync(0.8f, ct);

                if (loading != null)
                    await loading.ShowAsync(ct);

                if (fx != null)
                    await fx.FadeInAsync(0.5f, ct);

                _gm.CurrentDay++;
                _gm.RemainingActions = GameConstants.ActionsPerDay;
                UIManager.Instance?.ScheduleUI?.ResetDailyLimits();
                Debug.Log($"[GameManager] {_gm.CurrentDay}일차 시작");

                if (_gm.CurrentDay > GameConstants.MaxDay)
                {
                    if (fx != null) await fx.FadeOutAsync(0.5f, ct);
                    loading?.HideImmediate();
                    _gm.ChangePhase(GamePhase.Ending);
                    return;
                }

                _gm.AutoSave();

                await UniTask.Delay(1200, cancellationToken: ct);

                if (fx != null)
                    await fx.FadeOutAsync(0.5f, ct);

                loading?.HideImmediate();
                _gm.ChangePhase(GamePhase.DayLoop);

                await UniTask.Yield(ct);

                if (fx != null)
                    await fx.FadeInAsync(0.8f, ct);
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
