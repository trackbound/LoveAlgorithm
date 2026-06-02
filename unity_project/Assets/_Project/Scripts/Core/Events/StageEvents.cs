namespace LoveAlgo.Events
{
    // ── 스테이지 런타임 명령/인텐트 (M3 슬라이스2: BG + Char) ──
    // 슬라이스1 대사/선택지와 같은 구조: 순수 파서(StageParser)가 CSV Value를 인텐트(BgIntent/CharIntent)로
    // 분해하고, 엔진(NarrativeController)이 동결 수치(StageTuningSO)로 duration을 해석해 명령(Show*Command)에
    // 실어 발행한다. 뷰(StageView)는 명령만 구독해 표시하고 완료 핸들(CompletionHandle)을 풀어준다(ADR-007:
    // Service Locator·UI 직접참조 없음). enum/인텐트를 Core에 두는 이유 = 발행자(Narrative)·구독자(UI) 공통 최하위층.
    //
    // 이번 슬라이스 밖(다음): Overlay(로아 VirtualBG/모드)·CG·SD·CharFX(Shake/Jump/Glitch)·슬라이드(EnterUp/
    // ExitDown)·등장 SFX·speaker 자동등장. 별칭/카탈로그(한글명→파일) 없이 컨벤션 로딩으로 시작(실 스토리 이식 시 후속).

    /// <summary>배경 전환 방식. 구 <c>BGTransition</c> 1:1.</summary>
    public enum BgTransition
    {
        Cut,    // 즉시 교체
        Fade,   // 검은 화면 경유(front 페이드아웃→교체→페이드인)
        Cross   // 크로스페이드(back 페이드인 + front 페이드아웃)
    }

    /// <summary>캐릭터 슬롯 위치(좌/중앙/우). 생략 시 C.</summary>
    public enum CharSlot { L, C, R }

    /// <summary>캐릭터 슬롯 액션(이번 슬라이스 범위). EnterUp/ExitDown/Mode는 후속.</summary>
    public enum CharAction
    {
        Enter,  // 페이드 등장(캐릭터+표정)
        Exit,   // 페이드 퇴장
        Emote,  // 표정만 교체(페이드)
        Clear   // 즉시 숨김
    }

    /// <summary>
    /// BG 명령 파싱 결과(순수). <see cref="Duration"/>가 음수면 "CSV 미지정" — 엔진이 <c>StageTuningSO</c> 기본값으로 해석.
    /// </summary>
    public readonly struct BgIntent
    {
        public readonly string Name;
        public readonly BgTransition Transition;
        public readonly float Duration;
        public bool IsValid => !string.IsNullOrEmpty(Name);

        public BgIntent(string name, BgTransition transition, float duration)
        {
            Name = name;
            Transition = transition;
            Duration = duration;
        }
    }

    /// <summary>
    /// Char 명령 파싱 결과(순수). <see cref="Duration"/>가 음수면 "CSV 미지정" — 엔진이 액션별 기본값으로 해석.
    /// Exit/Clear는 <see cref="Character"/>/<see cref="Emote"/>를 사용하지 않는다.
    /// </summary>
    public readonly struct CharIntent
    {
        public readonly CharSlot Slot;
        public readonly CharAction Action;
        public readonly string Character;
        public readonly string Emote;
        public readonly float Duration;
        public bool IsValid => Action != CharAction.Enter || !string.IsNullOrEmpty(Character);

        public CharIntent(CharSlot slot, CharAction action, string character, string emote, float duration)
        {
            Slot = slot;
            Action = action;
            Character = character;
            Emote = emote;
            Duration = duration;
        }
    }

    /// <summary>
    /// 배경 표시 명령. <see cref="Duration"/>은 엔진이 이미 해석한 최종값(초). 뷰는 그대로 전환한다.
    /// </summary>
    public readonly struct ShowBackgroundCommand
    {
        public readonly string Name;
        public readonly BgTransition Transition;
        public readonly float Duration;
        public readonly CompletionHandle Handle;

        public ShowBackgroundCommand(string name, BgTransition transition, float duration, CompletionHandle handle)
        {
            Name = name;
            Transition = transition;
            Duration = duration;
            Handle = handle;
        }
    }

    /// <summary>
    /// 캐릭터 슬롯 제어 명령. <see cref="Duration"/>은 엔진이 해석한 최종 페이드 시간(초, Clear는 무시).
    /// </summary>
    public readonly struct ShowCharacterCommand
    {
        public readonly CharSlot Slot;
        public readonly CharAction Action;
        public readonly string Character;
        public readonly string Emote;
        public readonly float Duration;
        public readonly CompletionHandle Handle;

        public ShowCharacterCommand(CharSlot slot, CharAction action, string character, string emote, float duration, CompletionHandle handle)
        {
            Slot = slot;
            Action = action;
            Character = character;
            Emote = emote;
            Duration = duration;
            Handle = handle;
        }
    }
}
