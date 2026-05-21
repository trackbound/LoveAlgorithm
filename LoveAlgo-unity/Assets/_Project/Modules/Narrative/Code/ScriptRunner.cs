using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.UI;
using LoveAlgo.Core;
using LoveAlgo.Story.StoryEngine;
using LoveAlgo.Story.StoryEngine.Handlers;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스토리 스크립트 실행기 (리팩토링됨 — 실제 실행은 StoryEngine에 위임)
    /// </summary>
    public class ScriptRunner : SingletonMonoBehaviour<ScriptRunner>
    {
        [Header("스크립트")]
        [SerializeField] TextAsset scriptAsset;

        List<ScriptLine> lines;
        Dictionary<string, int> lineIndex;
        int currentIndex;
        bool isRunning;
        CancellationTokenSource cts;
        readonly object ctsLock = new();
        string currentScriptName;

        bool waitingForClick;
        bool clickReceived;

        bool autoMode;
        public event Action<bool> OnAutoModeChanged;
        float autoDelayBase = 1.5f;

        public event Action OnScriptEnd;
        public bool IsAutoMode => autoMode;
        /// <summary>텍스트 클릭 대기 중인지 (UI 입력 가드용)</summary>
        public bool IsWaitingForClick => waitingForClick;

        ScriptEngine _engine;

        /// <summary>스토리 CSV 캐시 — 같은 스크립트 반복 로드 시 Resources.Load 비용 회피.</summary>
        readonly Dictionary<string, TextAsset> _scriptCache = new();

        TextAsset LoadScriptAsset(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName)) return null;
            if (_scriptCache.TryGetValue(scriptName, out var cached) && cached != null)
                return cached;
            var asset = Resources.Load<TextAsset>($"Story/{scriptName}");
            if (asset != null) _scriptCache[scriptName] = asset;
            return asset;
        }

        public void SetAutoDelay(float normalized)
        {
            // 0=느림(6초 기본 딜레이), 1=빠름(0.3초) — 슬라이더 전 구간에서 차이 체감
            autoDelayBase = Mathf.Lerp(6.0f, 0.3f, normalized);
        }

        public string CurrentScriptName => currentScriptName;

        protected override void OnSingletonAwake()
        {
            float savedAutoSpeed = PlayerPrefs.GetFloat("AutoSpeed", GameConstants.DefaultAutoSpeed);
            SetAutoDelay(savedAutoSpeed);

            if (scriptAsset != null)
                LoadScript(scriptAsset);
        }

        void Start()
        {
            ExecutionDependencies.Reset();
            _engine = new ScriptEngine(
                () => lines,
                () => lineIndex,
                () => currentIndex,
                idx => currentIndex = idx,
                () => autoMode,
                v => autoMode = v,
                v => OnAutoModeChanged?.Invoke(v),
                () => waitingForClick,
                v => waitingForClick = v,
                () => clickReceived,
                v => clickReceived = v,
                () => autoDelayBase);

            LineHandlerRegistry.Register(new TextLineExecutor());
            LineHandlerRegistry.Register(new CharLineExecutor());
            LineHandlerRegistry.Register(new BGLineExecutor());
            LineHandlerRegistry.Register(new CGLineExecutor());
            LineHandlerRegistry.Register(new SDLineExecutor());
            LineHandlerRegistry.Register(new OverlayLineExecutor());
            LineHandlerRegistry.Register(new SoundLineExecutor());
            LineHandlerRegistry.Register(new FXLineExecutor());
            LineHandlerRegistry.Register(new PlaceLineExecutor());

            var dialogueUI = ExecutionDependencies.DialogueUI;
            var stage = ExecutionDependencies.Stage;
            if (dialogueUI != null && stage?.Character != null)
                dialogueUI.OnEmoteTag = emoteValue =>
                {
                    // 슬롯 지정 형식: "L:EyeSmile", "R:Sad" / 미지정: "EyeSmile" → 기본 C
                    int sep = emoteValue.IndexOf(':');
                    if (sep > 0 && sep <= 2)
                    {
                        string slot = emoteValue.Substring(0, sep);
                        string emote = emoteValue.Substring(sep + 1);
                        stage.CharacterEmote(slot, emote);
                    }
                    else
                    {
                        stage.CharacterEmote("C", emoteValue);
                    }
                };
        }

        protected override void OnDestroy()
        {
            // ScriptRunner가 Start에서 등록한 OnEmoteTag 콜백 해제.
            // DialogueUI가 더 오래 살아남는 경우 죽은 람다 호출 방지.
            var dialogueUI = ExecutionDependencies.DialogueUI;
            if (dialogueUI != null) dialogueUI.OnEmoteTag = null;

            base.OnDestroy();
            Stop();
        }

        public void LoadScript(TextAsset asset)
        {
            lines = ScriptParser.Parse(asset);
            lineIndex = ScriptParser.BuildLineIndex(lines);
            currentIndex = 0;
            currentScriptName = asset.name;
            StoryEngine.LineHandlerRegistry.ResetAllExecutorState();
        }

        public void LoadScript(string csv, string scriptName = null)
        {
            lines = ScriptParser.Parse(csv);
            lineIndex = ScriptParser.BuildLineIndex(lines);
            currentIndex = 0;
            if (!string.IsNullOrEmpty(scriptName))
                currentScriptName = scriptName;
            StoryEngine.LineHandlerRegistry.ResetAllExecutorState();
        }

        public async UniTask StartScript(string scriptName)
        {
            var asset = LoadScriptAsset(scriptName);
            if (asset == null)
            {
                Debug.LogError($"[ScriptRunner] 스크립트 '{scriptName}'를 찾을 수 없습니다.");
                return;
            }
            currentScriptName = scriptName;
            LoadScript(asset);
            Run();
            await UniTask.WaitUntil(() => !isRunning);
        }

        public async UniTask StartScriptFrom(string scriptName, string lineId, int lineIdx)
        {
            var asset = LoadScriptAsset(scriptName);
            if (asset == null)
            {
                Debug.LogError($"[ScriptRunner] 스크립트 '{scriptName}' 없음");
                return;
            }

            currentScriptName = scriptName;
            LoadScript(asset);

            if (!string.IsNullOrEmpty(lineId) && lineIndex.TryGetValue(lineId, out int idx))
            {
                _engine.RebuildLogFromPreviousLines(idx);
                Stop();
                currentIndex = idx;
                cts = new CancellationTokenSource();
                isRunning = true;
                RunAsync(cts.Token).Forget();
            }
            else if (lineIdx > 0 && lineIdx < lines.Count)
            {
                _engine.RebuildLogFromPreviousLines(lineIdx);
                Stop();
                currentIndex = lineIdx;
                cts = new CancellationTokenSource();
                isRunning = true;
                RunAsync(cts.Token).Forget();
            }
            else if (lineIdx >= lines.Count)
            {
                Debug.Log($"[ScriptRunner] StartScriptFrom: 스크립트 이미 완료 (index={lineIdx}, total={lines.Count})");
                isRunning = false;
            }
            else
            {
                Run();
            }

            await UniTask.WaitUntil(() => !isRunning);
        }

        public void Run()
        {
            if (lines == null || lines.Count == 0)
            {
                Debug.LogWarning("[ScriptRunner] 로드된 스크립트가 없습니다.");
                return;
            }

            Stop();
            currentIndex = 0;
            cts = new CancellationTokenSource();
            isRunning = true;
            RunAsync(cts.Token).Forget();
        }

        public void RunFrom(string lineId)
        {
            if (lineIndex == null)
            {
                Debug.LogError("[ScriptRunner] 로드된 스크립트가 없습니다. RunFrom 호출 불가.");
                return;
            }

            if (lineIndex.TryGetValue(lineId, out int index))
            {
                Stop();
                currentIndex = index;
                cts = new CancellationTokenSource();
                isRunning = true;
                RunAsync(cts.Token).Forget();
            }
            else
            {
                Debug.LogError($"[ScriptRunner] LineID '{lineId}'를 찾을 수 없습니다.");
            }
        }

        public void Stop()
        {
            isRunning = false;

            CancellationTokenSource oldCts;
            lock (ctsLock)
            {
                oldCts = cts;
                cts = null;
            }
            oldCts?.Cancel();
            oldCts?.Dispose();

            var dialogueUI = ExecutionDependencies.DialogueUI;
            var stage = ExecutionDependencies.Stage;
            dialogueUI?.ResetMonologueState();
            var monologueDim = stage?.MonologueDim;
            if (monologueDim != null && monologueDim.IsShowing)
                monologueDim.HideImmediate();

            ExecutionDependencies.Audio?.StopVoice();
        }

        public int CurrentIndex => currentIndex;
        public bool IsRunning => isRunning;

        public ScriptLine CurrentLine =>
            lines != null && currentIndex >= 0 && currentIndex < lines.Count
            ? lines[currentIndex]
            : null;

        public int LineCount => lines?.Count ?? 0;

        public ScriptLine GetLine(int index) =>
            lines != null && index >= 0 && index < lines.Count ? lines[index] : null;

        public void JumpToIndex(int index)
        {
            if (lines == null || index < 0 || index >= lines.Count)
            {
                Debug.LogWarning($"[ScriptRunner] JumpToIndex: 범위 초과 ({index}/{lines?.Count ?? 0})");
                return;
            }

            Stop();
            // CG enter→exit 등 페어 상태가 점프 사이로 새지 않도록 폐기
            StoryEngine.LineHandlerRegistry.ResetAllExecutorState();
            currentIndex = index;
            cts = new CancellationTokenSource();
            isRunning = true;
            RunAsync(cts.Token).Forget();
        }

        public void Rewind(int textCount = 1)
        {
            if (lines == null || lines.Count == 0) return;

            int targetTextIndex = _engine.FindPreviousTextIndex(currentIndex, textCount);
            int startIndex = _engine.FindDirectionStartIndex(targetTextIndex);

            Debug.Log($"[ScriptRunner] Rewind: {textCount}개 Text 전 → index {startIndex} 부터 재생");

            Stop();
            currentIndex = startIndex;
            cts = new CancellationTokenSource();
            isRunning = true;
            RunAsync(cts.Token).Forget();
        }

        public void OnClick()
        {
            var dialogueUI = ExecutionDependencies.DialogueUI;
            if (dialogueUI != null && dialogueUI.IsTyping)
            {
                dialogueUI.RequestSkip();
                return;
            }

            if (waitingForClick)
            {
                clickReceived = true;
            }
        }

        public void ToggleAutoMode()
        {
            autoMode = !autoMode;
            Debug.Log($"[ScriptRunner] Auto Mode: {autoMode}");
            OnAutoModeChanged?.Invoke(autoMode);

            if (autoMode && waitingForClick)
                clickReceived = true;
        }

        public void SetAutoMode(bool enabled)
        {
            autoMode = enabled;
            OnAutoModeChanged?.Invoke(autoMode);
            if (autoMode && waitingForClick)
                clickReceived = true;
        }

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            Debug.Log("[ScriptRunner] 스크립트 실행 시작");

            while (isRunning && currentIndex < lines.Count)
            {
                ct.ThrowIfCancellationRequested();

                var line = lines[currentIndex];
                Debug.Log($"[ScriptRunner] [{currentIndex}] {line}");

                bool shouldContinue = await _engine.ExecuteLineAsync(line, ct);

                if (!shouldContinue)
                    break;

                await _engine.HandleNextAsync(line, ct);
                _engine.MarkPreTextBeatIfNeeded(line);

                currentIndex++;
            }

            isRunning = false;
            Debug.Log("[ScriptRunner] 스크립트 실행 종료");
            OnScriptEnd?.Invoke();
        }
    }
}
