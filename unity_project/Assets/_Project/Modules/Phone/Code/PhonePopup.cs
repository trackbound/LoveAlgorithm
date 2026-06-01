using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI;
using LoveAlgo.Core;

namespace LoveAlgo.Phone
{
    /// <summary>
    /// 폰 메인 패널 (PopupBase)
    /// 
    /// PhonePopup 프리팹에 연결:
    ///   - Sidebar: Tab_Friend, Tab_Chat, Tab_Theme
    ///   - FriendList: 친구 목록
    ///   - ChatList: 대화방 목록
    ///   - ChatRoomPanel: 개별 대화방
    ///   - ProfilePanel: 프로필 팝업
    /// </summary>
    public class PhonePopup : PopupBase
    {
        [Header("탭 그룹")]
        [SerializeField] TabGroup tabGroup;

        [Header("패널")]
        [SerializeField] GameObject friendListPanel;
        [SerializeField] GameObject chatListPanel;
        [SerializeField] GameObject chatRoomPanel;
        [SerializeField] GameObject profilePanel;

        [Header("친구 목록")]
        [SerializeField] Transform friendListContent;
        [SerializeField] PhoneFriendSlot friendSlotPrefab;
        [SerializeField] PhoneFriendSlot playerSlotPrefab;

        [Header("채팅 목록")]
        [SerializeField] Transform chatListContent;
        [SerializeField] PhoneChatSlot chatSlotPrefab;

        [Header("채팅방")]
        [SerializeField] PhoneChatRoom chatRoom;

        [Header("프로필")]
        [SerializeField] Image profileImage;
        [SerializeField] TMP_Text profileNameText;
        [SerializeField] TMP_Text profileStatusText;
        [SerializeField] Button profileCloseButton;

        [Header("뒤로가기")]
        [SerializeField] Button backButton;

        readonly List<PhoneFriendSlot> activeFriendSlots = new();
        readonly List<PhoneChatSlot> activeChatSlots = new();

        int currentTab; // 0=친구, 1=채팅 (Theme 탭은 데모 제외)
        string openedChatRoomId;

        protected override void Awake()
        {
            base.Awake();

            if (tabGroup != null)
                tabGroup.OnTabChanged += SwitchTab;
            if (backButton != null) backButton.onClick.AddListener(OnBackClick);
            if (profileCloseButton != null) profileCloseButton.onClick.AddListener(HideProfile);

            // 메신저 이벤트 구독
            MessengerSystem.OnNewMessage += OnExternalNewMessage;
        }

        protected override void OnDestroy()
        {
            MessengerSystem.OnNewMessage -= OnExternalNewMessage;
            if (tabGroup != null) tabGroup.OnTabChanged -= SwitchTab;
            if (backButton != null) backButton.onClick.RemoveListener(OnBackClick);
            if (profileCloseButton != null) profileCloseButton.onClick.RemoveListener(HideProfile);
            base.OnDestroy();
        }

        /// <summary>외부에서 새 메시지 도착 시 — 채팅창 열려있으면 자동 추가, 아니면 리스트 갱신</summary>
        void OnExternalNewMessage(string heroineId)
        {
            // 채팅창 열려있고 같은 캐릭터면 새 메시지 동적 추가
            if (chatRoomPanel != null && chatRoomPanel.activeSelf
                && string.Equals(openedChatRoomId, heroineId, System.StringComparison.OrdinalIgnoreCase))
            {
                chatRoom?.AppendLatestMessage();
                MessengerSystem.MarkAsRead(heroineId);
                return;
            }
            // 채팅 탭 활성이면 리스트 갱신 (New 뱃지 등)
            if (currentTab == 1) PopulateChatList();
        }

        public override void Show()
        {
            tabGroup?.Select(0, notify: false);
            SwitchTab(0);
            HideChatRoom();
            HideProfile();
            base.Show();
        }

        #region 탭

        void SwitchTab(int tab)
        {
            currentTab = tab;
            HideChatRoom();
            HideProfile();

            if (friendListPanel != null) friendListPanel.SetActive(tab == 0);
            if (chatListPanel != null) chatListPanel.SetActive(tab == 1);

            switch (tab)
            {
                case 0: PopulateFriendList(); break;
                case 1: PopulateChatList(); break;
            }
        }

        #endregion

        #region 친구 목록

        void PopulateFriendList()
        {
            ClearList(activeFriendSlots);

            if (friendSlotPrefab == null || friendListContent == null) return;

            // 플레이어 슬롯 (맨 위)
            if (playerSlotPrefab != null)
            {
                var playerSlot = Instantiate(playerSlotPrefab, friendListContent);
                var gs = GameState.Instance;
                playerSlot.Setup("Player", gs?.PlayerName ?? "나", "");
                activeFriendSlots.Add(playerSlot);
            }

            // 히로인 친구 목록
            var allFriends = MessengerSystem.GetAllFriends();
            foreach (var friend in allFriends)
            {
                var slot = Instantiate(friendSlotPrefab, friendListContent);
                slot.Setup(friend.HeroineId, friend.DisplayName, friend.StatusMessage,
                    onChat: OpenChatFromFriend,
                    onProfile: ShowProfile);
                activeFriendSlots.Add(slot);
            }
        }

        #endregion

        #region 채팅 목록

        void PopulateChatList()
        {
            ClearList(activeChatSlots);

            if (chatSlotPrefab == null || chatListContent == null) return;

            var rooms = MessengerSystem.GetActiveChatRooms();
            foreach (var room in rooms)
            {
                var slot = Instantiate(chatSlotPrefab, chatListContent);
                slot.Setup(room, OnChatRoomClick);
                activeChatSlots.Add(slot);
            }
        }

        void OnChatRoomClick(string heroineId)
        {
            OpenChatRoom(heroineId);
        }

        void OpenChatFromFriend(string heroineId)
        {
            SwitchTab(1); // 채팅 탭으로
            OpenChatRoom(heroineId);
        }

        #endregion

        #region 채팅방

        public void OpenChatRoom(string heroineId)
        {
            if (chatRoom == null) return;

            // 우측 영역: ChatRoom만 보이도록 (Profile 가림)
            if (chatRoomPanel != null) chatRoomPanel.SetActive(true);
            if (profilePanel != null) profilePanel.SetActive(false);

            chatRoom.Open(heroineId);
            openedChatRoomId = heroineId;
            MessengerSystem.MarkAsRead(heroineId);
        }

        void HideChatRoom()
        {
            if (chatRoomPanel != null) chatRoomPanel.SetActive(false);
            openedChatRoomId = null;
        }

        #endregion

        #region 프로필

        void ShowProfile(string heroineId)
        {
            var friend = MessengerSystem.GetFriend(heroineId);
            if (friend == null) return;

            if (profileNameText != null) profileNameText.text = friend.DisplayName;
            if (profileStatusText != null) profileStatusText.text = friend.StatusMessage;

            // 프로필 이미지 로드
            if (profileImage != null && !string.IsNullOrEmpty(friend.ProfileImagePath))
            {
                var sprite = Resources.Load<Sprite>(friend.ProfileImagePath);
                if (sprite != null) profileImage.sprite = sprite;
            }

            if (profilePanel != null) profilePanel.SetActive(true);
        }

        void HideProfile()
        {
            if (profilePanel != null) profilePanel.SetActive(false);
        }

        #endregion

        #region 내비게이션

        void OnBackClick()
        {
            // 채팅방 열려있으면 목록으로
            if (chatRoomPanel != null && chatRoomPanel.activeSelf)
            {
                HideChatRoom();
                SwitchTab(currentTab);
                return;
            }

            // 프로필 열려있으면 닫기
            if (profilePanel != null && profilePanel.activeSelf)
            {
                HideProfile();
                return;
            }

            // 폰 닫기
            Close();
        }

        #endregion

        void ClearList<T>(List<T> list) where T : MonoBehaviour
        {
            foreach (var item in list)
                if (item != null) Destroy(item.gameObject);
            list.Clear();
        }
    }
}
