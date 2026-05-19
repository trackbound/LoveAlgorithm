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
            // alias + 대소문자 정규화 → PascalCase canonical 토큰
            var command = CommandAliases.NormalizeFX(parts[0]);
            var dialogueUI = ExecutionDependencies.DialogueUI;

            switch (command)
            {
                case "DayEnd":
                    await Macros.DayEndMacroExecutor.ExecuteAsync(parts, ct);
                    return true;
                case "DayStart":
                    await Macros.DayStartMacroExecutor.ExecuteAsync(parts, ct);
                    return true;
                case "SceneEnd":
                    await Macros.SceneEndMacroExecutor.ExecuteAsync(parts, ct);
                    return true;
                case "SceneStart":
                    await Macros.SceneStartMacroExecutor.ExecuteAsync(parts, ct);
                    return true;
                case "Setup":
                    await Macros.SetupMacroExecutor.ExecuteAsync(line.Value, ct);
                    return true;
                case "Wait":
                    float waitSec = parts.Length > 1 && float.TryParse(parts[1], out float ws) ? ws : 1.0f;
                    await UniTask.Delay(TimeSpan.FromSeconds(waitSec), cancellationToken: ct);
                    return true;
                case "DialogueHide":
                    dialogueUI?.Hide();
                    return true;
                case "DialogueShow":
                    dialogueUI?.Clear();
                    dialogueUI?.Show();
                    return true;
            }

            // 정규화된 명령을 ScreenFX에 그대로 전달 (parts[0]만 교체)
            string normalizedValue = parts.Length > 1
                ? command + ":" + string.Join(":", parts, 1, parts.Length - 1)
                : command;

            var fx = ScreenFX.Instance;
            if (fx != null)
            {
                await fx.ExecuteAsync(normalizedValue, ct);
            }
            else
            {
                Debug.Log($"[FX] {normalizedValue}");
            }

            if (command == "FadeOut")
                dialogueUI?.HideImmediate();

            return true;
        }
    }
}
