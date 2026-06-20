using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 친구 목록 항목(*Slot, ButtonSlot 패턴 미러 — 피처 응집이라 Messenger asmdef 소유).
    /// 프로필/상태메시지는 옵션 바인딩(없으면 무시) — 비주얼 구성은 프리팹/감독 영역.
    /// </summary>
    public class FriendSlot : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Image portrait;

        public Button Button { get => button; set => button = value; }
        public TMP_Text NameText { get => nameText; set => nameText = value; }
        public TMP_Text StatusText { get => statusText; set => statusText = value; }
        public Image Portrait { get => portrait; set => portrait = value; }

        public string FriendId { get; private set; }

        public void Bind(string friendId, string displayName, string status, Sprite portraitSprite, Action<string> onClick)
        {
            FriendId = friendId;
            if (nameText != null) nameText.text = displayName;
            if (statusText != null) statusText.text = status;
            if (portrait != null && portraitSprite != null) portrait.sprite = portraitSprite;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (onClick != null) button.onClick.AddListener(() => onClick(FriendId));
            }
        }
    }
}
