using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 라벨+클릭→인덱스 바인딩 버튼(공용). <see cref="Bind"/>로 라벨 텍스트를 주입하고, 클릭 시 바인딩된
    /// 인덱스를 콜백으로 통지한다(상태 변경 없음 — ADR-007). 비주얼 상태(호버/눌림)는 별개 컴포넌트
    /// (<see cref="ButtonStateDriver"/>) 책임. 스토리 선택지(<see cref="ChoiceView"/>)·모달 버튼(<see cref="ModalView"/>)
    /// 양쪽이 동적 생성·바인딩한다.
    /// </summary>
    public class ButtonSlot : MonoBehaviour
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
