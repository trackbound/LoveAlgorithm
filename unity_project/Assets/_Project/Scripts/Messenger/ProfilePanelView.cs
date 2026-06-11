using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core; // GameStateSO

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 친구 탭 우측 프로필 영역(*View) — 기획서: 접근 시 빈 화면, 행 클릭 시 해당 프로필 출력
    /// (배경/사진/이름/상메), 플레이어 프로필만 편집 버튼, 프로필 사진 클릭 시 확대(줌).
    /// 표시 전용 — 편집 진입은 <see cref="EditRequested"/>로 위임(MessengerView가 배선).
    /// </summary>
    public class ProfilePanelView : MonoBehaviour
    {
        [SerializeField] GameObject emptyRoot;   // 빈 화면(기본)
        [SerializeField] GameObject contentRoot; // 프로필 표시
        [SerializeField] Image bgImage;
        [SerializeField] Image portraitImage;
        [SerializeField] Button portraitButton;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button editButton;      // 플레이어 프로필만 노출
        [Header("프로필 사진 확대(기획서 p12)")]
        [SerializeField] GameObject zoomRoot;
        [SerializeField] Image zoomImage;
        [SerializeField] TMP_Text zoomNameText;
        [SerializeField] Button zoomCloseButton; // 전체 화면 클릭 닫기
        [Header("데이터")]
        [SerializeField] GameStateSO state;
        [SerializeField] FriendCatalogSO friends;
        [SerializeField] MessengerProfileCatalogSO profileCatalog;
        [SerializeField] Sprite defaultBg;
        [SerializeField] Sprite defaultPortrait;
        [SerializeField] string defaultStatus = "상태 메세지입니다.";

        public GameObject EmptyRoot { get => emptyRoot; set => emptyRoot = value; }
        public GameObject ContentRoot { get => contentRoot; set => contentRoot = value; }
        public Image BgImage { get => bgImage; set => bgImage = value; }
        public Image PortraitImage { get => portraitImage; set => portraitImage = value; }
        public Button PortraitButton { get => portraitButton; set => portraitButton = value; }
        public TMP_Text NameText { get => nameText; set => nameText = value; }
        public TMP_Text StatusText { get => statusText; set => statusText = value; }
        public Button EditButton { get => editButton; set => editButton = value; }
        public GameObject ZoomRoot { get => zoomRoot; set => zoomRoot = value; }
        public Image ZoomImage { get => zoomImage; set => zoomImage = value; }
        public TMP_Text ZoomNameText { get => zoomNameText; set => zoomNameText = value; }
        public Button ZoomCloseButton { get => zoomCloseButton; set => zoomCloseButton = value; }
        public GameStateSO State { get => state; set => state = value; }
        public FriendCatalogSO Friends { get => friends; set => friends = value; }
        public MessengerProfileCatalogSO ProfileCatalog { get => profileCatalog; set => profileCatalog = value; }

        /// <summary>플레이어 프로필의 편집 버튼 클릭 통지 — 소비처: ProfileEditView(MessengerView 배선).</summary>
        public event Action EditRequested;

        /// <summary>현재 표시 중인 id(빈 화면이면 null). 플레이어 = FriendListView.PlayerRowId.</summary>
        public string CurrentId { get; private set; }

        void Awake()
        {
            if (portraitButton != null) portraitButton.onClick.AddListener(OpenZoom);
            if (zoomCloseButton != null) zoomCloseButton.onClick.AddListener(CloseZoom);
            if (editButton != null) editButton.onClick.AddListener(() => EditRequested?.Invoke());
        }

        /// <summary>빈 화면으로(탭 진입 기본 — 기획서 "접근 시에는 빈 화면으로 출력").</summary>
        public void Clear()
        {
            CurrentId = null;
            if (contentRoot != null) contentRoot.SetActive(false);
            if (emptyRoot != null) emptyRoot.SetActive(true);
            CloseZoom();
        }

        /// <summary>프로필 표시. 플레이어 행 id면 유저 프로필(편집 가능), 아니면 친구 카탈로그.</summary>
        public void Show(string id)
        {
            if (string.IsNullOrEmpty(id)) { Clear(); return; }
            CurrentId = id;

            bool isPlayer = id == FriendListView.PlayerRowId;
            Sprite portrait, bg;
            string display, status;

            if (isPlayer)
            {
                portrait = profileCatalog != null ? profileCatalog.ProfileImage(ImageIndex()) : null;
                bg = profileCatalog != null ? profileCatalog.Background(BgIndex()) : null;
                display = state != null ? state.Data.playerName : "";
                status = MessengerService.PlayerStatus(state, defaultStatus);
            }
            else
            {
                var entry = friends != null ? friends.Resolve(id) : null;
                portrait = entry != null ? entry.portrait : null;
                bg = entry != null ? entry.profileBg : null;
                display = friends != null ? friends.DisplayName(id) : id;
                status = entry != null && !string.IsNullOrEmpty(entry.defaultStatus) ? entry.defaultStatus : defaultStatus;
            }

            if (bgImage != null) bgImage.sprite = bg != null ? bg : defaultBg;
            if (portraitImage != null) portraitImage.sprite = portrait != null ? portrait : defaultPortrait;
            if (nameText != null) nameText.text = display;
            if (statusText != null) statusText.text = status;
            if (editButton != null) editButton.gameObject.SetActive(isPlayer); // 기획서: 유저 프로필만 편집

            if (emptyRoot != null) emptyRoot.SetActive(false);
            if (contentRoot != null) contentRoot.SetActive(true);
        }

        /// <summary>현재 id 재표시(편집 저장 후 갱신).</summary>
        public void Refresh()
        {
            if (!string.IsNullOrEmpty(CurrentId)) Show(CurrentId);
        }

        void OpenZoom()
        {
            if (zoomRoot == null || string.IsNullOrEmpty(CurrentId)) return;
            if (zoomImage != null && portraitImage != null) zoomImage.sprite = portraitImage.sprite;
            if (zoomNameText != null && nameText != null) zoomNameText.text = nameText.text;
            zoomRoot.SetActive(true);
        }

        void CloseZoom()
        {
            if (zoomRoot != null) zoomRoot.SetActive(false);
        }

        int ImageIndex() => state != null ? state.Data.messengerProfileImage : 0;
        int BgIndex() => state != null ? state.Data.messengerProfileBg : 0;
    }
}
