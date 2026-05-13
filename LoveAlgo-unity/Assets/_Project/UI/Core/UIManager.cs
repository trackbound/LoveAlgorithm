using LoveAlgo.Common;
using LoveAlgo.Narrative;
using LoveAlgo.Schedule;
using LoveAlgo.Shop;
using LoveAlgo.Story;
using LoveAlgo.Title;
using LoveAlgo.Tutorial;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI 매니저 — UI 인스턴스 부모 그룹 제공 + 호환성 wrapper.
    /// 메인 UI 인스턴스는 각 모듈이 lazy spawn (모듈 응집).
    /// 호환성: 옛 코드의 UIManager.Instance.X 호출은 모듈로 위임됨.
    /// </summary>
    public class UIManager : SingletonMonoBehaviour<UIManager>
    {
        [Header("UI Group Roots (인스턴스 부모)")]
        [Tooltip("비워두면 UIManager 하위에 자동 생성. 모듈이 GetGroupRoot()로 빌려 씀.")]
        [SerializeField] Transform storyRoot;
        [SerializeField] Transform simulateRoot;
        [SerializeField] Transform sceneRoot;

        // ── UI 인스턴스 호환성 wrapper (모듈 위임) ───────────────────
        // 옛 호출자(UIManager.Instance.X)를 위한 1줄 wrapper. 새 코드는 Services.Get<I*>() 직접 사용.
        public DialogueUI DialogueUI => Services.Get<INarrative>()?.DialogueUI;
        public DialogueShowButton DialogueShowButton => Services.Get<INarrative>()?.DialogueShowButton;
        public ChoicePopup ChoicePopup => Services.Get<INarrative>()?.ChoicePopup;
        public ScheduleUI ScheduleUI => Services.Get<ISchedule>()?.ScheduleUI;
        public ShopUI ShopUI => Services.Get<IShop>()?.ShopUI;
        public TitlePanel TitlePanel => Services.Get<ITitle>()?.TitlePanel;
        public UsernameUI UsernameUI => Services.Get<ITitle>()?.UsernameUI;
        public TutorialOverlay TutorialOverlay => Services.Get<ITutorial>()?.Overlay;

        /// <summary>UI 인스턴스 부모 그룹 — 모듈이 자기 UI를 spawn할 때 사용.</summary>
        public Transform GetGroupRoot(UIGroup group)
        {
            switch (group)
            {
                case UIGroup.Story:    return storyRoot    != null ? storyRoot    : EnsureGroup(ref storyRoot,    "Story",    0);
                case UIGroup.Simulate: return simulateRoot != null ? simulateRoot : EnsureGroup(ref simulateRoot, "Simulate", 1);
                case UIGroup.Scene:    return sceneRoot    != null ? sceneRoot    : EnsureGroup(ref sceneRoot,    "Scene",    2);
            }
            return transform;
        }

        Transform EnsureGroup(ref Transform field, string groupName, int siblingIndex)
        {
            var existing = transform.Find(groupName);
            if (existing != null) { field = existing; return field; }

            var go = new GameObject(groupName, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetSiblingIndex(siblingIndex);
            field = rt;
            return field;
        }

        static void SetActiveIfExists(MonoBehaviour ui, bool active)
        {
            if (ui != null) ui.gameObject.SetActive(active);
        }

        /// <summary>모든 메인 UI 숨기기 (생성된 인스턴스에 한해).</summary>
        public void HideAll()
        {
            var narr = Services.Get<INarrative>();
            SetActiveIfExists(narr?.DialogueUI, false);
            SetActiveIfExists(Services.Get<ISchedule>()?.ScheduleUI, false);
            var title = Services.Get<ITitle>();
            SetActiveIfExists(title?.TitlePanel, false);
            SetActiveIfExists(title?.UsernameUI, false);
            PopupManager.Instance?.Get<PlaceNotification>()?.HideImmediate();
        }

        /// <summary>특정 UI만 표시 (나머지 숨김). 표시 대상은 모듈 lazy-instantiate.</summary>
        public void ShowOnly(MainUIType type)
        {
            HideAll();
            switch (type)
            {
                case MainUIType.Dialogue:
                case MainUIType.Ending:
                    SetActiveIfExists(DialogueUI, true);
                    break;
                case MainUIType.Schedule:
                    SetActiveIfExists(ScheduleUI, true);
                    break;
                case MainUIType.Title:
                    SetActiveIfExists(TitlePanel, true);
                    break;
                case MainUIType.Username:
                    SetActiveIfExists(UsernameUI, true);
                    break;
            }
        }
    }

    /// <summary>UI 인스턴스 부모 그룹 — 모듈이 자기 UI를 spawn할 때 사용.</summary>
    public enum UIGroup
    {
        Story,      // DialogueUI, ChoicePopup, DialogueShowButton 등
        Simulate,   // ScheduleUI, ShopUI, QuickMenu, TutorialOverlay 등
        Scene       // TitlePanel, UsernameUI 등 씬 단위
    }

    public enum MainUIType
    {
        Dialogue,
        Schedule,
        Title,
        Username,
        Ending
    }
}
