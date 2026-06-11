using System;
using System.Collections.Generic;

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 선택지 효과 문자열 → Flow 명령 변환(순수). 스토리 선택지와 동일하게
    /// <c>Love:{히로인}:{n}</c>은 Affinity의 Dialogue 카테고리로 위임한다 — 메신저 +3점은 기획상
    /// "대화 포인트 15점"의 일부라 신규 카테고리를 만들지 않는다(호감도 정본 단일화, 금지선 #2).
    /// 그 외(<c>Flag:...</c> 등)는 Flow 명령 문법 그대로 통과 — 적용은 FlowCommandController가
    /// EventBus 너머에서 책임진다(피처 간 직접 참조 없음, ADR-007/011).
    /// </summary>
    public static class MessengerEffectMapper
    {
        public static List<string> ToFlowCommands(IEnumerable<string> effects)
        {
            var commands = new List<string>();
            if (effects == null) return commands;

            foreach (var effect in effects)
            {
                if (string.IsNullOrEmpty(effect)) continue;

                var parts = effect.Split(':');
                bool isLove = parts.Length >= 3
                    && (string.Equals(parts[0], "Love", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(parts[0], "AddLove", StringComparison.OrdinalIgnoreCase)); // 구 별칭

                commands.Add(isLove
                    ? $"Affinity:Point:{parts[1]}:Dialogue:{parts[2]}"
                    : effect);
            }
            return commands;
        }
    }
}
