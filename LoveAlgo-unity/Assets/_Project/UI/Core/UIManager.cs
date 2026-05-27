using LoveAlgo.Common;
using LoveAlgo.Contracts;
using LoveAlgo.Narrative;
using LoveAlgo.Schedule;
using LoveAlgo.Shop;
using LoveAlgo.Simulation;
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
        [UnityEngine.Serialization.FormerlySerializedAs("storyRoot")]
        [SerializeField] Transform narrativeRoot;
        [UnityEngine.Serialization.FormerlySerializedAs("simulateRoot")]
        [SerializeField] Transform simulationRoot;
        [UnityEngine.Serialization.FormerlySerializedAs("sceneRoot")]
        [SerializeField] Transform titleRoot;

        // ── UI 인스턴스 호환성 wrapper (모듈 위임) ───────────────────
        // 옛 호출자(UIManager.Instance.X)를 위한 1줄 wrapper. 새 코드는 Services.Get<I*>() 직접 사용.
        public DialogueUI DialogueUI => Services.TryGet<INarrative>()?.DialogueUI;
        public DialogueShowButton DialogueShowButton => Services.TryGet<INarrative>()?.DialogueShowButton;
        public ChoicePopup ChoicePopup => Services.TryGet<INarrative>()?.ChoicePopup;
        public ScheduleUI ScheduleUI => Services.TryGet<ISchedule>()?.ScheduleUI;
        public ShopUI ShopUI => Services.TryGet<IShop>()?.ShopUI;
        // ITitle.TitlePanel 는 ITitlePanel(인터페이스) 반환 — 옛 호출자 구체 타입 호환 위해 cast.
        public TitlePanel TitlePanel => Services.TryGet<ITitle>()?.TitlePanel as TitlePanel;
        // ITitle.UsernameUI 는 IUsernameUI(인터페이스) 반환 — 옛 호출자 구체 타입 호환 위해 cast.
        public UsernameUI UsernameUI => Services.TryGet<ITitle>()?.UsernameUI as UsernameUI;
        // ITutorial.Overlay 는 ITutorialOverlay(인터페이스) 반환 — 옛 호출자 구체 타입 호환 위해 cast.
        public TutorialOverlay TutorialOverlay => Services.TryGet<ITutorial>()?.Overlay as TutorialOverlay;
        // ISimulation.QuickMenu 는 IQuickMenu(인터페이스) 반환 — 옛 호출자 구체 타입 호환 위해 cast.
        public QuickMenu QuickMenu => Services.TryGet<ISimulation>()?.QuickMenu as QuickMenu;

        /// <summary>UI 인스턴스 부모 그룹 — 모듈이 자기 UI를 spawn할 때 사용.</summary>
        public Transform GetGroupRoot(UIGroup group)
        {
            switch (group)
            {
                case UIGroup.Narrative:  return narrativeRoot  != null ? narrativeRoot  : EnsureGroup(ref narrativeRoot,  "Narrative",  0);
                case UIGroup.Simulation: return simulationRoot != null ? simulationRoot : EnsureGroup(ref simulationRoot, "Simulation", 1);
                case UIGroup.Title:      return titleRoot      != null ? titleRoot      : EnsureGroup(ref titleRoot,      "Title",      2);
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

        /// <summary>모든 메인 UI 숨기기 + 시뮬레이션 컨텍스트 종료 (QuickMenu 비활성 포함).</summary>
        public void HideAll()
        {
            var narr = Services.Get<INarrative>();
            SetActiveIfExists(narr?.DialogueUI, false);

            // 시뮬레이션 컨텍스트 종료 — ScheduleUI/ShopUI/QuickMenu 모두 정리
            var sim = Services.Get<ISimulation>();
            if (sim != null && sim.IsActive) sim.ExitSimulation();
            else
            {
                SetActiveIfExists(Services.TryGet<ISchedule>()?.ScheduleUI, false);
                SetActiveIfExists(Services.TryGet<IShop>()?.ShopUI, false);
            }

            var title = Services.Get<ITitle>();
            SetActiveIfExists(title?.TitlePanel as MonoBehaviour, false); // ITitlePanel → 구체 cast (Phase B-4)
            SetActiveIfExists(title?.UsernameUI as MonoBehaviour, false); // IUsernameUI → 구체 cast (Phase B-2)
            PopupManager.Instance?.Get<PlaceNotification>()?.HideImmediate();
        }

        /// <summary>특정 UI만 표시. Schedule은 시뮬레이션 컨텍스트 진입(QuickMenu 자동 활성).</summary>
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
                    // 시뮬레이션 컨텍스트 진입 — SimulationModule이 ScheduleUI + QuickMenu 활성
                    var sim = Services.Get<ISimulation>();
                    if (sim != null) sim.EnterSimulation();
                    else SetActiveIfExists(ScheduleUI, true); // 폴백
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

    /// <summary>UI 인스턴스 부모 그룹 (모듈명 1:1 매핑) — 모듈이 자기 UI를 spawn할 때 사용.</summary>
    public enum UIGroup
    {
        Narrative,    // DialogueUI, ChoicePopup, DialogueShowButton 등
        Simulation,   // ScheduleUI, ShopUI, QuickMenu, TutorialOverlay 등
        Title         // TitlePanel, UsernameUI 등 진입 화면
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
