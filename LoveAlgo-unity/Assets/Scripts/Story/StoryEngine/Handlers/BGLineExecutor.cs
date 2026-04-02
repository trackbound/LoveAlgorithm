using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// BG 라인 실행기 — 배경 전환
    /// </summary>
    public class BGLineExecutor : ILineExecutor
    {
        public LineType Type => LineType.BG;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            var parts = line.Value.Split(':');
            string bgName = parts[0];
            bool isCut = parts.Length >= 2 && parts[1].Equals("Cut", System.StringComparison.OrdinalIgnoreCase);
            bool isFade = parts.Length >= 2 && parts[1].Equals("Fade", System.StringComparison.OrdinalIgnoreCase);
            bool isCrossFade = !isCut && !isFade;

            var background = ExecutionDependencies.Stage?.Background;
            if (background != null && !string.IsNullOrEmpty(background.CurrentBackground)
                && background.CurrentBackground.Equals(bgName, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[BG] '{bgName}' 동일 → 전환 효과 스킵");
                return true;
            }

            var cgLayer = ExecutionDependencies.Stage?.CG;
            bool cgCoversScreen = cgLayer != null && cgLayer.IsShowing;

            if (cgCoversScreen)
            {
                if (background != null)
                    await background.ChangeBackgroundAsync(bgName, BGTransition.Cut, 0f, ct);
                Debug.Log($"[BG] '{bgName}' → CG 뒤에서 즉시 교체");
            }
            else if (isCut || isCrossFade)
            {
                if (background != null)
                    await background.ExecuteAsync(line.Value, ct);
            }
            else
            {
                var character = ExecutionDependencies.Stage?.Character;
                var screenFX = ScreenFX.Instance;
                var dialogueUI = ExecutionDependencies.DialogueUI;

                float duration = 2.0f;
                if (parts.Length >= 3 && float.TryParse(parts[2], out float d))
                    duration = d;
                float halfDuration = duration * 0.5f;

                dialogueUI?.HideImmediate();
                if (screenFX != null)
                    await screenFX.FadeOutAsync(halfDuration, ct);

                character?.ClearAll();
                if (background != null)
                    await background.ChangeBackgroundAsync(bgName, BGTransition.Cut, 0f, ct);

                if (screenFX != null)
                    await screenFX.FadeInAsync(halfDuration, ct);

                ExecutionDependencies.Audio?.OnAllCharactersExit();
            }

            return true;
        }
    }
}
