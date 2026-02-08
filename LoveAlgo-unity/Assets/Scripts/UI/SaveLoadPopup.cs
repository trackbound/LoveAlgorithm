using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 세이브/로드 모달 팝업
    /// </summary>
    public class SaveLoadPopup : ModalPopupBase
    {
        [Header("애니메이션")]
        [SerializeField] RectTransform panelRect;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] float showDuration = 0.3f;
        [SerializeField] float hideDuration = 0.2f;
        [SerializeField] float slideOffset = 300f;

        [Header("SaveLoad UI")]
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text titleText;

        [Header("Slot Container")]
        [SerializeField] Transform slotContainer;
        [SerializeField] List<SaveLoadSlot> slotItems;

        [Header("Pagination")]
        [SerializeField] TMP_Text pageText;
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;

        [Header("Settings")]
        [SerializeField] int slotsPerPage = 6;
        [SerializeField] int userSlots = 29;  // 슬롯 1~29 (0은 자동저장)

        bool isSaveMode = true;
        int currentPage = 1;
        int totalPages = 1;
        Vector2 originalPosition;

        // 콜백
        System.Action<int> onSlotSelected;

        void Awake()
        {
            if (panelRect != null)
                originalPosition = panelRect.anchoredPosition;

            closeButton?.onClick.AddListener(Close);
            prevButton?.onClick.AddListener(PrevPage);
            nextButton?.onClick.AddListener(NextPage);

            // 슬롯 콜백 설정
            for (int i = 0; i < slotItems.Count; i++)
            {
                slotItems[i]?.Setup(i, OnSlotClicked);
            }

            totalPages = Mathf.CeilToInt((float)userSlots / slotsPerPage);
        }

        #region Show/Hide (슬라이드 애니메이션)

        public override void Show()
        {
            gameObject.SetActive(true);
            PlayShowAnimation().Forget();
        }

        public override void Hide()
        {
            PlayHideAnimation().Forget();
        }

        async UniTaskVoid PlayShowAnimation()
        {
            if (panelRect == null || canvasGroup == null)
            {
                base.Show();
                return;
            }

            canvasGroup.alpha = 0f;
            panelRect.anchoredPosition = originalPosition + new Vector2(slideOffset, 0);

            await DOTween.Sequence()
                .Append(canvasGroup.DOFade(1f, showDuration))
                .Join(panelRect.DOAnchorPos(originalPosition, showDuration).SetEase(Ease.OutQuad))
                .AsyncWaitForCompletion();
        }

        async UniTaskVoid PlayHideAnimation()
        {
            if (panelRect == null || canvasGroup == null)
            {
                base.Hide();
                return;
            }

            // 슬라이드 먼저 시작, 페이드는 후반부에만
            await DOTween.Sequence()
                .Append(panelRect.DOAnchorPos(originalPosition + new Vector2(slideOffset, 0), hideDuration).SetEase(Ease.InQuad))
                .Insert(hideDuration * 0.6f, canvasGroup.DOFade(0f, hideDuration * 0.4f))
                .AsyncWaitForCompletion();

            gameObject.SetActive(false);
        }

        #endregion

        /// <summary>
        /// 세이브 모드로 열기
        /// </summary>
        public void ShowSave(System.Action<int> onSelect = null)
        {
            isSaveMode = true;
            onSlotSelected = onSelect;
            
            if (titleText != null)
                titleText.text = "저장";

            currentPage = 1;
            RefreshSlots();
            Show();
        }

        /// <summary>
        /// 로드 모드로 열기
        /// </summary>
        public void ShowLoad(System.Action<int> onSelect = null)
        {
            isSaveMode = false;
            onSlotSelected = onSelect;
            
            if (titleText != null)
                titleText.text = "불러오기";

            currentPage = 1;
            RefreshSlots();
            Show();
        }

        void OnSlotClicked(int localIndex)
        {
            // 슬롯 1부터 시작 (0은 자동저장)
            int globalIndex = SaveManager.UserSlotStart + (currentPage - 1) * slotsPerPage + localIndex;
            
            if (isSaveMode)
            {
                OnSaveSlotClicked(globalIndex).Forget();
            }
            else
            {
                OnLoadSlotClicked(globalIndex).Forget();
            }
        }

        async UniTaskVoid OnSaveSlotClicked(int slotIndex)
        {
            bool hasData = SaveManager.Exists(slotIndex);

            if (hasData)
            {
                // 덮어쓰기 확인
                bool confirm = await PopupManager.Instance.ConfirmAsync("기존 데이터를 덮어쓰시겠습니까?");
                if (!confirm) return;
            }
            else
            {
                // 새 슬롯에 저장 확인
                bool confirm = await PopupManager.Instance.ConfirmAsync($"슬롯 {slotIndex}에 저장하시겠습니까?");
                if (!confirm) return;
            }

            onSlotSelected?.Invoke(slotIndex);
            Close();
        }

        async UniTaskVoid OnLoadSlotClicked(int slotIndex)
        {
            if (!SaveManager.Exists(slotIndex))
            {
                PopupManager.Instance?.Toast("빈 슬롯", "저장된 데이터가 없습니다.");
                return;
            }

            // 로드 확인 (현재 진행 데이터 유실 경고)
            bool confirm = await PopupManager.Instance.ConfirmAsync("현재 진행 중인 데이터는 사라집니다.\n불러오시겠습니까?");
            if (!confirm) return;

            onSlotSelected?.Invoke(slotIndex);
            Close();
        }

        #region Pagination

        void RefreshSlots()
        {
            int startIndex = (currentPage - 1) * slotsPerPage;

            for (int i = 0; i < slotItems.Count; i++)
            {
                var slot = slotItems[i];
                if (slot == null) continue;

                // 슬롯 1부터 시작
                int globalIndex = SaveManager.UserSlotStart + startIndex + i;

                if (startIndex + i >= userSlots)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                slot.gameObject.SetActive(true);
                slot.Setup(i, OnSlotClicked);

                // 세이브 데이터 확인
                var data = SaveManager.Load(globalIndex);
                if (data != null)
                {
                    slot.SetData(data.ChapterName, data.SaveTime);
                }
                else
                {
                    slot.SetEmpty();
                }
            }

            UpdatePageUI();
        }

        void UpdatePageUI()
        {
            if (pageText != null)
                pageText.text = $"{currentPage} / {totalPages}";

            if (prevButton != null)
                prevButton.interactable = currentPage > 1;

            if (nextButton != null)
                nextButton.interactable = currentPage < totalPages;
        }

        void PrevPage()
        {
            if (currentPage > 1)
            {
                currentPage--;
                RefreshSlots();
            }
        }

        void NextPage()
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                RefreshSlots();
            }
        }

        #endregion
    }
}
