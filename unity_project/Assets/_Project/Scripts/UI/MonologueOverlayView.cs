using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // SetMonologueOverlayCommand, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 독백 오버레이 뷰(*View). <see cref="SetMonologueOverlayCommand"/>를 구독해 전용 오버레이 Image의 알파를
    /// 코루틴 lerp로 페이드 인/아웃한다(ADR-007: UI는 표시만, 엔진은 이 뷰를 모름). 화자 빈 Text 라인(독백)에서
    /// 켜고 화자 있는 대사에서 끈다 — 판정은 엔진(<c>NarrativeController.PlayText</c>의 <c>line.IsNarration</c>)이 하고
    /// 뷰는 bool만 받는다(<see cref="SetDialogueVisibleCommand"/>/<see cref="SetCgModeCommand"/> 토글 패턴).
    ///
    /// 로아 전용 오버레이(<see cref="StageLayerKind"/>.Overlay / <c>StageLayerView</c>)와 별개의 상위 레이어다:
    /// 트리거가 자동(빈 화자)이고 z가 캐릭터·로아 오버레이보다 위. "고정 1종" — 비주얼(스프라이트/색)은
    /// 인스펙터에서 감독이 지정하고, 뷰는 알파만 0↔<see cref="shownAlpha"/>로 토글한다. 입력은 막지 않는다
    /// (독백 텍스트 진행은 위 <c>_UI</c> 대사창에서). 내러티브 종료/도구 리셋 시 즉시 해제.
    /// </summary>
    public class MonologueOverlayView : MonoBehaviour
    {
        [Tooltip("독백 오버레이 Image(고정 1종). 비주얼은 인스펙터에서 지정. 미바인딩 시 무동작.")]
        [SerializeField] Image overlay;
        [Tooltip("표시 상태일 때 목표 알파(0~1). 딤 농도는 이 값 또는 스프라이트 자체로 조절.")]
        [Range(0f, 1f)]
        [SerializeField] float shownAlpha = 1f;
        [Tooltip("페이드 인/아웃 시간(초). 0이면 즉시 토글.")]
        [SerializeField] float fadeDuration = 0.3f;

        public Image Overlay { get => overlay; set => overlay = value; }
        public float ShownAlpha { get => shownAlpha; set => shownAlpha = value; }
        public float FadeDuration { get => fadeDuration; set => fadeDuration = value; }

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        bool _shown;

        void OnEnable()
        {
            _sub = EventBus.Subscribe<SetMonologueOverlayCommand>(OnSet);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetOverlay());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetOverlay()); // 도구 화면 정리
            if (overlay != null)
            {
                overlay.raycastTarget = false; // 입력을 막지 않음(독백 텍스트는 위 _UI 대사창에서 진행).
                SetAlpha(0f);
                overlay.enabled = false;
            }
            _shown = false;
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
        }

        void OnSet(SetMonologueOverlayCommand e)
        {
            if (overlay == null) return;
            // 이미 목표 상태이고 진행 중인 페이드가 없으면 중복 페이드 생략(연속 독백 라인마다 재토글 방지).
            if (e.Active == _shown && _routine == null) return;
            _shown = e.Active;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Fade(e.Active ? shownAlpha : 0f));
        }

        IEnumerator Fade(float target)
        {
            overlay.enabled = true;
            float from = overlay.color.a;
            if (fadeDuration <= 0f)
            {
                SetAlpha(target);
            }
            else
            {
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    SetAlpha(Mathf.Lerp(from, target, Mathf.Clamp01(t / fadeDuration)));
                    yield return null;
                }
                SetAlpha(target);
            }
            if (target <= 0f) overlay.enabled = false; // 숨김 완료 시 비활성.
            _routine = null;
        }

        /// <summary>내러티브 종료/도구 리셋 시 잔여 오버레이가 시뮬레이션으로 새지 않도록 즉시 해제.</summary>
        void ResetOverlay()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _shown = false;
            if (overlay != null) { SetAlpha(0f); overlay.enabled = false; }
        }

        void SetAlpha(float a)
        {
            if (overlay == null) return;
            var c = overlay.color;
            c.a = a;
            overlay.color = c;
        }
    }
}
