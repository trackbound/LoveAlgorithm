using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using LoveAlgo.Story;
using LoveAlgo.UI;
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
        [Header("UI Prefabs (모듈 응집)")]
        [SerializeField] LogPopup logPopupPrefab;
        [SerializeField] DialogueUI dialogueUIPrefab;
        [SerializeField] DialogueShowButton dialogueShowButtonPrefab;
        [SerializeField] ChoicePopup choicePopupPrefab;

        LogPopup logPopupInstance;
        DialogueUI _dialogueUI;
        DialogueShowButton _dialogueShowButton;
        ChoicePopup _choicePopup;

        public DialogueUI DialogueUI
        {
            get
            {
                if (_dialogueUI == null && dialogueUIPrefab != null)
                {
                    _dialogueUI = SpawnUI(dialogueUIPrefab, UIGroup.Story);
                    // DialogueShowButton 동반 생성 (대사창 항상 동반)
                    if (dialogueShowButtonPrefab != null && _dialogueShowButton == null)
                    {
                        _dialogueShowButton = SpawnUI(dialogueShowButtonPrefab, UIGroup.Story);
                        if (_dialogueShowButton != null)
                        {
                            _dialogueShowButton.Bind(_dialogueUI);
                            _dialogueShowButton.gameObject.SetActive(true);
                        }
                    }
                }
                return _dialogueUI;
            }
        }

        public DialogueShowButton DialogueShowButton
        {
            get
            {
                if (_dialogueShowButton == null) _ = DialogueUI; // 동반 spawn 트리거
                return _dialogueShowButton;
            }
        }

        public ChoicePopup ChoicePopup => _choicePopup != null
            ? _choicePopup
            : (_choicePopup = SpawnUI(choicePopupPrefab, UIGroup.Story));

        void Awake()
        {
            Services.Register<INarrative>(this);
            if (logPopupPrefab != null && PopupManager.Instance != null)
                logPopupInstance = PopupManager.Instance.Register(logPopupPrefab);
        }

        T SpawnUI<T>(T prefab, UIGroup group) where T : MonoBehaviour
        {
            if (prefab == null) return null;
            var parent = UIManager.Instance?.GetGroupRoot(group);
            var inst = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
            inst.name = prefab.name;
            inst.gameObject.SetActive(false);
            UISoundManager.Instance?.BindButtonsInTransform(inst.transform);
            return inst;
        }

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

        // ── UI 진입점 ────────────────────────────────
        public void ShowLogUI(IReadOnlyList<DialogueLogEntry> log)
        {
            var popup = logPopupInstance != null ? logPopupInstance : PopupManager.Instance?.Get<LogPopup>();
            popup?.Show(log);
        }
    }
}
