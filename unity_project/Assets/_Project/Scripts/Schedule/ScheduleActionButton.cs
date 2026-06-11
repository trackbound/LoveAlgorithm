using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 정적 스케줄 액션 버튼(얇은 UI 어댑터). 인스펙터에서 고정한 <see cref="ScheduleType"/>를 클릭 시
    /// <see cref="ScheduleSelectedCommand"/>로 발행한다 — <see cref="ScheduleController"/>가 구독해 처리한다(ADR-007).
    ///
    /// 동적 슬롯 생성(<see cref="ScheduleView"/>+<see cref="ScheduleSlot"/>)을 대체하는, 디자인이 직접 authoring한
    /// 정적 버튼용. 표시(라벨/아이콘/레이아웃)는 버튼 계층에서 authoring하고, 이 어댑터는 "클릭 → 의도 발행"만 맡는다.
    /// 같은 Button의 onClick에 런타임 리스너를 다는 방식이라 인스펙터 OnClick 설정과 독립적이다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ScheduleActionButton : MonoBehaviour
    {
        [Tooltip("이 버튼이 클릭 시 발행할 스케줄 타입.")]
        [SerializeField] ScheduleType type;

        public ScheduleType Type { get => type; set => type = value; }

        Button _button;

        void Awake() => _button = GetComponent<Button>();

        void OnEnable()
        {
            if (_button == null) _button = GetComponent<Button>();
            _button.onClick.AddListener(Publish);
        }

        void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(Publish);
        }

        void Publish() => EventBus.Publish(new ScheduleSelectedCommand(type));
    }
}
