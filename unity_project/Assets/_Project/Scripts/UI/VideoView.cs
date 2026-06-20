using System;
using System.Collections;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Events; // PlayVideoCommand, CompletionHandle, NarrativeFinishedEvent, ResetNarrativeViewsCommand
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 풀스크린 영상 뷰(*View: Video). <see cref="PlayVideoCommand"/>를 구독해 Resources/Animation/{Name} 클립을
    /// 화면 전체(최상위 캔버스 order 32000)로 재생한다(ADR-007: UI는 표시만, 해석은 엔진). 자가완결 —
    /// Awake에서 전용 Canvas+RawImage+VideoPlayer+RenderTexture를 코드로 구성(인스펙터 바인딩 불필요, 씬엔 GO 하나만).
    ///
    /// 안정성: Prepare()→isPrepared 대기 후 Play()(검은 프레임/히치 방지) · waitForFirstFrame · loopPointReached로
    /// 종료 감지(isPlaying 폴링 시작-프레임 레이스 회피) · 준비 타임아웃 가드(코덱 불량/누락 시 hang 방지) ·
    /// 클립 없으면 즉시 핸들 완료. 비-Loop=종료까지 핸들 보류(엔진 await) · Loop=핸들 즉시 완료(비블로킹).
    /// Skippable이면 화면 클릭으로 스킵. 내러티브 종료/도구 화면정리 시 즉시 정리. RenderTexture는 OnDestroy에서 Release.
    /// </summary>
    public class VideoView : MonoBehaviour, IPointerClickHandler
    {
        const string ResourceFolder = "Animation"; // Resources/Animation/{name} (동결 컨벤션)
        const int SortingOrderTop = 32000;         // 모든 캔버스 위(동결: 구 VideoLayer)
        const float PrepareTimeout = 8f;           // 준비 상한(코덱 불량/누락 시 무한대기 방지)
        const float SkipGrace = 0.3f;              // 시작 직후 이 시간은 클릭 스킵 무시(실수 즉시 스킵 방지)
        const float EndFadeOut = 0.5f;             // 자연 종료 시 마지막 프레임 페이드 아웃(다음 CG와 크로스페이드)

        Canvas _canvas;
        RawImage _image;
        VideoPlayer _player;
        RenderTexture _rt;

        IDisposable _sub, _finishSub, _resetSub;
        Coroutine _routine;
        CompletionHandle _pending;
        bool _reachedEnd;
        bool _skipRequested;
        bool _pausedAudio; // 우리가 게임 오디오를 멈췄는지(복원 시 남의 일시정지 안 건드리도록)

        public bool IsPlaying => _image != null && _image.enabled;

        void Awake()
        {
            BuildHierarchy();
            HideImmediate();
        }

        void OnEnable()
        {
            _sub = EventBus.Subscribe<PlayVideoCommand>(OnPlay);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetView());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetView()); // 도구 화면 정리
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
            SetGameAudioPaused(false); // 재생 중 비활성화돼도 게임 오디오가 묶이지 않도록 안전 복원.
        }

        // ── 자가 구성(Awake 1회) ──
        void BuildHierarchy()
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrderTop;
            if (gameObject.GetComponent<CanvasScaler>() == null)
                gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            var imgGo = new GameObject("VideoImage");
            imgGo.transform.SetParent(transform, false);
            var rect = imgGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _image = imgGo.AddComponent<RawImage>();
            _image.color = Color.white;
            _image.raycastTarget = true; // 클릭 스킵 수신(부모 VideoView로 버블)
            _image.enabled = false;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.waitForFirstFrame = true;
            _player.skipOnDrop = true;
            _player.loopPointReached += OnLoopPointReached;
        }

        // ── 재생 ──
        void OnPlay(PlayVideoCommand e)
        {
            if (_player == null) { e.Handle?.Complete(); return; } // 방어(Awake 전 발행)

            var clip = Resources.Load<VideoClip>($"{ResourceFolder}/{e.Name}");
            if (clip == null)
            {
                Log.Warn($"[VideoView] 영상 없음: Resources/{ResourceFolder}/{e.Name} — 건너뜀");
                e.Handle?.Complete();
                return;
            }

            // 이전 재생 정리 — 끊긴 핸들이 엔진을 막지 않도록 먼저 완료.
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _pending?.Complete();
            _pending = e.Handle;

            _player.Stop();
            EnsureRenderTexture((int)clip.width, (int)clip.height);
            _player.clip = clip;
            _player.isLooping = e.Loop;
            _player.targetTexture = _rt;
            _image.texture = _rt;
            SetImageAlpha(1f); // 직전 종료 페이드가 중단돼 알파가 남아있을 수 있으니 원복.

            _reachedEnd = false;
            _skipRequested = false;
            _routine = StartCoroutine(Run(e));
        }

        IEnumerator Run(PlayVideoCommand e)
        {
            _player.Prepare();
            float t = 0f;
            while (!_player.isPrepared && t < PrepareTimeout) { t += Time.unscaledDeltaTime; yield return null; }
            if (!_player.isPrepared)
            {
                Log.Warn($"[VideoView] 영상 준비 시간초과({PrepareTimeout}s) — 건너뜀: {e.Name}");
                FinishAndHide();
                yield break;
            }

            _image.enabled = true;
            _player.Play();

            if (e.Loop)
            {
                // 무한 재생이라 블로킹 불가 — 핸들 즉시 완료, 영상은 Reset(종료/도구 정리)까지 배경 유지.
                // 배경 루프는 게임 오디오를 끄지 않는다(컷씬만 격리).
                var h = _pending; _pending = null; _routine = null; h?.Complete();
                yield break;
            }

            // 컷씬(non-loop): 영상 동안 게임 오디오 뮤트(영상 자체 사운드는 Direct 출력이라 유지). 종료 시 복원.
            SetGameAudioPaused(true);

            // 종료(loopPointReached) 또는 (Skippable일 때) 클릭 스킵까지 대기.
            // 시작 직후 SkipGrace 동안은 스킵 무시 — 대사 넘기던 클릭이 영상을 즉시 스킵하는 실수 방지.
            float played = 0f;
            while (!_reachedEnd)
            {
                if (e.Skippable && _skipRequested && played >= SkipGrace) break;
                played += Time.unscaledDeltaTime;
                yield return null;
            }

            // 자연 종료: 마지막 프레임을 유지한 채 알파만 페이드 아웃 → 동일 그림의 다음 CG(아래 레이어)가
            // 드러나며 부드럽게 이어진다(빈 배경 깜빡임 제거). 스킵/타임아웃은 응답성 위해 즉시 숨김.
            if (_reachedEnd)
                yield return FadeOutAndFinish();
            else
                FinishAndHide();
        }

        // 자연 종료 전용 크로스페이드: 핸들을 먼저 완료해 다음 CG를 영상 아래에 깔고(같은 그림),
        // 그 위의 영상 알파를 0으로 낮춰 동일 이미지가 드러나게 한다. 페이드 끝에 정지/숨김 + 알파 원복.
        IEnumerator FadeOutAndFinish()
        {
            SetGameAudioPaused(false);                          // 영상 사운드 종료 — 게임 오디오 복원.
            var h = _pending; _pending = null; h?.Complete();  // 내러티브 진행(CG가 아래 레이어로 등장).

            float t = 0f;
            while (t < EndFadeOut)
            {
                t += Time.unscaledDeltaTime;
                SetImageAlpha(1f - Mathf.Clamp01(t / EndFadeOut));
                yield return null;
            }

            _routine = null;
            if (_player != null) _player.Stop();
            HideImmediate();
            SetImageAlpha(1f); // 다음 재생 위해 알파 원복.
        }

        void SetImageAlpha(float a)
        {
            if (_image == null) return;
            var c = _image.color; c.a = a; _image.color = c;
        }

        void OnLoopPointReached(VideoPlayer _) => _reachedEnd = true;

        public void OnPointerClick(PointerEventData _) => _skipRequested = true;

        // 재생 종료 정리 + 보류 핸들 완료(정상 종료/스킵/타임아웃 공통).
        void FinishAndHide()
        {
            _routine = null;
            SetGameAudioPaused(false);
            if (_player != null) _player.Stop();
            HideImmediate();
            var h = _pending; _pending = null; h?.Complete();
        }

        // 내러티브 종료/도구 화면정리 — 진행 중이면 끊고 핸들을 풀어 엔진 hang 방지.
        void ResetView()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            SetGameAudioPaused(false);
            if (_player != null) _player.Stop();
            HideImmediate();
            var h = _pending; _pending = null; h?.Complete();
        }

        // 컷씬 영상 동안 게임 오디오(BGM/SFX) 일시정지 — 영상 Direct 사운드는 영향 없음.
        // 우리가 멈춘 경우만 복원해 다른 시스템의 일시정지를 덮지 않는다.
        void SetGameAudioPaused(bool paused)
        {
            if (paused) { AudioListener.pause = true; _pausedAudio = true; }
            else if (_pausedAudio) { AudioListener.pause = false; _pausedAudio = false; }
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
            SetGameAudioPaused(false);
            if (_player != null) _player.loopPointReached -= OnLoopPointReached;
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        }
    }
}
