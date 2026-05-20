using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// CG 라인 실행기 — CG 표시/종료. CG 표시 시 오토모드 일시정지.
    /// </summary>
    public class CGLineExecutor : ILineExecutor, IResettableExecutor
    {
        public LineType Type => LineType.CG;

        /// <summary>CG 표시 전 오토모드 상태 보존용 (인스턴스 필드 — 같은 ScriptRunner 내 enter/exit 페어).</summary>
        bool _wasAutoMode;

        /// <summary>
        /// 점프/스크립트 로드 시 stale 상태 폐기. LineHandlerRegistry.ResetAllExecutorState가 호출.
        /// </summary>
        public void ResetState() => _wasAutoMode = false;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var parts = line.Value.Split(':');
            // Exit/Close/Hide 모두 Exit canonical로 정규화
            bool isExit = CommandAliases.NormalizeCGAction(parts[0]) == "Exit";

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
