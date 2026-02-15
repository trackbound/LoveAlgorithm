using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 친구 목록 슬롯 (PhoneFriendSlot 프리팹에 연결)
    /// </summary>
    public class PhoneFriendSlot : MonoBehaviour
    {
        [SerializeField] Image profileImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button chatButton;
        [SerializeField] Button profileButton;

        string heroineId;
        Action<string> onChat;
        Action<string> onProfile;

        /// <summary>
        /// 슬롯 설정 (플레이어용 — 버튼 없음)
        /// </summary>
        public void Setup(string id, string name, string status)
        {
            heroineId = id;
            if (nameText != null) nameText.text = name;
            if (statusText != null) statusText.text = status;
            if (chatButton != null) chatButton.gameObject.SetActive(false);
            if (profileButton != null) profileButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// 슬롯 설정 (히로인용 — 채팅/프로필 버튼)
        /// </summary>
        public void Setup(string id, string name, string status,
            Action<string> onChat, Action<string> onProfile)
        {
            heroineId = id;
            this.onChat = onChat;
            this.onProfile = onProfile;

            if (nameText != null) nameText.text = name;
            if (statusText != null) statusText.text = status;

            if (chatButton != null)
            {
                chatButton.gameObject.SetActive(true);
                chatButton.onClick.RemoveAllListeners();
                chatButton.onClick.AddListener(() => this.onChat?.Invoke(heroineId));
            }

            if (profileButton != null)
            {
                profileButton.gameObject.SetActive(true);
                profileButton.onClick.RemoveAllListeners();
                profileButton.onClick.AddListener(() => this.onProfile?.Invoke(heroineId));
            }
        }
    }
}
