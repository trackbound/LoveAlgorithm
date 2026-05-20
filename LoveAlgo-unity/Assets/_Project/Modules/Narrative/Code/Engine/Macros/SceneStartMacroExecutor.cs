using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Macros
{
    /// <summary>
    /// SceneStart 매크로 — 장면 시작
    /// CSV: FX,,SceneStart[:배경[:EyeClose]],await
    /// 페이드인 후 짧은 여운(Wait)을 자동 포함하여 CSV에서 Wait 라인 불필요
    /// </summary>
    public static class SceneStartMacroExecutor
    {
        /// <summary>페이드인 후 기본 대기 시간(초)</summary>
        const float DefaultPauseAfterFadeIn = 0.4f;

        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            string bgPath = null;
            bool eyeClose = false;

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                bgPath = parts[1];

            if (parts.Length > 2 && parts[2].Equals("EyeClose", StringComparison.OrdinalIgnoreCase))
                eyeClose = true;

            var bgDisplay = bgPath ?? "(없음)";
            Debug.Log($"[Macro] SceneStart (bg={bgDisplay}, eyeClose={eyeClose})");

            var fx = ScreenFX.Instance;

            // 안전망 — 잔존 eye/tint 정리. eyeClose 모드면 직후 다시 설정.
            fx?.ResetAll();

            if (bgPath != null)
            {
                var background = ExecutionDependencies.Stage?.Background;
                var character = ExecutionDependencies.Stage?.Character;
                character?.ClearAll();
                if (background != null)
                    await background.ChangeBackgroundAsync(bgPath, BGTransition.Cut, 0f, ct);

                if (eyeClose)
                    fx?.EyeCloseImmediate();
            }

            float fadeDuration = eyeClose ? 0.3f : 0.6f;
            if (fx != null)
                await fx.FadeInAsync(fadeDuration, ct);

            // 페이드인 직후 짧은 여운 — 화면이 밝아진 뒤 바로 텍스트가 뜨는 것을 방지
            await UniTask.Delay(TimeSpan.FromSeconds(DefaultPauseAfterFadeIn), cancellationToken: ct);

            Debug.Log($"[Macro] SceneStart 완료 ({(eyeClose ? "EyeClose 유지" : "페이드인")})");
        }
    }
}
