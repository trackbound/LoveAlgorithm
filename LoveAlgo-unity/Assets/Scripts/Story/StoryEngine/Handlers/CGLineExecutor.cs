using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// CG 라인 실행기 — CG 표시/종료. CG 표시 시 오토모드 일시정지.
    /// </summary>
    public class CGLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.CG;

        /// <summary>CG 표시 전 오토모드 상태 보존용</summary>
        static bool _wasAutoMode;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var parts = line.Value.Split(':');
            bool isExit = parts[0].Equals("Exit", System.StringComparison.OrdinalIgnoreCase)
                       || parts[0].Equals("Close", System.StringComparison.OrdinalIgnoreCase);

            if (!isExit)
            {
                // CG 표시 시 오토모드 일시정지 — 수동 클릭 필요
                var runner = ScriptRunner.Instance;
                if (runner != null)
                {
                    _wasAutoMode = runner.IsAutoMode;
                    if (_wasAutoMode)
                        runner.SetAutoMode(false);
                }

                var character = ExecutionDependencies.Stage?.Character;
                if (character != null)
                    await character.ExitAllAsync(ct);
                ExecutionDependencies.DialogueUI?.Hide();
            }
            else
            {
                // CG 종료 시 오토모드 복원
                if (_wasAutoMode)
                {
                    var runner = ScriptRunner.Instance;
                    runner?.SetAutoMode(true);
                    _wasAutoMode = false;
                }

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
