using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// Schedule 명령 — 스케줄 UI를 열어 1회 선택 후 스토리로 복귀
    /// CSV: Flow,,Schedule,await
    /// </summary>
    public static class ScheduleFlowCommand
    {
        public static async UniTask ExecuteAsync(CancellationToken ct)
        {
            Debug.Log("[Flow] Schedule — 인라인 스케줄 시작");

            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[Flow] Schedule — GameManager 없음");
                return;
            }

            // 대화창 숨기고 스케줄 UI 표시
            var dialogueUI = UIManager.Instance?.DialogueUI;
            dialogueUI?.HideImmediate();
            UIManager.Instance?.ShowOnly(MainUIType.Schedule);

            // 1회 선택 완료까지 대기
            await gm.DayLoop.WaitForInlineScheduleAsync(ct);

            // 스토리 UI 복귀
            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
            dialogueUI?.Clear();
            dialogueUI?.HideImmediate();

            Debug.Log("[Flow] Schedule — 인라인 스케줄 완료, 스토리 복귀");
        }
    }
}
