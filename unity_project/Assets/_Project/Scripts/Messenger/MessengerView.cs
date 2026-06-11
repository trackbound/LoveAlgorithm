using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // OpenMessengerCommand, CloseMessengerCommand

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 루트(*View) — 열기/닫기 명령 구독 + 탭(친구/채팅) 전환(기획서: 진입 시 친구 탭 기본,
    /// 탭 재진입 시 기본 화면 복귀, 테마 탭은 삭제 확정이라 2탭). 메신저는 ScreenPhase가 아닌
    /// Overlay 축(ADR-013, LockScreen 선례) — 루트 active 토글만 책임진다.
    /// </summary>
    public class MessengerView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] GameObject friendPanel;
        [SerializeField] GameObject chatPanel;
        [SerializeField] Button friendTabButton;
        [SerializeField] Button chatTabButton;
        [SerializeField] Button closeButton;
        [SerializeField] FriendListView friendList;
        [SerializeField] ChatListView chatList;
        [SerializeField] ChatRoomView chatRoom;
        [SerializeField] ProfilePanelView profilePanel;
        [SerializeField] ProfileEditView profileEdit;

        public GameObject Root { get => root; set => root = value; }
        public GameObject FriendPanel { get => friendPanel; set => friendPanel = value; }
        public GameObject ChatPanel { get => chatPanel; set => chatPanel = value; }
        public Button FriendTabButton { get => friendTabButton; set => friendTabButton = value; }
        public Button ChatTabButton { get => chatTabButton; set => chatTabButton = value; }
        public Button CloseButton { get => closeButton; set => closeButton = value; }
        public FriendListView FriendList { get => friendList; set => friendList = value; }
        public ChatListView ChatList { get => chatList; set => chatList = value; }
        public ChatRoomView ChatRoom { get => chatRoom; set => chatRoom = value; }
        public ProfilePanelView ProfilePanel { get => profilePanel; set => profilePanel = value; }
        public ProfileEditView ProfileEdit { get => profileEdit; set => profileEdit = value; }

        readonly List<IDisposable> _subs = new();

        void Awake()
        {
            if (friendTabButton != null) friendTabButton.onClick.AddListener(ShowFriendTab);
            if (chatTabButton != null) chatTabButton.onClick.AddListener(ShowChatTab);
            if (closeButton != null) closeButton.onClick.AddListener(() => EventBus.Publish(new CloseMessengerCommand()));
        }

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<OpenMessengerCommand>(OnOpen));
            _subs.Add(EventBus.Subscribe<CloseMessengerCommand>(OnClose));
            if (chatList != null) chatList.RoomSelected += OnRoomSelected;
            if (friendList != null) friendList.FriendClicked += OnFriendClicked;
            if (profilePanel != null) profilePanel.EditRequested += OnEditRequested;
            if (profileEdit != null) profileEdit.Saved += OnProfileSaved;
        }

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
            if (chatList != null) chatList.RoomSelected -= OnRoomSelected;
            if (friendList != null) friendList.FriendClicked -= OnFriendClicked;
            if (profilePanel != null) profilePanel.EditRequested -= OnEditRequested;
            if (profileEdit != null) profileEdit.Saved -= OnProfileSaved;
        }

        void OnOpen(OpenMessengerCommand cmd)
        {
            if (root != null) root.SetActive(true);

            if (string.IsNullOrEmpty(cmd.RoomId))
            {
                ShowFriendTab(); // 기획서: 진입 시 친구 탭 기본
            }
            else
            {
                ShowChatTab();
                if (chatRoom != null) chatRoom.Show(cmd.RoomId);
            }
        }

        void OnClose(CloseMessengerCommand _)
        {
            if (chatRoom != null) chatRoom.Hide();
            if (root != null) root.SetActive(false);
        }

        /// <summary>친구 탭 — 우측은 기본(빈) 화면(기획서: 프로필 출력 구역은 접근 시 빈 화면).</summary>
        public void ShowFriendTab()
        {
            if (friendPanel != null) friendPanel.SetActive(true);
            if (chatPanel != null) chatPanel.SetActive(false);
            if (chatRoom != null) chatRoom.Hide();
            if (profileEdit != null) profileEdit.Close();
            if (profilePanel != null) profilePanel.Clear();
            if (friendList != null) friendList.Refresh();
        }

        /// <summary>채팅 탭 — 우측은 빈 화면, 방 클릭 시 채팅창(기획서: 접근 시 빈 화면 출력).</summary>
        public void ShowChatTab()
        {
            if (friendPanel != null) friendPanel.SetActive(false);
            if (chatPanel != null) chatPanel.SetActive(true);
            if (chatRoom != null) chatRoom.Hide();
            if (chatList != null) chatList.Refresh();
        }

        void OnRoomSelected(string roomId)
        {
            if (chatRoom != null) chatRoom.Show(roomId);
            // 읽음 반영(New 배지 갱신) — 가벼운 목록 리프레시.
            if (chatList != null) chatList.Refresh();
        }

        void OnFriendClicked(string id)
        {
            if (profilePanel != null) profilePanel.Show(id); // 기획서: 행 클릭 → 우측 프로필 출력
        }

        void OnEditRequested()
        {
            if (profileEdit != null) profileEdit.Open();
        }

        void OnProfileSaved()
        {
            if (profilePanel != null) profilePanel.Refresh();   // 새 사진/배경/상메 반영
            if (friendList != null) friendList.Refresh();       // 플레이어 행 상메 갱신
        }
    }
}
