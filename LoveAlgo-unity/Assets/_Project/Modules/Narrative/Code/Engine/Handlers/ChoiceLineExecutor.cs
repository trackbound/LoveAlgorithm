using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Story.StoryEngine.Handlers
{
    /// <summary>
    /// Choice 라인 실행기 — 선택지 표시 및 분기
    /// </summary>
    public class ChoiceLineExecutor : ILineExecutor
    {
        readonly System.Func<List<ScriptLine>> _collectOptions;
        readonly System.Func<Dictionary<string, int>> _lineIndex;
        readonly System.Func<int> _getCurrentIndex;
        readonly System.Action<int> _setCurrentIndex;
        readonly System.Func<bool> _getAutoMode;
        readonly System.Action<bool> _setAutoMode;
        readonly System.Action<bool> _onAutoModeChanged;

        public LineType Type => LineType.Choice;

        public ChoiceLineExecutor(
            System.Func<List<ScriptLine>> collectOptions,
            System.Func<Dictionary<string, int>> lineIndex,
            System.Func<int> getCurrentIndex,
            System.Action<int> setCurrentIndex,
            System.Func<bool> getAutoMode,
            System.Action<bool> setAutoMode,
            System.Action<bool> onAutoModeChanged)
        {
            _collectOptions = collectOptions;
            _lineIndex = lineIndex;
            _getCurrentIndex = getCurrentIndex;
            _setCurrentIndex = setCurrentIndex;
            _getAutoMode = getAutoMode;
            _setAutoMode = setAutoMode;
            _onAutoModeChanged = onAutoModeChanged;
        }

        public async UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct)
        {
            bool wasAutoMode = _getAutoMode();
            if (wasAutoMode)
            {
                _setAutoMode(false);
                _onAutoModeChanged?.Invoke(false);
            }

            var scriptOptions = _collectOptions();
            if (scriptOptions.Count == 0)
            {
                Debug.LogWarning("[Choice] 선택지가 없습니다.");
                return true;
            }

            var options = new List<OptionData>();
            foreach (var opt in scriptOptions)
                options.Add(OptionData.Parse(opt.Value));

            var choiceUI = UIManager.Instance?.ChoicePopup;
            if (choiceUI != null)
            {
                var result = await choiceUI.ShowAndWaitAsync(options, ct);
                if (result != null && !string.IsNullOrEmpty(result.JumpTarget))
                {
                    GameState.Instance?.AddChoice(result.JumpTarget);
                    if (_lineIndex().TryGetValue(result.JumpTarget, out int targetIndex))
                    {
                        _setCurrentIndex(targetIndex - 1);
                        Debug.Log($"[Choice] 선택 -> {result.JumpTarget}");
                    }
                    else
                    {
                        Debug.LogError($"[Choice] 점프 대상 '{result.JumpTarget}'을 찾을 수 없습니다.");
                    }
                }
            }
            else
            {
                Debug.Log($"[Choice] {options.Count}개 선택지 (첫 번째 자동 선택)");
                if (options.Count > 0 && !string.IsNullOrEmpty(options[0].JumpTarget))
                {
                    if (_lineIndex().TryGetValue(options[0].JumpTarget, out int targetIndex))
                        _setCurrentIndex(targetIndex - 1);
                }
            }

            _setAutoMode(wasAutoMode);
            if (wasAutoMode)
                _onAutoModeChanged?.Invoke(true);

            return true;
        }
    }
}
