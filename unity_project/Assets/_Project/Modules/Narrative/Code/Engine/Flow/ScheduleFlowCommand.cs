using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
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
            Log.Info("[Flow] Schedule — 인라인 스케줄 시작");

            // Headless 자동화: UI 우회, 효과 미적용으로 즉시 통과 (ADR §ScheduleFlowCommand).
            // 효과 적용 시 게임 상태 변화로 후속 라인 분기가 바뀔 수 있어 명시적 skip.
            if (Headless.IsEnabled)
            {
                Log.Info("[Flow] Schedule — headless 즉시 통과 (효과 미적용)");
                await UniTask.Yield(ct);
                return;
            }

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

            Log.Info("[Flow] Schedule — 인라인 스케줄 완료, 스토리 복귀");
        }
    }
}
