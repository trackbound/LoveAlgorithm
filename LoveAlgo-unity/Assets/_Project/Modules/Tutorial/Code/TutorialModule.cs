using LoveAlgo.Common;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Tutorial
{
    /// <summary>
    /// 튜토리얼 모듈 진입점.
    /// TutorialOverlay lazy spawn 소유.
    /// 씬 하이어라키: _Modules/TutorialModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class TutorialModule : MonoBehaviour, ITutorial
    {
        [Header("UI Prefab (모듈 응집)")]
        [SerializeField] TutorialOverlay overlayPrefab;

        TutorialOverlay _overlay;

        public TutorialOverlay Overlay
        {
            get
            {
                if (_overlay == null && overlayPrefab != null)
                {
                    var parent = UIManager.Instance?.GetGroupRoot(UIGroup.Simulate);
                    _overlay = parent != null ? Instantiate(overlayPrefab, parent) : Instantiate(overlayPrefab);
                    _overlay.name = overlayPrefab.name;
                    _overlay.gameObject.SetActive(false);
                    UISoundManager.Instance?.BindButtonsInTransform(_overlay.transform);
                }
                return _overlay;
            }
        }

        void Awake() => Services.Register<ITutorial>(this);

        void OnDestroy()
        {
            if (Services.TryGet<ITutorial>() == (ITutorial)this)
                Services.Unregister<ITutorial>();
        }
    }
}
