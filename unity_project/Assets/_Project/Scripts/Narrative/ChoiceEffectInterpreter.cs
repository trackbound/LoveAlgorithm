using System.Collections.Generic;
using LoveAlgo.Core; // GameStateSO

namespace LoveAlgo.Story
{
    /// <summary>
    /// 선택지 효과 문자열을 해석해 <see cref="GameStateSO"/>에 적용하는 순수 함수(EventBus 무관, EditMode 테스트).
    /// 구 <c>ChoicePopup.ApplyEffect</c>의 결정 로직을 1:1 이식하되, 통지/명령 발행은 어댑터(NarrativePlayer) 몫이라
    /// <see cref="ChoiceEffectResult"/>로 "무엇을 발행해야 하는지"를 돌려준다(FlowCommandInterpreter와 동일한 패턴).
    ///
    /// 적용 경계:
    /// - <c>Stat</c>/<c>Flag</c>/<c>Money</c>: 여기서 즉시 gs에 적용(결정적 상태 변경) + 통지용 변경내역 수집.
    /// - <c>Love</c>: gs에 직접 쓰지 않고 <c>Affinity:Point:Id:Dialogue:N</c> Flow 명령으로 위임한다.
    ///   호감도 정본은 AffinityFormula(heroinePoints) 한 곳뿐이며, FlowCommandRouter가 적용+SyncToGameState+통지를
    ///   책임지기 때문(감독 결정: 신 카테고리 시스템 단일화). 구처럼 lovePoints에 직접 가산하지 않는다.
    /// - <c>SFX</c>: 재생 명령 이름만 수집(어댑터가 PlaySfxCommand 발행).
    /// </summary>
    public static class ChoiceEffectInterpreter
    {
        public static ChoiceEffectResult Apply(GameStateSO gs, IEnumerable<string> effects)
        {
            var result = new ChoiceEffectResult();
            if (gs == null || effects == null) return result;

            foreach (var effect in effects)
                ApplyOne(gs, effect, result);

            return result;
        }

        static void ApplyOne(GameStateSO gs, string effect, ChoiceEffectResult result)
        {
            if (string.IsNullOrEmpty(effect)) return;

            var parts = effect.Split(':');
            if (parts.Length < 2) return;

            string type = parts[0];

            switch (type)
            {
                case "Love":
                case "AddLove": // 구 Command 별칭
                    // Love:Character:Value → Affinity 카테고리(Dialogue)로 위임.
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int loveValue))
                        result.FlowCommands.Add($"Affinity:Point:{parts[1]}:Dialogue:{loveValue}");
                    break;

                case "Stat":
                case "AddStat": // 구 Command 별칭
                    // Stat:StatName:Value
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int statValue))
                    {
                        int oldV = gs.GetStat(parts[1]);
                        gs.AddStat(parts[1], statValue);
                        int newV = gs.GetStat(parts[1]);
                        result.StatChanges.Add(new ChoiceEffectResult.StatChange(parts[1], oldV, newV));
                    }
                    break;

                case "Flag":
                case "Set": // 구 Command 별칭
                    // Flag:Name:true/false (값 생략 시 true). 통지 이벤트 없음 — 적용만.
                    if (parts.Length >= 3)
                        gs.SetFlag(parts[1], string.Equals(parts[2], "true", System.StringComparison.OrdinalIgnoreCase));
                    else
                        gs.SetFlag(parts[1], true);
                    break;

                case "Money":
                case "AddMoney": // 구 Command 별칭
                    // Money:Value
                    if (int.TryParse(parts[1], out int moneyValue))
                    {
                        gs.AddMoney(moneyValue);
                        result.MoneyChanged = true;
                        result.NewMoney = gs.Money;
                    }
                    break;

                case "SFX":
                    // SFX:Name
                    if (parts.Length >= 2)
                        result.SfxNames.Add(parts[1]);
                    break;
            }
        }
    }

    /// <summary>
    /// <see cref="ChoiceEffectInterpreter.Apply"/> 결과 — 어댑터가 EventBus로 발행해야 할 변경내역.
    /// Stat/Money는 적용 후 통지용, Love는 Flow 명령으로, SFX는 재생 명령으로 변환된다.
    /// </summary>
    public sealed class ChoiceEffectResult
    {
        public readonly List<StatChange> StatChanges = new();
        public bool MoneyChanged;
        public long NewMoney;
        public readonly List<string> FlowCommands = new(); // 호감도(Affinity:Point:...) 위임
        public readonly List<string> SfxNames = new();

        public readonly struct StatChange
        {
            public readonly string StatId;
            public readonly int OldValue;
            public readonly int NewValue;
            public StatChange(string statId, int oldValue, int newValue)
            {
                StatId = statId; OldValue = oldValue; NewValue = newValue;
            }
        }
    }
}
