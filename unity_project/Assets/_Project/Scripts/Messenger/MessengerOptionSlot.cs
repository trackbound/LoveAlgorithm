using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Messenger
{
    /// <summary>채팅 선택지 버튼 1개(기획서: 하단 선택지 박스 — 호버 강조, 핑크 박스).</summary>
    public class MessengerOptionSlot : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text labelText;

        public Button Button { get => button; set => button = value; }
        public TMP_Text LabelText { get => labelText; set => labelText = value; }

        public void Bind(int index, string label, Action<int> onClick)
        {
            if (labelText != null) labelText.text = label;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick(index));
            }
        }
    }
}
