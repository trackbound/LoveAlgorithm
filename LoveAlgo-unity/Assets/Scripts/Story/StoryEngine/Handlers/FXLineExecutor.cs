using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// FX 라인 실행기 — 시각 효과 + 매크로 디스패치
    /// </summary>
    public class FXLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.FX;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var parts = line.Value.Split(':');
            var command = parts[0].ToLowerInvariant();
            var dialogueUI = ExecutionDependencies.DialogueUI;

            switch (command)
            {
                case "dayend":
                    await Macros.DayEndMacroExecutor.ExecuteAsync(parts, ct);
                    return true;
                case "daystart":
                    await Macros.DayStartMacroExecutor.ExecuteAsync(parts, ct);
                    return true;
                case "sceneend":
                    await Macros.SceneEndMacroExecutor.ExecuteAsync(parts, ct);
                    return true;
                case "scenestart":
                    await Macros.SceneStartMacroExecutor.ExecuteAsync(parts, ct);
                    return true;
                case "setup":
                    await Macros.SetupMacroExecutor.ExecuteAsync(line.Value, ct);
                    return true;
                case "wait":
                    float waitSec = parts.Length > 1 && float.TryParse(parts[1], out float ws) ? ws : 1.0f;
                    await UniTask.Delay(TimeSpan.FromSeconds(waitSec), cancellationToken: ct);
                    return true;
                case "dialoguehide":
                    dialogueUI?.Hide();
                    return true;
                case "dialogueshow":
                    dialogueUI?.Clear();
                    dialogueUI?.Show();
                    return true;
            }

            var fx = ScreenFX.Instance;
            if (fx != null)
            {
                await fx.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[FX] {line.Value}");
            }

            if (command == "fadeout")
                dialogueUI?.HideImmediate();

            return true;
        }
    }
}
