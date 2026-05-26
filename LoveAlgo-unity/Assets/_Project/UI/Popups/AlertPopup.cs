using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 알림 팝업 (확인만). PopupBase 통합 흐름 사용.
    /// </summary>
    public class AlertPopup : PopupBase
    {
        public override PopupLayer Layer => PopupLayer.Dialog;

        [Header("UI 바인딩")]
        [SerializeField] TMP_Text messageText;
        [SerializeField] Button confirmButton;
        [SerializeField] TMP_Text confirmButtonText;

        [Header("설정")]
        [SerializeField] string defaultConfirmText = "확인";

        UniTaskCompletionSource tcs;
        readonly ListenerBag _listeners = new();

        protected override void Awake()
        {
            base.Awake();
            _listeners.Bind(confirmButton, OnConfirm);
        }

        /// <summary>팝업 표시 및 확인 대기.</summary>
        public UniTask ShowAsync(string message, string confirmText = null)
        {
            // 이전 대기 task 해제
            tcs?.TrySetResult();
            tcs = new UniTaskCompletionSource();

            if (messageText != null) messageText.text = message;
            if (confirmButtonText != null)
                confirmButtonText.text = confirmText ?? defaultConfirmText;

            Show();
            return tcs.Task;
        }

        void OnConfirm()
        {
            var pending = tcs;
            tcs = null;
            Hide();
            pending?.TrySetResult();
        }

        public override void Hide()
        {
            base.Hide();
            tcs?.TrySetResult();
            tcs = null;
        }

        protected override void OnDestroy()
        {
            _listeners.Dispose();
            tcs?.TrySetCanceled();
            tcs = null;
            base.OnDestroy();
        }
    }
}
