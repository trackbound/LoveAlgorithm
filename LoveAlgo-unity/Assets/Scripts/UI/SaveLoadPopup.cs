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

        // 콜백
        System.Action<int> onSlotSelected;

        protected override void Awake()
        {
            base.Awake();

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

        #region Show/Hide

        public override void Show()
        {
            gameObject.SetActive(true);
            PlayShowAnimation();
        }

        public override void Hide()
        {
            KillSequence();
            PlayHideAnimation();
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

            // 저장 실행 + 슬롯 갱신 (팝업은 유지)
            onSlotSelected?.Invoke(slotIndex);
            RefreshSlots();
        }

        async UniTaskVoid OnLoadSlotClicked(int slotIndex)
        {
            // 빈 슬롯은 무반응
            if (!SaveManager.Exists(slotIndex)) return;

            // 로드 확인
            bool confirm = await PopupManager.Instance.ConfirmAsync("해당 데이터를 불러오시겠습니까?");
            if (!confirm) return;

            // 로드는 씬 전환이므로 팝업 닫고 실행
            await PopupManager.Instance.CloseModalAsync();
            await UniTask.Delay(150);
            onSlotSelected?.Invoke(slotIndex);
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
                    var screenshot = SaveManager.LoadScreenshot(globalIndex);
                    slot.SetData(data.ChapterName, data.SaveTime, screenshot);
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
                pageText.text = $"{currentPage}";
        }

        void PrevPage()
        {
            // 첫 페이지에서 이전 → 마지막 페이지 (Loop)
            currentPage = currentPage > 1 ? currentPage - 1 : totalPages;
            RefreshSlots();
        }

        void NextPage()
        {
            // 마지막 페이지에서 다음 → 첫 페이지 (Loop)
            currentPage = currentPage < totalPages ? currentPage + 1 : 1;
            RefreshSlots();
        }

        #endregion
    }
}
