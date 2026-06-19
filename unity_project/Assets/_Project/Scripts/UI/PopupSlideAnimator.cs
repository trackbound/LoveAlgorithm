using System;
using System.Collections;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 오버레이 팝업의 등장/퇴장 연출: 창(<see cref="slideTarget"/>)을 화면 <b>오른쪽 밖 ↔ 정착 위치</b>로
    /// 수평 슬라이드하면서 루트 <see cref="canvasGroup"/> 알파를 함께 페이드한다(딤 포함 페이드 + 창 슬라이드).
    /// 열기=오른쪽→정착(슬라이드 인), 닫기=정착→오른쪽(되돌아 나감).
    ///
    /// 표시 토글의 주도권은 View가 갖는다(OverlayGate·형제정렬·입력 차단). 이 컴포넌트는 순수 비주얼 트윈만
    /// 담당하므로(ADR-007 표시 계층) SettingsView/SaveLoadView/ExtraView가 <see cref="PlayShow"/>/<see cref="PlayHide"/>를
    /// 호출하고, 부팅/즉시 숨김은 <see cref="ApplyHiddenInstant"/>로 스냅한다. Time.timeScale=0(일시정지)에서도
    /// 동작하도록 unscaled 시간을 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PopupSlideAnimator : MonoBehaviour
    {
        [Tooltip("슬라이드할 창(딤을 제외한 Panel/Window). 비우면 이 오브젝트의 RectTransform.")]
        [SerializeField] RectTransform slideTarget;
        [Tooltip("함께 페이드할 CanvasGroup(보통 팝업 루트). 비우면 이 오브젝트에서 탐색.")]
        [SerializeField] CanvasGroup canvasGroup;
        [Tooltip("연출 시간(초).")]
        [SerializeField] float duration = 0.25f;
        [Tooltip("화면 밖으로 미는 거리(px). 0이면 부모(루트) 폭으로 자동 — 어떤 패널도 우측 밖으로 나간다.")]
        [SerializeField] float offscreenDistance = 0f;

        Vector2 _shownPos;   // 디자인(정착) 위치 — 첫 Capture가 '이동 전'에 잡는다.
        bool _captured;
        Coroutine _co;

        // 첫 호출이 어떤 이동보다 먼저 일어나도록 보장(View.Awake의 SetVisible(false)가 최초 호출).
        void Capture()
        {
            if (_captured) return;
            if (slideTarget == null) slideTarget = transform as RectTransform;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            _shownPos = slideTarget.anchoredPosition;
            _captured = true;
        }

        float HiddenX
        {
            get
            {
                float d = offscreenDistance > 0f ? offscreenDistance : AutoDistance();
                return _shownPos.x + d; // 우측(+x)으로 밀어 화면 밖
            }
        }

        float AutoDistance()
        {
            var parent = slideTarget.parent as RectTransform;
            float w = parent != null ? parent.rect.width : 0f;
            return w > 1f ? w : 2200f; // 부모 폭만큼 이동 = 중앙 정렬 패널이면 확실히 화면 밖
        }

        // ── View가 호출 ───────────────────────────────────────────────────────────────
        /// <summary>현재 상태 → 정착 위치/알파1로 슬라이드 인.</summary>
        public void PlayShow()
        {
            Capture();
            Restart(Animate(_shownPos.x, 1f, null));
        }

        /// <summary>현재 상태 → 오른쪽 밖/알파0으로 슬라이드 아웃. 완료 콜백(예: 썸네일 정리)은 끝난 뒤 호출.</summary>
        public void PlayHide(Action onComplete = null)
        {
            Capture();
            Restart(Animate(HiddenX, 0f, onComplete));
        }

        /// <summary>연출 없이 숨김 상태로 스냅(부팅/중복 숨김).</summary>
        public void ApplyHiddenInstant()
        {
            Capture();
            StopCo();
            SetState(HiddenX, 0f);
        }

        void Restart(IEnumerator routine)
        {
            StopCo();
            if (isActiveAndEnabled) _co = StartCoroutine(routine);
        }

        void StopCo()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
        }

        IEnumerator Animate(float targetX, float targetAlpha, Action onComplete)
        {
            float startX = slideTarget.anchoredPosition.x;
            float startA = canvasGroup != null ? canvasGroup.alpha : 1f;
            float dur = Mathf.Max(0.0001f, duration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                SetState(Mathf.LerpUnclamped(startX, targetX, k), Mathf.LerpUnclamped(startA, targetAlpha, k));
                yield return null;
            }
            SetState(targetX, targetAlpha);
            _co = null;
            onComplete?.Invoke();
        }

        void SetState(float x, float alpha)
        {
            var p = slideTarget.anchoredPosition;
            p.x = x;
            slideTarget.anchoredPosition = p;
            if (canvasGroup != null) canvasGroup.alpha = alpha;
        }
    }
}
