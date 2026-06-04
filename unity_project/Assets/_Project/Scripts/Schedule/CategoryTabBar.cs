using UnityEngine;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 카테고리 탭 묶음. 탭 클릭 → 단일선택 시각 갱신 + <see cref="ScheduleView.ShowCategory"/> 호출
    /// (얇은 뷰 — 같은 피처라 직접 호출). 구 TabGroup(C# event·ListenerBag, 신 코드가 0구독이라 죽어있던 위젯)
    /// 대체 — 카테고리 전환을 실제 동작하게 신규 배선(HANDOFF "카테고리 탭 미연결" 해소).
    /// </summary>
    public class CategoryTabBar : MonoBehaviour
    {
        [Tooltip("비우면 Awake에서 자식 CategoryTab을 계층 순서로 자동 수집(categories와 정합).")]
        [SerializeField] CategoryTab[] tabs;
        [Tooltip("tabs[i]에 대응하는 카테고리(같은 길이·순서).")]
        [SerializeField] ScheduleCategory[] categories;
        [SerializeField] ScheduleView scheduleView;
        [Tooltip("시작 시 선택할 탭 인덱스.")]
        [SerializeField] int defaultIndex;

        int _current = -1;

        void Awake()
        {
            // 인스펙터 미배선(또는 빈 슬롯)이면 자식에서 자동 수집 — 계층 순서가 categories와 정합.
            // (씬 직렬화 참조 배열은 MCP로 못 채우므로, 자식 수집이 단일 진실원.)
            if (tabs == null || tabs.Length == 0 || tabs[0] == null)
                tabs = GetComponentsInChildren<CategoryTab>(true);

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                int idx = i; // 클로저 캡처용 — 루프 변수 직접 캡처 금지.
                tabs[i].Button.onClick.AddListener(() => Select(idx));
            }
        }

        void Start() => Select(defaultIndex);

        /// <summary>탭 선택: 단일선택 시각 갱신 + 해당 카테고리 슬롯 재구성 요청.</summary>
        public void Select(int index)
        {
            if (tabs == null || index < 0 || index >= tabs.Length) return;
            if (index == _current) return;
            _current = index;

            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i] != null) tabs[i].SetSelected(i == index);

            if (scheduleView != null && categories != null && index < categories.Length)
                scheduleView.ShowCategory(categories[index]);
        }

        public int CurrentIndex => _current;

        void OnDestroy()
        {
            if (tabs == null) return;
            foreach (var t in tabs)
                if (t != null) t.Button.onClick.RemoveAllListeners();
        }
    }
}
