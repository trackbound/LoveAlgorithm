using System;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Messenger
{
    /// <summary>프로필 편집 후보 1칸(사진/배경 공용) — 선택 시 체크 프레임 표시(기획서 편집 화면).</summary>
    public class ProfileChoiceSlot : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image image;
        [SerializeField] GameObject selectedFrame;

        public Button Button { get => button; set => button = value; }
        public Image Image { get => image; set => image = value; }
        public GameObject SelectedFrame { get => selectedFrame; set => selectedFrame = value; }

        public int Index { get; private set; }

        public void Bind(int index, Sprite sprite, bool selected, Action<int> onClick)
        {
            Index = index;
            if (image != null && sprite != null) image.sprite = sprite;
            SetSelected(selected);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick(Index));
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null) selectedFrame.SetActive(selected);
        }
    }
}
