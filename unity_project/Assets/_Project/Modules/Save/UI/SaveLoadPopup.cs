using LoveAlgo.Contracts;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Common;
using LoveAlgo.Save;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 세이브/로드 모달 팝업
    /// </summary>
    public class SaveLoadPopup : PopupBase
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

        [Header("이름 입력 Sub-Panel (옵션)")]
        [Tooltip("프리팹에 배치되어 있으면 슬롯 클릭 시 이름 입력 흐름 사용. 미배치면 Confirm 폴백.")]
        [SerializeField] GameObject nameInputPanel;
        [SerializeField] TMP_Text nameInputTitle;
        [SerializeField] TMP_InputField nameInputField;
        [SerializeField] Button nameInputConfirm;
        [SerializeField] Button nameInputCancel;
        [Tooltip("최대 글자수 (한글/공백 포함)")]
        [SerializeField] int nameInputMaxLength = 24;

        bool isSaveMode = true;
        int currentPage = 1;
        int totalPages = 1;

        // 콜백 — slot index + 사용자 입력 라벨 (null 또는 빈 문자열이면 자동값 사용)
        System.Action<int, string> onSlotSelected;
        UniTaskCompletionSource<string> nameInputTcs;

        // Service cache
        ISave save;
        readonly ListenerBag _listeners = new();

        protected override void Awake()
        {
            base.Awake();

            save = Services.Get<ISave>();

            _listeners.Bind(closeButton, Close);
            _listeners.Bind(prevButton, PrevPage);
            _listeners.Bind(nextButton, NextPage);

            // 이름 입력 panel — Inspector 바인딩 시에만 wire (없으면 폴백)
            if (nameInputPanel != null)
            {
                nameInputPanel.SetActive(false);
                if (nameInputField != null) nameInputField.characterLimit = nameInputMaxLength;
                _listeners.Bind(nameInputConfirm, OnNameInputConfirm);
                _listeners.Bind(nameInputCancel, OnNameInputCancel);
            }

            // 슬롯 콜백 설정
            for (int i = 0; i < slotItems.Count; i++)
            {
                slotItems[i]?.Setup(i, OnSlotClicked);
            }

            // 자동저장 슬롯(1) + 유저 슬롯(userSlots)
            int totalSlots = 1 + userSlots;
            totalPages = Mathf.CeilToInt((float)totalSlots / slotsPerPage);
        }

        protected override void OnDestroy()
        {
            _listeners.Dispose();
            base.OnDestroy();
        }

        #region Show/Hide

        public override void Show()
        {
            KillSequence();
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
        /// 세이브 모드로 열기. 콜백 시그니처: (slotIndex, customLabel) — customLabel은
        /// null/빈값이면 자동 chapterName 사용.
        /// </summary>
        public void ShowSave(System.Action<int, string> onSelect = null)
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
        /// 로드 모드로 열기. customLabel은 무시됨.
        /// </summary>
        public void ShowLoad(System.Action<int, string> onSelect = null)
        {
            isSaveMode = false;
            onSlotSelected = onSelect;

            if (titleText != null)
                titleText.text = "불러오기";

            currentPage = 1;
            RefreshSlots();
            Show();
        }

        // ── 하위 호환 (기존 Action<int> 시그니처) ───────────────────
        public void ShowSave(System.Action<int> onSelect)
            => ShowSave(onSelect != null ? (System.Action<int, string>)((s, _) => onSelect(s)) : null);

        public void ShowLoad(System.Action<int> onSelect)
            => ShowLoad(onSelect != null ? (System.Action<int, string>)((s, _) => onSelect(s)) : null);

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
        /// 1페이지: 자동저장(0) + 유저 슬롯(1~5)
        /// 2페이지+: 유저 슬롯(6~11, 12~17, ...)
        /// </summary>
        int GetGlobalSlotIndex(int localIndex)
        {
            return (currentPage - 1) * slotsPerPage + localIndex;
        }

        async UniTaskVoid OnSaveSlotClicked(int slotIndex)
        {
            // 자동 저장 슬롯(0)은 수동 저장 불가 — 안내 팝업 후 종료
            if (slotIndex == save.AutoSaveSlot)
            {
                await PopupSystem.Instance.AlertAsync("자동 저장 슬롯입니다.\n다른 슬롯에 저장해 주세요.");
                return;
            }

            bool hasData = save.Exists(slotIndex);
            string customLabel = null;

            // 이름 입력 panel이 wire되어 있으면 입력 흐름 사용, 아니면 Confirm 폴백
            if (nameInputPanel != null)
            {
                string prefill = hasData ? (save.Load(slotIndex)?.ChapterName ?? "") : "";
                customLabel = await ShowNameInputAsync(
                    hasData ? "저장 이름 (덮어쓰기)" : "저장 이름",
                    prefill);
                if (customLabel == null) return;  // 사용자 취소
            }
            else
            {
                string confirmMsg = hasData
                    ? "슬롯의 기존 데이터는 사라집니다.\n저장을 계속하시겠습니까?"
                    : "해당 슬롯에 저장하시겠습니까?";
                bool confirm = await PopupSystem.Instance.ConfirmAsync(confirmMsg);
                if (!confirm) return;
            }

            // ShowSave()에서 팝업 열기 전 미리 캡처한 pending 썸네일은 첫 commit 시 삭제됨.
            // 같은 SaveLoadPopup 세션에서 2번째 이후 저장에도 정확한 썸네일이 적용되도록
            // 매 저장 직전에 다시 캡처. (Confirm 팝업은 이미 닫혔고, SaveLoadPopup/Confirm 등
            // PopupSystem 하위 모든 팝업은 SaveThumbnailSystem 화이트리스트 캡처에서
            // 자동으로 숨겨진다 — 화이트리스트: 캐릭터 CG, 배경 BG, ScheduleUI, ShopUI)
            await save.CapturePendingScreenshotAsync();

            // 저장 실행 + 슬롯 갱신 (팝업은 유지)
            onSlotSelected?.Invoke(slotIndex, customLabel);
            RefreshSlots();
        }

        /// <summary>
        /// 이름 입력 sub-panel을 표시하고 사용자 입력값을 await.
        /// 반환: 입력된 라벨(빈 값이면 ""로 반환 → 자동값 사용). 취소 시 null.
        /// </summary>
        async UniTask<string> ShowNameInputAsync(string title, string prefill)
        {
            if (nameInputPanel == null) return "";

            if (nameInputTitle != null) nameInputTitle.text = title;
            if (nameInputField != null)
            {
                nameInputField.SetTextWithoutNotify(prefill ?? "");
                nameInputField.ActivateInputField();
            }

            nameInputTcs = new UniTaskCompletionSource<string>();
            nameInputPanel.SetActive(true);

            string result;
            try { result = await nameInputTcs.Task; }
            finally
            {
                nameInputPanel.SetActive(false);
                nameInputTcs = null;
            }
            return result;
        }

        void OnNameInputConfirm()
        {
            string value = nameInputField != null ? (nameInputField.text ?? "") : "";
            nameInputTcs?.TrySetResult(value.Trim());
        }

        void OnNameInputCancel()
        {
            nameInputTcs?.TrySetResult(null);
        }

        async UniTaskVoid OnLoadSlotClicked(int slotIndex)
        {
            // 빈 슬롯은 무반응
            if (!save.Exists(slotIndex)) return;

            // 로드 확인
            bool confirm = await PopupSystem.Instance.ConfirmAsync(
                "이 부분부터 시작할까요?", "예", "아니오");
            if (!confirm) return;

            // 로드는 씬 전환이므로 팝업 닫고 실행 (customLabel은 로드에서 무시)
            await HideAsync();
            await UniTask.Delay(150);
            onSlotSelected?.Invoke(slotIndex, null);
        }

        #region Pagination

        void RefreshSlots()
        {
            int totalSlots = 1 + userSlots; // 자동저장(0) + 유저 슬롯(1~N)

            for (int i = 0; i < slotItems.Count; i++)
            {
                var slot = slotItems[i];
                if (slot == null) continue;

                int globalIndex = GetGlobalSlotIndex(i);

                // 범위 초과 체크
                if (globalIndex >= totalSlots)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                slot.gameObject.SetActive(true);

                bool isAutoSave = (globalIndex == save.AutoSaveSlot);
                slot.Setup(i, OnSlotClicked, autoSave: isAutoSave);
                slot.SetDisplayNumber(globalIndex);

                // 세이브 모드에서도 자동저장 슬롯을 클릭 가능하게 둔다.
                // 클릭 시 OnSaveSlotClicked가 안내 팝업을 띄우고 종료한다.
                slot.SetInteractable(true);

                // 세이브 데이터 확인
                var data = save.Load(globalIndex);
                if (data != null)
                {
                    var screenshot = save.LoadScreenshot(globalIndex);
                    string label = isAutoSave ? "자동 저장" : data.ChapterName;
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
