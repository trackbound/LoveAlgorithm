using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 토스트 팝업 (자동 사라짐)
    /// </summary>
    public class ToastPopup : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text messageText;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("설정")]
        [SerializeField] float fadeInDuration = 0.2f;
        [SerializeField] float fadeOutDuration = 0.3f;

        CancellationTokenSource cts;

        void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// 토스트 표시
        /// </summary>
        public void Show(string title, string message, float duration = 2f)
        {
            // 이전 토스트 취소
            cts?.Cancel();
            cts = new CancellationTokenSource();

            ShowAsync(title, message, duration, cts.Token).Forget();
        }

        async UniTaskVoid ShowAsync(string title, string message, float duration, CancellationToken ct)
        {
            // 텍스트 설정
            if (titleText != null)
                titleText.text = title;
            if (messageText != null)
                messageText.text = message;

            gameObject.SetActive(true);

            // 페이드 인
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                await canvasGroup.DOFade(1f, fadeInDuration)
                    .ToUniTask(cancellationToken: ct);
            }

            // 대기
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);

            // 페이드 아웃
            if (canvasGroup != null)
            {
                await canvasGroup.DOFade(0f, fadeOutDuration)
                    .ToUniTask(cancellationToken: ct);
            }

            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
