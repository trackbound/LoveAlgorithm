using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Modules.Affinity;
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// CSV에서 호감도 모듈을 호출하는 Flow 명령.
    ///
    /// 형식:
    ///   Affinity:EventChoice:{heroineId}:{eventTag}:{basePoints}
    ///       이벤트 선택 기록 + 기본 포인트 추가. 3차(eventTag="Event3")는 자동 +2 보정.
    ///   Affinity:Point:{heroineId}:{category}:{amount}
    ///       카테고리 포인트만 추가. category는 PointCategory enum 이름 (Event/Dialogue/Gift/MiniGame).
    ///
    /// 예시:
    ///   ,Flow,,Affinity:EventChoice:HaYeEun:Event1:3,>
    ///   ,Flow,,Affinity:Point:HaYeEun:Dialogue:1,>
    ///   ,Flow,,Affinity:Point:SeoDaEun:Gift:4,>
    /// </summary>
    public static class AffinityFlowCommand
    {
        public static void Execute(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogWarning("[Affinity] 인자 부족: Affinity:{Sub}:...");
                return;
            }

            var aff = Services.TryGet<IAffinity>();
            if (aff == null)
            {
                Debug.LogError("[Affinity] IAffinity 서비스 미등록 — 씬에 AffinityModule이 있는지 확인");
                return;
            }

            switch (parts[1])
            {
                case "EventChoice":
                    HandleEventChoice(parts, aff);
                    return;

                case "Point":
                    HandlePoint(parts, aff);
                    return;

                default:
                    Debug.LogWarning($"[Affinity] 알 수 없는 서브명령: {parts[1]}");
                    return;
            }
        }

        static void HandleEventChoice(string[] parts, IAffinity aff)
        {
            // Affinity:EventChoice:{heroineId}:{eventTag}:{basePoints}
            if (parts.Length < 5)
            {
                Debug.LogWarning("[Affinity] EventChoice 인자 부족: Affinity:EventChoice:{heroineId}:{eventTag}:{basePoints}");
                return;
            }
            string heroineId = parts[2];
            string eventTag  = parts[3];
            if (!int.TryParse(parts[4], out int basePoints))
            {
                Debug.LogWarning($"[Affinity] basePoints 파싱 실패: {parts[4]}");
                return;
            }
            aff.RecordEventChoice(heroineId, eventTag, basePoints);
        }

        static void HandlePoint(string[] parts, IAffinity aff)
        {
            // Affinity:Point:{heroineId}:{category}:{amount}
            if (parts.Length < 5)
            {
                Debug.LogWarning("[Affinity] Point 인자 부족: Affinity:Point:{heroineId}:{category}:{amount}");
                return;
            }
            string heroineId = parts[2];
            if (!System.Enum.TryParse<PointCategory>(parts[3], out var category))
            {
                Debug.LogWarning($"[Affinity] PointCategory 파싱 실패: {parts[3]} (Event/Dialogue/Gift/MiniGame 중 하나)");
                return;
            }
            if (!int.TryParse(parts[4], out int amount))
            {
                Debug.LogWarning($"[Affinity] amount 파싱 실패: {parts[4]}");
                return;
            }
            aff.AddPoint(heroineId, category, amount);
        }
    }
}
