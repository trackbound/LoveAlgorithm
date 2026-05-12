using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// Overlay 라인 실행기 — 오버레이 레이어 제어
    /// </summary>
    public class OverlayLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.Overlay;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var overlay = ExecutionDependencies.Stage?.VirtualBG;
            if (overlay != null)
            {
                await overlay.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[Overlay] {line.Value}");
            }
            return true;
        }
    }
}
