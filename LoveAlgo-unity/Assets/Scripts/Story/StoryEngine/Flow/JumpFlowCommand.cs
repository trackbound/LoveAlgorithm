using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// Jump 명령 — 지정된 LineID로 점프
    /// </summary>
    public static class JumpFlowCommand
    {
        public static bool Execute(string[] parts, Dictionary<string, int> lineIndex, ref int currentIndex)
        {
            if (parts.Length > 1)
            {
                string targetId = parts[1];
                if (lineIndex.TryGetValue(targetId, out int targetIndex))
                {
                    currentIndex = targetIndex - 1;
                    Debug.Log($"[Flow] Jump -> {targetId} (index {targetIndex})");
                    return true;
                }
                Debug.LogError($"[Flow] Jump 대상 '{targetId}'를 찾을 수 없습니다.");
            }
            return false;
        }
    }
}
