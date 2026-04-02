using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Macros
{
    /// <summary>
    /// SceneStart 매크로 — 장면 시작
    /// CSV: FX,,SceneStart[:배경[:EyeClose]],await
    /// </summary>
    public static class SceneStartMacroExecutor
    {
        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            string bgPath = null;
            bool eyeClose = false;

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                bgPath = parts[1];

            if (parts.Length > 2 && parts[2].Equals("EyeClose", System.StringComparison.OrdinalIgnoreCase))
                eyeClose = true;

            var bgDisplay = bgPath ?? "(없음)";
            Debug.Log($"[Macro] SceneStart (bg={bgDisplay}, eyeClose={eyeClose})");

            var fx = ScreenFX.Instance;

            if (bgPath != null)
            {
                var background = ExecutionDependencies.Stage?.Background;
                var character = ExecutionDependencies.Stage?.Character;
                character?.ClearAll();
                if (background != null)
                    await background.ChangeBackgroundAsync(bgPath, BGTransition.Cut, 0f, ct);

                if (eyeClose)
                    fx?.EyeCloseImmediate();
                else
                    fx?.EyeOpenImmediate();
            }
            else
            {
                fx?.EyeOpenImmediate();
            }

            float fadeDuration = eyeClose ? 0.3f : 0.6f;
            if (fx != null)
                await fx.FadeInAsync(fadeDuration, ct);

            if (bgPath != null)
                Debug.Log($"[Macro] SceneStart: BG '{bgPath}' ({(eyeClose ? "EyeClose 유지" : "페이드인")})");
            else
                Debug.Log("[Macro] SceneStart: FadeIn (배경 미지정)");
        }
    }
}
