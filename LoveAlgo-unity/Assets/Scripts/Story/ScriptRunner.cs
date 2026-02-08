using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.UI;
using LoveAlgo.Core;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스토리 스크립트 실행기
    /// </summary>
    public class ScriptRunner : MonoBehaviour
    {
        public static ScriptRunner Instance { get; private set; }

        [Header("스크립트")]
        [SerializeField] TextAsset scriptAsset;

        List<ScriptLine> lines;
        Dictionary<string, int> lineIndex;
        int currentIndex;
        bool isRunning;
        CancellationTokenSource cts;
        string currentScriptName;  // 현재 실행 중인 스크립트명 (저장용)

        // 외부에서 클릭 입력 받을 때 사용
        bool waitingForClick;
        bool clickReceived;

        // Auto 모드
        bool autoMode;
        float autoDelayBase = 1.5f;           // 기본 딜레이
        float autoDelayPerCharacter = 0.05f;  // 글자당 추가 시간
        float autoDelayMin = 1.0f;
        float autoDelayMax = 5.0f;

        public event Action OnScriptEnd;

        // Auto 모드 프로퍼티
        public bool IsAutoMode => autoMode;
        
        /// <summary>
        /// 현재 실행 중인 스크립트명 (세이브용)
        /// </summary>
        public string CurrentScriptName => currentScriptName;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            if (scriptAsset != null)
            {
                LoadScript(scriptAsset);
            }
        }

        void Start()
        {
            // 인라인 Emote 태그 콜백 연결 (UIManager, StageManager 초기화 후)
            var dialogueUI = UIManager.Instance?.DialogueUI;
            var stage = StageManager.Instance;
            if (dialogueUI != null && stage?.Character != null)
            {
                dialogueUI.OnEmoteTag = emote => stage.CharacterEmote("C", emote);
            }
        }

        void OnDestroy()
        {
            Stop();
        }

        /// <summary>
        /// 스크립트 로드
        /// </summary>
        public void LoadScript(TextAsset asset)
        {
            lines = ScriptParser.Parse(asset);
            lineIndex = ScriptParser.BuildLineIndex(lines);
            currentIndex = 0;
            currentScriptName = asset.name;  // 스크립트명 저장 (세이브용)
        }

        /// <summary>
        /// 스크립트 로드 (문자열)
        /// </summary>
        public void LoadScript(string csv)
        {
            lines = ScriptParser.Parse(csv);
            lineIndex = ScriptParser.BuildLineIndex(lines);
            currentIndex = 0;
        }

        /// <summary>
        /// 스크립트 로드 후 실행 (Resources 폴더에서)
        /// </summary>
        public async UniTask StartScript(string scriptName)
        {
            var asset = Resources.Load<TextAsset>($"Story/{scriptName}");
            if (asset == null)
            {
                Debug.LogError($"[ScriptRunner] 스크립트 '{scriptName}'를 찾을 수 없습니다. (Resources/Story/{scriptName})");
                return;
            }
            currentScriptName = scriptName;  // 스크립트명 저장 (세이브용)
            LoadScript(asset);
            Run();
            // 완료될 때까지 대기
            await UniTask.WaitUntil(() => !isRunning);
        }

        /// <summary>
        /// 스크립트 로드 후 특정 위치부터 실행 (로드용)
        /// LineId 우선, 없으면 인덱스 사용
        /// </summary>
        public async UniTask StartScriptFrom(string scriptName, string lineId, int lineIdx)
        {
            var asset = Resources.Load<TextAsset>($"Story/{scriptName}");
            if (asset == null)
            {
                Debug.LogError($"[ScriptRunner] 스크립트 '{scriptName}' 없음 (Resources/Story/{scriptName})");
                return;
            }

            currentScriptName = scriptName;
            LoadScript(asset);

            // LineId가 있으면 LineId로 점프
            if (!string.IsNullOrEmpty(lineId) && lineIndex.TryGetValue(lineId, out int idx))
            {
                Stop();
                currentIndex = idx;
                cts = new CancellationTokenSource();
                isRunning = true;
                RunAsync(cts.Token).Forget();
            }
            else if (lineIdx > 0 && lineIdx < lines.Count)
            {
                Stop();
                currentIndex = lineIdx;
                cts = new CancellationTokenSource();
                isRunning = true;
                RunAsync(cts.Token).Forget();
            }
            else if (lineIdx >= lines.Count)
            {
                // 스크립트 끝까지 진행된 상태 → 실행하지 않음
                Debug.Log($"[ScriptRunner] StartScriptFrom: 스크립트 이미 완료 (index={lineIdx}, total={lines.Count})");
                isRunning = false;
            }
            else
            {
                Run();
            }

            // 완료될 때까지 대기
            await UniTask.WaitUntil(() => !isRunning);
        }

        /// <summary>
        /// 스크립트 실행 시작
        /// </summary>
        public void Run()
        {
            if (lines == null || lines.Count == 0)
            {
                Debug.LogWarning("[ScriptRunner] 로드된 스크립트가 없습니다.");
                return;
            }

            Stop();
            currentIndex = 0;  // 처음부터 시작
            cts = new CancellationTokenSource();
            isRunning = true;
            RunAsync(cts.Token).Forget();
        }

        /// <summary>
        /// 특정 LineID부터 실행
        /// </summary>
        public void RunFrom(string lineId)
        {
            if (lineIndex.TryGetValue(lineId, out int index))
            {
                Stop();
                currentIndex = index;  // Run() 호출 전에 설정
                cts = new CancellationTokenSource();
                isRunning = true;
                RunAsync(cts.Token).Forget();
            }
            else
            {
                Debug.LogError($"[ScriptRunner] LineID '{lineId}'를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 실행 중지
        /// </summary>
        public void Stop()
        {
            isRunning = false;
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        /// <summary>
        /// 현재 인덱스
        /// </summary>
        public int CurrentIndex => currentIndex;

        /// <summary>
        /// 실행 중 여부
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// 현재 라인 (디버그용)
        /// </summary>
        public ScriptLine CurrentLine => 
            lines != null && currentIndex >= 0 && currentIndex < lines.Count 
            ? lines[currentIndex] 
            : null;

        /// <summary>
        /// N개의 Text 라인 전으로 되감기 후 재생
        /// 해당 Text 직전의 연출(BG, Char, Sound 등)부터 실행
        /// </summary>
        public void Rewind(int textCount = 1)
        {
            if (lines == null || lines.Count == 0) return;

            // 현재 위치에서 N개의 Text를 찾아 거슬러 올라감
            int targetTextIndex = FindPreviousTextIndex(currentIndex, textCount);
            
            // 해당 Text 직전의 연출 시작점 찾기
            int startIndex = FindDirectionStartIndex(targetTextIndex);
            
            Debug.Log($"[ScriptRunner] Rewind: {textCount}개 Text 전 → index {startIndex} 부터 재생");
            
            // 중지 후 해당 위치부터 재실행
            Stop();
            currentIndex = startIndex;
            cts = new CancellationTokenSource();
            isRunning = true;
            RunAsync(cts.Token).Forget();
        }

        /// <summary>
        /// 현재 위치에서 N개의 Text 라인을 거슬러 올라가 해당 Text의 인덱스 반환
        /// </summary>
        int FindPreviousTextIndex(int fromIndex, int textCount)
        {
            int foundCount = 0;
            int resultIndex = fromIndex;

            // fromIndex부터 역순 탐색 (현재 라인이 Text면 포함)
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
                    resultIndex = i;  // 못 찾으면 가장 오래된 Text
                }
            }

            return resultIndex;
        }

        /// <summary>
        /// 주어진 Text 인덱스 직전의 연출(BG, Char, Sound, FX) 시작점 찾기
        /// 연속된 연출 라인의 첫 번째 인덱스 반환
        /// </summary>
        int FindDirectionStartIndex(int textIndex)
        {
            int startIndex = textIndex;

            // textIndex 직전부터 역순으로 연출 라인 탐색
            for (int i = textIndex - 1; i >= 0; i--)
            {
                var type = lines[i].Type;
                
                // 연출 타입이면 계속 거슬러 올라감
                if (type == LineType.BG || type == LineType.Char || 
                    type == LineType.Sound || type == LineType.FX)
                {
                    startIndex = i;
                }
                else
                {
                    // Text, Choice, Option, Flow 등을 만나면 중단
                    break;
                }
            }

            return startIndex;
        }

        /// <summary>
        /// 클릭 입력 (외부에서 호출)
        /// </summary>
        public void OnClick()
        {
            if (waitingForClick)
            {
                clickReceived = true;
            }
        }

        /// <summary>
        /// Auto 모드 토글
        /// </summary>
        public void ToggleAutoMode()
        {
            autoMode = !autoMode;
            Debug.Log($"[ScriptRunner] Auto Mode: {autoMode}");
            
            // Auto 모드 켜졌을 때 클릭 대기 중이면 즉시 진행
            if (autoMode && waitingForClick)
            {
                clickReceived = true;
            }
        }

        /// <summary>
        /// Auto 모드 설정
        /// </summary>
        public void SetAutoMode(bool enabled)
        {
            autoMode = enabled;
            if (autoMode && waitingForClick)
            {
                clickReceived = true;
            }
        }

        /// <summary>
        /// 메인 실행 루프
        /// </summary>
        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            Debug.Log("[ScriptRunner] 스크립트 실행 시작");

            while (isRunning && currentIndex < lines.Count)
            {
                ct.ThrowIfCancellationRequested();

                var line = lines[currentIndex];
                Debug.Log($"[ScriptRunner] [{currentIndex}] {line}");

                // Type별 실행
                bool shouldContinue = await ExecuteLineAsync(line, ct);

                if (!shouldContinue)
                {
                    // Flow:End 등으로 종료
                    break;
                }

                // Next 처리
                await HandleNextAsync(line, ct);

                currentIndex++;
            }

            isRunning = false;
            Debug.Log("[ScriptRunner] 스크립트 실행 종료");
            OnScriptEnd?.Invoke();
        }

        /// <summary>
        /// 라인 실행 (Type별 분기)
        /// </summary>
        async UniTask<bool> ExecuteLineAsync(ScriptLine line, CancellationToken ct)
        {
            switch (line.Type)
            {
                case LineType.Text:
                    await ExecuteTextAsync(line, ct);
                    break;

                case LineType.Char:
                    await ExecuteCharAsync(line, ct);
                    break;

                case LineType.BG:
                    await ExecuteBGAsync(line, ct);
                    break;

                case LineType.CG:
                    await ExecuteCGAsync(line, ct);
                    break;

                case LineType.Overlay:
                    await ExecuteOverlayAsync(line, ct);
                    break;

                case LineType.Sound:
                    await ExecuteSoundAsync(line, ct);
                    break;

                case LineType.FX:
                    await ExecuteFXAsync(line, ct);
                    break;

                case LineType.Flow:
                    return await ExecuteFlowAsync(line, ct);

                case LineType.Choice:
                    await ExecuteChoiceAsync(line, ct);
                    break;

                case LineType.Option:
                    // Option은 Choice에서 수집하므로 여기선 스킵
                    break;
            }

            return true;
        }

        /// <summary>
        /// Next 처리
        /// </summary>
        async UniTask HandleNextAsync(ScriptLine line, CancellationToken ct)
        {
            // Choice는 이미 사용자 선택을 받았으므로 Next 처리 스킵
            if (line.Type == LineType.Choice)
                return;

            switch (line.NextType)
            {
                case NextType.Immediate:
                    // 즉시 다음으로
                    break;

                case NextType.Click:
                    await WaitForClickAsync(ct);
                    break;

                case NextType.Await:
                    // ExecuteXXX에서 이미 await 처리됨
                    break;

                case NextType.Delay:
                    await UniTask.Delay(TimeSpan.FromSeconds(line.DelaySeconds), cancellationToken: ct);
                    break;
            }
        }

        /// <summary>
        /// 클릭 대기 (Auto 모드 시 딜레이 후 자동 진행)
        /// </summary>
        async UniTask WaitForClickAsync(CancellationToken ct)
        {
            waitingForClick = true;
            clickReceived = false;

            if (autoMode)
            {
                // Auto 모드: 텍스트 길이에 따라 딜레이 조절
                int textLen = UIManager.Instance?.DialogueUI?.LastDisplayedTextLength ?? 0;
                float dynamicDelay = autoDelayBase + (textLen * autoDelayPerCharacter);
                dynamicDelay = Mathf.Clamp(dynamicDelay, autoDelayMin, autoDelayMax);

                var delayTask = UniTask.Delay(TimeSpan.FromSeconds(dynamicDelay), cancellationToken: ct);
                var clickTask = UniTask.WaitUntil(() => clickReceived, cancellationToken: ct);
                await UniTask.WhenAny(delayTask, clickTask);
            }
            else
            {
                // 일반 모드: 클릭 대기
                await UniTask.WaitUntil(() => clickReceived, cancellationToken: ct);
            }

            waitingForClick = false;
            clickReceived = false;
        }

        #region Type별 실행 (TODO: 실제 구현)

        async UniTask ExecuteTextAsync(ScriptLine line, CancellationToken ct)
        {
            // 대사 UI에 텍스트 표시
            var dialogueUI = UIManager.Instance?.DialogueUI;
            if (dialogueUI != null)
            {
                await dialogueUI.ShowTextAsync(line.Speaker, line.Value, ct);
            }
            else
            {
                Debug.Log($"[Text] {(string.IsNullOrEmpty(line.Speaker) ? "(나레이션)" : line.Speaker)}: {line.Value}");
            }
        }

        async UniTask ExecuteCharAsync(ScriptLine line, CancellationToken ct)
        {
            // 캐릭터 제어
            var character = StageManager.Instance?.Character;
            if (character != null)
            {
                await character.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[Char] {line.Value}");
            }
        }

        async UniTask ExecuteBGAsync(ScriptLine line, CancellationToken ct)
        {
            // 전환 타입 파싱 (Cross가 아니면 캐릭터 퇴장)
            var parts = line.Value.Split(':');
            bool isCrossFade = parts.Length >= 2 && parts[1].Equals("Cross", System.StringComparison.OrdinalIgnoreCase);
            
            if (!isCrossFade)
            {
                // Fade, Cut 등은 장면 전환 → 캐릭터 자동 퇴장
                var character = StageManager.Instance?.Character;
                if (character != null)
                {
                    await character.ExitAllAsync(ct);
                }
            }
            
            // 배경 전환
            var background = StageManager.Instance?.Background;
            if (background != null)
            {
                await background.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[BG] {line.Value}");
            }
        }

        async UniTask ExecuteCGAsync(ScriptLine line, CancellationToken ct)
        {
            var parts = line.Value.Split(':');
            bool isExit = parts[0].Equals("Exit", System.StringComparison.OrdinalIgnoreCase);
            
            if (!isExit)
            {
                // CG 표시 시: 캐릭터 자동 퇴장 + 대사창 숨김
                var character = StageManager.Instance?.Character;
                if (character != null)
                {
                    await character.ExitAllAsync(ct);
                }
                
                // 대사창 숨김
                var dialogueUI = UIManager.Instance?.DialogueUI;
                dialogueUI?.Hide();
            }
            else
            {
                // CG 종료 시: 대사창 표시
                var dialogueUI = UIManager.Instance?.DialogueUI;
                dialogueUI?.Show();
            }
            
            // CG 레이어 제어
            var cg = StageManager.Instance?.CG;
            if (cg != null)
            {
                await cg.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[CG] {line.Value}");
            }
        }

        async UniTask ExecuteOverlayAsync(ScriptLine line, CancellationToken ct)
        {
            // 오버레이 레이어 제어
            var overlay = StageManager.Instance?.VirtualBG;
            if (overlay != null)
            {
                await overlay.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[Overlay] {line.Value}");
            }
        }

        async UniTask ExecuteSoundAsync(ScriptLine line, CancellationToken ct)
        {
            // 오디오 재생
            if (AudioManager.Instance != null)
            {
                await AudioManager.Instance.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[Sound] {line.Value}");
            }
        }

        async UniTask ExecuteFXAsync(ScriptLine line, CancellationToken ct)
        {
            var parts = line.Value.Split(':');
            var command = parts[0];
            var dialogueUI = UIManager.Instance?.DialogueUI;
            
            // 대사창 제어 명령
            switch (command)
            {
                case "FadeOut":
                    // FadeOut 후 대사창 숨기기 (즉시 Hide하면 깜박임 발생)
                    // → 페이드 완료 후 숨김
                    break;
                    
                case "DialogueHide":
                    // 대사창 숨김 (단독 명령)
                    dialogueUI?.Hide();
                    return; // ScreenFX 실행 안 함
                    
                case "DialogueShow":
                    // 대사창 표시 (단독 명령)
                    dialogueUI?.Show();
                    return; // ScreenFX 실행 안 함
            }
            
            // 시각 효과 (ScreenFX 전역 싱글톤)
            var fx = Core.ScreenFX.Instance;
            if (fx != null)
            {
                await fx.ExecuteAsync(line.Value, ct);
            }
            else
            {
                Debug.Log($"[FX] {line.Value}");
            }

            // FadeOut 완료 후 대사창 숨김 (화면이 완전히 덮인 후)
            if (command == "FadeOut")
            {
                dialogueUI?.Hide();
            }
        }

        async UniTask<bool> ExecuteFlowAsync(ScriptLine line, CancellationToken ct)
        {
            // Flow 명령 파싱
            var parts = line.Value.Split(':');
            var command = parts[0];

            switch (command)
            {
                case "Jump":
                    if (parts.Length > 1)
                    {
                        string targetId = parts[1];
                        if (lineIndex.TryGetValue(targetId, out int targetIndex))
                        {
                            currentIndex = targetIndex - 1; // 루프에서 +1 되므로
                            Debug.Log($"[Flow] Jump -> {targetId} (index {targetIndex})");
                        }
                        else
                        {
                            Debug.LogError($"[Flow] Jump 대상 '{targetId}'를 찾을 수 없습니다.");
                        }
                    }
                    break;

                case "End":
                    Debug.Log("[Flow] End - 스크립트 종료");
                    return false;

                case "Save":
                    // 자동 저장
                    Core.GameManager.Instance?.AutoSave();
                    break;

                case "If":
                    // 형식: If:조건:점프대상
                    // 예: If:Love:Roa>=30:Confession, If:Flag:Met_Roa:Reunion
                    if (ExecuteFlowIf(line.Value))
                        return true; // 점프 성공 시 계속 진행
                    break;
            }

            await UniTask.CompletedTask;
            return true;
        }

        /// <summary>
        /// Flow:If 조건 분기 처리
        /// 형식: If:조건:점프대상
        /// </summary>
        bool ExecuteFlowIf(string value)
        {
            // If:Love:Roa>=30:Confession → parts: ["If", "Love", "Roa>=30", "Confession"]
            // If:Flag:Met_Roa:Reunion → parts: ["If", "Flag", "Met_Roa", "Reunion"]
            // If:!Flag:Confessed:FirstMeet → parts: ["If", "!Flag", "Confessed", "FirstMeet"]
            // If:Stat:Int>=20:SmartChoice → parts: ["If", "Stat", "Int>=20", "SmartChoice"]

            var parts = value.Split(':');
            if (parts.Length < 3)
            {
                Debug.LogWarning($"[Flow:If] 잘못된 형식: {value}");
                return false;
            }

            // 마지막 파트는 점프 대상
            string jumpTarget = parts[^1];

            // 조건 조합 (If 제외, 점프대상 제외)
            // If:Love:Roa>=30:Confession → "Love:Roa>=30"
            // If:Flag:Met_Roa:Reunion → "Flag:Met_Roa"
            string condition = string.Join(":", parts[1..^1]);

            bool result = GameState.Instance?.EvaluateCondition(condition) ?? false;
            Debug.Log($"[Flow:If] 조건: {condition} = {result}");

            if (result && lineIndex.TryGetValue(jumpTarget, out int targetIndex))
            {
                currentIndex = targetIndex;
                Debug.Log($"[Flow:If] 점프: {jumpTarget} (index: {targetIndex})");
                return true;
            }

            return false;
        }

        async UniTask ExecuteChoiceAsync(ScriptLine line, CancellationToken ct)
        {
            // 선택지에서는 Auto 모드 일시정지
            bool wasAutoMode = autoMode;
            autoMode = false;

            // Option 라인들 수집
            var scriptOptions = CollectOptions();

            if (scriptOptions.Count == 0)
            {
                Debug.LogWarning("[Choice] 선택지가 없습니다.");
                return;
            }

            // OptionData로 변환
            var options = new List<OptionData>();
            foreach (var opt in scriptOptions)
            {
                options.Add(OptionData.Parse(opt.Value));
            }

            // ChoiceUI로 선택지 표시
            var choiceUI = UIManager.Instance?.ChoiceUI;
            if (choiceUI != null)
            {
                var result = await choiceUI.ShowAndWaitAsync(options, ct);
                
                if (result != null && !string.IsNullOrEmpty(result.JumpTarget))
                {
                    if (lineIndex.TryGetValue(result.JumpTarget, out int targetIndex))
                    {
                        currentIndex = targetIndex - 1;
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
                // ChoiceUI 없으면 로그만
                Debug.Log($"[Choice] {options.Count}개 선택지 (첫 번째 자동 선택)");
                if (options.Count > 0 && !string.IsNullOrEmpty(options[0].JumpTarget))
                {
                    if (lineIndex.TryGetValue(options[0].JumpTarget, out int targetIndex))
                    {
                        currentIndex = targetIndex - 1;
                    }
                }
            }

            // Auto 모드 복원
            autoMode = wasAutoMode;
        }

        /// <summary>
        /// 현재 위치 이후의 Option 라인들 수집
        /// </summary>
        List<ScriptLine> CollectOptions()
        {
            var options = new List<ScriptLine>();
            int i = currentIndex + 1;

            while (i < lines.Count && lines[i].Type == LineType.Option)
            {
                options.Add(lines[i]);
                i++;
            }

            // Option들은 스킵하도록 인덱스 조정
            currentIndex = i - 1;

            return options;
        }

        #endregion
    }
}
