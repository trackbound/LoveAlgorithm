using System.Collections.Generic;
using UnityEngine; // TextAsset

namespace LoveAlgo.Events
{
    // ── 내러티브 런타임 명령/통지 이벤트(M3 슬라이스1: 대사+선택지) ──
    // 엔진(NarrativeController)이 UI에 "무엇을 보여라"만 명령하고, 구체 UI(DialogueView/ChoiceView)가 구독해
    // 표시한다(ADR-007: Service Locator·UI 직접참조 없음). 클릭/선택 결과는 완료 핸들(아래 *Request)에
    // 실어 되돌린다 — HANDOFF의 "완료-이벤트(완료 핸들 실은 이벤트)" 패턴. 핸들은 참조형 클래스라
    // readonly struct 이벤트에 안전하게 실린다. Core asmdef에 두는 이유: 발행자(Narrative)와 구독자(UI)가
    // 공통 참조할 수 있는 최하위 계층.

    /// <summary>
    /// 선택지 완료 핸들. 뷰가 사용자가 고른 옵션 인덱스를 <see cref="Select"/>로 채우면,
    /// 엔진 코루틴은 <see cref="IsComplete"/>가 참이 될 때까지 대기한다.
    /// </summary>
    public sealed class ChoiceRequest
    {
        public int SelectedIndex { get; private set; } = -1;
        public bool IsComplete => SelectedIndex >= 0;
        public void Select(int index) { if (index >= 0) SelectedIndex = index; }
    }

    /// <summary>
    /// 스토리 스크립트 재생 시작 명령. <see cref="InlineCsv"/>가 있으면 그것을, 없으면 <see cref="Script"/>를
    /// 파싱해 재생한다(테스트는 InlineCsv로 주입). <c>NarrativeController</c>가 구독.
    /// </summary>
    public readonly struct PlayScriptCommand
    {
        public readonly TextAsset Script;
        public readonly string InlineCsv;
        public readonly string Name;

        public PlayScriptCommand(TextAsset script, string name = null)
        {
            Script = script;
            InlineCsv = null;
            Name = string.IsNullOrEmpty(name) ? (script != null ? script.name : "") : name;
        }

        public PlayScriptCommand(string inlineCsv, string name)
        {
            Script = null;
            InlineCsv = inlineCsv;
            Name = name ?? "";
        }
    }

    /// <summary>
    /// 인라인 일시정지 지점. <see cref="CharIndex"/>번째 글자가 표시된 직후 <see cref="Seconds"/>초 멈춘다
    /// (대사 본문의 <c>&lt;wait:sec&gt;</c> 태그 위치). 표시 텍스트는 태그가 제거된 상태.
    /// </summary>
    public readonly struct InlinePause
    {
        public readonly int CharIndex;
        public readonly float Seconds;
        public InlinePause(int charIndex, float seconds) { CharIndex = charIndex; Seconds = seconds; }
    }

    /// <summary>
    /// 인라인 표정 지점. <see cref="CharIndex"/>번째 글자가 표시되는 시점에 화자의 캐릭터가 표정을
    /// <see cref="Emote"/>로 바꾼다(대사 본문의 <c>&lt;emote=표정/&gt;</c> 태그 위치). 표시 텍스트는 태그 제거 상태.
    /// </summary>
    public readonly struct InlineEmote
    {
        public readonly int CharIndex;
        public readonly string Emote;
        public InlineEmote(int charIndex, string emote) { CharIndex = charIndex; Emote = emote; }
    }

    /// <summary>
    /// 대사 1줄 표시 명령. <see cref="RequireClick"/>=true(Next=click)면 뷰는 타이핑 후 클릭 입력까지
    /// 기다린 뒤 핸들을 완료한다. false면 타이핑이 끝나는 즉시 완료(딜레이/즉시 진행은 엔진이 처리).
    /// <see cref="Text"/>는 인라인 태그가 제거된 표시용 텍스트이며, <see cref="Pauses"/>가 타이핑 중 멈춤 지점을 준다
    /// (엔진이 파싱해 채움 — 뷰는 표시만). 인라인 태그가 없으면 Pauses=null.
    /// </summary>
    public readonly struct ShowDialogueCommand
    {
        public readonly string Speaker;
        public readonly string Text;
        public readonly bool RequireClick;
        public readonly CompletionHandle Handle;
        public readonly IReadOnlyList<InlinePause> Pauses;
        public readonly IReadOnlyList<InlineEmote> Emotes;
        /// <summary>화자의 캐릭터 코드 ID(엔진이 별칭 카탈로그로 해석, 예: "c01"). 인라인 &lt;emote&gt;의 슬롯
        /// 매칭용 — 표시는 <see cref="Speaker"/>(원문) 그대로. 미등록 화자/카탈로그 부재면 null(뷰는 Speaker 폴백).</summary>
        public readonly string SpeakerId;

        public ShowDialogueCommand(string speaker, string text, bool requireClick, CompletionHandle handle,
            IReadOnlyList<InlinePause> pauses = null, IReadOnlyList<InlineEmote> emotes = null, string speakerId = null)
        {
            Speaker = speaker;
            Text = text;
            RequireClick = requireClick;
            Handle = handle;
            Pauses = pauses;
            Emotes = emotes;
            SpeakerId = speakerId;
        }
    }

    /// <summary>
    /// 화자 캐릭터의 표정 변경 *명령*(인라인 <c>&lt;emote=표정/&gt;</c>). DialogueView가 타이핑 중 해당 지점에서
    /// 발행 → StageView가 구독해, 그 화자가 올라간 슬롯의 스프라이트를 표정 버전으로 즉시 교체한다(ADR-007:
    /// UI끼리도 직접참조 없이 EventBus). 화자→슬롯은 StageView가 Char 명령으로 추적한 슬롯→캐릭터에 직접 매칭.
    /// Speaker엔 해석된 캐릭터 코드 ID(<c>ShowDialogueCommand.SpeakerId</c>)가 실린다 — 미해석 시 원문 화자명 폴백.
    /// </summary>
    public readonly struct ShowSpeakerEmoteCommand
    {
        public readonly string Speaker;
        public readonly string Emote;
        public ShowSpeakerEmoteCommand(string speaker, string emote) { Speaker = speaker; Emote = emote; }
    }

    /// <summary>
    /// 선택지 표시 명령. <see cref="OptionTexts"/> 순서대로 버튼을 만들고, 클릭 시 핸들에 인덱스를 채운다.
    /// 효과/점프 해석은 엔진(NarrativeController) 몫 — 뷰는 라벨 표시와 선택 인덱스 회수만 한다.
    /// </summary>
    public readonly struct ShowChoiceCommand
    {
        public readonly IReadOnlyList<string> OptionTexts;
        public readonly ChoiceRequest Handle;

        public ShowChoiceCommand(IReadOnlyList<string> optionTexts, ChoiceRequest handle)
        {
            OptionTexts = optionTexts;
            Handle = handle;
        }
    }

    /// <summary>
    /// 스크립트 재생이 끝났음을 알리는 통지(EventBus). 페이즈 오케스트레이션/시뮬 복귀가 구독.
    /// (엔진은 종료 직전 <c>RequestPhaseCommand(Schedule)</c>도 함께 발행한다.)
    /// </summary>
    public readonly struct NarrativeFinishedEvent
    {
        public readonly string ScriptName;
        public NarrativeFinishedEvent(string scriptName) { ScriptName = scriptName; }
    }

    /// <summary>
    /// 연출 뷰(스테이지/배경/캐릭터·CG·SD·Overlay·페이드·틴트·아이마스크·카메라·흔들기)를 즉시 초기화하라는 명령.
    /// 스토리 기획 도구가 새 스크립트를 Apply하기 직전에 발행해, 직전 재생을 abort로 끊었을 때 남는 잔여 연출
    /// (틴트/검은 바/스테이지 등)을 청소한다 — abort는 <see cref="NarrativeFinishedEvent"/>를 쏘지 않으므로
    /// (데이루프 보호). 연출 뷰만 구독하고 GameManager는 구독하지 않는다(하루가 넘어가지 않음). 파라미터 없음.
    /// </summary>
    public readonly struct ResetNarrativeViewsCommand
    {
    }

    /// <summary>
    /// 오토(자동 진행) 모드 토글 명령(M3 슬라이스2). <see cref="On"/>=true면 대사 뷰가 클릭 없이 타이핑 완료 후
    /// 지연을 두고 자동 진행한다(클릭 시 즉시). DialogueView가 구독. 토글 UI/설정 영속화는 후속.
    /// </summary>
    public readonly struct SetAutoModeCommand
    {
        public readonly bool On;
        public SetAutoModeCommand(bool on) { On = on; }
    }

    /// <summary>대사창(루트) 표시/숨김 명령. FX 매크로 DialogueShow/DialogueHide가 발행 → DialogueView가 root를 토글.</summary>
    public readonly struct SetDialogueVisibleCommand
    {
        public readonly bool Visible;
        public SetDialogueVisibleCommand(bool visible) { Visible = visible; }
    }

    /// <summary>
    /// 독백 오버레이 표시/숨김 명령. 화자가 빈 Text 라인(독백/내레이션)에 진입하면 <see cref="Active"/>=true,
    /// 화자 있는 대사면 false로 <c>NarrativeController.PlayText</c>가 발행(판정=<c>ScriptLine.IsNarration</c>) →
    /// <c>MonologueOverlayView</c>가 전용 오버레이를 페이드 토글한다(ADR-007: 엔진은 뷰를 모름).
    /// 로아 전용 오버레이(<see cref="StageLayerKind"/>.Overlay)와 별개의 상위 레이어 — 트리거(자동)·z(더 위)가 다르다.
    /// </summary>
    public readonly struct SetMonologueOverlayCommand
    {
        public readonly bool Active;
        public SetMonologueOverlayCommand(bool active) { Active = active; }
    }
}
