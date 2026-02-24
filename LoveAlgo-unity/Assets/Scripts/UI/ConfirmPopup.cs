using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 확인 팝업 (확인/취소)
    /// </summary>
    public class ConfirmPopup : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] TMP_Text messageText;
        [SerializeField] TMP_Text subText;  // 선택적 (스케줄 효과 등)
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text confirmButtonText;
        [SerializeField] TMP_Text cancelButtonText;

        [Header("설정")]
        [SerializeField] string defaultConfirmText = "확인";
        [SerializeField] string defaultCancelText = "취소";

        UniTaskCompletionSource<bool> tcs;

        public bool IsVisible => gameObject.activeSelf;

        void Awake()
        {
            confirmButton?.onClick.AddListener(OnConfirm);
            cancelButton?.onClick.AddListener(OnCancel);
        }

        /// <summary>
        /// 팝업 표시 및 결과 대기 (Async)
        /// </summary>
        public UniTask<bool> ShowAsync(string message, string sub = null,
            string confirmText = null, string cancelText = null)
        {
            // 이전 작업 취소
            tcs?.TrySetResult(false);
            tcs = new UniTaskCompletionSource<bool>();

            ShowInternal(message, sub, confirmText, cancelText);
            return tcs.Task;
        }

        /// <summary>
        /// 팝업 표시 (콜백 버전)
        /// </summary>
        public void Show(string message, Action onConfirm, Action onCancel = null)
        {
            // 이전 작업 취소
            tcs?.TrySetResult(false);
            tcs = new UniTaskCompletionSource<bool>();

            ShowInternal(message, null, null, null);

            // 콜백 처리
            tcs.Task.ContinueWith(result =>
            {
                if (result) onConfirm?.Invoke();
                else onCancel?.Invoke();
            });
        }

        void ShowInternal(string message, string sub, string confirmText, string cancelText)
        {
            // Dimmer 표시
            PopupManager.Instance?.ShowTopDimmer(true);
            
            // UI 설정
            if (messageText != null) messageText.text = message;
            
            // 서브텍스트 (없으면 숨김)
            if (subText != null)
            {
                subText.gameObject.SetActive(!string.IsNullOrEmpty(sub));
                subText.text = sub ?? "";
            }
            
            if (confirmButtonText != null) 
                confirmButtonText.text = confirmText ?? defaultConfirmText;
            if (cancelButtonText != null) 
                cancelButtonText.text = cancelText ?? defaultCancelText;

            gameObject.SetActive(true);
        }

        void OnConfirm()
        {
            Hide();
            tcs?.TrySetResult(true);
        }

        void OnCancel()
        {
            Hide();
            tcs?.TrySetResult(false);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            PopupManager.Instance?.ShowTopDimmer(false);
        }
    }
}
