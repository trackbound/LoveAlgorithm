using LoveAlgo.Contracts;
using LoveAlgo.Common;
using LoveAlgo.LockScreen.UI;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.LockScreen
{
    /// <summary>
    /// PC잠금 모듈 진입점.
    /// LockScreenController를 ILockScreen으로 노출 + LockScreenPanel lazy spawn.
    /// 씬: _Modules/LockScreenModule (Controller + Module 컴포넌트)
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(LockScreenController))]
    public class LockScreenModule : MonoBehaviour
    {
        [Header("Panel (씬 인스턴스 우선 / 없으면 prefab spawn)")]
        [SerializeField] LockScreenPanel panelSceneInstance;
        [SerializeField] LockScreenPanel panelPrefab;

        LockScreenController controller;
        LockScreenPanel _panel;

        public LockScreenPanel Panel
        {
            get
            {
                if (_panel != null) return _panel;
                if (panelSceneInstance != null) return _panel = panelSceneInstance;
                if (panelPrefab == null) return null;

                // UIGroup.Title 재활용 — LockScreen 전용 그룹 신설 시 변경
                var parent = UIManager.Instance?.GetGroupRoot(UIGroup.Title);
                _panel = parent != null ? Instantiate(panelPrefab, parent) : Instantiate(panelPrefab);
                _panel.name = panelPrefab.name;
                UISoundManager.Instance?.BindButtonsInTransform(_panel.transform);
                _panel.gameObject.SetActive(false);
                return _panel;
            }
        }

        void Awake()
        {
            controller = GetComponent<LockScreenController>();
            if (controller != null)
                Services.Register<ILockScreen>(controller);
        }

        void OnDestroy()
        {
            if (controller != null && Services.TryGet<ILockScreen>() == (ILockScreen)controller)
                Services.Unregister<ILockScreen>();
        }
    }
}
