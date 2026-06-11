using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // SettingsSO
using LoveAlgo;        // GameConstants
using LoveAlgo.Events; // 설정 커맨드
using LoveAlgo.UI;     // ButtonSpriteSwap
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LoveAlgo.Settings
{
    /// <summary>
    /// 설정 팝업 뷰(*View, ADR-013 Overlay). <see cref="ShowSettingsCommand"/>로 표시, 컨트롤→커맨드 발행,
    /// <see cref="SettingsSO"/>를 읽어 초기화(ADR-007: 표시·발행만, 적용·영속은 SettingsController/오너).
    /// 볼륨·속도=라이브 발행, 해상도/전체화면=로컬 스테이징 후 Apply 버튼에 <see cref="ApplyDisplayCommand"/>.
    /// 버튼은 네이티브 Button+ColorTint+ButtonSpriteSwap(전체화면 토글=SetOn, 화살표 경계=SetInteractable).
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;

        [Header("볼륨/속도 (라이브)")]
        [SerializeField] Slider bgmSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Slider textSpeedSlider;
        [SerializeField] Slider autoSpeedSlider;

        [Header("화면 (스테이징 → 적용)")]
        [SerializeField] Button fullscreenButton;
        [SerializeField] Button windowButton;
        [SerializeField] Button leftArrow;
        [SerializeField] Button rightArrow;
        [SerializeField] Image resolutionImage;
        [Tooltip("GameConstants.Resolutions 순서의 해상도 라벨 스프라이트(lbl_config_res_*).")]
        [SerializeField] Sprite[] resolutionLabels;

        [Header("액션")]
        [SerializeField] Button applyButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button closeButton;

        [SerializeField] SettingsSO settings; // 미바인딩 시 Shared

        SettingsSO S => settings != null ? settings : (settings = SettingsSO.Shared);
        IDisposable _showSub;
        IDisposable _gate; // OverlayGate 토큰(표시 중에만 non-null — 뒤로가기 CloseTop이 닫기 호출)
        bool _wiring; // 슬라이더 초기화 중 onValueChanged 발행 가드
        bool _visible; // OverlayGate 토큰 전이 추적
        int _pendingResIndex;
        bool _pendingFullscreen;
        ButtonSpriteSwap _fsSwap, _winSwap, _leftSwap, _rightSwap;

        void Awake()
        {
            _fsSwap = Swap(fullscreenButton);
            _winSwap = Swap(windowButton);
            _leftSwap = Swap(leftArrow);
            _rightSwap = Swap(rightArrow);
            BindControls();
            SetVisible(false); // 부팅 숨김(프리팹 CanvasGroup alpha0과 정합)
        }

        void OnEnable() => _showSub = EventBus.Subscribe<ShowSettingsCommand>(_ => Show());
        void OnDisable()
        {
            _showSub?.Dispose();
            _showSub = null;
            _gate?.Dispose(); // 표시 중 파괴/비활성 시 게이트 누수 방지(중복 무해)
            _gate = null;
            _visible = false;
        }

        static ButtonSpriteSwap Swap(Button b) => b != null ? b.GetComponent<ButtonSpriteSwap>() : null;

        void BindControls()
        {
            BindSlider(bgmSlider, v => Publish(new SetVolumeCommand(AudioChannel.Bgm, v)));
            BindSlider(sfxSlider, v => Publish(new SetVolumeCommand(AudioChannel.Sfx, v)));
            BindSlider(textSpeedSlider, v => Publish(new SetTextSpeedCommand(v)));
            BindSlider(autoSpeedSlider, v => Publish(new SetAutoSpeedCommand(v)));
            Click(fullscreenButton, () => StageFullscreen(true));
            Click(windowButton, () => StageFullscreen(false));
            Click(leftArrow, () => StageResolution(_pendingResIndex - 1));
            Click(rightArrow, () => StageResolution(_pendingResIndex + 1));
            Click(applyButton, ApplyDisplay);
            Click(resetButton, ResetAll);
            Click(closeButton, () => SetVisible(false));
        }

        static void Click(Button b, UnityAction a) { if (b != null) b.onClick.AddListener(a); }
        void BindSlider(Slider s, Action<float> onChange)
        {
            if (s != null) s.onValueChanged.AddListener(v => { if (!_wiring) onChange(v); });
        }

        static void Publish<T>(T cmd) where T : struct => EventBus.Publish(cmd);

        /// <summary>설정값으로 컨트롤 갱신 후 표시.</summary>
        public void Show()
        {
            RefreshFromSettings();
            SetVisible(true);
        }

        void RefreshFromSettings()
        {
            var s = S;
            if (s == null) return;
            _wiring = true;
            if (bgmSlider != null) bgmSlider.value = s.BgmVolume;
            if (sfxSlider != null) sfxSlider.value = s.SfxVolume;
            if (textSpeedSlider != null) textSpeedSlider.value = s.TextSpeed;
            if (autoSpeedSlider != null) autoSpeedSlider.value = s.AutoSpeed;
            _wiring = false;
            _pendingResIndex = Mathf.Clamp(s.ResolutionIndex, 0, GameConstants.Resolutions.Length - 1);
            _pendingFullscreen = s.Fullscreen;
            RefreshDisplayVisual();
        }

        void StageFullscreen(bool fs) { _pendingFullscreen = fs; RefreshDisplayVisual(); }

        void StageResolution(int idx)
        {
            _pendingResIndex = Mathf.Clamp(idx, 0, GameConstants.Resolutions.Length - 1);
            RefreshDisplayVisual();
        }

        void RefreshDisplayVisual()
        {
            int max = GameConstants.Resolutions.Length - 1;
            if (resolutionImage != null && resolutionLabels != null && _pendingResIndex < resolutionLabels.Length)
                resolutionImage.sprite = resolutionLabels[_pendingResIndex];

            if (_leftSwap != null) _leftSwap.SetInteractable(_pendingResIndex > 0);
            else if (leftArrow != null) leftArrow.interactable = _pendingResIndex > 0;
            if (_rightSwap != null) _rightSwap.SetInteractable(_pendingResIndex < max);
            else if (rightArrow != null) rightArrow.interactable = _pendingResIndex < max;

            if (_fsSwap != null) _fsSwap.SetOn(_pendingFullscreen);
            if (_winSwap != null) _winSwap.SetOn(!_pendingFullscreen);
        }

        void ApplyDisplay() => Publish(new ApplyDisplayCommand(_pendingResIndex, _pendingFullscreen));

        void ResetAll()
        {
            Publish(new ResetSettingsCommand()); // 컨트롤러가 SettingsSO 리셋+재발행(EventBus 동기)
            RefreshFromSettings();               // 갱신된 SettingsSO로 컨트롤 갱신
        }

        void SetVisible(bool v)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = v ? 1f : 0f;
            canvasGroup.interactable = v;
            canvasGroup.blocksRaycasts = v;
            if (v)
            {
                // 재표시 포함 항상 시각·논리 최상단 동기화 — 형제 팝업 위로(SetAsLastSibling) +
                // 게이트 스택 맨 위로 재푸시(뒤로가기 CloseTop 대상 = 눈에 보이는 최상단 보장).
                transform.SetAsLastSibling();
                _gate?.Dispose();
                _gate = OverlayGate.Push(() => SetVisible(false));
                _visible = true;
            }
            else if (_visible)
            {
                _gate?.Dispose();
                _gate = null;
                _visible = false;
            }
        }
    }
}
