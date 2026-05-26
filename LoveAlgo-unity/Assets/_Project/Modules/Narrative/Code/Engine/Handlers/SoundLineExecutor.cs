using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// Sound 라인 실행기 — 오디오 재생
    /// </summary>
    public class SoundLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.Sound;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var audio = ExecutionDependencies.Audio;
            if (audio != null)
            {
                await audio.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Log.Info($"[Sound] {line.Value}");
            }
            return true;
        }
    }
}
