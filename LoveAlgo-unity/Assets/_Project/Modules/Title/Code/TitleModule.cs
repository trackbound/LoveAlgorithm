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
        [Header("UI Prefabs (모듈 응집)")]
        [SerializeField] TitlePanel titlePanelPrefab;
        [SerializeField] UsernameUI usernameUIPrefab;
        [SerializeField] ExtraPopup extraPopupPrefab;

        TitlePanel _titlePanel;
        UsernameUI _usernameUI;
        ExtraPopup _extraPopupInstance;

        public TitlePanel TitlePanel => _titlePanel != null
            ? _titlePanel
            : (_titlePanel = SpawnUI(titlePanelPrefab, UIGroup.Scene));

        public UsernameUI UsernameUI => _usernameUI != null
            ? _usernameUI
            : (_usernameUI = SpawnUI(usernameUIPrefab, UIGroup.Scene));

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
            var popup = _extraPopupInstance != null
                ? _extraPopupInstance
                : PopupManager.Instance?.Get<ExtraPopup>();
            popup?.Show();
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
