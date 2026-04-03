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
        [SerializeField] float fadeInDuration = 0.3f;
        [SerializeField] float fadeOutDuration = 0.4f;
        [SerializeField] float slideOffset = 15f;   // 아래에서 위로 슬라이드 (px)

        CancellationTokenSource cts;
        RectTransform rectTransform;
        Vector2 originalPos;

        void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
                originalPos = rectTransform.anchoredPosition;
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

            // 슬라이드 + 페이드 등장
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = originalPos + new Vector2(0, -slideOffset);
                    var seq = DOTween.Sequence();
                    _ = seq.Join(canvasGroup.DOFade(1f, fadeInDuration).SetEase(Ease.OutCubic));
                    _ = seq.Join(rectTransform.DOAnchorPos(originalPos, fadeInDuration).SetEase(Ease.OutCubic));
                    await seq.ToUniTask(cancellationToken: ct);
                }
                else
                {
                    await canvasGroup.DOFade(1f, fadeInDuration)
                        .SetEase(Ease.OutCubic)
                        .ToUniTask(cancellationToken: ct);
                }
            }

            // 대기
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);

            // 슬라이드 + 페이드 퇴장
            if (canvasGroup != null)
            {
                if (rectTransform != null)
                {
                    var seq = DOTween.Sequence();
                    _ = seq.Join(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.InCubic));
                    _ = seq.Join(rectTransform.DOAnchorPosY(originalPos.y + slideOffset, fadeOutDuration).SetEase(Ease.InCubic));
                    await seq.ToUniTask(cancellationToken: ct);
                    rectTransform.anchoredPosition = originalPos;
                }
                else
                {
                    await canvasGroup.DOFade(0f, fadeOutDuration)
                        .SetEase(Ease.InCubic)
                        .ToUniTask(cancellationToken: ct);
                }
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
