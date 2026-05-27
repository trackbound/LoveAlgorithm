using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 내러티브(스크립트 엔진) 모듈 외부 계약.
    /// 구현: <see cref="LoveAlgo.Narrative.NarrativeModule"/>. 내부 위임: <see cref="ScriptRunner"/>.
    /// </summary>
    public interface INarrative
    {
        // ── 상태 ──────────────────────────────────────────────
        bool IsRunning { get; }
        bool IsAutoMode { get; }
        bool IsWaitingForClick { get; }
        string CurrentScriptName { get; }
        int CurrentIndex { get; }
        ScriptLine CurrentLine { get; }
        int LineCount { get; }

        // ── 이벤트 ────────────────────────────────────────────
        event Action<bool> OnAutoModeChanged;
        event Action OnScriptEnd;

        // ── 로드/실행 ─────────────────────────────────────────
        void LoadScript(TextAsset asset);
        void LoadScript(string csv, string scriptName = null);
        UniTask StartScript(string scriptName);
        UniTask StartScriptFrom(string scriptName, string lineId, int lineIdx);
        void Run();
        void RunFrom(string lineId);
        void Stop();

        // ── 제어 ──────────────────────────────────────────────
        ScriptLine GetLine(int index);
        void JumpToIndex(int index);
        void Rewind(int textCount = 1);
        void OnClick();
        void ToggleAutoMode();
        void SetAutoMode(bool enabled);
        void SetAutoDelay(float normalized);

        /// <summary>대사 타이핑 속도 (0=느림, 1=빠름 — 정규화된 슬라이더 값). Settings UI에서 호출.</summary>
        void SetTextSpeed(float normalized);

        // ── UI 진입점 ────────────────────────────────
        /// <summary>대사 로그 팝업 표시.</summary>
        void ShowLogUI(IReadOnlyList<DialogueLogEntry> log);

        // ── UI 인스턴스 노출 (모듈 응집 — UIManager.lazy spawn에서 이전) ────
        // Phase B-7a: DialogueShowButton 은 NarrativeModule 내부 동반 spawn(DialogueUI getter)
        //             전용이라 외부 호출자 0. INarrative 멤버 제거.
        DialogueUI DialogueUI { get; }
        IChoicePopup ChoicePopup { get; }
    }
}
