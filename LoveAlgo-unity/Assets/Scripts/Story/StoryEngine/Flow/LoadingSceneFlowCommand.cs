using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// LoadingScene 명령 — 로딩화면 표시 (자동저장 없음 — Save는 CSV에서 별도 배치)
    /// CSV: Flow,,LoadingScene[:표시시간],await
    /// </summary>
    public static class LoadingSceneFlowCommand
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            float displayTime = parts.Length > 1 && float.TryParse(parts[1], out float dt) ? dt : 2.0f;
            Debug.Log($"[Flow] LoadingScene (표시={displayTime}s)");

            var loading = LoadingScreen.Instance;
            if (loading != null)
            {
                await loading.ShowForAsync(displayTime, ct);
            }
            else
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(displayTime), cancellationToken: ct);
            }

            Debug.Log("[Flow] LoadingScene 완료");
        }
    }
}
