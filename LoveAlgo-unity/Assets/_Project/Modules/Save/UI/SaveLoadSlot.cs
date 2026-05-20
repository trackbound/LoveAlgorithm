using System;
using LoveAlgo.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 세이브 슬롯 아이템
    /// </summary>
    public class SaveLoadSlot : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] TMP_Text slotNumberText;
        [SerializeField] TMP_Text chapterText;
        [SerializeField] TMP_Text dateText;
        [SerializeField] Image screenshotImage;
        [SerializeField] GameObject emptyLabel;
        [SerializeField] GameObject dataContainer;
        [SerializeField] Button button;

        int slotIndex;       // 클릭 콜백용 로컬 인덱스
        int displayNumber;   // 표시용 글로벌 슬롯 번호
        bool hasData;
        bool isAutoSaveSlot;
        Action<int> onClick;

        readonly ListenerBag _listeners = new();

        void Awake()
        {
            _listeners.Bind(button, RaiseClick);
        }

        void OnDestroy() => _listeners.Dispose();

        void RaiseClick() => onClick?.Invoke(slotIndex);

        /// <summary>
        /// 슬롯 초기화
        /// </summary>
        public void Setup(int localIndex, Action<int> onClickCallback, bool autoSave = false)
        {
            slotIndex = localIndex;
            onClick = onClickCallback;
            isAutoSaveSlot = autoSave;
        }

        /// <summary>
        /// 슬롯 번호 텍스트 설정 (글로벌 슬롯 인덱스로 갱신)
        /// </summary>
        public void SetDisplayNumber(int globalSlotIndex)
        {
            displayNumber = globalSlotIndex;
            if (slotNumberText != null)
                slotNumberText.text = isAutoSaveSlot ? "자동 저장" : $"슬롯 {globalSlotIndex}";
        }

        /// <summary>
        /// 버튼 인터랙션 설정
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        /// <summary>
        /// 빈 슬롯으로 표시
        /// </summary>
        public void SetEmpty()
        {
            hasData = false;

            if (slotNumberText != null)
                slotNumberText.text = isAutoSaveSlot ? "자동 저장" : $"슬롯 {displayNumber}";
            
            if (emptyLabel != null)
                emptyLabel.SetActive(true);
            
            if (dataContainer != null)
                dataContainer.SetActive(false);
        }

        /// <summary>
        /// 데이터가 있는 슬롯으로 표시
        /// </summary>
        public void SetData(string chapter, DateTime saveTime, Sprite screenshot = null)
        {
            hasData = true;
            
            if (slotNumberText != null)
                slotNumberText.text = isAutoSaveSlot ? "자동 저장" : $"슬롯 {displayNumber}";

            if (chapterText != null)
                chapterText.text = chapter ?? "알 수 없음";
            
            if (dateText != null)
                dateText.text = saveTime.ToString("yyyy/MM/dd HH:mm");

            if (screenshotImage != null)
            {
                if (screenshot != null)
                {
                    screenshotImage.sprite = screenshot;
                    screenshotImage.enabled = true;
                }
                else
                {
                    screenshotImage.enabled = false;
                }
            }
            
            if (emptyLabel != null)
                emptyLabel.SetActive(false);
            
            if (dataContainer != null)
                dataContainer.SetActive(true);
        }

        public bool HasData => hasData;
        public int SlotIndex => slotIndex;
    }
}
