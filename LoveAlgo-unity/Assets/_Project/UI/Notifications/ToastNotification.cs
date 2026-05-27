using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 토스트 팝업 (자동 사라짐). 순차 메시지 지원.
    /// PopupBase 통합 흐름 사용 (Layer=Notification).
    /// </summary>
    public class ToastNotification : PopupBase
    {
        public override PopupLayer Layer => PopupLayer.Notification;
        public override PopupAnimation AnimationType => PopupAnimation.SlideRight;

        [Header("UI 바인딩")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text messageText;

        [Header("설정")]
        [SerializeField] float fadeInDuration = 0.3f;
        [SerializeField] float fadeOutDuration = 0.4f;
        [SerializeField] float toastSlideOffset = 15f;   // 아래→위 슬라이드(px)
        [SerializeField] float textSwapDuration = 0.15f; // 텍스트 교체 페이드

        CancellationTokenSource cts;

        // D8: 토스트 큐 — 빠른 연속 호출이 서로 cancel하지 않고 순차 재생되도록.
        // 같은 메시지 dedup + 큐 길이 cap. ShowSequence는 자체 시퀀스라 큐 우회.
        readonly ToastQueue _queue = new(maxPending: 4);
        bool _pumpRunning;

        protected override void Awake()
        {
            base.Awake();
            // base.canvasGroup / panelRect / originalPosition 사용 (PopupBase가 캐싱)
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (panelRect == null) panelRect = GetComponent<RectTransform>();
            if (panelRect != null && originalPosition == Vector2.zero)
                originalPosition = panelRect.anchoredPosition;
        }

        /// <summary>
        /// 토스트 표시 (단일 메시지). D8: 진행 중이면 큐에 추가, dedup/cap 적용.
        /// </summary>
        public void Show(string title, string message, float duration = 2f)
        {
            var req = new ToastRequest(title, message, duration);
            if (!_queue.TryEnqueue(req)) return; // dedup/cap → 드롭
            if (_pumpRunning) return; // 펌프가 알아서 처리

            cts?.Cancel();
            cts = new CancellationTokenSource();
            PumpAsync(cts.Token).Forget();
        }

        /// <summary>토스트 큐 펌프 — 큐가 빌 때까지 한 개씩 재생.</summary>
        async UniTaskVoid PumpAsync(CancellationToken ct)
        {
            _pumpRunning = true;
            try
            {
                while (_queue.TryDequeueNext(out var req))
                {
                    await ShowAsync(req.Title, req.Message, req.Duration, ct);
                }
            }
            finally
            {
                _queue.MarkCurrentFinished();
                _pumpRunning = false;
            }
        }

        /// <summary>
        /// 순차 토스트 — 프레임은 유지하면서 메시지를 하나씩 교체
        /// </summary>
        public void ShowSequence(string title, List<string> messages, float holdPerItem = 0.8f)
        {
            if (messages == null || messages.Count == 0) return;

            // 1개면 일반 토스트
            if (messages.Count == 1)
            {
                Show(title, messages[0], 2f);
                return;
            }

            // ShowSequence는 preempt 의미 — 큐 청소 + 진행 중인 펌프/시퀀스 모두 중단
            _queue.Clear();
            cts?.Cancel();
            cts = new CancellationTokenSource();

            ShowSequenceAsync(title, messages, holdPerItem, cts.Token).Forget();
        }

        async UniTaskVoid ShowSequenceAsync(string title, List<string> messages, float holdPerItem, CancellationToken ct)
        {
            if (titleText != null)
                titleText.text = title;
            if (messageText != null)
            {
                messageText.text = messages[0];
                messageText.alpha = 1f;
            }

            gameObject.SetActive(true);
            PopupManager.Instance?.NotifyOpened(this);

            // ── 토스트 프레임 등장 ──
            await FadeInFrameAsync(ct);

            // ── 첫 메시지 홀드 ──
            await UniTask.Delay(TimeSpan.FromSeconds(holdPerItem), cancellationToken: ct);

            // ── 후속 메시지 순차 교체 (텍스트만 페이드) ──
            for (int i = 1; i < messages.Count; i++)
            {
                // 텍스트 페이드 아웃
                if (messageText != null)
                {
                    await DOTween.ToAlpha(
                        () => messageText.color, c => messageText.color = c,
                        0f, textSwapDuration
                    ).SetEase(Ease.InCubic).ToUniTask(cancellationToken: ct);

                    messageText.text = messages[i];

                    // 텍스트 페이드 인
                    await DOTween.ToAlpha(
                        () => messageText.color, c => messageText.color = c,
                        1f, textSwapDuration
                    ).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(holdPerItem), cancellationToken: ct);
            }

            // ── 마지막 메시지 약간 더 유지 ──
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);

            // ── 토스트 프레임 퇴장 ──
            await FadeOutFrameAsync(ct);

            PopupManager.Instance?.NotifyClosed(this);
            gameObject.SetActive(false);
        }

        async UniTaskVoid ShowAsync(string title, string message, float duration, CancellationToken ct)
        {
            if (titleText != null)
                titleText.text = title;
            if (messageText != null)
            {
                messageText.text = message;
                messageText.alpha = 1f;
            }

            gameObject.SetActive(true);
            PopupManager.Instance?.NotifyOpened(this);

            await FadeInFrameAsync(ct);
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
            await FadeOutFrameAsync(ct);

            PopupManager.Instance?.NotifyClosed(this);
            gameObject.SetActive(false);
        }

        /// <summary>토스트 프레임 슬라이드+페이드 등장</summary>
        async UniTask FadeInFrameAsync(CancellationToken ct)
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = 0f;
            if (panelRect != null)
            {
                panelRect.anchoredPosition = originalPosition + new Vector2(0, -toastSlideOffset);
                var seq = DOTween.Sequence();
                _ = seq.Join(canvasGroup.DOFade(1f, fadeInDuration).SetEase(Ease.OutCubic));
                _ = seq.Join(panelRect.DOAnchorPos(originalPosition, fadeInDuration).SetEase(Ease.OutCubic));
                await seq.ToUniTask(cancellationToken: ct);
            }
            else
            {
                await canvasGroup.DOFade(1f, fadeInDuration)
                    .SetEase(Ease.OutCubic)
                    .ToUniTask(cancellationToken: ct);
            }
        }

        /// <summary>토스트 프레임 슬라이드+페이드 퇴장</summary>
        async UniTask FadeOutFrameAsync(CancellationToken ct)
        {
            if (canvasGroup == null) return;

            if (panelRect != null)
            {
                var seq = DOTween.Sequence();
                _ = seq.Join(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.InCubic));
                _ = seq.Join(panelRect.DOAnchorPosY(originalPosition.y + toastSlideOffset, fadeOutDuration).SetEase(Ease.InCubic));
                await seq.ToUniTask(cancellationToken: ct);
                panelRect.anchoredPosition = originalPosition;
            }
            else
            {
                await canvasGroup.DOFade(0f, fadeOutDuration)
                    .SetEase(Ease.InCubic)
                    .ToUniTask(cancellationToken: ct);
            }
        }

        protected override void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
            base.OnDestroy();
        }
    }
}
