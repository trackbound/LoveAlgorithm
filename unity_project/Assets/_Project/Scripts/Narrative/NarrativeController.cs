using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization; // float 파싱(InvariantCulture)
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // 내러티브/UI/Flow/오디오 이벤트
using UnityEngine;
using UnityEngine.Serialization; // FormerlySerializedAs (fxTuning → screenFadeTuning 바인딩 보존)

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 내러티브 런타임 엔진의 EventBus 어댑터(M3 슬라이스1: 대사+선택지). 구 ScriptRunner/ScriptEngine의
    /// 거대한 디스패치를 결정 로직(순수 ScriptCursor/ChoiceParser/ChoiceEffectInterpreter)과 분리하고,
    /// 여기선 코루틴 진행 + UI 명령 발행 + 완료 핸들 대기만 한다(ADR-007, FlowCommandController 패턴 미러).
    ///
    /// 흐름: <see cref="PlayScriptCommand"/> 구독 → 파싱 → <c>RequestPhaseCommand(Story)</c> → 라인 루프
    /// (Text=대사 명령+핸들 대기, Choice=선택지 명령+효과/점프, Flow=Jump/End 직접·Affinity/Day는 Router로 위임)
    /// → 종료 시 <c>RequestPhaseCommand(Schedule)</c> + <see cref="NarrativeFinishedEvent"/>.
    ///
    /// 슬라이스 범위 밖(스킵+로그): Char/BG/CG/SD/Overlay/Sound/FX/Place, 인라인 태그, 오토모드,
    /// 점프 페이드/스테이지 합성/로그 복원, 선택지 조건 필터링, Flow의 Save/Schedule/Username/LockScreen 등.
    /// 씬 하이어라키: _Managers/NarrativeController, 인스펙터에서 <see cref="state"/> 바인딩(선택지 효과 적용 대상).
    /// </summary>
    public class NarrativeController : MonoBehaviour
    {
        [Tooltip("선택지 효과(Stat/Money) 적용 대상 런타임 상태. 인스펙터/부팅 주입.")]
        [SerializeField] GameStateSO state;

        [Tooltip("Delay 진행 시 최대 대기 상한(초) — 잘못된 CSV로 무한 대기 방지.")]
        [SerializeField] float maxDelaySeconds = 10f;

        [Tooltip("스테이지(BG/Char) 전환 기본 시간 동결 SO(ADR-012). 미바인딩 시 폴백 상수 사용.")]
        [SerializeField] StageTuningSO stageTuning;

        [Tooltip("스크린 페이드(페이드/플래시) 기본 시간 동결 SO(ADR-012). 미바인딩 시 폴백 상수 사용.")]
        [FormerlySerializedAs("fxTuning")]
        [SerializeField] ScreenFadeTuningSO screenFadeTuning;

        [Tooltip("흔들기 FX(Stage/Dialogue/Char) 강도·지속·진동 프로파일 동결 SO(ADR-012). 미바인딩 시 폴백 상수 사용.")]
        [SerializeField] ShakeTuningSO shakeTuning;

        [Tooltip("카메라 FX(Zoom/Pan/Reset) 기본 시간 동결 SO(ADR-012). 미바인딩 시 폴백 상수 사용.")]
        [SerializeField] CameraTuningSO cameraTuning;

        [Tooltip("색 틴트 FX 프리셋 색·기본 알파/지속 동결 SO(ADR-012). 미바인딩 시 폴백 상수 사용.")]
        [SerializeField] ColorTintTuningSO colorTintTuning;

        [Tooltip("아이마스크 FX(눈감기/뜨기/깜빡) 기본 지속 동결 SO(ADR-012). 미바인딩 시 폴백 상수 사용.")]
        [SerializeField] EyeMaskTuningSO eyeMaskTuning;

        [Tooltip("스테이지 레이어(CG/SD/Overlay) 페이드 기본 시간 동결 SO(ADR-012). 미바인딩 시 폴백 상수 사용.")]
        [SerializeField] StageLayerTuningSO stageLayerTuning;
        [Tooltip("위치 배너(Place) 동결 수치. 미바인딩 시 폴백 상수(0.45/2.0/0.35).")]
        [SerializeField] PlaceTuningSO placeTuning;

        [Tooltip("에셋 별칭(한글명)→코드ID 카탈로그. 엔진이 명령 발행 전 해석(뷰는 모름). 미바인딩 시 원문 그대로.")]
        [SerializeField] ResourceAliasCatalogSO aliasCatalog;

        public GameStateSO State { get => state; set => state = value; }
        public StageTuningSO StageTuning { get => stageTuning; set => stageTuning = value; }
        public ScreenFadeTuningSO ScreenFadeTuning { get => screenFadeTuning; set => screenFadeTuning = value; }
        public ShakeTuningSO ShakeTuning { get => shakeTuning; set => shakeTuning = value; }
        public CameraTuningSO CameraTuning { get => cameraTuning; set => cameraTuning = value; }
        public ColorTintTuningSO ColorTintTuning { get => colorTintTuning; set => colorTintTuning = value; }
        public EyeMaskTuningSO EyeMaskTuning { get => eyeMaskTuning; set => eyeMaskTuning = value; }
        public StageLayerTuningSO StageLayerTuning { get => stageLayerTuning; set => stageLayerTuning = value; }
        public PlaceTuningSO PlaceTuning { get => placeTuning; set => placeTuning = value; }
        public ResourceAliasCatalogSO AliasCatalog { get => aliasCatalog; set => aliasCatalog = value; }

        /// <summary>현재 스크립트가 재생 중인가(재진입 가드).</summary>
        public bool IsRunning { get; private set; }

        IDisposable _sub;
        Coroutine _currentRun;

        void OnEnable() => _sub = EventBus.Subscribe<PlayScriptCommand>(OnPlayScript);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        void OnPlayScript(PlayScriptCommand cmd)
        {
            // 진행 중이면 중단하고 새로 시작(기획 도구 재적용 지원). 중단 시 NarrativeFinishedEvent는 발행하지
            // 않는다 — GameManager 저녁 이벤트 씨임의 WaitUntil이 하루를 앞당기지 않도록. 잔여 FX/UI는 새 스크립트
            // 명령이 덮거나 정상 종료의 ClearAll이 정리(즉시 완전 리셋은 후속). 중단된 완료 핸들은 GC(콜백 없음).
            if (_currentRun != null)
            {
                StopCoroutine(_currentRun);
                _currentRun = null;
                IsRunning = false;
            }

            // 인라인·에셋 둘 다 없으면 순수 Stop 의도 — 위 중단만 하고 조용히 종료(파싱/경고 없음).
            // 빈 InlineCsv는 TextAsset(null) 경로로 새지 않게 여기서 가른다(도구 Stop 버튼).
            if (string.IsNullOrEmpty(cmd.InlineCsv) && cmd.Script == null)
                return;

            List<ScriptLine> lines = !string.IsNullOrEmpty(cmd.InlineCsv)
                ? ScriptParser.Parse(cmd.InlineCsv)
                : ScriptParser.Parse(cmd.Script);

            if (lines == null || lines.Count == 0)
            {
                // 빈 스크립트 = 중단만(Stop 의도) — 위에서 진행분을 멈췄으니 새로 시작하지 않는다.
                Log.Warn($"[NarrativeController] 빈 스크립트 — 재생 생략 (name='{cmd.Name}').");
                return;
            }

            _currentRun = StartCoroutine(Run(lines, cmd.Name));
        }

        IEnumerator Run(List<ScriptLine> lines, string scriptName)
        {
            IsRunning = true;
            EventBus.Publish(new RequestPhaseCommand(ScreenPhase.Story));

            var cursor = new ScriptCursor(lines);
            bool end = false;

            while (!end && cursor.HasCurrent)
            {
                var line = cursor.Current;
                switch (line.Type)
                {
                    case LineType.Text:
                        yield return PlayText(line);
                        cursor.MoveNext();
                        break;

                    case LineType.Choice:
                        yield return PlayChoice(cursor);
                        // PlayChoice가 점프했으면 커서는 이미 대상; 아니면 Choice+Option 블록을 건너뛴다.
                        // 점프 여부는 _lastChoiceJumped로 전달(코루틴은 반환값을 못 주므로 필드 경유).
                        if (!_lastChoiceJumped) cursor.SkipChoiceBlock();
                        break;

                    case LineType.Flow:
                        if (IsLoadingScene(line.Value))
                        {
                            // 대기형 Flow(로딩 화면) — HandleFlow(동기)로는 못 기다리므로 코루틴으로 처리.
                            yield return PlayLoading(line);
                            cursor.MoveNext();
                        }
                        else if (IsLockScreen(line.Value))
                        {
                            // 대기형 Flow(잠금화면) — 비번 입력(Submit)까지 핸들 대기. 컨트롤러+뷰 배선 필수(미배선=hang).
                            yield return PlayLockScreen(line);
                            cursor.MoveNext();
                        }
                        else
                        {
                            bool flowJumped = HandleFlow(line, cursor, ref end);
                            if (!flowJumped && !end) cursor.MoveNext();
                        }
                        break;

                    case LineType.BG:
                        yield return PlayStageBg(line);
                        cursor.MoveNext();
                        break;

                    case LineType.Char:
                        yield return PlayStageChar(line);
                        cursor.MoveNext();
                        break;

                    case LineType.Sound:
                        PlaySound(line);
                        yield return WaitSound(line);
                        cursor.MoveNext();
                        break;

                    case LineType.FX:
                        yield return PlayFx(line);
                        cursor.MoveNext();
                        break;

                    case LineType.CG:
                        yield return PlayStageLayer(line, StageLayerKind.CG);
                        cursor.MoveNext();
                        break;

                    case LineType.SD:
                        yield return PlayStageLayer(line, StageLayerKind.SD);
                        cursor.MoveNext();
                        break;

                    case LineType.Overlay:
                        yield return PlayStageLayer(line, StageLayerKind.Overlay);
                        cursor.MoveNext();
                        break;

                    case LineType.Place:
                        yield return PlayPlace(line);
                        cursor.MoveNext();
                        break;

                    default:
                        // Option(미아) 등 — 이번 슬라이스 미지원, 건너뜀.
                        Log.Info($"[NarrativeController] 슬라이스 범위 밖 라인 스킵: {line}");
                        cursor.MoveNext();
                        break;
                }
            }

            EventBus.Publish(new RequestPhaseCommand(ScreenPhase.Schedule));
            EventBus.Publish(new NarrativeFinishedEvent(scriptName));
            IsRunning = false;
            _currentRun = null;
        }

        IEnumerator PlayText(ScriptLine line)
        {
            bool requireClick = line.NextType == NextType.Click;
            var parsed = InlineTagParser.Parse(line.Value); // 인라인 태그(<wait:sec>·<emote=표정/>) 분해 → 표시텍스트+멈춤/표정 지점.
            var req = new CompletionHandle();
            // 화자/표정 별칭 해석: 표시는 원문(Speaker), 슬롯 매칭은 코드 ID(SpeakerId)·표정 코드 — 뷰는 카탈로그를 모른다.
            EventBus.Publish(new ShowDialogueCommand(line.Speaker, parsed.Text, requireClick, req,
                parsed.Pauses, ResolveInlineEmotes(parsed.Emotes), ResolveSpeakerId(line.Speaker)));

            // 뷰가 타이핑/클릭을 마칠 때까지 대기(구독자 없으면 즉시 완료되지 않으므로 가드).
            yield return new WaitUntil(() => req.IsComplete);

            if (line.NextType == NextType.Delay && line.DelaySeconds > 0f)
            {
                float wait = Mathf.Min(line.DelaySeconds, maxDelaySeconds);
                yield return new WaitForSeconds(wait);
            }
        }

        bool _lastChoiceJumped;

        IEnumerator PlayChoice(ScriptCursor cursor)
        {
            _lastChoiceJumped = false;

            var optionValues = cursor.PeekOptionValues();
            if (optionValues.Count == 0)
            {
                Log.Warn("[NarrativeController] Choice 라인에 Option이 없습니다 — 건너뜀.");
                yield break;
            }

            var options = ChoiceParser.VisibleOptions(ChoiceParser.ParseOptions(optionValues), state); // if:조건 필터링
            if (options.Count == 0)
            {
                Log.Warn("[NarrativeController] 조건을 만족하는 선택지가 없습니다 — 건너뜀.");
                yield break;
            }

            var labels = new List<string>(options.Count);
            foreach (var o in options) labels.Add(o.ButtonText);

            var req = new ChoiceRequest();
            EventBus.Publish(new ShowChoiceCommand(labels, req));
            yield return new WaitUntil(() => req.IsComplete);

            int idx = Mathf.Clamp(req.SelectedIndex, 0, options.Count - 1);
            var chosen = options[idx];

            ApplyChoiceEffects(chosen.Effects);

            if (!string.IsNullOrEmpty(chosen.JumpTarget))
            {
                if (cursor.TryJump(chosen.JumpTarget))
                    _lastChoiceJumped = true;
                else
                    Debug.LogError($"[NarrativeController] 점프 대상 '{chosen.JumpTarget}'을 찾을 수 없습니다.");
            }
        }

        void ApplyChoiceEffects(List<string> effects)
        {
            if (state == null)
            {
                if (effects != null && effects.Count > 0)
                    Debug.LogError("[NarrativeController] state(GameStateSO) 미바인딩 — 선택지 효과 적용 불가.");
                return;
            }

            var r = ChoiceEffectInterpreter.Apply(state, effects);

            foreach (var sc in r.StatChanges)
                EventBus.Publish(new StatChangedEvent(sc.StatId, sc.OldValue, sc.NewValue));

            if (r.MoneyChanged)
                EventBus.Publish(new MoneyChangedEvent(r.NewMoney));

            // 호감도는 Flow 명령으로 위임 — FlowCommandController가 적용 + AffinityChangedEvent 발행.
            foreach (var cmd in r.FlowCommands)
                EventBus.Publish(new FlowCommandRequestedEvent(cmd));

            foreach (var sfx in r.SfxNames)
                EventBus.Publish(new PlaySfxCommand(sfx));
        }

        /// <summary>Flow 라인 처리. 점프했으면 true(커서가 이미 대상). End면 <paramref name="end"/>=true.</summary>
        bool HandleFlow(ScriptLine line, ScriptCursor cursor, ref bool end)
        {
            string value = line.Value ?? "";
            int ci = value.IndexOf(':');
            string head = (ci >= 0 ? value.Substring(0, ci) : value).Trim();

            if (string.Equals(head, "Jump", StringComparison.OrdinalIgnoreCase))
            {
                string target = ci >= 0 ? value.Substring(ci + 1).Trim() : "";
                if (cursor.TryJump(target)) return true;
                Debug.LogError($"[NarrativeController] Flow Jump 대상 '{target}'을 찾을 수 없습니다.");
                return false;
            }

            if (string.Equals(head, "End", StringComparison.OrdinalIgnoreCase))
            {
                end = true;
                return false;
            }

            if (string.Equals(head, "Affinity", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(head, "Day", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(head, "Flag", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(head, "Set", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(head, "Ending", StringComparison.OrdinalIgnoreCase))
            {
                // 순수 FlowCommandInterpreter의 어댑터(FlowCommandController)가 적용 + (필요시) 통지. Flag는 적용만.
                EventBus.Publish(new FlowCommandRequestedEvent(value));
                return false;
            }

            if (string.Equals(head, "If", StringComparison.OrdinalIgnoreCase))
            {
                // If:<조건>:<점프대상> — 조건 참이면 점프(true), 거짓이면 통과(false). 조건에 ':'가 있어(Stat:Int>=5)
                // rest의 마지막 ':' 뒤=점프대상, 앞=조건으로 분리(구 IfFlowCommand 의미 1:1). 평가는 순수 ConditionEvaluator.
                string rest = ci >= 0 ? value.Substring(ci + 1) : "";
                int lastColon = rest.LastIndexOf(':');
                if (lastColon <= 0)
                {
                    Log.Warn($"[NarrativeController] 잘못된 If 형식(If:조건:점프대상): \"{value}\"");
                    return false;
                }
                string cond = rest.Substring(0, lastColon);
                string target = rest.Substring(lastColon + 1).Trim();
                if (!ConditionEvaluator.Evaluate(state, cond)) return false; // 조건 거짓 → 다음 라인
                if (cursor.TryJump(target)) return true;
                Debug.LogError($"[NarrativeController] If 점프 대상 '{target}'을 찾을 수 없습니다.");
                return false;
            }

            if (string.Equals(head, "Save", StringComparison.OrdinalIgnoreCase))
            {
                // 스토리 체크포인트 → 자동저장 슬롯에 세이브(SaveManager가 SaveRequestedEvent 구독). 0=자동저장 슬롯 계약.
                EventBus.Publish(new SaveRequestedEvent(0, "story-save"));
                return false;
            }

            if (string.Equals(head, "Value", StringComparison.OrdinalIgnoreCase))
            {
                // 풀게임 "낮→스케줄→밤" 흐름의 스케줄 지점 마커(Value:Schedule). 프롤로그는 선형 튜토리얼이라
                // 스케줄 UI 없이 직접 서술 → 의도적 no-op(감독 결정 2026-06-05). 미지원 아님이라 스킵 로그 안 냄.
                return false;
            }

            // Username/LockScreen/Message/MiniGame 등 — 이번 슬라이스 미지원.
            Log.Info($"[NarrativeController] 슬라이스 범위 밖 Flow 스킵: \"{value}\"");
            return false;
        }

        // ── 스테이지(M3 슬라이스2: BG + Char) ──
        // 순수 StageParser로 Value를 인텐트로 분해 → 동결 수치(StageTuningSO)로 duration 해석 →
        // ShowBackgroundCommand/ShowCharacterCommand 발행 → Next에 따라 대기(Await/Click=핸들, Delay=초, Immediate=비대기).

        IEnumerator PlayStageBg(ScriptLine line)
        {
            var intent = StageParser.ParseBackground(line.Value);
            if (!intent.IsValid)
            {
                Log.Warn($"[NarrativeController] 잘못된 BG 라인 — 건너뜀: \"{line.Value}\"");
                yield break;
            }

            float dur = intent.Duration >= 0f
                ? intent.Duration
                : (stageTuning != null ? stageTuning.BgTransitionDefault : 0.5f);

            var req = new CompletionHandle();
            EventBus.Publish(new ShowBackgroundCommand(ResolveBgName(intent.Name), intent.Transition, dur, req));
            yield return WaitNext(line, () => req.IsComplete);
        }

        IEnumerator PlayStageChar(ScriptLine line)
        {
            var intent = StageParser.ParseCharacter(line.Value);
            if (!intent.IsValid)
            {
                Log.Warn($"[NarrativeController] 잘못된 Char 라인 — 건너뜀: \"{line.Value}\"");
                yield break;
            }

            float dur = ResolveCharDuration(intent.Action);
            var (ch, em) = ResolveCharEmote(intent.Character, intent.Emote, intent.Action);
            var req = new CompletionHandle();
            EventBus.Publish(new ShowCharacterCommand(intent.Slot, intent.Action, ch, em, dur, req));
            yield return WaitNext(line, () => req.IsComplete);
        }

        // ── 별칭 해석(작가 한글명→코드ID) ──
        // 발행 직전에 엔진이 해석한다 — 뷰의 컨벤션 로딩(Resources.Load)은 무변경·카탈로그 무지(ColorTint 프리셋 선례).
        // 카탈로그 미바인딩/미등록 이름은 원문 그대로(passthrough) — 코드명 직접 기입과 신규 에셋이 카탈로그 없이도 동작.

        string ResolveBgName(string n)  => aliasCatalog != null ? aliasCatalog.ResolveBg(n) : n;
        string ResolveBgmName(string n) => aliasCatalog != null ? aliasCatalog.ResolveBgm(n) : n;
        string ResolveSfxName(string n) => aliasCatalog != null ? aliasCatalog.ResolveSfx(n) : n;

        string ResolveLayerName(StageLayerKind kind, string n)
        {
            if (aliasCatalog == null) return n;
            switch (kind)
            {
                case StageLayerKind.CG: return aliasCatalog.ResolveCg(n);
                case StageLayerKind.SD: return aliasCatalog.ResolveSd(n);
                default:                return n; // Overlay: 별칭 미운영(코드명 직기입)
            }
        }

        (string character, string emote) ResolveCharEmote(string character, string emote, CharAction action)
        {
            if (aliasCatalog == null) return (character, emote);
            bool known = aliasCatalog.TryResolveCharacter(character, out string id);
            string ch = known ? id : character;
            string em = string.IsNullOrEmpty(emote) ? emote : aliasCatalog.ResolveEmote(emote);
            // 등장 시 표정 생략 → 기본 표정 보정(캐릭터 단독 스프라이트는 없다: c01_00 등). 등록 캐릭터만(코드 직기입 의도 보존).
            if (action == CharAction.Enter && known && string.IsNullOrEmpty(em))
                em = aliasCatalog.DefaultEmote;
            return (ch, em);
        }

        /// <summary>화자명→캐릭터 코드 ID(인라인 emote 슬롯 매칭용). 미등록 화자({{Player}}/내레이션)면 null.</summary>
        string ResolveSpeakerId(string speaker)
            => aliasCatalog != null && aliasCatalog.TryResolveCharacter(speaker, out string id) ? id : null;

        IReadOnlyList<InlineEmote> ResolveInlineEmotes(IReadOnlyList<InlineEmote> emotes)
        {
            if (aliasCatalog == null || emotes == null || emotes.Count == 0) return emotes;
            var resolved = new List<InlineEmote>(emotes.Count);
            for (int i = 0; i < emotes.Count; i++)
                resolved.Add(new InlineEmote(emotes[i].CharIndex, aliasCatalog.ResolveEmote(emotes[i].Emote)));
            return resolved;
        }

        float ResolveCharDuration(CharAction action)
        {
            switch (action)
            {
                case CharAction.Enter: return stageTuning != null ? stageTuning.CharEnterDefault : 0.5f;
                case CharAction.Exit:  return stageTuning != null ? stageTuning.CharExitDefault : 0.4f;
                case CharAction.Emote: return stageTuning != null ? stageTuning.CharEmoteDefault : 0.25f;
                default:               return 0f; // Clear = 즉시
            }
        }

        /// <summary>연출 라인 Next 진행 공통: Await/Click=핸들 완료 대기, Delay=초 대기, Immediate=비대기(애니 병행).</summary>
        IEnumerator WaitNext(ScriptLine line, Func<bool> isComplete)
        {
            switch (line.NextType)
            {
                case NextType.Await:
                case NextType.Click:
                    yield return new WaitUntil(isComplete);
                    break;
                case NextType.Delay:
                    if (line.DelaySeconds > 0f)
                        yield return new WaitForSeconds(Mathf.Min(line.DelaySeconds, maxDelaySeconds));
                    break;
                // Immediate: 대기하지 않음.
            }
        }

        // ── 사운드(M3 슬라이스2: BGM/SFX/Voice) ──
        // 순수 SoundParser로 Value를 인텐트로 분해 → 카테고리별 기존 오디오 명령을 발행(AudioManager가 구독·재생).
        // 완료 핸들이 없으므로(오디오는 fire-and-forget) Next는 Delay만 대기 — await/click이어도 블록하지 않는다.

        void PlaySound(ScriptLine line)
        {
            var intent = SoundParser.Parse(line.Value);
            if (!intent.IsValid)
            {
                Log.Warn($"[NarrativeController] 잘못된 Sound 라인 — 건너뜀: \"{line.Value}\"");
                return;
            }

            switch (intent.Category)
            {
                case SoundCategory.Bgm:
                    if (intent.IsStop) EventBus.Publish(new StopBgmCommand(intent.Fade));
                    else EventBus.Publish(new PlayBgmCommand(ResolveBgmName(intent.Name), intent.Fade));
                    break;
                case SoundCategory.Sfx:
                    EventBus.Publish(new PlaySfxCommand(ResolveSfxName(intent.Name)));
                    break;
                case SoundCategory.Voice:
                    if (intent.IsStop) EventBus.Publish(new StopVoiceCommand());
                    else EventBus.Publish(new PlayVoiceCommand(intent.Name));
                    break;
            }
        }

        IEnumerator WaitSound(ScriptLine line)
        {
            if (line.NextType == NextType.Delay && line.DelaySeconds > 0f)
                yield return new WaitForSeconds(Mathf.Min(line.DelaySeconds, maxDelaySeconds));
        }

        // ── 위치 배너(Place) ──
        // 순수 PlaceParser로 "제목 | 장소" 분해 → 동결 수치(PlaceTuningSO)로 등장/유지/퇴장 해석 → ShowPlaceCommand
        // 발행 → PlaceCardView가 배너를 페이드 인→유지→아웃. 비블로킹(Next는 () => true로 await도 즉시 통과, Delay만 존중).
        IEnumerator PlayPlace(ScriptLine line)
        {
            var intent = PlaceParser.Parse(line.Value);
            if (!intent.IsValid)
            {
                Log.Warn($"[NarrativeController] 잘못된 Place 라인 — 건너뜀: \"{line.Value}\"");
                yield break;
            }

            float enter = placeTuning != null ? placeTuning.EnterDuration : 0.45f;
            float hold = placeTuning != null ? placeTuning.HoldDuration : 2.0f;
            float exit = placeTuning != null ? placeTuning.ExitDuration : 0.35f;

            EventBus.Publish(new ShowPlaceCommand(intent.Title, intent.Place, enter, hold, exit, new CompletionHandle()));
            yield return WaitNext(line, () => true);
        }

        // ── 로딩 화면(LoadingScene/Loading) — 대기형 Flow ──
        // displayTime(기본 2.0s) 동안 LoadingScreenView가 풀스크린 오버레이를 띄운다. Flow지만 대기가 필요해
        // Run 루프가 코루틴으로 분기(HandleFlow는 동기). 씬 전환 사이 로딩 비트(구 LoadingScene 재작성).
        const float LoadingDefaultSeconds = 2.0f;

        static bool IsLoadingScene(string value)
        {
            string h = HeadOf(value);
            return string.Equals(h, "LoadingScene", StringComparison.OrdinalIgnoreCase)
                || string.Equals(h, "Loading", StringComparison.OrdinalIgnoreCase);
        }

        IEnumerator PlayLoading(ScriptLine line)
        {
            float secs = LoadingDefaultSeconds;
            int ci = (line.Value ?? "").IndexOf(':');
            if (ci >= 0 &&
                float.TryParse(line.Value.Substring(ci + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s >= 0f)
                secs = s;

            var req = new CompletionHandle();
            EventBus.Publish(new ShowLoadingCommand(secs, req));
            yield return WaitNext(line, () => req.IsComplete);
        }

        static bool IsLockScreen(string value)
            => string.Equals(HeadOf(value), "LockScreen", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 대기형 Flow(잠금화면, LockScreen). 순수 <see cref="LockScreenParser"/>로 분해 후 ShowLockScreenCommand를
        /// 발행하고 비번 입력 완료(핸들)까지 대기 — LockScreenController(저장·핸들 완료) + LockScreenView(입력 UI)가
        /// 함께 배선돼 있어야 진행한다(미배선 시 Submit이 없어 hang). 파싱 실패면 스킵.
        /// </summary>
        IEnumerator PlayLockScreen(ScriptLine line)
        {
            var intent = LockScreenParser.Parse(line.Value);
            if (!intent.IsValid)
            {
                Log.Info($"[NarrativeController] LockScreen 파싱 실패 — 스킵: \"{line.Value}\"");
                yield break;
            }

            var req = new CompletionHandle();
            EventBus.Publish(new ShowLockScreenCommand(intent.Mode, intent.FadeOut, intent.TimeOverride, req));
            yield return WaitNext(line, () => req.IsComplete);
        }

        // ── 스크린 페이드(M3 슬라이스2: FadeOut/FadeIn/Flash) ──
        // 순수 ScreenFadeParser로 화면 페이드만 인식(나머지 FX는 형제 파서로 위임) → 동결 수치(ScreenFadeTuningSO)로
        // duration 해석 → ShowScreenFadeCommand 발행 → Next 대기(WaitNext). ScreenFadeView가 최상위 오버레이로 표시.

        IEnumerator PlayFx(ScriptLine line)
        {
            // 스크린 페이드(FadeOut/FadeIn/Flash) 먼저 시도.
            var screen = ScreenFadeParser.Parse(line.Value);
            if (screen.IsValid)
            {
                float dur = screen.Duration >= 0f ? screen.Duration : ResolveFadeDuration(screen.Kind);
                var req = new CompletionHandle();
                EventBus.Publish(new ShowScreenFadeCommand(screen.Kind, dur, req));
                yield return WaitNext(line, () => req.IsComplete);
                yield break;
            }

            // 흔들기(StageShake/DialogueShake/CharShake/CamShake) 시도.
            var shake = ShakeParser.Parse(line.Value);
            if (shake.IsValid)
            {
                yield return PlayShake(line, shake);
                yield break;
            }

            // 카메라(CamZoom/CamPan/CamReset) 시도.
            var cam = CameraParser.Parse(line.Value);
            if (cam.IsValid)
            {
                yield return PlayCamera(line, cam);
                yield break;
            }

            // 색 틴트(ColorTint) 시도.
            var tint = ColorTintParser.Parse(line.Value);
            if (tint.IsValid)
            {
                yield return PlayColorTint(line, tint);
                yield break;
            }

            // 아이마스크(EyeClose/EyeOpen/EyeCloseImmediate/EyeBlink) 시도.
            var eye = EyeMaskParser.Parse(line.Value);
            if (eye.IsValid)
            {
                yield return PlayEyeMask(line, eye);
                yield break;
            }

            // 씬 진입/퇴장(SceneStart/SceneEnd) — EyeMask·BG 재발행(대사 가리지 않는 Wake 스타일).
            var scene = SceneFxParser.Parse(line.Value);
            if (scene.IsValid)
            {
                yield return PlaySceneFx(line, scene);
                yield break;
            }

            // 일괄 셋업(Setup) — 즉시(Cut) BG/BGM/Char/Overlay/Eye를 기존 명령으로 재발행.
            var setup = SetupMacroParser.Parse(line.Value);
            if (setup.IsValid)
            {
                yield return PlaySetup(line, setup);
                yield break;
            }

            // 대기(Wait[:초]) — 단순 일시정지(기본 1.0s, maxDelaySeconds로 캡).
            if (WaitMacroParser.TryParse(line.Value, out float waitSec))
            {
                yield return new WaitForSeconds(Mathf.Min(waitSec, maxDelaySeconds));
                yield break;
            }

            // 대사창 표시/숨김(DialogueShow/DialogueHide) — 무인자 토글 → DialogueView가 root SetActive.
            string fxHead = HeadOf(line.Value);
            if (string.Equals(fxHead, "DialogueShow", StringComparison.OrdinalIgnoreCase))
            {
                EventBus.Publish(new SetDialogueVisibleCommand(true));
                yield return WaitNext(line, () => true);
                yield break;
            }
            if (string.Equals(fxHead, "DialogueHide", StringComparison.OrdinalIgnoreCase))
            {
                EventBus.Publish(new SetDialogueVisibleCommand(false));
                yield return WaitNext(line, () => true);
                yield break;
            }

            // 영상(Video) — 미구현 스텁: 명시적으로 인식·로그 후 건너뜀(await여도 즉시 통과해 hang 방지).
            // 실제 재생은 후속(VideoPlayer). graceful skip이라 프롤로그 흐름을 막지 않는다.
            if (string.Equals(fxHead, "Video", StringComparison.OrdinalIgnoreCase))
            {
                int vci = line.Value.IndexOf(':');
                string videoName = vci >= 0 ? line.Value.Substring(vci + 1).Trim() : "";
                Log.Info($"[NarrativeController] Video 스텁(미구현) — 건너뜀: \"{videoName}\"");
                yield return WaitNext(line, () => true);
                yield break;
            }

            // 캐릭터(Jump/Dim/Glitch) 등 — 이번 슬라이스 미지원.
            Log.Info($"[NarrativeController] 슬라이스 범위 밖 FX 스킵: \"{line.Value}\"");
        }

        static string HeadOf(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            int ci = value.IndexOf(':');
            return (ci >= 0 ? value.Substring(0, ci) : value).Trim();
        }

        // ── FX 매크로(Setup): 즉시 일괄 셋업 ──
        // SetupMacroParser로 BG/BGM/Char[:slot]/Overlay/Eye 분해 → 기존 명령을 Cut/즉시(dur 0)로 재발행(신규 뷰/이벤트 0).
        // 즉시 연출이라 개별 완료를 대기하지 않음 — WaitNext에 () => true를 줘 await/click도 즉시 통과(Delay만 존중).
        IEnumerator PlaySetup(ScriptLine line, SetupIntent s)
        {
            if (s.Bg != null)
                EventBus.Publish(new ShowBackgroundCommand(ResolveBgName(s.Bg), BgTransition.Cut, 0f, new CompletionHandle()));
            if (s.Bgm != null)
                EventBus.Publish(new PlayBgmCommand(ResolveBgmName(s.Bgm)));
            if (s.CharName != null)
            {
                var (ch, em) = ResolveCharEmote(s.CharName, "", CharAction.Enter);
                EventBus.Publish(new ShowCharacterCommand(ParseSetupSlot(s.CharSlot), CharAction.Enter, ch, em, 0f, new CompletionHandle()));
            }
            if (s.Overlay != null)
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.Overlay, false, s.Overlay, LayerTransition.Cut, 0f, new CompletionHandle()));
            if (s.Eye != null)
            {
                var action = string.Equals(s.Eye, "Open", StringComparison.OrdinalIgnoreCase)
                    ? EyeMaskAction.Open : EyeMaskAction.CloseImmediate;
                EventBus.Publish(new EyeMaskCommand(action, 0f, 0f, 0f, new CompletionHandle()));
            }
            yield return WaitNext(line, () => true);
        }

        static CharSlot ParseSetupSlot(string s)
        {
            if (string.IsNullOrEmpty(s)) return CharSlot.C;
            switch (s.Trim().ToLowerInvariant())
            {
                case "l": case "left":  return CharSlot.L;
                case "r": case "right": return CharSlot.R;
                default:                return CharSlot.C;
            }
        }

        // ── FX 매크로(SceneStart/SceneEnd): 씬 진입/퇴장(EyeMask·BG 재발행) ──
        // SceneEnd=눈감기(암전, EyeMask라 대사/캐릭터 안 가림). SceneStart=BG 즉시(Cut) + (EyeClose면 즉시 감고 유지=
        // 암전 모놀로그 / 아니면 눈뜨기 리빌 + pause). 수치는 매크로 family 상수(STORY_COMMANDS 정본 — 시각-튜닝 SO와
        // 구분, Setup/Wait와 동일 규율). EyeMask 페어로 통일(SceneEnd→다음 씬 대사가 검은 화면 위로 진행하는 구 Wake 패턴 보존).
        const float SceneEndCloseDefault = 0.5f; // SceneEnd 기본 눈감기
        const float SceneStartOpenDur = 0.6f;    // SceneStart eyeOpen
        const float SceneStartPauseAfter = 0.4f; // SceneStart pauseAfter

        IEnumerator PlaySceneFx(ScriptLine line, SceneFxIntent s)
        {
            if (s.Kind == SceneFxKind.End)
            {
                float dur = s.Duration >= 0f ? s.Duration : SceneEndCloseDefault;
                var req = new CompletionHandle();
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.Close, dur, dur, 0f, req));
                yield return WaitNext(line, () => req.IsComplete);
                yield break;
            }

            // SceneStart: BG 즉시(Cut) → 눈 처리.
            if (s.Bg != null)
                EventBus.Publish(new ShowBackgroundCommand(ResolveBgName(s.Bg), BgTransition.Cut, 0f, new CompletionHandle()));

            if (s.EyeClose)
            {
                // 눈 감고 유지(암전 모놀로그) — 즉시.
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.CloseImmediate, 0f, 0f, 0f, new CompletionHandle()));
                yield return WaitNext(line, () => true);
                yield break;
            }

            // 눈 뜨며 리빌 + pause.
            var open = new CompletionHandle();
            EventBus.Publish(new EyeMaskCommand(EyeMaskAction.Open, SceneStartOpenDur, SceneStartOpenDur, 0f, open));
            yield return new WaitUntil(() => open.IsComplete);
            yield return new WaitForSeconds(SceneStartPauseAfter);
        }

        float ResolveFadeDuration(ScreenFadeKind kind)
        {
            if (kind == ScreenFadeKind.Flash)
                return screenFadeTuning != null ? screenFadeTuning.FlashDefault : 0.14f;
            return screenFadeTuning != null ? screenFadeTuning.FadeDefault : 0.9f;
        }

        // ── 흔들기 FX(M3 슬라이스2: StageShake/DialogueShake/CharShake) ──
        // 순수 ShakeParser로 대상/강도/지속 분해 → 동결 수치(ShakeTuningSO)로 px·지속·진동 프로파일 해석 →
        // ShakeCommand 발행 → Next 대기(WaitNext). ShakeView(대상별)가 자기 RectTransform을 감쇠 진동시킨다.

        IEnumerator PlayShake(ScriptLine line, ShakeIntent intent)
        {
            float strength = intent.StrengthPx >= 0f
                ? intent.StrengthPx
                : (intent.Target == ShakeTarget.Char
                    ? (shakeTuning != null ? shakeTuning.CharStrength : 18f)
                    : (shakeTuning != null ? shakeTuning.PresetPx(intent.Preset) : DefaultPresetPx(intent.Preset)));

            float dur = intent.Duration >= 0f
                ? intent.Duration
                : (shakeTuning != null ? shakeTuning.ShakeDuration : 0.3f);

            var profile = ResolveShakeProfile(intent.Target);
            var req = new CompletionHandle();
            EventBus.Publish(new ShakeCommand(intent.Target, intent.Slot, strength, dur, profile, req));
            yield return WaitNext(line, () => req.IsComplete);
        }

        ShakeProfile ResolveShakeProfile(ShakeTarget target)
        {
            if (shakeTuning != null)
            {
                var p = shakeTuning.ProfileFor(target);
                return new ShakeProfile(p.xMultiplier, p.yMultiplier, p.rotationMultiplier, p.frequencyHz, p.damping, shakeTuning.HitlagSeconds);
            }
            // 폴백(동결 상수) — SO 미바인딩 시. 값 = ShakeTuningSO 기본값과 동일.
            switch (target)
            {
                case ShakeTarget.Dialogue: return new ShakeProfile(1.0f, 0.12f, 0.02f, 6.0f, 6.5f, 0.025f);
                case ShakeTarget.Char:     return new ShakeProfile(1.0f, 1.0f, 0.0f, 12.0f, 6.5f, 0.025f);
                default:                   return new ShakeProfile(1.0f, 0.35f, 0.06f, 5.0f, 5.2f, 0.025f);
            }
        }

        static float DefaultPresetPx(ShakeStrength preset)
        {
            switch (preset)
            {
                case ShakeStrength.Weak:   return 10f;
                case ShakeStrength.Strong: return 50f;
                default:                   return 25f;
            }
        }

        // ── 카메라 FX(M3 슬라이스2: CamZoom/CamPan/CamReset) ──
        // 순수 CameraParser로 종류/배율/오프셋 분해 → 동결 수치(CameraTuningSO)로 duration 해석 →
        // CameraCommand 발행 → Next 대기(WaitNext). CameraView가 _Stage 콘텐츠 래퍼의 scale/pos를 lerp.

        IEnumerator PlayCamera(ScriptLine line, CameraIntent intent)
        {
            float dur = intent.Duration >= 0f ? intent.Duration : ResolveCameraDuration(intent.Kind);
            var req = new CompletionHandle();
            EventBus.Publish(new CameraCommand(intent.Kind, intent.ZoomScale, intent.PanX, intent.PanY, dur, req));
            yield return WaitNext(line, () => req.IsComplete);
        }

        float ResolveCameraDuration(CameraKind kind)
        {
            switch (kind)
            {
                case CameraKind.Zoom:  return cameraTuning != null ? cameraTuning.ZoomDefault : 0.5f;
                case CameraKind.Pan:   return cameraTuning != null ? cameraTuning.PanDefault : 0.5f;
                default:                 return cameraTuning != null ? cameraTuning.ResetDefault : 0.4f; // Reset
            }
        }

        // ── 색 틴트 FX(M3 슬라이스2: ColorTint) ──
        // 순수 ColorTintParser로 프리셋/Clear/알파/지속 분해 → 동결 수치(ColorTintTuningSO)로 색·알파·지속 해석 →
        // ColorTintCommand 발행(RGB 분리) → Next 대기(WaitNext). ColorTintView가 최상위 오버레이 색을 lerp.

        IEnumerator PlayColorTint(ScriptLine line, ColorTintIntent intent)
        {
            float dur = intent.Duration >= 0f
                ? intent.Duration
                : (colorTintTuning != null ? colorTintTuning.DefaultDuration : 0.5f);

            var req = new CompletionHandle();
            if (intent.IsClear)
            {
                EventBus.Publish(new ColorTintCommand(0f, 0f, 0f, 0f, dur, true, req));
            }
            else
            {
                float alpha = intent.Alpha >= 0f
                    ? intent.Alpha
                    : (colorTintTuning != null ? colorTintTuning.DefaultAlpha : 0.25f);
                Color c = colorTintTuning != null ? colorTintTuning.ColorFor(intent.Preset) : Color.gray;
                EventBus.Publish(new ColorTintCommand(c.r, c.g, c.b, alpha, dur, false, req));
            }
            yield return WaitNext(line, () => req.IsComplete);
        }

        // ── 아이마스크 FX(M3 슬라이스2: 눈감기/뜨기/깜빡) ──
        // 순수 EyeMaskParser로 동작/지속 분해 → 동결 수치(EyeMaskTuningSO)로 지속 해석 → EyeMaskCommand 발행 →
        // Next 대기(WaitNext). EyeMaskView가 상/하 검은 바를 눈꺼풀처럼 보간.

        IEnumerator PlayEyeMask(ScriptLine line, EyeMaskIntent intent)
        {
            float closeDur = intent.CloseDuration >= 0f
                ? intent.CloseDuration
                : (intent.Action == EyeMaskAction.Blink
                    ? (eyeMaskTuning != null ? eyeMaskTuning.BlinkCloseDefault : 0.1f)
                    : (eyeMaskTuning != null ? eyeMaskTuning.CloseDefault : 0.8f));

            float openDur = intent.OpenDuration >= 0f
                ? intent.OpenDuration
                : (intent.Action == EyeMaskAction.Blink
                    ? (eyeMaskTuning != null ? eyeMaskTuning.BlinkOpenDefault : 0.15f)
                    : (eyeMaskTuning != null ? eyeMaskTuning.OpenDefault : 0.8f));

            float holdDur = intent.HoldDuration >= 0f
                ? intent.HoldDuration
                : (eyeMaskTuning != null ? eyeMaskTuning.BlinkHoldDefault : 0.05f);

            var req = new CompletionHandle();
            EventBus.Publish(new EyeMaskCommand(intent.Action, closeDur, openDur, holdDur, req));
            yield return WaitNext(line, () => req.IsComplete);
        }

        // ── 스테이지 레이어(M3 슬라이스2: CG/SD/Overlay) ──
        // 순수 StageLayerParser로 표시/종료·이름·전환·지속 분해 → 동결 수치(StageLayerTuningSO)로 fade 해석 →
        // ShowStageLayerCommand 발행 → Next 대기(WaitNext). StageLayerView가 컨벤션 로딩해 알파 lerp.
        // CG 진입/종료 시 뷰가 SetCgModeCommand를 발행해 대사창·캐릭터를 토글(엔진은 관여 안 함).

        IEnumerator PlayStageLayer(ScriptLine line, StageLayerKind kind)
        {
            var intent = StageLayerParser.Parse(line.Value);
            if (!intent.IsValid)
            {
                Log.Warn($"[NarrativeController] 잘못된 {kind} 라인 — 건너뜀: \"{line.Value}\"");
                yield break;
            }

            float dur = intent.Duration >= 0f ? intent.Duration : ResolveLayerFade(kind);
            var req = new CompletionHandle();
            EventBus.Publish(new ShowStageLayerCommand(kind, intent.IsClose, ResolveLayerName(kind, intent.Name), intent.Transition, dur, req));
            yield return WaitNext(line, () => req.IsComplete);
        }

        float ResolveLayerFade(StageLayerKind kind)
        {
            if (stageLayerTuning == null) return 0.5f;
            switch (kind)
            {
                case StageLayerKind.SD:      return stageLayerTuning.SdFadeDefault;
                case StageLayerKind.Overlay: return stageLayerTuning.OverlayFadeDefault;
                default:                     return stageLayerTuning.CgFadeDefault;
            }
        }
    }
}
