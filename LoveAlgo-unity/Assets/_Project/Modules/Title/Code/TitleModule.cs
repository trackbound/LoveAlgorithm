using LoveAlgo.Contracts;
using LoveAlgo.Common;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Title
{
    /// <summary>
    /// 타이틀 모듈 진입점.
    /// TitlePanel/UsernameUI lazy spawn + ExtraPopup PopupManager 등록.
    /// 씬 하이어라키: _Modules/TitleModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class TitleModule : MonoBehaviour, ITitle
    {
        [Header("UI (씬 인스턴스 우선 / 없으면 prefab spawn)")]
        [Tooltip("씬에 미리 배치된 인스턴스. 비어있으면 prefab spawn.")]
        [SerializeField] TitlePanel titlePanelSceneInstance;
        [SerializeField] TitlePanel titlePanelPrefab;
        [SerializeField] UsernameUI usernameUISceneInstance;
        [SerializeField] UsernameUI usernameUIPrefab;

        [Header("Popups (PopupManager 등록)")]
        [SerializeField] ExtraPopup extraPopupPrefab;

        TitlePanel _titlePanel;
        UsernameUI _usernameUI;
        ExtraPopup _extraPopupInstance;

        public TitlePanel TitlePanel
        {
            get
            {
                if (_titlePanel != null) return _titlePanel;
                if (titlePanelSceneInstance != null) return _titlePanel = titlePanelSceneInstance;
                return _titlePanel = SpawnUI(titlePanelPrefab, UIGroup.Title);
            }
        }

        // ITitle.UsernameUI 는 IUsernameUI 반환 — concrete UsernameUI 가 인터페이스 구현.
        public IUsernameUI UsernameUI
        {
            get
            {
                if (_usernameUI != null) return _usernameUI;
                if (usernameUISceneInstance != null) return _usernameUI = usernameUISceneInstance;
                return _usernameUI = SpawnUI(usernameUIPrefab, UIGroup.Title);
            }
        }

        void Awake()
        {
            Services.Register<ITitle>(this);
            if (extraPopupPrefab != null && PopupManager.Instance != null)
                _extraPopupInstance = PopupManager.Instance.Register(extraPopupPrefab);
        }

        void OnDestroy()
        {
            if (Services.TryGet<ITitle>() == (ITitle)this)
                Services.Unregister<ITitle>();
        }

        public void ShowExtraUI()
        {
            var popup = EnsureExtraPopup();
            popup?.Show();
        }

        ExtraPopup EnsureExtraPopup()
        {
            if (_extraPopupInstance != null) return _extraPopupInstance;
            if (extraPopupPrefab == null) return null;
            var pm = PopupManager.Instance;
            if (pm == null) return null;
            _extraPopupInstance = pm.Register(extraPopupPrefab);
            return _extraPopupInstance;
        }

        T SpawnUI<T>(T prefab, UIGroup group) where T : MonoBehaviour
        {
            if (prefab == null) return null;
            var parent = UIManager.Instance?.GetGroupRoot(group);
            var inst = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
            inst.name = prefab.name;
            UISoundManager.Instance?.BindButtonsInTransform(inst.transform);
            return inst;
        }
    }
}
