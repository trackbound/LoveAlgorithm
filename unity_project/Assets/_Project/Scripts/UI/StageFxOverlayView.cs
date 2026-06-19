using System;
using System.Collections;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Events; // PlayStageFxCommand, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 스테이지 투명 오버레이 FX 뷰(*View: StageFx). <see cref="PlayStageFxCommand"/>를 구독해
    /// Resources/Animation/{Name} 투명 클립을 캐릭터(_Stage, order -10) 위·대사 UI(order ≥0) 아래
    /// (전용 캔버스 order -5)에 알파 블렌딩으로 재생한다. 논블로킹 — 엔진이 발행 후 곧장 다음 줄로
    /// 진행하므로 이 뷰는 재생·자동종료만 담당(표정 명령이 효과 위로 인터리브). 자가구성(VideoView 패턴):
    /// Awake에서 Canvas+RawImage+VideoPlayer+RenderTexture 구성(인스펙터 바인딩 불필요, 씬엔 GO 하나만).
    /// 탭은 통과(raycastTarget=false, 스킵 아님) · 게임 오디오 무간섭(부가 효과). 클립 없거나 준비 실패 시
    /// 경고 후 즉시 종료(hang 0). 내러티브 종료/도구 화면정리 시 정리. RenderTexture는 OnDestroy에서 Release.
    /// </summary>
    public class StageFxOverlayView : MonoBehaviour
    {
        const string ResourceFolder = "Animation"; // Resources/Animation/{name}
        const int SortingOrder = -5;               // _Stage(-10) 위, 모든 UI(≥0) 아래
        const float PrepareTimeout = 8f;           // 준비 상한(코덱 불량/누락 시 무한대기 방지)

        Canvas _canvas;
        RawImage _image;
        VideoPlayer _player;
        RenderTexture _rt;

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        bool _reachedEnd;

        void Awake()
        {
            BuildHierarchy();
            HideImmediate();
        }

        void OnEnable()
        {
            _sub = EventBus.Subscribe<PlayStageFxCommand>(OnPlay);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetView());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetView());
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
        }

        // ── 자가 구성(Awake 1회) ──
        void BuildHierarchy()
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            if (gameObject.GetComponent<CanvasScaler>() == null)
                gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var imgGo = new GameObject("StageFxImage");
            imgGo.transform.SetParent(transform, false);
            var rect = imgGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _image = imgGo.AddComponent<RawImage>();
            _image.color = Color.white;
            _image.raycastTarget = false; // 탭 통과(대사 진행 막지 않음 — 스킵 아님)
            _image.enabled = false;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.audioOutputMode = VideoAudioOutputMode.None; // 부가 효과 — 게임 오디오 무간섭(소스도 무음)
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.waitForFirstFrame = true;
            _player.skipOnDrop = true;
            _player.loopPointReached += OnLoopPointReached;
        }

        // ── 재생 ──
        void OnPlay(PlayStageFxCommand e)
        {
            if (_player == null) return; // 방어(Awake 전 발행)

            var clip = Resources.Load<VideoClip>($"{ResourceFolder}/{e.Name}");
            if (clip == null)
            {
                Log.Warn($"[StageFxOverlayView] 효과 없음: Resources/{ResourceFolder}/{e.Name} — 건너뜀");
                return; // 논블로킹 — 엔진은 이미 다음 줄로 진행
            }

            if (_routine != null) { StopCoroutine(_routine); _routine = null; }

            _player.Stop();
            EnsureRenderTexture((int)clip.width, (int)clip.height);
            _player.clip = clip;
            _player.isLooping = e.Loop;
            _player.targetTexture = _rt;
            _image.texture = _rt;

            _reachedEnd = false;
            _routine = StartCoroutine(Run(e));
        }

        IEnumerator Run(PlayStageFxCommand e)
        {
            _player.Prepare();
            float t = 0f;
            while (!_player.isPrepared && t < PrepareTimeout) { t += Time.unscaledDeltaTime; yield return null; }
            if (!_player.isPrepared)
            {
                Log.Warn($"[StageFxOverlayView] 효과 준비 시간초과({PrepareTimeout}s) — 건너뜀: {e.Name}");
                HideImmediate();
                _routine = null;
                yield break;
            }

            _image.enabled = true;
            _player.Play();

            if (e.Loop) { _routine = null; yield break; } // 루프는 Reset(종료/도구 정리)까지 배경 유지

            while (!_reachedEnd) yield return null;
            HideImmediate();
            _routine = null;
        }

        void OnLoopPointReached(VideoPlayer _) => _reachedEnd = true;

        // 내러티브 종료/도구 화면정리 — 진행 중이면 끊고 정리.
        void ResetView()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_player != null) _player.Stop();
            HideImmediate();
        }

        void HideImmediate()
        {
            if (_image != null) _image.enabled = false;
            if (_player != null) _player.targetTexture = null;
        }

        void EnsureRenderTexture(int w, int h)
        {
            if (w <= 0) w = 1;
            if (h <= 0) h = 1;
            if (_rt != null && _rt.width == w && _rt.height == h) return;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            _rt.Create();
        }

        void OnDestroy()
        {
            if (_player != null) _player.loopPointReached -= OnLoopPointReached;
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        }
    }
}
