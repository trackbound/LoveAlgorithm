using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// Jump 명령 — 지정된 LineID로 점프.
    /// 디버그용 무한 루프 가드: 1초 안에 MaxHopsPerWindow회를 넘으면 점프 거부 + LogError.
    /// 정상 시나리오(선택지/분기)에서는 절대 트리거되지 않을 만큼 여유 있게 설정.
    /// </summary>
    public static class JumpFlowCommand
    {
        const int MaxHopsPerWindow = 100;
        const float WindowSeconds = 1f;
        static int _recentHops;
        static float _windowStartTime;

        public static bool Execute(string[] parts, Dictionary<string, int> lineIndex, ref int currentIndex)
        {
            if (parts.Length > 1)
            {
                string targetId = parts[1];
                if (lineIndex.TryGetValue(targetId, out int targetIndex))
                {
                    if (!CheckHopBudget(targetId)) return false;
                    currentIndex = targetIndex - 1;
                    Log.Info($"[Flow] Jump -> {targetId} (index {targetIndex})");
                    return true;
                }
                Debug.LogError($"[Flow] Jump 대상 '{targetId}'를 찾을 수 없습니다.");
            }
            return false;
        }

        static bool CheckHopBudget(string targetId)
        {
            float now = Time.unscaledTime;
            if (now - _windowStartTime > WindowSeconds)
            {
                _windowStartTime = now;
                _recentHops = 0;
            }
            _recentHops++;
            if (_recentHops > MaxHopsPerWindow)
            {
                Debug.LogError(
                    $"[Flow] Jump 폭주 감지: 최근 {WindowSeconds:F1}s 안에 {_recentHops}회. " +
                    $"마지막 대상: '{targetId}'. CSV 무한 루프 의심 — 점프 거부.");
                return false;
            }
            return true;
        }

        /// <summary>Reload Domain Off 가드 — PlayMode 진입 시 옛 hop 카운터 리셋.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticStateOnLoad()
        {
            _recentHops = 0;
            _windowStartTime = 0f;
        }
    }
}
