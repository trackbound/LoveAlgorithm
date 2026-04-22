using UnityEngine;
using LoveAlgo.Story;
using LoveAlgo.Schedule;
using LoveAlgo.Shop;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI 매니저 - 메인 UI들의 Show/Hide 관리
    /// 인스펙터에는 프리팹을 바인딩하고, 첫 접근 시 lazy-instantiate 한다.
    /// 팝업은 PopupManager에서 별도 관리.
    /// </summary>
    public class UIManager : SingletonMonoBehaviour<UIManager>
    {
        [Header("UI Group Roots (인스턴스 부모)")]
        [Tooltip("비워두면 UIManager 하위에 자동 생성. 하이어라키에서 순서대로 Story/Simulate/Scene 자식으로 정리됨.")]
        [SerializeField] Transform storyRoot;
        [SerializeField] Transform simulateRoot;
        [SerializeField] Transform sceneRoot;

        [Header("Story")]
        [SerializeField] DialogueUI dialogueUIPrefab;
        [SerializeField] DialogueShowButton dialogueShowButtonPrefab;
        [SerializeField] ChoiceUI choiceUIPrefab;
        [SerializeField] PlaceUI placeUIPrefab;

        [Header("Simulate")]
        [SerializeField] ScheduleUI scheduleUIPrefab;
        [SerializeField] ShopPopup shopUIPrefab;
        [SerializeField] QuickMenuUI quickMenuUIPrefab;

        [Header("Tutorial")]
        [Tooltip("범용 튜토리얼 오버레이 (CSV+flagKey 인자로 재사용). 화면 전체 dim을 위해 독립 프리합.")]
        [SerializeField] TutorialOverlay tutorialOverlayPrefab;

        [Header("Scene")]
        [SerializeField] TitleUI titleUIPrefab;
        [SerializeField] UsernameUI usernameUIPrefab;

        // ── lazy-instantiated 캐시 ───────────────────────────────────────
        DialogueUI _dialogueUI;
        DialogueShowButton _dialogueShowButton;
        ChoiceUI _choiceUI;
        PlaceUI _placeUI;
        ScheduleUI _scheduleUI;
        ShopPopup _shopUI;
        QuickMenuUI _quickMenuUI;
        TutorialOverlay _tutorialOverlay;
        TitleUI _titleUI;
        UsernameUI _usernameUI;

        // ── 외부 공개 프로퍼티 (첫 접근 시 자동 인스턴스화) ──────────────
        public DialogueUI DialogueUI
        {
            get
            {
                if (_dialogueUI == null)
                {
                    // 캐시를 Spawn 전에 채우면 안 되지만, Spawn 직후 즉시 대입해야
                    // 하위 컴포넌트의 OnEnable 등에서 재귀 호출이 발생하지 않는다.
                    var inst = Spawn(dialogueUIPrefab, GroupRoot.Story);
                    _dialogueUI = inst;
                    // DialogueShowButton 동시 생성 (대사창 항상 동반)
                    if (dialogueShowButtonPrefab != null && _dialogueShowButton == null)
                    {
                        _dialogueShowButton = Spawn(dialogueShowButtonPrefab, GroupRoot.Story);
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
                if (_dialogueShowButton == null)
                {
                    _ = DialogueUI; // 동반 spawn 트리거
                }
                return _dialogueShowButton;
            }
        }
        public ChoiceUI ChoiceUI => _choiceUI != null ? _choiceUI : (_choiceUI = Spawn(choiceUIPrefab, GroupRoot.Story));
        public PlaceUI PlaceUI => _placeUI != null ? _placeUI : (_placeUI = Spawn(placeUIPrefab, GroupRoot.Story));
        public ScheduleUI ScheduleUI => _scheduleUI != null ? _scheduleUI : (_scheduleUI = Spawn(scheduleUIPrefab, GroupRoot.Simulate));
        public ShopPopup ShopUI => _shopUI != null ? _shopUI : (_shopUI = Spawn(shopUIPrefab, GroupRoot.Simulate));
        public QuickMenuUI QuickMenuUI => _quickMenuUI != null ? _quickMenuUI : (_quickMenuUI = Spawn(quickMenuUIPrefab, GroupRoot.Simulate));
        public TutorialOverlay TutorialOverlay => _tutorialOverlay != null ? _tutorialOverlay : (_tutorialOverlay = Spawn(tutorialOverlayPrefab, GroupRoot.Simulate));
        public TitleUI TitleUI => _titleUI != null ? _titleUI : (_titleUI = Spawn(titleUIPrefab, GroupRoot.Scene));
        public UsernameUI UsernameUI => _usernameUI != null ? _usernameUI : (_usernameUI = Spawn(usernameUIPrefab, GroupRoot.Scene));

        enum GroupRoot { Story, Simulate, Scene }

        Transform GetGroupRoot(GroupRoot group)
        {
            switch (group)
            {
                case GroupRoot.Story:    return storyRoot    != null ? storyRoot    : EnsureGroup(ref storyRoot,    "Story",    0);
                case GroupRoot.Simulate: return simulateRoot != null ? simulateRoot : EnsureGroup(ref simulateRoot, "Simulate", 1);
                case GroupRoot.Scene:    return sceneRoot    != null ? sceneRoot    : EnsureGroup(ref sceneRoot,    "Scene",    2);
            }
            return transform;
        }

        /// <summary>그룹 부모가 비어있으면 UIManager 하위에 자동 생성 (RectTransform stretch)</summary>
        Transform EnsureGroup(ref Transform field, string groupName, int siblingIndex)
        {
            // 하이어라키에 이미 같은 이름이 있으면 재사용
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

        T Spawn<T>(T prefab, GroupRoot group) where T : MonoBehaviour
        {
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] {typeof(T).Name} 프리팹이 인스펙터에 바인딩되지 않았습니다.");
                return null;
            }
            var parent = GetGroupRoot(group);
            var inst = Instantiate(prefab, parent);
            inst.name = prefab.name; // (Clone) 제거
            inst.gameObject.SetActive(false);
            return inst;
        }

        void SetActiveIfExists(MonoBehaviour ui, bool active)
        {
            if (ui != null)
                ui.gameObject.SetActive(active);
        }

        /// <summary>
        /// 모든 메인 UI 숨기기 (생성된 인스턴스에 한해서만 동작 — 미사용 UI는 인스턴스화하지 않음)
        /// </summary>
        public void HideAll()
        {
            SetActiveIfExists(_dialogueUI, false);
            SetActiveIfExists(_scheduleUI, false);
            SetActiveIfExists(_titleUI, false);
            SetActiveIfExists(_usernameUI, false);
            SetActiveIfExists(_quickMenuUI, false);
            _placeUI?.HideImmediate();
        }

        /// <summary>
        /// 특정 UI만 표시 (나머지 숨김). 표시 대상은 lazy-instantiate.
        /// </summary>
        public void ShowOnly(MainUIType type)
        {
            HideAll();

            switch (type)
            {
                case MainUIType.Dialogue:
                case MainUIType.Ending:
                    // 엔딩은 DialogueUI를 재사용한다.
                    SetActiveIfExists(DialogueUI, true);
                    break;
                case MainUIType.Schedule:
                    SetActiveIfExists(ScheduleUI, true);
                    SetActiveIfExists(QuickMenuUI, true);
                    break;
                case MainUIType.Title:
                    SetActiveIfExists(TitleUI, true);
                    break;
                case MainUIType.Username:
                    SetActiveIfExists(UsernameUI, true);
                    break;
            }
        }
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
