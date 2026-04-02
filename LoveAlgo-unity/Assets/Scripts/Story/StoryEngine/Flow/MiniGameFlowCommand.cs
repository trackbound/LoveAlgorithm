using System.Threading;
using Cysharp.Threading.Tasks;
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
                string gameName = parts[1];
                string heroineId = parts[2];
                await LoveAlgo.MiniGame.MiniGameLauncher.LaunchAsync(gameName, heroineId);
            }
        }
    }
}
