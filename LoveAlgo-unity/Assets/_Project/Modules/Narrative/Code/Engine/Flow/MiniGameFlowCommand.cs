using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// MiniGame 명령 — 미니게임 실행
    /// 형식: MiniGame:게임이름:히로인ID
    /// </summary>
    public static class MiniGameFlowCommand
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            if (parts.Length >= 3)
            {
                // Headless 자동화: 미니게임 launch 자체 skip (ADR §MiniGameFlowCommand).
                if (Headless.IsEnabled)
                {
                    Log.Info($"[Flow] MiniGame:{parts[1]} — headless skip");
                    await UniTask.Yield(ct);
                    return;
                }

                string gameName = parts[1];
                string heroineId = parts[2];
                await LoveAlgo.MiniGame.MiniGameLauncher.LaunchAsync(gameName, heroineId);
            }
        }
    }
}
