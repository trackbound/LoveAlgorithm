using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 알림 팝업 (확인만)
    /// </summary>
    public class AlertPopup : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] TMP_Text messageText;
        [SerializeField] Button confirmButton;
        [SerializeField] TMP_Text confirmButtonText;

        [Header("설정")]
        [SerializeField] string defaultConfirmText = "확인";

        UniTaskCompletionSource tcs;

        public bool IsVisible => gameObject.activeSelf;

        void Awake()
        {
            confirmButton?.onClick.AddListener(OnConfirm);
        }

        /// <summary>
        /// 팝업 표시 및 확인 대기
        /// </summary>
        public UniTask ShowAsync(string message, string confirmText = null)
        {
            // 이전 작업 완료 처리
            tcs?.TrySetResult();
            tcs = new UniTaskCompletionSource();

            // Dimmer 표시
            PopupManager.Instance?.ShowTopDimmer(true);

            // UI 설정
            if (messageText != null) messageText.text = message;
            if (confirmButtonText != null)
                confirmButtonText.text = confirmText ?? defaultConfirmText;

            gameObject.SetActive(true);
            return tcs.Task;
        }

        void OnConfirm()
        {
            Hide();
            tcs?.TrySetResult();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            PopupManager.Instance?.ShowTopDimmer(false);
            // 외부에서 Hide() 호출 시에도 대기 중인 UniTask를 완료
            tcs?.TrySetResult();
        }

        void OnDestroy()
        {
            tcs?.TrySetCanceled();
            tcs = null;
        }
    }
}
