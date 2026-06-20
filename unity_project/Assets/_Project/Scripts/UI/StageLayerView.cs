using System;
using System.Collections;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Events; // ShowStageLayerCommand, StageLayerKind, LayerTransition, SetCgModeCommand, CompletionHandle, NarrativeFinishedEvent
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 스테이지 이미지 레이어 뷰(*View, M3 슬라이스2: CG/SD/Overlay). <see cref="ShowStageLayerCommand"/>를 구독해
    /// 종류별 Image의 스프라이트를 컨벤션 로딩(<c>CG/SD/Overlay + "/" + name</c>)하고 알파를 코루틴 lerp(Cut=즉시),
    /// 완료 시 핸들을 푼다(ADR-007: UI는 표시만). 셋은 독립이라 종류별 코루틴/핸들 슬롯을 따로 둔다.
    ///
    /// CG는 전체화면 컷신이라 진입 시 <see cref="SetCgModeCommand"/>(true)를 발행해 대사창·캐릭터를 숨기고 종료 시 복원
    /// (DialogueView/StageView가 구독). z-위치는 씬 배선으로 정한다: Overlay=배경 위·캐릭터 아래, SD=캐릭터 위, CG=최상.
    /// </summary>
    public class StageLayerView : MonoBehaviour
    {
        [Tooltip("CG(전체화면 컷신) 이미지.")]
        [SerializeField] Image cgImage;
        [Tooltip("SD(치비 부분) 이미지.")]
        [SerializeField] Image sdImage;
        [Tooltip("Overlay(무드 보조배경) 이미지.")]
        [SerializeField] Image overlayImage;
        [Tooltip("LayerTransition.Glitch 전환에 쓰는 UI 글리치 머티리얼(LoveAlgo/UIGlitch). 미바인딩 시 일반 Fade로 폴백.")]
        [SerializeField] Material glitchMaterial;

        public Image CgImage { get => cgImage; set => cgImage = value; }
        public Image SdImage { get => sdImage; set => sdImage = value; }
        public Image OverlayImage { get => overlayImage; set => overlayImage = value; }
        public Material GlitchMaterial { get => glitchMaterial; set => glitchMaterial = value; }

        static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");

        IDisposable _sub, _finishSub, _resetSub;
        readonly Coroutine[] _routines = new Coroutine[3];
        readonly CompletionHandle[] _pending = new CompletionHandle[3];
        readonly Material[] _baseMat = new Material[3];   // 글리치 전 원본 머티리얼(보통 기본 UI) — 종료 시 복원.
        readonly Material[] _glitchInst = new Material[3]; // 종류별 글리치 머티리얼 런타임 인스턴스(공유 에셋 변형 방지).

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ShowStageLayerCommand>(OnShow);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ClearAll());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ClearAll()); // 도구 화면 정리
            _baseMat[(int)StageLayerKind.CG] = cgImage != null ? cgImage.material : null;
            _baseMat[(int)StageLayerKind.SD] = sdImage != null ? sdImage.material : null;
            _baseMat[(int)StageLayerKind.Overlay] = overlayImage != null ? overlayImage.material : null;
            InitImage(cgImage);
            InitImage(sdImage);
            InitImage(overlayImage);
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
            for (int i = 0; i < _glitchInst.Length; i++)
            {
                if (_glitchInst[i] != null) { Destroy(_glitchInst[i]); _glitchInst[i] = null; }
            }
        }

        Image ImageFor(StageLayerKind kind)
        {
            switch (kind)
            {
                case StageLayerKind.SD:      return sdImage;
                case StageLayerKind.Overlay: return overlayImage;
                default:                     return cgImage;
            }
        }

        static string FolderFor(StageLayerKind kind)
        {
            switch (kind)
            {
                case StageLayerKind.SD:      return "SD";
                case StageLayerKind.Overlay: return "Overlay";
                default:                     return "CG";
            }
        }

        void OnShow(ShowStageLayerCommand e)
        {
            int i = (int)e.Kind;
            var img = ImageFor(e.Kind);
            if (img == null) { e.Handle?.Complete(); return; }

            if (_routines[i] != null)
            {
                StopCoroutine(_routines[i]);
                _pending[i]?.Complete(); // 끊긴 이전 핸들이 엔진을 막지 않도록.
            }
            _pending[i] = e.Handle;
            _routines[i] = StartCoroutine(Run(e, img, i));
        }

        IEnumerator Run(ShowStageLayerCommand e, Image img, int i)
        {
            if (e.IsClose)
            {
                yield return RunTransition(img, i, img.color.a, 0f, e.Transition, e.Duration, true);
                img.enabled = false;
                if (e.Kind == StageLayerKind.CG) EventBus.Publish(new SetCgModeCommand(false));
            }
            else
            {
                var sprite = Resources.Load<Sprite>($"{FolderFor(e.Kind)}/{e.Name}");
                if (sprite != null) img.sprite = sprite;
                else Log.Warn($"[StageLayerView] 스프라이트 없음: {FolderFor(e.Kind)}/{e.Name}");

                SetAlpha(img, 0f);
                img.enabled = true;
                if (e.Kind == StageLayerKind.CG)
                {
                    // CG 진입 = 오토모드 정지(인벤토리 §CG). CG 모드 발행보다 먼저 — 대사창 root 하위
                    // 구독자(인포 바)가 꺼지기 전에 받도록.
                    EventBus.Publish(new SetAutoModeCommand(false));
                    EventBus.Publish(new SetCgModeCommand(true));
                }
                yield return RunTransition(img, i, 0f, 1f, e.Transition, e.Duration, false);
            }

            Finish(i);
        }

        // 전환 디스패치: Glitch는 글리치 머티리얼 구동, 그 외(Cut/Fade)는 알파 lerp.
        // 글리치 머티리얼 미바인딩/0초면 일반 Fade로 폴백(안전).
        IEnumerator RunTransition(Image img, int i, float from, float to, LayerTransition transition, float duration, bool isClose)
        {
            if (transition == LayerTransition.Glitch && glitchMaterial != null && duration > 0f)
            {
                yield return GlitchFade(img, i, from, to, duration, isClose);
                yield break;
            }
            // 비-글리치 전환: 직전 글리치가 중단돼 인스턴스가 남아있을 수 있으니 원본 머티리얼로 복원.
            if (img.material != _baseMat[i]) img.material = _baseMat[i];
            yield return Fade(img, from, to, transition, duration);
        }

        // 글리치 전환: 종류별 머티리얼 인스턴스를 입히고 _GlitchAmount를 broken→clean(Enter) 또는
        // clean→broken(Close)으로 구동하며 알파를 동반 lerp, 종료 시 원본 머티리얼 복원.
        IEnumerator GlitchFade(Image img, int i, float from, float to, float duration, bool isClose)
        {
            var inst = _glitchInst[i];
            if (inst == null) { inst = new Material(glitchMaterial); _glitchInst[i] = inst; }
            img.material = inst;

            float gFrom = isClose ? 0f : 1f;
            float gTo = isClose ? 1f : 0f;

            float t = 0f;
            SetAlpha(img, from);
            inst.SetFloat(GlitchAmountId, gFrom);
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                // 알파는 빠르게(60%에 도달) → 등장은 즉시 보이고, 글리치는 전체 구간에 걸쳐 풀려 "나타난 뒤 안정화".
                // Enter: 0~0.6 구간에 알파 상승. Close: 0.4~1 구간에 알파 하강(붕괴되며 뒤늦게 사라짐).
                float kA = isClose ? Mathf.Clamp01((k - 0.4f) / 0.6f) : Mathf.Clamp01(k / 0.6f);
                SetAlpha(img, Mathf.Lerp(from, to, kA));
                inst.SetFloat(GlitchAmountId, Mathf.Lerp(gFrom, gTo, k));
                yield return null;
            }
            SetAlpha(img, to);
            inst.SetFloat(GlitchAmountId, gTo);

            img.material = _baseMat[i]; // 정상 상태는 글리치 없음 — 기본 머티리얼로 복귀.
        }

        IEnumerator Fade(Image img, float from, float to, LayerTransition transition, float duration)
        {
            if (transition == LayerTransition.Cut || duration <= 0f)
            {
                SetAlpha(img, to);
                yield break;
            }
            float t = 0f;
            SetAlpha(img, from);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAlpha(img, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAlpha(img, to);
        }

        void Finish(int i)
        {
            var h = _pending[i];
            _pending[i] = null;
            _routines[i] = null;
            h?.Complete();
        }

        /// <summary>내러티브 종료 시 모든 레이어 숨김 + CG 모드 해제(잔여 컷신이 시뮬레이션으로 새지 않도록).</summary>
        void ClearAll()
        {
            for (int i = 0; i < _routines.Length; i++)
            {
                if (_routines[i] != null) { StopCoroutine(_routines[i]); _pending[i]?.Complete(); _pending[i] = null; _routines[i] = null; }
                var img = ImageFor((StageLayerKind)i);
                if (img != null && img.material != _baseMat[i]) img.material = _baseMat[i]; // 글리치 머티리얼 잔존 방지.
            }
            InitImage(cgImage);
            InitImage(sdImage);
            InitImage(overlayImage);
            EventBus.Publish(new SetCgModeCommand(false));
        }

        void InitImage(Image img)
        {
            if (img == null) return;
            img.raycastTarget = false;
            SetAlpha(img, 0f);
            img.enabled = false;
        }

        static void SetAlpha(Image img, float a)
        {
            if (img == null) return;
            var c = img.color;
            c.a = a;
            img.color = c;
        }
    }
}
