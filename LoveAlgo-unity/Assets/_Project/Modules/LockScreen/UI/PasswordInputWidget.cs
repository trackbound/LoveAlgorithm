using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// 4자리 PIN 입력 위젯.
    /// 가상 키패드(0~9 + 백스페이스) + 4개 슬롯(*/숫자) + 눈 토글 + 열쇠 아이콘.
    /// 4자 입력 완료 시 OnPinEntered 발행.
    /// </summary>
    public class PasswordInputWidget : MonoBehaviour
    {
        [Header("Slots (4개)")]
        [Tooltip("4개 슬롯의 텍스트 (각 자리)")]
        [SerializeField] List<TMP_Text> slotTexts = new List<TMP_Text>();

        [Header("Keypad")]
        [Tooltip("0~9 버튼 (인덱스 = 숫자)")]
        [SerializeField] List<Button> digitButtons = new List<Button>();
        [SerializeField] Button backspaceButton;
        [SerializeField] Button confirmButton;     // 4자 입력 후 확정 (선택사항 — 자동 확정도 가능)

        [Header("Reveal Toggle")]
        [Tooltip("눈 아이콘 토글 (켜면 숫자 표시, 끄면 *)")]
        [SerializeField] Toggle revealToggle;

        [Header("Key Icon")]
        [Tooltip("3회 오류 시 표출되는 열쇠 아이콘 GameObject")]
        [SerializeField] GameObject keyIcon;

        [Header("Settings")]
        [Tooltip("4자 입력 시 자동 확정 (Confirm 버튼 없을 때)")]
        [SerializeField] bool autoConfirmOnFull = true;
        [SerializeField] string maskChar = "●";

        readonly char[] buffer = new char[4];
        int cursor;

        /// <summary>PIN 4자 확정 시 발행. (pin4 string)</summary>
        public event Action<string> OnPinEntered;

        void Awake()
        {
            // 숫자 버튼 바인딩
            for (int i = 0; i < digitButtons.Count && i < 10; i++)
            {
                int digit = i;
                if (digitButtons[i] != null)
                    digitButtons[i].onClick.AddListener(() => PressDigit(digit));
            }

            if (backspaceButton != null) backspaceButton.onClick.AddListener(PressBackspace);
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (revealToggle != null) revealToggle.onValueChanged.AddListener(_ => RefreshSlots());
        }

        void OnEnable()
        {
            Clear();
            SetKeyIcon(false);
        }

        public void Clear()
        {
            for (int i = 0; i < 4; i++) buffer[i] = '\0';
            cursor = 0;
            RefreshSlots();
        }

        public void SetKeyIcon(bool show)
        {
            if (keyIcon != null) keyIcon.SetActive(show);
        }

        void PressDigit(int digit)
        {
            if (cursor >= 4) return;
            buffer[cursor++] = (char)('0' + digit);
            RefreshSlots();

            if (autoConfirmOnFull && cursor == 4) Confirm();
        }

        void PressBackspace()
        {
            if (cursor == 0) return;
            buffer[--cursor] = '\0';
            RefreshSlots();
        }

        void Confirm()
        {
            if (cursor < 4) return;
            string pin = new string(buffer);
            OnPinEntered?.Invoke(pin);
        }

        void RefreshSlots()
        {
            bool reveal = revealToggle != null && revealToggle.isOn;
            for (int i = 0; i < slotTexts.Count; i++)
            {
                if (slotTexts[i] == null) continue;
                if (i < cursor)
                {
                    slotTexts[i].text = reveal ? buffer[i].ToString() : maskChar;
                }
                else
                {
                    slotTexts[i].text = "";
                }
            }
        }
    }
}
