using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // 내러티브/UI/Flow/오디오 이벤트
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 내러티브 런타임 엔진의 EventBus 어댑터(M3 슬라이스1: 대사+선택지). 구 ScriptRunner/ScriptEngine의
    /// 거대한 디스패치를 결정 로직(순수 ScriptCursor/ChoiceParser/ChoiceEffectInterpreter)과 분리하고,
    /// 여기선 코루틴 진행 + UI 명령 발행 + 완료 핸들 대기만 한다(ADR-007, FlowCommandController 패턴 미러).
    ///
    /// 흐름: <see cref="PlayScriptCommand"/> 구독 → 파싱 → <c>ShowUiGroupCommand(Narrative)</c> → 라인 루프
    /// (Text=대사 명령+핸들 대기, Choice=선택지 명령+효과/점프, Flow=Jump/End 직접·Affinity/Day는 Router로 위임)
    /// → 종료 시 <c>ShowUiGroupCommand(Simulation)</c> + <see cref="NarrativeFinishedEvent"/>.
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

        [Tooltip("스크린 FX(페이드/플래시) 기본 시간 동결 SO(ADR-012). 미바인딩 시 폴백 상수 사용.")]
        [SerializeField] ScreenFxTuningSO fxTuning;

        [Tooltip("흔들기 FX(Stage/Dialogue/Char) 강도·지속·진동 프로파일 동결 SO(ADR-012). 미바인딩 시 폴백 상수 사용.")]
        [SerializeField] ShakeTuningSO shakeTuning;

        public GameStateSO State { get => state; set => state = value; }
        public StageTuningSO StageTuning { get => stageTuning; set => stageTuning = value; }
        public ScreenFxTuningSO FxTuning { get => fxTuning; set => fxTuning = value; }
        public ShakeTuningSO ShakeTuning { get => shakeTuning; set => shakeTuning = value; }

        /// <summary>현재 스크립트가 재생 중인가(재진입 가드).</summary>
        public bool IsRunning { get; private set; }

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<PlayScriptCommand>(OnPlayScript);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        void OnPlayScript(PlayScriptCommand cmd)
        {
            if (IsRunning)
            {
                Log.Warn("[NarrativeController] 이미 스크립트 재생 중 — 새 PlayScriptCommand 무시.");
                return;
            }

            List<ScriptLine> lines = !string.IsNullOrEmpty(cmd.InlineCsv)
                ? ScriptParser.Parse(cmd.InlineCsv)
                : ScriptParser.Parse(cmd.Script);

            if (lines == null || lines.Count == 0)
            {
                Log.Warn($"[NarrativeController] 빈 스크립트 — 재생 생략 (name='{cmd.Name}').");
                return;
            }

            StartCoroutine(Run(lines, cmd.Name));
        }

        IEnumerator Run(List<ScriptLine> lines, string scriptName)
        {
            IsRunning = true;
            EventBus.Publish(new ShowUiGroupCommand(UIGroup.Narrative));

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
                        bool flowJumped = HandleFlow(line, cursor, ref end);
                        if (!flowJumped && !end) cursor.MoveNext();
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

                    default:
                        // CG/SD/Overlay/Place/Option(미아) — 이번 슬라이스 미지원, 건너뜀.
                        Log.Info($"[NarrativeController] 슬라이스 범위 밖 라인 스킵: {line}");
                        cursor.MoveNext();
                        break;
                }
            }

            EventBus.Publish(new ShowUiGroupCommand(UIGroup.Simulation));
            EventBus.Publish(new NarrativeFinishedEvent(scriptName));
            IsRunning = false;
        }

        IEnumerator PlayText(ScriptLine line)
        {
            bool requireClick = line.NextType == NextType.Click;
            var parsed = InlineTagParser.Parse(line.Value); // 인라인 태그(<wait:sec>) 분해 → 표시텍스트+멈춤지점.
            var req = new CompletionHandle();
            EventBus.Publish(new ShowDialogueCommand(line.Speaker, parsed.Text, requireClick, req, parsed.Pauses));

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

            var options = ChoiceParser.ParseOptions(optionValues);
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
                string.Equals(head, "Day", StringComparison.OrdinalIgnoreCase))
            {
                // 순수 FlowCommandInterpreter의 어댑터(FlowCommandController)가 적용 + 통지.
                EventBus.Publish(new FlowCommandRequestedEvent(value));
                return false;
            }

            // Save/Schedule/Username/LockScreen/Message/MiniGame/LoadingScene/If/Mark 등 — 이번 슬라이스 미지원.
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
            EventBus.Publish(new ShowBackgroundCommand(intent.Name, intent.Transition, dur, req));
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
            var req = new CompletionHandle();
            EventBus.Publish(new ShowCharacterCommand(intent.Slot, intent.Action, intent.Character, intent.Emote, dur, req));
            yield return WaitNext(line, () => req.IsComplete);
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
                    else EventBus.Publish(new PlayBgmCommand(intent.Name, intent.Fade));
                    break;
                case SoundCategory.Sfx:
                    EventBus.Publish(new PlaySfxCommand(intent.Name));
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

        // ── 스크린 FX(M3 슬라이스2: FadeOut/FadeIn/Flash) ──
        // 순수 FxParser로 스크린 FX만 인식(나머지 FX는 슬라이스 밖 스킵) → 동결 수치(ScreenFxTuningSO)로
        // duration 해석 → ShowScreenFxCommand 발행 → Next 대기(WaitNext). ScreenFxView가 최상위 오버레이로 표시.

        IEnumerator PlayFx(ScriptLine line)
        {
            // 스크린 오버레이(FadeOut/FadeIn/Flash) 먼저 시도.
            var screen = FxParser.ParseScreen(line.Value);
            if (screen.IsValid)
            {
                float dur = screen.Duration >= 0f ? screen.Duration : ResolveFxDuration(screen.Kind);
                var req = new CompletionHandle();
                EventBus.Publish(new ShowScreenFxCommand(screen.Kind, dur, req));
                yield return WaitNext(line, () => req.IsComplete);
                yield break;
            }

            // 흔들기(StageShake/DialogueShake/CharShake) 시도.
            var shake = ShakeParser.Parse(line.Value);
            if (shake.IsValid)
            {
                yield return PlayShake(line, shake);
                yield break;
            }

            // 카메라/Eye/Tint/캐릭터(Jump/Dim/Glitch)/매크로 등 — 이번 슬라이스 미지원.
            Log.Info($"[NarrativeController] 슬라이스 범위 밖 FX 스킵: \"{line.Value}\"");
        }

        float ResolveFxDuration(ScreenFxKind kind)
        {
            if (kind == ScreenFxKind.Flash)
                return fxTuning != null ? fxTuning.FlashDefault : 0.14f;
            return fxTuning != null ? fxTuning.FadeDefault : 0.9f;
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
    }
}
