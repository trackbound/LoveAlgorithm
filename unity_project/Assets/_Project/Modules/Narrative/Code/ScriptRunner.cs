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
            base.OnDestroy();
            Stop();
        }

        public void LoadScript(TextAsset asset)
        {
            lines = ScriptParser.Parse(asset);
            lineIndex = ScriptParser.BuildLineIndex(lines);
            currentIndex = 0;
            currentScriptName = asset.name;
            MarkRegistry.Rebuild(lines);
        }

        public void LoadScript(string csv, string scriptName = null)
        {
            lines = ScriptParser.Parse(csv);
            lineIndex = ScriptParser.BuildLineIndex(lines);
            currentIndex = 0;
            if (!string.IsNullOrEmpty(scriptName))
                currentScriptName = scriptName;
            MarkRegistry.Rebuild(lines);
        }

        public async UniTask StartScript(string scriptName)
        {
            // StreamingAssets/Story/{scriptName}.csv 로드 (빌드 후 외부·내부 편집 가능)
            string csv = await StoryAssetLoader.LoadCsvAsync(scriptName);
            if (string.IsNullOrEmpty(csv))
            {
                Debug.LogError($"[ScriptRunner] 스크립트 '{scriptName}'를 찾을 수 없습니다.");
                return;
            }
            currentScriptName = scriptName;
            LoadScript(csv, scriptName);
            Run();
            await UniTask.WaitUntil(() => !isRunning);
        }

        public async UniTask StartScriptFrom(string scriptName, string lineId, int lineIdx)
            => await StartScriptFromInternal(scriptName, lineId, lineIdx, withStageSync: false);

        /// <summary>
        /// 디버그 점프용: 스크립트 로드 → 0..target 무대 상태 합성·복원 → 실행.
        /// 중간 라인부터 시작해도 누적된 BG/Char/BGM/CG/SD/Overlay가 표시됨.
        /// </summary>
        public UniTask StartScriptFromWithStageSync(string scriptName, string lineId, int lineIdx)
            => StartScriptFromInternal(scriptName, lineId, lineIdx, withStageSync: true);

        async UniTask StartScriptFromInternal(string scriptName, string lineId, int lineIdx, bool withStageSync)
        {
            // StreamingAssets/Story/{scriptName}.csv 로드
            string csv = await StoryAssetLoader.LoadCsvAsync(scriptName);
            if (string.IsNullOrEmpty(csv))
            {
                Debug.LogError($"[ScriptRunner] 스크립트 '{scriptName}' 없음");
                return;
            }

            currentScriptName = scriptName;
            LoadScript(csv, scriptName);

            if (!string.IsNullOrEmpty(lineId) && lineIndex.TryGetValue(lineId, out int idx))
            {
                _engine.RebuildLogFromPreviousLines(idx);
                if (withStageSync)
                {
                    var data = StageStateSynthesizer.Synthesize(lines, idx - 1);
                    await LoveAlgo.Story.SaveSystem.StageRestorer.RestoreAsync(data);
                }
                Stop();
                currentIndex = idx;
                cts = new CancellationTokenSource();
                isRunning = true;
                RunAsync(cts.Token).Forget();
            }
            else if (lineIdx > 0 && lineIdx < lines.Count)
            {
                _engine.RebuildLogFromPreviousLines(lineIdx);
                if (withStageSync)
                {
                    var data = StageStateSynthesizer.Synthesize(lines, lineIdx - 1);
                    await LoveAlgo.Story.SaveSystem.StageRestorer.RestoreAsync(data);
                }
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

        bool _explicitlyStopped;

        public void Stop()
        {
            _explicitlyStopped = true;  // 자연 종료와 구분 — RunAsync가 OnScriptEnd 발화 안 함
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
            currentIndex = index;
            cts = new CancellationTokenSource();
            isRunning = true;
            RunAsync(cts.Token).Forget();
        }

        /// <summary>
        /// 0~(index-1) 라인을 forward-scan해서 누적된 무대 상태를 합성·즉시 복원한 뒤 index 라인부터 실행.
        /// 중간 라인 점프 시 빈 화면 문제 방지.
        ///
        /// <param name="withFade">true(기본)면 검은 페이드 아웃 → 복원 → 페이드 인.
        ///   비동기 복원 중 부분 상태(BGM 크로스페이드, 캐릭터 페이드인 등 100~700ms)가 보이지 않도록 가림.
        ///   편집기 [저장&적용]처럼 화면이 IMGUI에 가려진 상태에서는 false 권장.</param>
        /// </summary>
        public async UniTask JumpWithStateSyncAsync(int index, bool withFade = true)
        {
            if (lines == null || index < 0 || index >= lines.Count)
            {
                Debug.LogWarning($"[ScriptRunner] JumpWithStateSync: 범위 초과 ({index}/{lines?.Count ?? 0})");
                return;
            }

            Stop();

            // 페이드 아웃 — 부분 상태가 보이지 않도록 검은 화면으로
            var fx = ScreenFX.Instance;
            if (withFade && fx != null && !fx.IsFadeBlack)
            {
                StageSyncLog.Section("StageSync", "fade-out 0.25s");
                await fx.FadeOutAsync(0.25f);
            }

            // 타겟 직전까지의 누적 무대 상태 합성 + 복원
            var data = StageStateSynthesizer.Synthesize(lines, index - 1);
            await LoveAlgo.Story.SaveSystem.StageRestorer.RestoreAsync(data);

            // 타겟 라인부터 실행
            currentIndex = index;
            cts = new CancellationTokenSource();
            isRunning = true;
            RunAsync(cts.Token).Forget();

            // 페이드 인
            if (withFade && fx != null)
            {
                StageSyncLog.Section("StageSync", "fade-in 0.35s");
                await fx.FadeInAsync(0.35f);
            }
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
            _explicitlyStopped = false;  // 새 실행 시작 — 플래그 리셋
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

            // OnScriptEnd는 자연 종료(라인 끝까지 진행) 또는 Flow:End 명시 종료에만 발화.
            // 외부 Stop()으로 인한 중단(편집기 토글, 점프, 로드 등)에서는 발화 금지 —
            // 안 그러면 Prologue OnScriptEnd 핸들러가 잘못 트리거되어 Title로 복귀하는 버그 발생.
            if (_explicitlyStopped)
            {
                Debug.Log("[ScriptRunner] 스크립트 실행 중단 (외부 Stop) — OnScriptEnd 발화 안 함");
            }
            else
            {
                Debug.Log("[ScriptRunner] 스크립트 실행 종료");
                OnScriptEnd?.Invoke();
            }
        }
    }
}
