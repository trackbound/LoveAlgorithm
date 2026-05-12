using System;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using LoveAlgo.Story;
using UnityEngine;

namespace LoveAlgo.Narrative
{
    /// <summary>
    /// 내러티브 모듈 진입점.
    /// ScriptRunner 싱글톤을 INarrative 인터페이스로 노출.
    /// 씬 하이어라키: _Modules/NarrativeModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class NarrativeModule : MonoBehaviour, INarrative
    {
        void Awake() => Services.Register<INarrative>(this);

        void OnDestroy()
        {
            if (Services.TryGet<INarrative>() == (INarrative)this)
                Services.Unregister<INarrative>();
        }

        ScriptRunner Runner => ScriptRunner.Instance;

        public bool IsRunning => Runner != null && Runner.IsRunning;
        public bool IsAutoMode => Runner != null && Runner.IsAutoMode;
        public bool IsWaitingForClick => Runner != null && Runner.IsWaitingForClick;
        public string CurrentScriptName => Runner?.CurrentScriptName;
        public int CurrentIndex => Runner?.CurrentIndex ?? 0;
        public ScriptLine CurrentLine => Runner?.CurrentLine;
        public int LineCount => Runner?.LineCount ?? 0;

        public event Action<bool> OnAutoModeChanged
        {
            add { if (Runner != null) Runner.OnAutoModeChanged += value; }
            remove { if (Runner != null) Runner.OnAutoModeChanged -= value; }
        }

        public event Action OnScriptEnd
        {
            add { if (Runner != null) Runner.OnScriptEnd += value; }
            remove { if (Runner != null) Runner.OnScriptEnd -= value; }
        }

        public void LoadScript(TextAsset asset) => Runner?.LoadScript(asset);
        public void LoadScript(string csv, string scriptName = null) => Runner?.LoadScript(csv, scriptName);

        public UniTask StartScript(string scriptName)
            => Runner != null ? Runner.StartScript(scriptName) : UniTask.CompletedTask;

        public UniTask StartScriptFrom(string scriptName, string lineId, int lineIdx)
            => Runner != null ? Runner.StartScriptFrom(scriptName, lineId, lineIdx) : UniTask.CompletedTask;

        public void Run() => Runner?.Run();
        public void RunFrom(string lineId) => Runner?.RunFrom(lineId);
        public void Stop() => Runner?.Stop();

        public ScriptLine GetLine(int index) => Runner?.GetLine(index);
        public void JumpToIndex(int index) => Runner?.JumpToIndex(index);
        public void Rewind(int textCount = 1) => Runner?.Rewind(textCount);
        public void OnClick() => Runner?.OnClick();
        public void ToggleAutoMode() => Runner?.ToggleAutoMode();
        public void SetAutoMode(bool enabled) => Runner?.SetAutoMode(enabled);
        public void SetAutoDelay(float normalized) => Runner?.SetAutoDelay(normalized);
    }
}
