using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// Place 라인 실행기 — 장소/이벤트 배너 (fire-and-forget)
    /// </summary>
    public class PlaceLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.Place;

        public UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var placeUI = UIManager.Instance?.PlaceUI;
            if (placeUI != null)
            {
                placeUI.ExecuteAsync(line.Value, ct).Forget();
            }
            else
            {
                Debug.Log($"[Place] {line.Value}");
            }
            return UniTask.FromResult(true);
        }
    }
}
