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
        /// <summary>페이드 아웃/인 시간 (초)</summary>
        const float FadeDuration = 0.35f;

        public static async UniTask ExecuteAsync(CancellationToken ct)
        {
            Debug.Log("[Flow] Schedule — 인라인 스케줄 시작");

            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[Flow] Schedule — GameManager 없음");
                return;
            }

            var fx = ScreenFX.Instance;
            var dialogueUI = UIManager.Instance?.DialogueUI;

            // 페이드 아웃 → UI 전환 → 페이드 인
            if (fx != null) await fx.FadeOutAsync(FadeDuration, ct);

            dialogueUI?.HideImmediate();
            UIManager.Instance?.ShowOnly(MainUIType.Schedule);

            if (fx != null) await fx.FadeInAsync(FadeDuration, ct);

            // 1회 선택 완료까지 대기
            await gm.DayLoop.WaitForInlineScheduleAsync(ct);

            // 페이드 아웃 → 스토리 UI 복귀 → 페이드 인
            if (fx != null) await fx.FadeOutAsync(FadeDuration, ct);

            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
            dialogueUI?.Clear();
            dialogueUI?.HideImmediate();

            if (fx != null) await fx.FadeInAsync(FadeDuration, ct);

            Debug.Log("[Flow] Schedule — 인라인 스케줄 완료, 스토리 복귀");
        }
    }
}
