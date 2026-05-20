using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 화면 전체 덮는 영상 재생. 대화창·HUD 위에 떠서 컷씬·인트로 연출용.
    /// CSV: <c>FX,,Video:파일명[:Loop][:Skippable],await</c>
    /// 리소스: <c>Resources/Animation/{name}</c>
    ///
    /// 자동 인스턴스화 — 처음 호출 시 GameObject + Canvas + RawImage + VideoPlayer 생성.
    /// </summary>
    public class VideoLayer : SingletonMonoBehaviour<VideoLayer>
    {
        const string ResourceFolder = "Animation";
        const int SortingOrderTop = 32000; // 다른 캔버스 위

        Canvas canvas;
        RawImage image;
        VideoPlayer player;
        RenderTexture rt;

        bool isPlaying;
        bool skipRequested;

        protected override void OnSingletonAwake()
        {
            BuildHierarchy();
            DontDestroyOnLoad(gameObject);
            HideImmediate();
        }

        // ─── 자동 생성 ─────────────────────────────────
        /// <summary>씬에 인스턴스가 없으면 자동 생성한다.</summary>
        public static VideoLayer EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[VideoLayer]");
            return go.AddComponent<VideoLayer>();
        }

        void BuildHierarchy()
        {
            // Canvas — 최상위 sort order, ScreenSpaceOverlay
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrderTop;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            gameObject.AddComponent<GraphicRaycaster>();

            // 화면 전체 RawImage
            var imgGo = new GameObject("Image");
            imgGo.transform.SetParent(transform, false);
            var rt = imgGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            image = imgGo.AddComponent<RawImage>();
            image.color = Color.white;
            image.enabled = false;

            // VideoPlayer — RenderTexture로 출력
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.waitForFirstFrame = true;
        }

        // ─── 재생 API ──────────────────────────────────
        /// <summary>지정 클립 재생 후 종료까지 대기. loop=true면 영원히 재생되니 Stop으로 끊을 것.</summary>
        public async UniTask PlayAsync(string clipName, bool loop = false, bool skippable = true, CancellationToken ct = default)
        {
            var clip = Resources.Load<VideoClip>($"{ResourceFolder}/{clipName}");
            if (clip == null)
            {
                Debug.LogWarning($"[VideoLayer] 영상 없음: Resources/{ResourceFolder}/{clipName}");
                return;
            }

            // RenderTexture (클립 해상도)
            EnsureRenderTexture((int)clip.width, (int)clip.height);
            player.clip = clip;
            player.isLooping = loop;
            player.targetTexture = rt;
            image.texture = rt;
            image.enabled = true;
            isPlaying = true;
            skipRequested = false;

            player.Prepare();
            await UniTask.WaitUntil(() => player.isPrepared, cancellationToken: ct);
            player.Play();

            if (loop) return; // 호출자가 Stop으로 끊어야 함

            // 종료 대기 + 스킵 가능
            while (player.isPlaying && !ct.IsCancellationRequested)
            {
                if (skippable && skipRequested) break;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            Stop();
        }

        /// <summary>외부에서 영상 스킵 요청 (Skippable=true일 때만 반영).</summary>
        public void RequestSkip() => skipRequested = true;

        public void Stop()
        {
            if (player != null && player.isPlaying) player.Stop();
            HideImmediate();
        }

        public void HideImmediate()
        {
            isPlaying = false;
            skipRequested = false;
            if (image != null) image.enabled = false;
            if (player != null) player.targetTexture = null;
        }

        void EnsureRenderTexture(int w, int h)
        {
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) rt.Release();
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            rt.Create();
        }

        protected override void OnDestroy()
        {
            if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
            base.OnDestroy();
        }

        public bool IsPlaying => isPlaying;
    }
}
