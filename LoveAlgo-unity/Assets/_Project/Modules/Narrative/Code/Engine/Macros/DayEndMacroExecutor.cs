using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Story.StoryEngine.Macros
{
    /// <summary>
    /// DayEnd 매크로 — 하루 마무리 연출
    /// CSV: FX,,DayEnd[:페이드시간],await
    /// </summary>
    public static class DayEndMacroExecutor
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            var cfg = FXDefaultsConfig.Instance;
            float defFadeOut = cfg != null ? cfg.dayEndFadeOut : 0.8f;
            float defFadeIn  = cfg != null ? cfg.dayEndFadeIn  : 0.3f;
            float fadeDuration = parts.Length > 1 && float.TryParse(parts[1], out float fd) ? fd : defFadeOut;
            float totalDuration = 5.0f;
            float startTime = Time.time;
            Debug.Log($"[Macro] DayEnd (fade={fadeDuration}s, total={totalDuration}s)");

            var dialogueUI = ExecutionDependencies.DialogueUI;
            var fx = ScreenFX.Instance;
            var stage = ExecutionDependencies.Stage;

            SaveManager.CapturePendingScreenshot();

            if (fx != null)
                await fx.FadeOutAsync(fadeDuration, ct);

            dialogueUI?.HideImmediate();
            stage?.Character?.ClearAll();
            stage?.VirtualBG?.HideImmediate();
            PopupManager.Instance?.Get<PlaceNotification>()?.HideImmediate();

            if (ExecutionDependencies.Audio != null)
                await ExecutionDependencies.Audio.ExecuteAsync("BGM:Stop", ct);

            var bgLine = new ScriptLine("", LineType.BG, "", "BG_BlackCut", NextType.Immediate);
            var bgExec = new Handlers.BGLineExecutor();
            await bgExec.ExecuteAsync(bgLine, ct);

            fx?.EyeCloseImmediate();

            if (fx != null)
                await fx.FadeInAsync(defFadeIn, ct);

            if (GameManager.Instance != null)
                await GameManager.Instance.AutoSaveAsync();

            float elapsed = Time.time - startTime;
            float remaining = totalDuration - elapsed;
            if (remaining > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(remaining), cancellationToken: ct);

            Debug.Log("[Macro] DayEnd 완료");
        }
    }
}
