using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// Char 라인 실행기 — 캐릭터 제어
    /// </summary>
    public class CharLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.Char;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var character = ExecutionDependencies.Stage?.Character;
            if (character != null)
            {
                await character.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[Char] {line.Value}");
            }
            return true;
        }
    }
}
