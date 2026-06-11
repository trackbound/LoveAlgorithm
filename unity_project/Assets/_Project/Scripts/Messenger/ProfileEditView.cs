using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core; // GameStateSO

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 플레이어 프로필 편집(*View) — 기획서 p9~10: 프로필 사진/배경 후보 중 선택(체크 프레임),
    /// 상태메시지 입력(현재 값 표시, 빈 값=기본 문구 유지), 우측 미리보기(dim), 저장/닫기.
    /// 저장만 상태에 기록(MessengerService) — 닫기는 폐기. 표시 전용(ADR-007).
    /// </summary>
    public class ProfileEditView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Transform imageContainer;
        [SerializeField] Transform bgContainer;
        [SerializeField] ProfileChoiceSlot slotPrefab;
        [SerializeField] TMP_InputField statusInput;
        [SerializeField] Button saveButton;
        [SerializeField] Button closeButton;
        [Header("미리보기(기획서: 동일하게 dim 적용)")]
        [SerializeField] Image previewBg;
        [SerializeField] Image previewPortrait;
        [Header("데이터")]
        [SerializeField] GameStateSO state;
        [SerializeField] MessengerProfileCatalogSO profileCatalog;

        public GameObject Root { get => root; set => root = value; }
        public Transform ImageContainer { get => imageContainer; set => imageContainer = value; }
        public Transform BgContainer { get => bgContainer; set => bgContainer = value; }
        public ProfileChoiceSlot SlotPrefab { get => slotPrefab; set => slotPrefab = value; }
        public TMP_InputField StatusInput { get => statusInput; set => statusInput = value; }
        public Button SaveButton { get => saveButton; set => saveButton = value; }
        public Button CloseButton { get => closeButton; set => closeButton = value; }
        public Image PreviewBg { get => previewBg; set => previewBg = value; }
        public Image PreviewPortrait { get => previewPortrait; set => previewPortrait = value; }
        public GameStateSO State { get => state; set => state = value; }
        public MessengerProfileCatalogSO ProfileCatalog { get => profileCatalog; set => profileCatalog = value; }

        /// <summary>저장 완료 통지 — 소비처: 프로필 패널/친구 목록 갱신(MessengerView 배선).</summary>
        public event Action Saved;

        readonly List<ProfileChoiceSlot> _imageSlots = new();
        readonly List<ProfileChoiceSlot> _bgSlots = new();
        int _pendingImage;
        int _pendingBg;

        public bool IsOpen => root != null && root.activeSelf;
        public int PendingImage => _pendingImage;
        public int PendingBg => _pendingBg;

        void Awake()
        {
            if (saveButton != null) saveButton.onClick.AddListener(Save);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            if (state == null || profileCatalog == null)
            {
                Debug.LogError("[ProfileEditView] state/profileCatalog 미바인딩 — 편집 열기 불가.");
                return;
            }

            _pendingImage = state.Data.messengerProfileImage;
            _pendingBg = state.Data.messengerProfileBg;
            if (statusInput != null) statusInput.text = state.Data.messengerStatusMessage; // 빈 값=placeholder 노출

            RebuildSlots();
            UpdatePreview();
            if (root != null) root.SetActive(true);
        }

        public void Close()
        {
            ClearSlots();
            if (root != null) root.SetActive(false);
        }

        void Save()
        {
            string status = statusInput != null ? statusInput.text.Trim() : "";
            MessengerService.SetPlayerProfile(state, _pendingImage, _pendingBg, status);
            Close();
            Saved?.Invoke();
        }

        void RebuildSlots()
        {
            ClearSlots();
            if (slotPrefab == null || imageContainer == null || bgContainer == null)
            {
                Debug.LogError("[ProfileEditView] slotPrefab/컨테이너 미바인딩 — 후보 표시 불가.");
                return;
            }

            var images = profileCatalog.ProfileImages;
            for (int i = 0; i < images.Count; i++)
            {
                var slot = Instantiate(slotPrefab, imageContainer);
                slot.Bind(i, images[i], i == _pendingImage, OnImagePicked);
                _imageSlots.Add(slot);
            }

            var bgs = profileCatalog.Backgrounds;
            for (int i = 0; i < bgs.Count; i++)
            {
                var slot = Instantiate(slotPrefab, bgContainer);
                slot.Bind(i, bgs[i], i == _pendingBg, OnBgPicked);
                _bgSlots.Add(slot);
            }
        }

        void OnImagePicked(int index)
        {
            _pendingImage = index;
            foreach (var s in _imageSlots) s.SetSelected(s.Index == index);
            UpdatePreview();
        }

        void OnBgPicked(int index)
        {
            _pendingBg = index;
            foreach (var s in _bgSlots) s.SetSelected(s.Index == index);
            UpdatePreview();
        }

        void UpdatePreview()
        {
            if (previewBg != null)
            {
                var bg = profileCatalog.Background(_pendingBg);
                if (bg != null) previewBg.sprite = bg;
            }
            if (previewPortrait != null)
            {
                var img = profileCatalog.ProfileImage(_pendingImage);
                if (img != null) previewPortrait.sprite = img;
            }
        }

        void ClearSlots()
        {
            foreach (var s in _imageSlots) if (s != null) Destroy(s.gameObject);
            foreach (var s in _bgSlots) if (s != null) Destroy(s.gameObject);
            _imageSlots.Clear();
            _bgSlots.Clear();
        }

        void OnDisable() => ClearSlots();
    }
}
