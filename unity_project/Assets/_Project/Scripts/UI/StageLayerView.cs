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

        public Image CgImage { get => cgImage; set => cgImage = value; }
        public Image SdImage { get => sdImage; set => sdImage = value; }
        public Image OverlayImage { get => overlayImage; set => overlayImage = value; }

        IDisposable _sub, _finishSub, _resetSub;
        readonly Coroutine[] _routines = new Coroutine[3];
        readonly CompletionHandle[] _pending = new CompletionHandle[3];

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ShowStageLayerCommand>(OnShow);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ClearAll());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ClearAll()); // 도구 화면 정리
            InitImage(cgImage);
            InitImage(sdImage);
            InitImage(overlayImage);
        }

        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _sub = _finishSub = _resetSub = null;
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
                yield return Fade(img, img.color.a, 0f, e.Transition, e.Duration);
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
                if (e.Kind == StageLayerKind.CG) EventBus.Publish(new SetCgModeCommand(true));
                yield return Fade(img, 0f, 1f, e.Transition, e.Duration);
            }

            Finish(i);
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
