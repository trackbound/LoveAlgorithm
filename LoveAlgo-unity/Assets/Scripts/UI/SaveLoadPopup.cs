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
        [SerializeField] Image prevButtonImage;
        [SerializeField] Image nextButtonImage;
        [SerializeField] Sprite prevSprite;
        [SerializeField] Sprite prevDisabledSprite;
        [SerializeField] Sprite nextSprite;
        [SerializeField] Sprite nextDisabledSprite;

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
            int globalIndex = GetGlobalSlotIndex(localIndex);
            
            if (isSaveMode)
            {
                OnSaveSlotClicked(globalIndex).Forget();
            }
            else
            {
                OnLoadSlotClicked(globalIndex).Forget();
            }
        }

        /// <summary>
        /// 로컬 인덱스 → 글로벌 슬롯 인덱스 변환
        /// 팝업에서는 유저 슬롯(1~N)만 노출
        /// </summary>
        int GetGlobalSlotIndex(int localIndex)
        {
            int startIndex = (currentPage - 1) * slotsPerPage;
            return SaveManager.UserSlotStart + startIndex + localIndex;
        }

        async UniTaskVoid OnSaveSlotClicked(int slotIndex)
        {
            bool hasData = SaveManager.Exists(slotIndex);

            if (hasData)
            {
                // 덮어쓰기 확인
                bool confirm = await PopupManager.Instance.ConfirmAsync("슬롯의 기존 데이터는 사라집니다.\n저장을 계속하시겠습니까?");
                if (!confirm) return;
            }
            else
            {
                // 새 슬롯에 저장 확인
                bool confirm = await PopupManager.Instance.ConfirmAsync("해당 슬롯에 저장하시겠습니까?");
                if (!confirm) return;
            }

            // UI 숨기고 캡처 후 복원 (팝업이 썸네일에 찍히지 않도록)
            var popupState = PopupManager.Instance.HideForThumbnailCapture();
            await UniTask.Yield();  // 1프레임 대기 (렌더링 반영)
            SaveManager.CapturePendingScreenshot();
            PopupManager.Instance.RestoreAfterThumbnailCapture(popupState);

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
            for (int i = 0; i < slotItems.Count; i++)
            {
                var slot = slotItems[i];
                if (slot == null) continue;

                int globalIndex = GetGlobalSlotIndex(i);

                // 범위 초과 체크
                if (globalIndex > userSlots)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                slot.gameObject.SetActive(true);

                slot.Setup(i, OnSlotClicked, autoSave: false);
                slot.SetDisplayNumber(globalIndex);

                // 세이브 데이터 확인
                var data = SaveManager.Load(globalIndex);
                if (data != null)
                {
                    var screenshot = SaveManager.LoadScreenshot(globalIndex);
                    string label = data.ChapterName;
                    slot.SetData(label, data.SaveTime, screenshot);
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

            UpdatePageButtonUI();
        }

        void UpdatePageButtonUI()
        {
            bool isFirst = currentPage <= 1;
            bool isLast = currentPage >= totalPages;

            if (prevButton != null)
                prevButton.interactable = !isFirst;
            if (prevButtonImage != null)
                prevButtonImage.sprite = isFirst ? prevDisabledSprite : prevSprite;

            if (nextButton != null)
                nextButton.interactable = !isLast;
            if (nextButtonImage != null)
                nextButtonImage.sprite = isLast ? nextDisabledSprite : nextSprite;
        }

        void PrevPage()
        {
            if (currentPage <= 1) return;
            currentPage--;
            RefreshSlots();
        }

        void NextPage()
        {
            if (currentPage >= totalPages) return;
            currentPage++;
            RefreshSlots();
        }

        #endregion
    }
}
