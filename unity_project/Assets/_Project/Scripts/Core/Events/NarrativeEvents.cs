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
    /// 대사 1줄 표시 완료 핸들. 뷰가 타이핑/클릭 진행을 마치면 <see cref="Complete"/>를 호출하고,
    /// 엔진 코루틴은 <see cref="IsComplete"/>가 참이 될 때까지 대기한다.
    /// </summary>
    public sealed class DialogueRequest
    {
        public bool IsComplete { get; private set; }
        public void Complete() => IsComplete = true;
    }

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
    /// 대사 1줄 표시 명령. <see cref="RequireClick"/>=true(Next=click)면 뷰는 타이핑 후 클릭 입력까지
    /// 기다린 뒤 핸들을 완료한다. false면 타이핑이 끝나는 즉시 완료(딜레이/즉시 진행은 엔진이 처리).
    /// </summary>
    public readonly struct ShowDialogueCommand
    {
        public readonly string Speaker;
        public readonly string Text;
        public readonly bool RequireClick;
        public readonly DialogueRequest Handle;

        public ShowDialogueCommand(string speaker, string text, bool requireClick, DialogueRequest handle)
        {
            Speaker = speaker;
            Text = text;
            RequireClick = requireClick;
            Handle = handle;
        }
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
    /// (엔진은 종료 직전 <c>ShowUiGroupCommand(Simulation)</c>도 함께 발행한다.)
    /// </summary>
    public readonly struct NarrativeFinishedEvent
    {
        public readonly string ScriptName;
        public NarrativeFinishedEvent(string scriptName) { ScriptName = scriptName; }
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
}
