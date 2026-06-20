using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowToastCommand
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 토스트 알림 뷰(*View). <see cref="ShowToastCommand"/>를 구독해 페이드 인 → 유지 → 페이드 아웃하는
    /// 비차단 알림을 표시한다(ADR-007: 표시만, 문구·의미는 호출부). 입력을 막지 않으며 선택 핸들이 없다 —
    /// 단순 피드백용. 표시 중 새 명령이 오면 진행 연출을 끊고 다시 띄운다(최신 우선). 타임스케일과 무관하게
    /// 동작하도록 언스케일드 시간을 사용(설정 팝업이 일시정지를 걸어도 정상 표시).
    /// </summary>
    public class ToastView : MonoBehaviour
    {
        [Tooltip("토스트 비주얼 루트 CanvasGroup(알파·입력 제어). 미바인딩 시 자신의 CanvasGroup을 사용.")]
        [SerializeField] CanvasGroup canvasGroup;
        [Tooltip("제목 텍스트(없으면 생략, 빈 제목이면 비활성).")]
        [SerializeField] TMP_Text titleText;
        [Tooltip("본문 텍스트(없으면 생략).")]
        [SerializeField] TMP_Text messageText;

        [Header("타이밍(초)")]
        [SerializeField] float fadeIn = 0.2f;
        [SerializeField] float hold = 1.6f;
        [SerializeField] float fadeOut = 0.4f;

        IDisposable _sub;
        Coroutine _routine;

        void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            SetHidden();
        }

        void OnEnable() => _sub = EventBus.Subscribe<ShowToastCommand>(OnShow);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            SetHidden();
        }

        /// <summary>토스트 표시 — 문구 채우고 페이드 연출 재시작(직접 호출도 가능, 테스트).</summary>
        public void OnShow(ShowToastCommand e)
        {
            if (titleText != null)
            {
                titleText.text = e.Title ?? "";
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(e.Title));
            }
            if (messageText != null) messageText.text = e.Message ?? "";

            if (!isActiveAndEnabled) return; // 비활성 GO에선 코루틴 불가(무해 — 표시 생략)
            float holdSec = e.Duration > 0f ? e.Duration : hold;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Play(holdSec));
        }

        IEnumerator Play(float holdSec)
        {
            if (canvasGroup == null) yield break;
            yield return Fade(0f, 1f, fadeIn);
            yield return new WaitForSecondsRealtime(holdSec);
            yield return Fade(1f, 0f, fadeOut);
            SetHidden();
            _routine = null;
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            canvasGroup.alpha = from;
            if (dur <= 0f) { canvasGroup.alpha = to; yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        void SetHidden()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false; // 토스트는 입력을 막지 않음
        }
    }
}
