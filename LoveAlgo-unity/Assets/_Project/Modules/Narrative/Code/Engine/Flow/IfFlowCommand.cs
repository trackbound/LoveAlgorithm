using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// If 명령 — 조건 분기
    /// 형식: If:조건:점프대상
    /// </summary>
    public static class IfFlowCommand
    {
        public static bool Execute(string value, Dictionary<string, int> lineIndex, ref int currentIndex)
        {
            var parts = value.Split(':');
            if (parts.Length < 3)
            {
                Debug.LogWarning($"[Flow:If] 잘못된 형식: {value}");
                return false;
            }

            string jumpTarget = parts[^1];
            string condition = string.Join(":", parts[1..^1]);

            bool result = GameState.Instance?.EvaluateCondition(condition) ?? false;
            Debug.Log($"[Flow:If] 조건: {condition} = {result}");

            if (result && lineIndex.TryGetValue(jumpTarget, out int targetIndex))
            {
                currentIndex = targetIndex - 1;
                Debug.Log($"[Flow:If] 점프: {jumpTarget} (index: {targetIndex})");
                return true;
            }

            return false;
        }
    }
}
