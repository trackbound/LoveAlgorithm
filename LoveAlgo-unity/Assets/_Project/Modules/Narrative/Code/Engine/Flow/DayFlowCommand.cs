using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// Day 명령 — CurrentDay 강제 설정 (스토리 진행 표시용)
    /// CSV: Flow,,Day:N,>
    /// 예: Flow,,Day:2,>  → CurrentDay = 2
    /// </summary>
    public static class DayFlowCommand
    {
        public static UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            // parts[2] = "Day:N"
            string arg = parts.Length > 2 ? parts[2] : string.Empty;
            int colon = arg.IndexOf(':');
            if (colon < 0)
            {
                Debug.LogWarning($"[Flow] Day — 형식 오류 (Day:N 필요): {arg}");
                return UniTask.CompletedTask;
            }

            string dayStr = arg.Substring(colon + 1).Trim();
            if (!int.TryParse(dayStr, out int day) || day < 1)
            {
                Debug.LogWarning($"[Flow] Day — 숫자 파싱 실패: {dayStr}");
                return UniTask.CompletedTask;
            }

            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[Flow] Day — GameManager 없음");
                return UniTask.CompletedTask;
            }

            int prev = gm.CurrentDay;
            gm.CurrentDay = day;
            Log.Info($"[Flow] Day — CurrentDay {prev} → {day}");
            return UniTask.CompletedTask;
        }
    }
}
