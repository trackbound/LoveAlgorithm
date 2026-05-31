using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// SD 라인 실행기 — SD 컷씬
    /// </summary>
    public class SDLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.SD;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var sd = ExecutionDependencies.Stage?.SDCutscene;
            if (sd == null)
            {
                Log.Info($"[SD] {line.Value}");
                return true;
            }

            var charLayer = ExecutionDependencies.Stage?.Character;
            string command = line.Value.Split(':')[0];
            bool isExit = command.Equals("Exit", StringComparison.OrdinalIgnoreCase)
                       || command.Equals("Close", StringComparison.OrdinalIgnoreCase);

            if (!isExit && !sd.IsShowing && charLayer != null && !charLayer.IsLayerHidden)
                await charLayer.SetVisibleAsync(false, ct);

            await sd.ExecuteAsync(line.Value, ct);

            if (isExit && charLayer != null && charLayer.IsLayerHidden)
                await charLayer.SetVisibleAsync(true, ct);

            return true;
        }
    }
}
