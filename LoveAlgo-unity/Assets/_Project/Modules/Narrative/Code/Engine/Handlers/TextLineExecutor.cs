using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// Text 라인 실행기 — 대사/나레이션 표시
    /// </summary>
    public class TextLineExecutor : ILineExecutor
    {
        bool _needsPreTextBeat;

        public LineType Type => LineType.Text;

        public void MarkPreTextBeat() => _needsPreTextBeat = true;

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            if (_needsPreTextBeat)
            {
                _needsPreTextBeat = false;
                await UniTask.Delay(TimeSpan.FromSeconds(0.15f), cancellationToken: ct);
            }

            bool isMonologue = string.IsNullOrEmpty(line.Speaker);
            var monologueDim = ExecutionDependencies.Stage?.MonologueDim;
            if (monologueDim != null)
            {
                if (isMonologue && !monologueDim.IsShowing)
                    await monologueDim.ShowAsync(ct: ct);
                else if (!isMonologue && monologueDim.IsShowing)
                    await monologueDim.HideAsync(ct: ct);
            }

            var dialogueUI = ExecutionDependencies.DialogueUI;
            if (dialogueUI != null)
            {
                await dialogueUI.ShowTextAsync(line.Speaker, line.Value, ct);
            }
            else
            {
                Debug.Log($"[Text] {(string.IsNullOrEmpty(line.Speaker) ? "(나레이션)" : line.Speaker)}: {line.Value}");
            }

            return true;
        }
    }
}
