using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Macros
{
    /// <summary>
    /// DayStart 매크로 — 하루 시작
    /// CSV: FX,,DayStart[:배경[:행동수]],>
    /// </summary>
    public static class DayStartMacroExecutor
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            string bgPath = null;
            int actions = GameConstants.ActionsPerDay;

            if (parts.Length > 1)
            {
                if (int.TryParse(parts[1], out int a))
                    actions = a;
                else
                {
                    bgPath = parts[1];
                    if (parts.Length > 2 && int.TryParse(parts[2], out int a2))
                        actions = a2;
                }
            }

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.AdvanceDay(actions);
                Debug.Log($"[Macro] DayStart → Day {gm.CurrentDay}, Actions={actions}");
            }

            var fx = ScreenFX.Instance;

            if (bgPath != null)
            {
                var bgLine = new ScriptLine("", LineType.BG, "", $"{bgPath}:Cut", NextType.Immediate);
                var bgExec = new Handlers.BGLineExecutor();
                await bgExec.ExecuteAsync(bgLine, ct);
                Debug.Log($"[Macro] DayStart: BG '{bgPath}' 세팅 완료 (EyeClose 뒤)");
            }
            else
            {
                fx?.EyeOpenImmediate();
                Debug.Log("[Macro] DayStart: Eye 해제 (배경 미지정)");
            }
        }
    }
}
