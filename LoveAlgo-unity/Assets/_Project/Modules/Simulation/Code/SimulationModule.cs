using System;
using System.Collections.Generic;
using LoveAlgo.Common;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Simulation
{
    /// <summary>
    /// 시뮬레이션 컨텍스트 호스트.
    /// 자체 sub-mode 구현은 모르고, sub-mode가 RegisterSubMode로 자기 등록.
    /// QuickMenu는 컨텍스트 동안 상시 표시.
    /// 씬 하이어라키: _Modules/SimulationModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class SimulationModule : MonoBehaviour, ISimulation
    {
        [Header("QuickMenu Prefab (시뮬 컨텍스트 사이드바)")]
        [SerializeField] QuickMenu quickMenuPrefab;

        [Header("기본 메인 모드 (Enter 시 진입)")]
        [SerializeField] SimulationMode mainMode = SimulationMode.Schedule;

        readonly Dictionary<SimulationMode, ISimulationSubMode> subModes = new();
        QuickMenu _quickMenu;

        public SimulationMode CurrentMode { get; private set; } = SimulationMode.None;
        public bool IsActive => CurrentMode != SimulationMode.None;

        public event Action OnEntered;
        public event Action OnExited;
        public event Action<SimulationMode> OnSubModeChanged;

        void Awake() => Services.Register<ISimulation>(this);

        void OnDestroy()
        {
            if (Services.TryGet<ISimulation>() == (ISimulation)this)
                Services.Unregister<ISimulation>();
        }

        public void RegisterSubMode(ISimulationSubMode subMode)
        {
            if (subMode == null) return;
            subModes[subMode.Mode] = subMode;
        }

        public void EnterSimulation(SimulationMode mainModeOverride = SimulationMode.None)
        {
            EnsureQuickMenu();
            _quickMenu?.gameObject.SetActive(true);

            var target = (mainModeOverride == SimulationMode.None) ? mainMode : mainModeOverride;
            OpenSubMode(target);
            OnEntered?.Invoke();
        }

        public void OpenSubMode(SimulationMode mode)
        {
            if (subModes.TryGetValue(CurrentMode, out var prev))
                prev?.Exit();

            CurrentMode = mode;
            if (subModes.TryGetValue(mode, out var next))
                next?.Enter();

            OnSubModeChanged?.Invoke(mode);
        }

        public void CloseSubMode()
        {
            if (CurrentMode == mainMode || CurrentMode == SimulationMode.None)
                ExitSimulation();
            else
                OpenSubMode(mainMode);
        }

        public void ExitSimulation()
        {
            if (subModes.TryGetValue(CurrentMode, out var cur))
                cur?.Exit();

            if (_quickMenu != null) _quickMenu.gameObject.SetActive(false);
            CurrentMode = SimulationMode.None;
            OnExited?.Invoke();
        }

        void EnsureQuickMenu()
        {
            if (_quickMenu != null) return;
            if (quickMenuPrefab == null)
            {
                Debug.LogWarning("[SimulationModule] quickMenuPrefab not assigned");
                return;
            }
            _quickMenu = Instantiate(quickMenuPrefab);
            _quickMenu.name = quickMenuPrefab.name;
            _quickMenu.gameObject.SetActive(false);
            UISoundManager.Instance?.BindButtonsInTransform(_quickMenu.transform);
        }
    }
}
