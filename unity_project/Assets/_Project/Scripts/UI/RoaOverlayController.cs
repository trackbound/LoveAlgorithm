using System;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Events; // ShowCharacterCommand, ShowSpeakerEmoteCommand, SetRoaDeviceCommand, ShowStageLayerCommand, StageLayerKind, LayerTransition, CharAction, CompletionHandle, NarrativeFinishedEvent, ResetNarrativeViewsCommand, RoaDevice
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로아(Roa) 전용 오버레이 자동 결합 컨트롤러(뷰). 로아가 중앙 슬롯에 등장/표정변경/퇴장할 때 디바이스
    /// (pc/모바일)×감정 카테고리(기본/긍정/부정)에 맞는 Overlay 레이어를 자동으로 띄우고/전환/내린다.
    /// ShowCharacterCommand·인라인 ShowSpeakerEmoteCommand·SetRoaDeviceCommand를 구독해, 기존
    /// ShowStageLayerCommand(Overlay)로 StageLayerView를 구동한다(ADR-007: State 읽기/쓰기 없는 순수 뷰).
    /// 디바이스는 항상 명령으로 받으며, 세이브 복원도 GameBootstrap이 SetRoaDeviceCommand→Char Enter 순으로
    /// 재발행해 자동 재구성된다.
    /// </summary>
    public class RoaOverlayController : MonoBehaviour
    {
        [Tooltip("로아 오버레이 규칙 SO. 미바인딩 시 configResourcePath에서 로드.")]
        [SerializeField] RoaOverlaySO config;
        [Tooltip("config 미바인딩 시 Resources에서 찾을 경로.")]
        [SerializeField] string configResourcePath = "Data/RoaOverlay";
        [Tooltip("오버레이 표시/전환/종료 페이드 시간(초).")]
        [SerializeField] float fadeSeconds = 0.25f;

        public RoaOverlaySO Config { get => config; set => config = value; }
        public float FadeSeconds { get => fadeSeconds; set => fadeSeconds = value; }

        IDisposable _charSub, _emoteSub, _deviceSub, _finishSub, _resetSub;
        RoaDevice _device;
        RoaOverlaySO.Category _category;
        bool _present;
        CharSlot _slot; // 로아가 등장한 슬롯 — Exit/Clear는 캐릭터 id를 싣지 않으므로 슬롯으로 퇴장을 판별한다.

        void OnEnable()
        {
            if (config == null && !string.IsNullOrEmpty(configResourcePath))
                config = Resources.Load<RoaOverlaySO>(configResourcePath);

            _device = config != null ? config.DefaultDevice : RoaDevice.Pc;
            _category = RoaOverlaySO.Category.Default;
            _present = false;

            _charSub = EventBus.Subscribe<ShowCharacterCommand>(OnChar);
            _emoteSub = EventBus.Subscribe<ShowSpeakerEmoteCommand>(OnSpeakerEmote);
            _deviceSub = EventBus.Subscribe<SetRoaDeviceCommand>(OnDevice);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetState());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetState());
        }

        void OnDisable()
        {
            _charSub?.Dispose(); _emoteSub?.Dispose(); _deviceSub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _charSub = _emoteSub = _deviceSub = _finishSub = _resetSub = null;
        }

        bool IsRoa(string id) =>
            config != null && !string.IsNullOrEmpty(id)
            && string.Equals(id.Trim(), config.RoaCharId, StringComparison.OrdinalIgnoreCase);

        void OnDevice(SetRoaDeviceCommand e)
        {
            _device = e.Device;
            if (_present) ShowOverlay(); // 같은 카테고리, 새 디바이스
        }

        void OnChar(ShowCharacterCommand e)
        {
            if (config == null) return;
            switch (e.Action)
            {
                case CharAction.Enter:
                    if (!IsRoa(e.Character)) return;
                    _slot = e.Slot;
                    _category = config.ResolveCategory(e.Emote);
                    _present = true;
                    ShowOverlay();
                    break;
                case CharAction.Emote:
                    if (!IsRoa(e.Character)) return;
                    ApplyCategory(config.ResolveCategory(e.Emote));
                    break;
                case CharAction.Exit:
                case CharAction.Clear:
                    // 엔진은 바 `C:Exit`/`Clear`를 캐릭터 id 없이 발행한다(StageParser). id로 판별하면
                    // 로아 퇴장을 놓쳐 오버레이가 다음 씬까지 잔존하므로(학교에서 다은 등장 시 잔존 버그),
                    // 로아가 등장해 있던 슬롯이 비워질 때 내린다.
                    if (_present && e.Slot == _slot) { _present = false; HideOverlay(); }
                    break;
            }
        }

        void OnSpeakerEmote(ShowSpeakerEmoteCommand e)
        {
            if (config == null || !_present || !IsRoa(e.Speaker) || string.IsNullOrEmpty(e.Emote)) return;
            ApplyCategory(config.ResolveCategory(e.Emote));
        }

        void ApplyCategory(RoaOverlaySO.Category cat)
        {
            if (!_present || cat == _category) return;
            _category = cat;
            ShowOverlay();
        }

        void ShowOverlay()
        {
            string name = config.OverlayName(_device, _category);
            EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.Overlay, false, name, LayerTransition.Fade, fadeSeconds, new CompletionHandle()));
        }

        void HideOverlay()
        {
            EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.Overlay, true, null, LayerTransition.Fade, fadeSeconds, new CompletionHandle()));
        }

        // 내러티브 종료/도구 리셋: StageLayerView가 오버레이 이미지를 ClearAll하므로 여기선 런타임 상태만 비운다.
        // 디바이스는 다음 SetRoaDeviceCommand까지 유지(잔여 무해 — 로아 부재 시 아무 것도 그리지 않음).
        void ResetState()
        {
            _present = false;
            _category = RoaOverlaySO.Category.Default;
        }
    }
}
