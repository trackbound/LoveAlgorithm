using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 확인 팝업에 전달할 텍스트 데이터.
    /// 사용하지 않는 필드는 null로 두면 해당 TMP_Text가 자동 비활성화됨.
    /// </summary>
    public struct ConfirmPopupData
    {
        public string mainText;
        public string sub1;
        public string sub2;
        public string sub3;
        public string sub4;
        public string confirmText;
        public string cancelText;
    }

    /// <summary>
    /// 확인 팝업 (예/아니요). 결과 bool 반환.
    /// </summary>
    public class ConfirmPopup : PopupBase<bool>
    {
        [Header("텍스트 슬롯")]
        [FormerlySerializedAs("messageText")]
        [SerializeField] TMP_Text mainTextField;
        [FormerlySerializedAs("subText")]
        [SerializeField] TMP_Text sub1Text;
        [SerializeField] TMP_Text sub2Text;
        [SerializeField] TMP_Text sub3Text;
        [SerializeField] TMP_Text sub4Text;

        [Header("버튼")]
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text confirmButtonText;
        [SerializeField] TMP_Text cancelButtonText;

        [Header("설정")]
        [SerializeField] string defaultConfirmText = "예";
        [SerializeField] string defaultCancelText = "아니요";

        protected override void Awake()
        {
            base.Awake();
            confirmButton?.onClick.AddListener(OnConfirm);
            cancelButton?.onClick.AddListener(OnCancel);
        }

        public UniTask<bool> ShowAsync(ConfirmPopupData data)
        {
            var task = AwaitResult();
            Apply(data);
            Show();
            return task;
        }

        public UniTask<bool> ShowAsync(string message, string confirmText = null, string cancelText = null)
            => ShowAsync(new ConfirmPopupData
            {
                mainText = message,
                confirmText = confirmText,
                cancelText = cancelText,
            });

        public void Show(string message, Action onConfirm, Action onCancel = null)
        {
            ShowAsync(message).ContinueWith(result =>
            {
                if (result) onConfirm?.Invoke();
                else onCancel?.Invoke();
            });
        }

        void Apply(ConfirmPopupData data)
        {
            SetSlot(mainTextField, data.mainText);
            SetSlot(sub1Text, data.sub1);
            SetSlot(sub2Text, data.sub2);
            SetSlot(sub3Text, data.sub3);
            SetSlot(sub4Text, data.sub4);

            if (confirmButtonText != null)
                confirmButtonText.text = data.confirmText ?? defaultConfirmText;
            if (cancelButtonText != null)
                cancelButtonText.text = data.cancelText ?? defaultCancelText;
        }

        static void SetSlot(TMP_Text field, string value)
        {
            if (field == null) return;
            bool hasValue = !string.IsNullOrEmpty(value);
            field.gameObject.SetActive(hasValue);
            field.text = hasValue ? value : "";
        }

        void OnConfirm() => Complete(true);
        void OnCancel() => Complete(false);
    }
}
