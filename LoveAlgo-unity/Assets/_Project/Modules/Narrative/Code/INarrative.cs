using System;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;
using UnityEngine;

namespace LoveAlgo.Narrative
{
    /// <summary>
    /// 내러티브(스크립트 엔진) 모듈 외부 계약.
    /// 구현: <see cref="NarrativeModule"/>. 내부 위임: <see cref="ScriptRunner"/>.
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
    }
}
