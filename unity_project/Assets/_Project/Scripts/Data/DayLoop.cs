namespace LoveAlgo.Core
{
    /// <summary><see cref="DayLoop.AdvanceDay"/> 결과 — 새 일차와 엔딩 진입 여부.</summary>
    public readonly struct DayAdvanceResult
    {
        public readonly int Day;
        public readonly bool EnteredEnding;
        public DayAdvanceResult(int day, bool enteredEnding)
        {
            Day = day;
            EnteredEnding = enteredEnding;
        }
    }

    /// <summary>
    /// 데이루프 진행 공식 (REWRITE_FEATURE_INVENTORY §5: MaxDay 30, ActionsPerDay 2).
    ///
    /// 구 <c>DayLoopController</c>에서 UI/페이드/ScriptRunner/세이브/세션버프 같은 오케스트레이션을
    /// 걷어내고 남는 순수 진행 로직만 재구현한다. 상태는 전부 <see cref="GameStateSO"/>에 두고,
    /// 이 클래스는 상태를 인자로 받는 정적 함수만 제공한다 (Service Locator/싱글톤 없음, ADR-007).
    ///
    /// MaxDay/ActionsPerDay는 <see cref="GameConstants"/>(Data 레이어, GameBalanceSO 로드 + §5 폴백)에서 읽는다.
    /// 그래서 이 모듈은 LoveAlgo.Data asmdef에 둔다(GameTimeline 질의도 동일 asmdef).
    /// </summary>
    public static class DayLoop
    {
        /// <summary>새 게임 시작 — 1일차, 행동 풀충전.</summary>
        public static void BeginRun(GameStateSO gs)
        {
            if (gs == null) return;
            gs.Day = 1;
            gs.RemainingActions = GameConstants.ActionsPerDay;
        }

        /// <summary>자유행동 1회 소모. 남은 행동이 0 이하가 되면(=하루 종료 조건) true.</summary>
        public static bool ConsumeAction(GameStateSO gs)
        {
            if (gs == null) return false;
            if (gs.RemainingActions > 0) gs.RemainingActions--;
            return gs.RemainingActions <= 0;
        }

        /// <summary>
        /// 하루 종료 → 다음 날. 일차 +1, 행동 풀충전.
        /// 새 일차가 MaxDay를 넘으면 엔딩 진입(<see cref="DayAdvanceResult.EnteredEnding"/>=true).
        /// </summary>
        public static DayAdvanceResult AdvanceDay(GameStateSO gs)
        {
            if (gs == null) return new DayAdvanceResult(0, false);
            gs.Day++;
            gs.RemainingActions = GameConstants.ActionsPerDay;
            return new DayAdvanceResult(gs.Day, gs.Day > GameConstants.MaxDay);
        }

        /// <summary>현재 일차가 MaxDay를 넘겨 엔딩 구간에 들어섰는지.</summary>
        public static bool IsEndingReached(GameStateSO gs) =>
            gs != null && gs.Day > GameConstants.MaxDay;

        // ── 타임라인 질의 위임 (GameTimeline은 동일 Data asmdef) ──
        /// <summary>현재 일차가 자유행동일인지.</summary>
        public static bool IsFreeDay(GameStateSO gs) => gs != null && GameTimeline.IsFreeDay(gs.Day);

        /// <summary>현재 일차가 이벤트일(개인/단체)인지.</summary>
        public static bool IsEventDay(GameStateSO gs) => gs != null && GameTimeline.IsEventDay(gs.Day);
    }
}
