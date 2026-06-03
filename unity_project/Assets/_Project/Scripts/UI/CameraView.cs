using System;
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // CameraCommand, CameraKind, CompletionHandle, NarrativeFinishedEvent
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 카메라 FX 뷰(*View, M3 슬라이스2: CamZoom/CamPan/CamReset). <see cref="CameraCommand"/>를 구독해 _Stage
    /// 콘텐츠 래퍼의 localScale(줌)·anchoredPosition(팬)을 코루틴 lerp하고, 완료 시 핸들을 푼다(ADR-007: UI는 표시만).
    /// UI 무대엔 월드 카메라가 없으므로 "카메라"=콘텐츠 트랜스폼 조작(구 ScreenFX의 stageTransform DOScale/DOAnchorPos 동치).
    /// 슬라이스1처럼 DOTween 미사용. 줌/팬은 지속 상태(다음 명령/리셋까지 유지), 내러티브 종료 시 원점 복귀.
    ///
    /// 같은 콘텐츠 래퍼에 ShakeView(Stage)도 붙는다 — 흔들기는 일시 변위(끝나면 현재값 복귀), 카메라는 지속 변위라
    /// CSV가 둘을 순차(await) 배치하면 충돌하지 않는다(흔들기가 팬 위치를 rest로 잡고 그 자리로 복귀).
    /// </summary>
    public class CameraView : MonoBehaviour
    {
        [Tooltip("줌(localScale)·팬(anchoredPosition) 대상. 미지정 시 자기 자신.")]
        [SerializeField] RectTransform body;

        public RectTransform Body { get => body; set => body = value; }

        IDisposable _sub, _finishSub;
        Coroutine _routine;
        CompletionHandle _pending;

        void Awake()
        {
            if (body == null) body = transform as RectTransform;
        }

        void OnEnable()
        {
            _sub = EventBus.Subscribe<CameraCommand>(OnCommand);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetImmediate());
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose();
            _sub = _finishSub = null;
        }

        void OnCommand(CameraCommand e)
        {
            if (body == null) { e.Handle?.Complete(); return; }

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _pending?.Complete(); // 끊긴 이전 핸들이 엔진을 막지 않도록.
            }
            _pending = e.Handle;
            _routine = StartCoroutine(Run(e));
        }

        IEnumerator Run(CameraCommand e)
        {
            Vector3 fromScale = body.localScale;
            Vector2 fromPos = body.anchoredPosition;

            // 종류별 목표값. Reset = 줌·팬 동시 원점.
            Vector3 toScale = (e.Kind == CameraKind.Zoom) ? Vector3.one * e.ZoomScale
                            : (e.Kind == CameraKind.Reset) ? Vector3.one
                            : fromScale; // Pan은 스케일 유지
            Vector2 toPos = (e.Kind == CameraKind.Pan) ? new Vector2(e.PanX, e.PanY)
                          : (e.Kind == CameraKind.Reset) ? Vector2.zero
                          : fromPos;     // Zoom은 위치 유지

            float duration = e.Duration;
            if (duration <= 0f)
            {
                body.localScale = toScale;
                body.anchoredPosition = toPos;
                Finish();
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Ease(Mathf.Clamp01(t / duration), e.Kind);
                body.localScale = Vector3.LerpUnclamped(fromScale, toScale, k);
                body.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, k);
                yield return null;
            }
            body.localScale = toScale;
            body.anchoredPosition = toPos;
            Finish();
        }

        // 구 연출 이징 보존: 줌/팬=InOutCubic, 리셋=OutCubic.
        static float Ease(float x, CameraKind kind)
        {
            if (kind == CameraKind.Reset)
            {
                float inv = 1f - x;
                return 1f - inv * inv * inv; // OutCubic
            }
            return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) * 0.5f; // InOutCubic
        }

        void Finish()
        {
            var h = _pending;
            _pending = null;
            _routine = null;
            h?.Complete();
        }

        /// <summary>내러티브 종료 시 줌·팬을 즉시 원점으로(잔여 카메라 상태가 시뮬레이션으로 새지 않도록).</summary>
        void ResetImmediate()
        {
            if (_routine != null) { StopCoroutine(_routine); _pending?.Complete(); _pending = null; _routine = null; }
            if (body != null) { body.localScale = Vector3.one; body.anchoredPosition = Vector2.zero; }
        }
    }
}
