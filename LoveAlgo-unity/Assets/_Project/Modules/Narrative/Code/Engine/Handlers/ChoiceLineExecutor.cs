using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
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

            // 자동 선택 경로 — UI 없거나 Headless면 첫 선택지를 즉시 선택 (ADR §ChoiceLineExecutor).
            // 두 경로(UI null vs Headless toggle)가 같은 자동 분기로 통합.
            var choiceUI = UIManager.Instance?.ChoicePopup;
            bool useAutoFirst = choiceUI == null || Headless.IsEnabled;

            if (!useAutoFirst)
            {
                var result = await choiceUI.ShowAndWaitAsync(options, ct);
                if (result != null && !string.IsNullOrEmpty(result.JumpTarget))
                {
                    // 라벨 검증을 먼저 — 미존재 라벨이면 ChoiceHistory를 dirty화하지 않음
                    if (_lineIndex().TryGetValue(result.JumpTarget, out int targetIndex))
                    {
                        GameState.Instance?.AddChoice(result.JumpTarget);
                        _setCurrentIndex(targetIndex - 1);
                        Log.Info($"[Choice] 선택 -> {result.JumpTarget}");
                    }
                    else
                    {
                        Debug.LogError($"[Choice] 점프 대상 '{result.JumpTarget}'을 찾을 수 없습니다. (ChoiceHistory 기록 안 함)");
                    }
                }
            }
            else
            {
                string reason = Headless.IsEnabled ? "headless" : "UI 없음";
                Log.Info($"[Choice] {options.Count}개 선택지 ({reason} → 첫 번째 자동 선택)");
                if (options.Count > 0 && !string.IsNullOrEmpty(options[0].JumpTarget))
                {
                    if (_lineIndex().TryGetValue(options[0].JumpTarget, out int targetIndex))
                    {
                        GameState.Instance?.AddChoice(options[0].JumpTarget);
                        _setCurrentIndex(targetIndex - 1);
                    }
                    else
                    {
                        Debug.LogError($"[Choice] 자동 선택 대상 '{options[0].JumpTarget}'을 찾을 수 없습니다.");
                    }
                }
            }

            _setAutoMode(wasAutoMode);
            if (wasAutoMode)
                _onAutoModeChanged?.Invoke(true);

            return true;
        }
    }
}
