using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Story.StoryEngine.Macros
{
    /// <summary>
    /// SceneEnd 매크로 — 가벼운 장면 전환
    /// CSV: FX,,SceneEnd[:페이드시간],await
    /// </summary>
    public static class SceneEndMacroExecutor
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            float fadeDuration = parts.Length > 1 && float.TryParse(parts[1], out float fd) ? fd : 0.5f;
            Debug.Log($"[Macro] SceneEnd (fade={fadeDuration}s)");

            var dialogueUI = ExecutionDependencies.DialogueUI;
            var fx = ScreenFX.Instance;
            var stage = ExecutionDependencies.Stage;

            if (fx != null)
                await fx.FadeOutAsync(fadeDuration, ct);

            dialogueUI?.HideImmediate();
            stage?.Character?.ClearAll();
            stage?.VirtualBG?.HideImmediate();
            PopupManager.Instance?.Get<PlaceNotification>()?.HideImmediate();

            if (ExecutionDependencies.Audio != null)
                await ExecutionDependencies.Audio.ExecuteAsync("BGM:Stop:Fade:1.0", ct);

            Debug.Log("[Macro] SceneEnd 완료 (암전 유지)");
        }
    }
}
