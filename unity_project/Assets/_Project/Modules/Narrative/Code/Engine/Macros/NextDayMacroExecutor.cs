using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine.Macros
{
    /// <summary>
    /// NextDay 매크로 — DayEnd + DayStart 한 줄 sugar.
    /// 가장 흔한 "하루 종료 → 다음날 시작" 패턴을 한 줄로 처리.
    ///
    /// CSV:
    ///   FX,,NextDay:Wake:BG_Room_Morning,await    # = DayEnd:Wake + DayStart:BG_Room_Morning (Wake)
    ///   FX,,NextDay:Cut:BG_Cafe_Day,await         # = DayEnd:Cut + DayStart:BG_Cafe_Day:Cut
    ///   FX,,NextDay:Cut:BG_X:3,await              # mode + bg + actions
    ///   FX,,NextDay:BG_Room,await                 # mode 생략 시 Wake
    ///
    /// 인자 순서는 자유 — 숫자=actions, Wake/Cut/Reveal=mode, 그 외=bgPath.
    /// </summary>
    public static class NextDayMacroExecutor
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            // mode + bgPath + actions 파싱
            string mode = "Wake";
            string bgPath = null;
            string actionsToken = null;

            for (int i = 1; i < parts.Length; i++)
            {
                string token = parts[i];
                if (string.IsNullOrEmpty(token)) continue;

                if (IsMode(token))
                    mode = NormalizeMode(token);
                else if (int.TryParse(token, out _))
                    actionsToken = token;
                else if (bgPath == null)
                    bgPath = token;
            }

            Debug.Log($"[Macro] NextDay (mode={mode}, bg={bgPath ?? "(없음)"}, actions={actionsToken ?? "default"})");

            // 1) DayEnd 호출 — mode 전달
            var dayEndArgs = new[] { "DayEnd", mode };
            await DayEndMacroExecutor.ExecuteAsync(dayEndArgs, ct);

            // 2) DayStart 호출 — bg + mode (+actions)
            var startArgs = BuildDayStartArgs(bgPath, mode, actionsToken);
            await DayStartMacroExecutor.ExecuteAsync(startArgs, ct);
        }

        static string[] BuildDayStartArgs(string bgPath, string mode, string actionsToken)
        {
            var list = new System.Collections.Generic.List<string> { "DayStart" };
            if (!string.IsNullOrEmpty(bgPath)) list.Add(bgPath);
            list.Add(mode);
            if (!string.IsNullOrEmpty(actionsToken)) list.Add(actionsToken);
            return list.ToArray();
        }

        static bool IsMode(string token)
        {
            return string.Equals(token, "Wake",   StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "Cut",    StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "Reveal", StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeMode(string token)
        {
            if (string.Equals(token, "Reveal", StringComparison.OrdinalIgnoreCase)) return "Cut";
            // 첫 글자 대문자로 정규화
            return char.ToUpperInvariant(token[0]) + token.Substring(1).ToLowerInvariant();
        }
    }
}
