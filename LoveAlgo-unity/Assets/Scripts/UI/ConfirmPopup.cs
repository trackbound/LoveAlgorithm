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
    /// 확인 팝업 (확인/취소).
    /// 프리팹 이름으로 PopupManager에서 조회되며, 최대 5개 텍스트 슬롯을 지원한다.
    /// </summary>
    public class ConfirmPopup : MonoBehaviour
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

        UniTaskCompletionSource<bool> tcs;

        public bool IsVisible => gameObject.activeSelf;

        void Awake()
        {
            confirmButton?.onClick.AddListener(OnConfirm);
            cancelButton?.onClick.AddListener(OnCancel);
        }

        /// <summary>
        /// 팝업 표시 및 결과 대기 (데이터 구조체 버전)
        /// </summary>
        public UniTask<bool> ShowAsync(ConfirmPopupData data)
        {
            tcs?.TrySetCanceled();
            tcs = new UniTaskCompletionSource<bool>();

            ShowInternal(data);
            return tcs.Task;
        }

        /// <summary>
        /// 팝업 표시 및 결과 대기 (간편 버전)
        /// </summary>
        public UniTask<bool> ShowAsync(string message, string confirmText = null, string cancelText = null)
        {
            return ShowAsync(new ConfirmPopupData
            {
                mainText = message,
                confirmText = confirmText,
                cancelText = cancelText,
            });
        }

        /// <summary>
        /// 팝업 표시 (콜백 버전)
        /// </summary>
        public void Show(string message, Action onConfirm, Action onCancel = null)
        {
            tcs?.TrySetCanceled();
            tcs = new UniTaskCompletionSource<bool>();

            ShowInternal(new ConfirmPopupData { mainText = message });

            tcs.Task.ContinueWith(result =>
            {
                if (result) onConfirm?.Invoke();
                else onCancel?.Invoke();
            });
        }

        void ShowInternal(ConfirmPopupData data)
        {
            PopupManager.Instance?.ShowTopDimmer(true);

            SetSlot(mainTextField, data.mainText);
            SetSlot(sub1Text, data.sub1);
            SetSlot(sub2Text, data.sub2);
            SetSlot(sub3Text, data.sub3);
            SetSlot(sub4Text, data.sub4);

            if (confirmButtonText != null)
                confirmButtonText.text = data.confirmText ?? defaultConfirmText;
            if (cancelButtonText != null)
                cancelButtonText.text = data.cancelText ?? defaultCancelText;

            gameObject.SetActive(true);
        }

        static void SetSlot(TMP_Text field, string value)
        {
            if (field == null) return;
            bool hasValue = !string.IsNullOrEmpty(value);
            field.gameObject.SetActive(hasValue);
            field.text = hasValue ? value : "";
        }

        void OnConfirm()
        {
            tcs?.TrySetResult(true);
            Hide();
        }

        void OnCancel()
        {
            tcs?.TrySetResult(false);
            Hide();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            PopupManager.Instance?.ShowTopDimmer(false);
            tcs?.TrySetResult(false);
        }

        void OnDestroy()
        {
            tcs?.TrySetCanceled();
            tcs = null;
        }
    }
}
