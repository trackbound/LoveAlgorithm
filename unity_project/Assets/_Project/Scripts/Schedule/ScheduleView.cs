using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // OverlayGate

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 선택 인게임 화면(*UI). 카테고리의 스케줄들로 슬롯을 구성하고,
    /// 슬롯 클릭을 받아 ScheduleSelectedCommand를 발행한다(얇은 뷰 — 로직은 ScheduleController/Service).
    /// 표시 메타데이터는 ScheduleTable, 슬롯 인스턴스는 slotPrefab으로 동적 생성.
    /// </summary>
    public class ScheduleView : MonoBehaviour
    {
        [SerializeField] Transform slotContainer;
        [SerializeField] ScheduleSlot slotPrefab;
        [Tooltip("OnEnable 시 처음 표시할 카테고리.")]
        [SerializeField] ScheduleCategory startCategory = ScheduleCategory.Exercise;

        readonly List<ScheduleSlot> _spawned = new();

        public Transform SlotContainer { get => slotContainer; set => slotContainer = value; }
        public ScheduleSlot SlotPrefab { get => slotPrefab; set => slotPrefab = value; }
        public ScheduleCategory StartCategory { get => startCategory; set => startCategory = value; }
        public IReadOnlyList<ScheduleSlot> Slots => _spawned;

        void OnEnable() => ShowCategory(startCategory);
        void OnDisable() => Clear();

        /// <summary>카테고리에 속한 스케줄들로 슬롯을 재구성한다. 카테고리 탭 버튼이 호출.</summary>
        public void ShowCategory(ScheduleCategory category)
        {
            Clear();
            if (slotPrefab == null || slotContainer == null) return;
            foreach (var type in ScheduleTable.GetTypes(category))
            {
                var slot = Instantiate(slotPrefab, slotContainer);
                slot.Bind(type, OnSlotSelected);
                _spawned.Add(slot);
            }
        }

        void OnSlotSelected(ScheduleType type)
        {
            if (OverlayGate.IsBlocked) return; // 오버레이 열림 중 선택 무시(안전망 — 마우스는 raycast가 막음)
            EventBus.Publish(new ScheduleSelectedCommand(type));
        }

        void Clear()
        {
            foreach (var s in _spawned)
                if (s != null) Destroy(s.gameObject);
            _spawned.Clear();
        }
    }
}
