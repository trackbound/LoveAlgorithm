using System;
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

        int slotIndex;
        bool hasData;
        bool isAutoSaveSlot;
        Action<int> onClick;

        void Awake()
        {
            button?.onClick.AddListener(() => onClick?.Invoke(slotIndex));
        }

        /// <summary>
        /// 슬롯 초기화
        /// </summary>
        public void Setup(int index, Action<int> onClickCallback, bool autoSave = false)
        {
            slotIndex = index;
            onClick = onClickCallback;
            isAutoSaveSlot = autoSave;
        }

        /// <summary>
        /// 빈 슬롯으로 표시
        /// </summary>
        public void SetEmpty()
        {
            hasData = false;
            
            if (slotNumberText != null)
                slotNumberText.text = isAutoSaveSlot ? "Auto" : $"슬롯 {slotIndex + 1}";
            
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
                slotNumberText.text = isAutoSaveSlot ? "Auto" : $"슬롯 {slotIndex + 1}";
            
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
