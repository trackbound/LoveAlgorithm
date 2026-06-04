using System;
using LoveAlgo.Core;     // GameStateSO
using LoveAlgo.Affinity; // AffinityFormula, PointCategory

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>처리된 Flow 명령 종류.</summary>
    public enum FlowCommandKind { Unknown, AffinityEventChoice, AffinityPoint, Day, Flag }

    /// <summary>
    /// <see cref="FlowCommandInterpreter.Apply"/> 결과. 무엇이 어떻게 바뀌었는지(또는 실패 사유)를 담아
    /// 호출자(내러티브 엔진, 후속 슬라이스)가 통지 이벤트 발행 여부를 결정한다 — 인터프리터 자체는 EventBus를 모른다.
    /// </summary>
    public readonly struct FlowCommandResult
    {
        public readonly bool Ok;
        public readonly FlowCommandKind Kind;
        public readonly string HeroineId; // Affinity 계열에서만 유효
        public readonly int NewScore;     // Affinity 적용 후 총점(보너스 포함)
        public readonly int Day;          // Day 계열에서만 유효
        public readonly string Error;     // 실패 사유(성공 시 null)

        FlowCommandResult(bool ok, FlowCommandKind kind, string heroineId, int newScore, int day, string error)
        {
            Ok = ok; Kind = kind; HeroineId = heroineId; NewScore = newScore; Day = day; Error = error;
        }

        public static FlowCommandResult Affinity(FlowCommandKind kind, string heroineId, int newScore)
            => new(true, kind, heroineId, newScore, 0, null);

        public static FlowCommandResult DayResult(int day)
            => new(true, FlowCommandKind.Day, null, 0, day, null);

        public static FlowCommandResult FlagResult()
            => new(true, FlowCommandKind.Flag, null, 0, 0, null);

        public static FlowCommandResult Fail(FlowCommandKind kind, string error)
            => new(false, kind, null, 0, 0, error);
    }

    /// <summary>
    /// CSV Flow 명령의 상태 변경 계열(<c>Affinity:</c>/<c>Day:</c>)을 해석해 <see cref="GameStateSO"/>에 적용하는
    /// 순수 인터프리터. 구 <c>AffinityFlowCommand</c>/<c>DayFlowCommand</c>의 Service Locator 경로를 대체하되,
    /// 호감도 채점은 기존 <see cref="AffinityFormula"/>에 그대로 위임한다(금지선2: 공식 무변경, ADR-007).
    ///
    /// EventBus/Services/MonoBehaviour를 모른다 = 결정적이고 EditMode에서 테스트 가능. 통지 발행과 엔진 연결은
    /// 호출자(내러티브 엔진 이식 슬라이스) 책임. 제어흐름 Flow(Jump/If/LoadingScene/MiniGame/Message 등)는
    /// 엔진 내부 처리라 여기 범위 밖.
    ///
    /// 문법(구 AffinityFlowCommand 1:1):
    ///   Affinity:EventChoice:{heroineId}:{eventTag}:{basePoints}  — 이벤트 선택 기록 + 포인트(+Event3 재선택 +2)
    ///   Affinity:Point:{heroineId}:{Event|Dialogue|Gift|MiniGame}:{amount}
    ///   Day:{N}  — 일차 표시용 강제 설정(실제 전환/오토세이브 아님; 그건 DayLoop/GameManager 경로)
    ///   Flag:{name}[:true|false]  — 플래그 설정(값 생략 시 true; Set 별칭). 분기(If/선택지 조건)가 읽음. 통지 없음.
    /// </summary>
    public static class FlowCommandInterpreter
    {
        public static FlowCommandResult Apply(GameStateSO gs, string command)
        {
            if (gs == null) return FlowCommandResult.Fail(FlowCommandKind.Unknown, "GameStateSO null");
            if (string.IsNullOrWhiteSpace(command))
                return FlowCommandResult.Fail(FlowCommandKind.Unknown, "빈 명령");

            string[] parts = command.Split(':');
            switch (parts[0])
            {
                case "Affinity": return ApplyAffinity(gs, parts);
                case "Day":      return ApplyDay(gs, parts);
                case "Flag":
                case "Set":      return ApplyFlag(gs, parts);
                default:         return FlowCommandResult.Fail(FlowCommandKind.Unknown, $"알 수 없는 Flow 명령: {parts[0]}");
            }
        }

        static FlowCommandResult ApplyAffinity(GameStateSO gs, string[] parts)
        {
            if (parts.Length < 2)
                return FlowCommandResult.Fail(FlowCommandKind.Unknown, "Affinity 서브명령 없음");

            switch (parts[1])
            {
                case "EventChoice":
                {
                    // Affinity:EventChoice:{heroineId}:{eventTag}:{basePoints}
                    if (parts.Length < 5)
                        return FlowCommandResult.Fail(FlowCommandKind.AffinityEventChoice, "EventChoice 인자 부족");
                    string heroineId = parts[2];
                    string eventTag  = parts[3];
                    if (!int.TryParse(parts[4], out int basePoints))
                        return FlowCommandResult.Fail(FlowCommandKind.AffinityEventChoice, $"basePoints 파싱 실패: {parts[4]}");
                    if (AffinityFormula.IndexOf(heroineId) < 0)
                        return FlowCommandResult.Fail(FlowCommandKind.AffinityEventChoice, $"알 수 없는 히로인: {heroineId}");

                    AffinityFormula.RecordEventChoice(gs, heroineId, eventTag, basePoints);
                    return FlowCommandResult.Affinity(FlowCommandKind.AffinityEventChoice, heroineId, AffinityFormula.TotalScore(gs, heroineId));
                }
                case "Point":
                {
                    // Affinity:Point:{heroineId}:{category}:{amount}
                    if (parts.Length < 5)
                        return FlowCommandResult.Fail(FlowCommandKind.AffinityPoint, "Point 인자 부족");
                    string heroineId = parts[2];
                    if (!Enum.TryParse<PointCategory>(parts[3], out var category))
                        return FlowCommandResult.Fail(FlowCommandKind.AffinityPoint, $"카테고리 파싱 실패: {parts[3]} (Event/Dialogue/Gift/MiniGame)");
                    if (!int.TryParse(parts[4], out int amount))
                        return FlowCommandResult.Fail(FlowCommandKind.AffinityPoint, $"amount 파싱 실패: {parts[4]}");
                    if (AffinityFormula.IndexOf(heroineId) < 0)
                        return FlowCommandResult.Fail(FlowCommandKind.AffinityPoint, $"알 수 없는 히로인: {heroineId}");

                    AffinityFormula.AddPoint(gs, heroineId, category, amount);
                    return FlowCommandResult.Affinity(FlowCommandKind.AffinityPoint, heroineId, AffinityFormula.TotalScore(gs, heroineId));
                }
                default:
                    return FlowCommandResult.Fail(FlowCommandKind.Unknown, $"알 수 없는 Affinity 서브명령: {parts[1]}");
            }
        }

        static FlowCommandResult ApplyDay(GameStateSO gs, string[] parts)
        {
            // Day:{N}
            if (parts.Length < 2)
                return FlowCommandResult.Fail(FlowCommandKind.Day, "Day 인자 없음 (Day:N)");
            if (!int.TryParse(parts[1].Trim(), out int day) || day < 1)
                return FlowCommandResult.Fail(FlowCommandKind.Day, $"Day 파싱 실패: {parts[1]}");

            gs.Day = day;
            return FlowCommandResult.DayResult(day);
        }

        static FlowCommandResult ApplyFlag(GameStateSO gs, string[] parts)
        {
            // Flag:이름[:true|false] (값 생략 시 true). 통지 없음 — 적용만(ChoiceEffectInterpreter Flag와 동일 문법).
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                return FlowCommandResult.Fail(FlowCommandKind.Flag, "Flag 이름 없음 (Flag:이름[:true|false])");
            bool value = parts.Length < 3 || string.Equals(parts[2], "true", StringComparison.OrdinalIgnoreCase);
            gs.SetFlag(parts[1], value);
            return FlowCommandResult.FlagResult();
        }
    }
}
