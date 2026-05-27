using System;
using LoveAlgo.Contracts;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.UI;
using LoveAlgo.Story.StoryEngine.Handlers;
using LoveAlgo.Story.StoryEngine.Flow;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 스크립트 실행 엔진 — 라인 디스패치 + 실행 루프만 담당
    /// </summary>
    public class ScriptEngine
    {
        readonly Func<List<ScriptLine>> _getLines;
        readonly Func<Dictionary<string, int>> _getLineIndex;
        readonly Func<int> _getCurrentIndex;
        readonly Action<int> _setCurrentIndex;
        readonly Func<bool> _getAutoMode;
        readonly Action<bool> _setAutoMode;
        readonly Action<bool> _onAutoModeChanged;
        readonly Action<string> _onLogEntry;
        readonly Func<bool> _getWaitingForClick;
        readonly Action<bool> _setWaitingForClick;
        readonly Func<bool> _getClickReceived;
        readonly Action<bool> _setClickReceived;
        readonly Func<float> _getAutoDelayBase;

        TextLineExecutor _textExecutor;
        ChoiceLineExecutor _choiceExecutor;
        bool _needsPreTextBeat;

        public ScriptEngine(
            Func<List<ScriptLine>> getLines,
            Func<Dictionary<string, int>> getLineIndex,
            Func<int> getCurrentIndex,
            Action<int> setCurrentIndex,
            Func<bool> getAutoMode,
            Action<bool> setAutoMode,
            Action<bool> onAutoModeChanged,
            Func<bool> getWaitingForClick,
            Action<bool> setWaitingForClick,
            Func<bool> getClickReceived,
            Action<bool> setClickReceived,
            Func<float> getAutoDelayBase = null,
            Action<string> onLogEntry = null)
        {
            _getLines = getLines;
            _getLineIndex = getLineIndex;
            _getCurrentIndex = getCurrentIndex;
            _setCurrentIndex = setCurrentIndex;
            _getAutoMode = getAutoMode;
            _setAutoMode = setAutoMode;
            _onAutoModeChanged = onAutoModeChanged;
            _getWaitingForClick = getWaitingForClick;
            _setWaitingForClick = setWaitingForClick;
            _getClickReceived = getClickReceived;
            _setClickReceived = setClickReceived;
            _getAutoDelayBase = getAutoDelayBase ?? (() => 1.5f);
            _onLogEntry = onLogEntry;

            _textExecutor = new TextLineExecutor();
            _choiceExecutor = new ChoiceLineExecutor(
                CollectOptions, getLineIndex, getCurrentIndex, setCurrentIndex,
                getAutoMode, setAutoMode, onAutoModeChanged);
        }

        /// <summary>
        /// 라인 실행 — 레지스트리에서 해당 타입의 실행기를 찾아 위임
        /// </summary>
        public async UniTask<bool> ExecuteLineAsync(ScriptLine line, CancellationToken ct)
        {
            if (line.Type == LineType.Option)
                return true;

            if (line.Type == LineType.Text)
                _textExecutor.MarkPreTextBeat();

            if (line.Type == LineType.Choice)
                return await _choiceExecutor.ExecuteAsync(line, ct);

            if (line.Type == LineType.Flow)
                return await ExecuteFlowAsync(line, ct);

            if (LineHandlerRegistry.TryGet(line.Type, out var executor))
                return await executor.ExecuteAsync(line, ct);

            Debug.LogWarning($"[ScriptEngine] 알 수 없는 LineType: {line.Type}");
            return true;
        }

        async UniTask<bool> ExecuteFlowAsync(ScriptLine line, CancellationToken ct)
        {
            var parts = line.Value.Split(':');
            var command = parts[0];
            var lineIndex = _getLineIndex();
            int curIdx = _getCurrentIndex();

            switch (command)
            {
                case "Jump":
                    Flow.JumpFlowCommand.Execute(parts, lineIndex, ref curIdx);
                    _setCurrentIndex(curIdx);
                    return true;

                case "If":
                    Flow.IfFlowCommand.Execute(line.Value, lineIndex, ref curIdx);
                    _setCurrentIndex(curIdx);
                    return true;

                case "LoadingScene":
                    await Flow.LoadingSceneFlowCommand.ExecuteAsync(parts, ct);
                    return true;

                case "MiniGame":
                    await Flow.MiniGameFlowCommand.ExecuteAsync(parts, ct);
                    return true;

                case "Save":
                    if (LoveAlgo.Core.GameManager.Instance != null)
                        await LoveAlgo.Core.GameManager.Instance.AutoSaveAsync("scripted");
                    return true;

                case "Schedule":
                    await Flow.ScheduleFlowCommand.ExecuteAsync(ct);
                    return true;

                case "Username":
                    await Flow.UsernameFlowCommand.ExecuteAsync(ct);
                    return true;

                case "Day":
                    await Flow.DayFlowCommand.ExecuteAsync(parts, ct);
                    return true;

                case "End":
                    return false;

                case "Affinity":
                    Flow.AffinityFlowCommand.Execute(parts);
                    return true;

                case "LockScreen":
                    await Flow.LockScreenFlowCommand.ExecuteAsync(parts, ct);
                    return true;

                case "Message":
                    await Flow.MessageFlowCommand.ExecuteAsync(parts, ct);
                    return true;

                case "Mark":
                    // 무대 합성·점프 시스템 전용 메타 라인. 실행 시점엔 no-op.
                    Flow.MarkFlowCommand.Execute(parts);
                    return true;

                default:
                    Debug.LogWarning($"[Flow] 알 수 없는 Flow 명령: {command}");
                    return true;
            }
        }

        /// <summary>
        /// Next 처리
        /// </summary>
        public async UniTask HandleNextAsync(ScriptLine line, CancellationToken ct)
        {
            if (line.Type == LineType.Choice)
                return;

            switch (line.NextType)
            {
                case NextType.Immediate:
                    break;
                case NextType.Click:
                    await WaitForClickAsync(ct);
                    break;
                case NextType.Await:
                    if (line.Type == LineType.Place)
                    {
                        var placeUI = PopupManager.Instance?.Get<PlaceNotification>();
                        if (placeUI != null && placeUI.IsShowing)
                            await UniTask.WaitUntil(() => !placeUI.IsShowing, cancellationToken: ct);
                    }
                    break;
                case NextType.Delay:
                    await UniTask.Delay(TimeSpan.FromSeconds(line.DelaySeconds), cancellationToken: ct);
                    break;
            }
        }

        /// <summary>
        /// 시각 연출 후 대사 시작 전 호흡 플래그 설정
        /// </summary>
        public void MarkPreTextBeatIfNeeded(ScriptLine line)
        {
            switch (line.Type)
            {
                case LineType.Char:
                case LineType.BG:
                case LineType.CG:
                case LineType.SD:
                case LineType.Overlay:
                case LineType.FX:
                case LineType.Place:
                    _textExecutor.MarkPreTextBeat();
                    break;
            }
        }

        /// <summary>
        /// 클릭 대기 (Auto 모드 시 딜레이 후 자동 진행)
        /// </summary>
        async UniTask WaitForClickAsync(CancellationToken ct)
        {
            // Headless 자동화: 클릭 대기를 즉시 통과 (ADR §진입점별 헤드리스 규약).
            // 일반 플레이는 IsEnabled=false라 영향 없음.
            if (Headless.IsEnabled)
            {
                await UniTask.Yield(ct);
                return;
            }

            var dialogueUI = ExecutionDependencies.DialogueUI;
            int textLen = dialogueUI?.LastDisplayedTextLength ?? 0;
            float autoDelayBase = _getAutoDelayBase();
            float autoDelayPerCharacter = 0.03f;
            float autoDelayMin = 0.5f;
            float autoDelayMax = 8.0f;

            _setWaitingForClick(true);
            _setClickReceived(false);

            if (_getAutoMode())
            {
                float dynamicDelay = autoDelayBase + (textLen * autoDelayPerCharacter);
                dynamicDelay = Mathf.Clamp(dynamicDelay, autoDelayMin, autoDelayMax);

                var delayTask = UniTask.Delay(TimeSpan.FromSeconds(dynamicDelay), cancellationToken: ct);
                var clickTask = UniTask.WaitUntil(() => _getClickReceived(), cancellationToken: ct);
                await UniTask.WhenAny(delayTask, clickTask);

                var popupMgr = PopupManager.Instance;
                if (popupMgr != null && popupMgr.IsAnyPopupOpen)
                    await UniTask.WaitUntil(() => !popupMgr.IsAnyPopupOpen, cancellationToken: ct);
            }
            else
            {
                await UniTask.WaitUntil(() => _getClickReceived(), cancellationToken: ct);
            }

            _setWaitingForClick(false);
        }

        /// <summary>
        /// Option 라인 수집
        /// </summary>
        List<ScriptLine> CollectOptions()
        {
            var options = new List<ScriptLine>();
            var lines = _getLines();
            int i = _getCurrentIndex() + 1;

            while (i < lines.Count && lines[i].Type == LineType.Option)
            {
                options.Add(lines[i]);
                i++;
            }

            _setCurrentIndex(i - 1);
            return options;
        }

        /// <summary>
        /// 되감기를 위한 이전 Text 인덱스 찾기
        /// </summary>
        public int FindPreviousTextIndex(int fromIndex, int textCount)
        {
            var lines = _getLines();
            int foundCount = 0;
            int resultIndex = fromIndex;

            for (int i = fromIndex; i >= 0; i--)
            {
                if (lines[i].Type == LineType.Text)
                {
                    foundCount++;
                    if (foundCount >= textCount)
                    {
                        resultIndex = i;
                        break;
                    }
                    resultIndex = i;
                }
            }

            return resultIndex;
        }

        /// <summary>
        /// 연출 시작점 찾기
        /// </summary>
        public int FindDirectionStartIndex(int textIndex)
        {
            var lines = _getLines();
            int startIndex = textIndex;

            for (int i = textIndex - 1; i >= 0; i--)
            {
                var type = lines[i].Type;
                if (type == LineType.BG || type == LineType.Char ||
                    type == LineType.Sound || type == LineType.FX)
                {
                    startIndex = i;
                }
                else
                {
                    break;
                }
            }

            return startIndex;
        }

        /// <summary>
        /// 로드/점프 시 로그 + 오디오 상태 복원
        /// </summary>
        public void RebuildLogFromPreviousLines(int targetIndex)
        {
            var dialogueUI = ExecutionDependencies.DialogueUI;
            var lines = _getLines();
            if (dialogueUI == null || lines == null) return;

            int startIdx = 0;
            for (int i = targetIndex - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line.Type == LineType.FX && line.Value != null)
                {
                    string val = line.Value;
                    if (val.StartsWith("SceneStart", System.StringComparison.OrdinalIgnoreCase) ||
                        val.StartsWith("SceneEnd", System.StringComparison.OrdinalIgnoreCase))
                    {
                        startIdx = i + 1;
                        break;
                    }
                }
                if (line.Type == LineType.Flow && line.Value != null &&
                    line.Value.Equals("LoadingScene", System.StringComparison.OrdinalIgnoreCase))
                {
                    startIdx = i + 1;
                    break;
                }
            }

            // ── BGM 상태 복원: 점프 지점 이전의 마지막 Sound:BGM 명령을 찾아 반영 ──
            // - 같은 BGM이면 끊지 않고 그대로 흐르게 (연속성).
            // - 다른 BGM이면 0.5s 페이드로 전환.
            // - 가장 최근 명령이 Stop이면 BGM 정지(이전 BGM이 새 컨텍스트로 새는 것 방지).
            string lastBGM = null;
            bool lastWasStop = false;
            for (int i = targetIndex - 1; i >= startIdx; i--)
            {
                var line = lines[i];
                if (line.Type == LineType.Sound && line.Value != null &&
                    line.Value.StartsWith("BGM:", System.StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Value.Split(':');
                    if (parts.Length >= 2)
                    {
                        if (parts[1].Equals("Stop", System.StringComparison.OrdinalIgnoreCase))
                            lastWasStop = true;
                        else
                            lastBGM = parts[1];
                    }
                    break; // 가장 최근 BGM 명령만 필요
                }
            }

            var audio = ExecutionDependencies.Audio;
            if (audio != null)
            {
                if (lastWasStop)
                {
                    if (!string.IsNullOrEmpty(audio.CurrentBGM))
                    {
                        audio.StopBGMAsync(0.5f).Forget();
                        Debug.Log("[ScriptEngine] BGM 정지 복원");
                    }
                }
                else if (lastBGM != null && audio.CurrentBGM != lastBGM)
                {
                    audio.PlayBGMAsync(lastBGM, 0.5f).Forget();
                    Debug.Log($"[ScriptEngine] BGM 복원: {lastBGM}");
                }
                // lastBGM == null && !lastWasStop: 새 컨텍스트에 BGM 지시가 없으므로 현재 BGM 유지.
            }

            // ── 대사 로그 복원 ──
            for (int i = startIdx; i < targetIndex; i++)
            {
                var line = lines[i];
                if (line.Type == LineType.Text && !string.IsNullOrEmpty(line.Value))
                    dialogueUI.AddLogEntry(line.Speaker, line.Value);
            }

            Debug.Log($"[ScriptEngine] 로그 복원: {startIdx}~{targetIndex - 1} 구간, {dialogueUI.DialogueLog.Count}개 항목, BGM: {lastBGM ?? "없음"}");
        }
    }
}
