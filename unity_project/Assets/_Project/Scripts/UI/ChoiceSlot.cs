using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 선택지 1개 버튼(리스트 항목). 라벨을 표시하고, 클릭 시 바인딩된 인덱스를 콜백으로 통지한다
    /// (상태 변경 없음 — ADR-007). 구조는 Schedule의 ScheduleSlot 미러. <see cref="ChoiceView"/>가 동적 생성.
    /// </summary>
    public class ChoiceSlot : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text labelText;

        int _index;
        Action<int> _onSelected;

        public Button Button { get => button; set => button = value; }
        public TMP_Text LabelText { get => labelText; set => labelText = value; }

        void Awake()
        {
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        /// <summary>인덱스/라벨을 바인딩한다.</summary>
        public void Bind(int index, string label, Action<int> onSelected)
        {
            _index = index;
            _onSelected = onSelected;
            if (labelText != null) labelText.text = label ?? "";
        }

        void HandleClick() => _onSelected?.Invoke(_index);

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }
    }
}
