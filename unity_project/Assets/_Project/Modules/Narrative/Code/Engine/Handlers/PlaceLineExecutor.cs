using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// Place 라인 실행기 — 장소/이벤트 배너.
    /// <para>
    /// 의도적으로 fire-and-forget이다. PlaceNotification은 화면 한 켠에 잠시 떴다
    /// 자동으로 사라지는 알림이라 다음 라인을 차단할 필요가 없고, 차단하면 보너스
    /// 표시 시간만큼 스토리가 멈춘다. 진행을 막고 싶다면 await로 바꿔야 한다.
    /// </para>
    /// </summary>
    public class PlaceLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.Place;

        public UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var placeUI = PopupSystem.Instance?.Get<PlaceNotification>();
            if (placeUI != null)
            {
                // fire-and-forget: 의도된 동작. 클래스 주석 참조.
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
