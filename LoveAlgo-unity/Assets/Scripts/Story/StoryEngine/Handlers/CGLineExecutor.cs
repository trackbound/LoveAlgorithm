using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// CG 라인 실행기 — CG 표시/종료
    /// </summary>
    public class CGLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.CG;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var parts = line.Value.Split(':');
            bool isExit = parts[0].Equals("Exit", System.StringComparison.OrdinalIgnoreCase)
                       || parts[0].Equals("Close", System.StringComparison.OrdinalIgnoreCase);

            if (!isExit)
            {
                var character = ExecutionDependencies.Stage?.Character;
                if (character != null)
                    await character.ExitAllAsync(ct);
                ExecutionDependencies.DialogueUI?.Hide();
            }
            else
            {
                ExecutionDependencies.DialogueUI?.Show();
            }

            var cg = ExecutionDependencies.Stage?.CG;
            if (cg != null)
            {
                await cg.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[CG] {line.Value}");
            }

            return true;
        }
    }
}
