using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
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
            var placeUI = PopupManager.Instance?.Get<PlaceNotification>();
            if (placeUI != null)
            {
                placeUI.ExecuteAsync(line.Value, ct).Forget();
            }
            else
            {
                Log.Info($"[Place] {line.Value}");
            }
            return UniTask.FromResult(true);
        }
    }
}
