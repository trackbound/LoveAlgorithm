using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Messenger
{
    /// <summary>채팅 리스트 항목 — 방 이름 + 마지막 메시지 미리보기 + New 배지(기획서 채팅 탭).</summary>
    public class ChatRoomSlot : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text previewText;
        [SerializeField] GameObject newBadge;
        [SerializeField] Image portrait;

        public Button Button { get => button; set => button = value; }
        public TMP_Text NameText { get => nameText; set => nameText = value; }
        public TMP_Text PreviewText { get => previewText; set => previewText = value; }
        public GameObject NewBadge { get => newBadge; set => newBadge = value; }
        public Image Portrait { get => portrait; set => portrait = value; }

        public string RoomId { get; private set; }

        public void Bind(string roomId, string displayName, string preview, bool hasNew, Sprite portraitSprite, Action<string> onClick)
        {
            RoomId = roomId;
            if (nameText != null) nameText.text = displayName;
            if (previewText != null) previewText.text = preview;
            if (newBadge != null) newBadge.SetActive(hasNew);
            if (portrait != null && portraitSprite != null) portrait.sprite = portraitSprite;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick(RoomId));
            }
        }
    }
}
