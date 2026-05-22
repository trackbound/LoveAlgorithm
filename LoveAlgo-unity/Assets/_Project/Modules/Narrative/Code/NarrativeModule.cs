using LoveAlgo.Contracts;
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
        [Header("Popups (lazy spawn — PopupManager 등록)")]
        [SerializeField] LogPopup logPopupPrefab;

        [Header("UI (씬 인스턴스 우선 / 없으면 prefab spawn)")]
        [Tooltip("씬에 미리 배치된 인스턴스 (자주 사용되는 UI에 권장). 비어있으면 prefab으로 spawn.")]
        [SerializeField] DialogueUI dialogueUISceneInstance;
        [SerializeField] DialogueUI dialogueUIPrefab;
        [SerializeField] DialogueShowButton dialogueShowButtonSceneInstance;
        [SerializeField] DialogueShowButton dialogueShowButtonPrefab;
        [SerializeField] ChoicePopup choicePopupSceneInstance;
        [SerializeField] ChoicePopup choicePopupPrefab;

        LogPopup logPopupInstance;
        DialogueUI _dialogueUI;
        DialogueShowButton _dialogueShowButton;
        ChoicePopup _choicePopup;

        public DialogueUI DialogueUI
        {
            get
            {
                if (_dialogueUI != null) return _dialogueUI;
                _dialogueUI = ResolveOrSpawn(dialogueUISceneInstance, dialogueUIPrefab, UIGroup.Narrative);

                // DialogueShowButton 동반 (대사창 항상 동반)
                if (_dialogueShowButton == null)
                {
                    _dialogueShowButton = ResolveOrSpawn(dialogueShowButtonSceneInstance, dialogueShowButtonPrefab, UIGroup.Narrative);
                    if (_dialogueShowButton != null && _dialogueUI != null)
                    {
                        _dialogueShowButton.Bind(_dialogueUI);
                        _dialogueShowButton.gameObject.SetActive(true);
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

        public ChoicePopup ChoicePopup
        {
            get
            {
                if (_choicePopup != null) return _choicePopup;
                _choicePopup = ResolveOrSpawn(choicePopupSceneInstance, choicePopupPrefab, UIGroup.Narrative);
                return _choicePopup;
            }
        }

        void Awake()
        {
            Services.Register<INarrative>(this);
            if (logPopupPrefab != null && PopupManager.Instance != null)
                logPopupInstance = PopupManager.Instance.Register(logPopupPrefab);
        }

        /// <summary>씬에 미리 배치된 인스턴스가 있으면 그대로, 없으면 prefab으로 spawn.</summary>
        T ResolveOrSpawn<T>(T sceneInstance, T prefab, UIGroup group) where T : MonoBehaviour
        {
            if (sceneInstance != null) return sceneInstance;
            return SpawnUI(prefab, group);
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
            var popup = EnsureLogPopup();
            popup?.Show(log);
        }

        LogPopup EnsureLogPopup()
        {
            if (logPopupInstance != null) return logPopupInstance;
            if (logPopupPrefab == null) return null;
            var pm = PopupManager.Instance;
            if (pm == null) return null;
            logPopupInstance = pm.Register(logPopupPrefab);
            return logPopupInstance;
        }
    }
}
