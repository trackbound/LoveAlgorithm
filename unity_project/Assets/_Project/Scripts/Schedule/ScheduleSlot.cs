using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 1칸(리스트 항목). 표시 데이터는 ScheduleTable에서 읽고,
    /// 클릭 시 바인딩된 ScheduleType을 콜백으로 통지한다(상태 변경 없음 — ADR-007).
    /// </summary>
    public class ScheduleSlot : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text descText;

        ScheduleType _type;
        Action<ScheduleType> _onSelected;

        public Button Button { get => button; set => button = value; }
        public TMP_Text NameText { get => nameText; set => nameText = value; }
        public TMP_Text DescText { get => descText; set => descText = value; }

        void Awake()
        {
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        /// <summary>슬롯에 스케줄 타입을 바인딩하고 이름/설명 표시를 갱신한다.</summary>
        public void Bind(ScheduleType type, Action<ScheduleType> onSelected)
        {
            _type = type;
            _onSelected = onSelected;
            var e = ScheduleTable.Get(type);
            if (nameText != null) nameText.text = e.displayName;
            if (descText != null) descText.text = e.description;
        }

        void HandleClick() => _onSelected?.Invoke(_type);

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }
    }
}
