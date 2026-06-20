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
        [Tooltip("있으면 우→좌 슬라이드 인 / 좌←우 아웃 연출, 없으면 즉시 표시.")]
        [SerializeField] PopupSlideAnimator slide;

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
        [Tooltip("화면설정(해상도/전체화면) 적용 버튼 — 적용 + 토스트(변동 플래그는 유지).")]
        [SerializeField] Button applyButton;
        [Tooltip("전체 적용(확인) 버튼 — 적용 + 토스트 + 변동 플래그 해제(닫기 확인의 기준).")]
        [SerializeField] Button confirmButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button closeButton;

        [Header("문구 (토스트/확인 모달)")]
        [SerializeField] string applyToastTitle = "적용";
        [SerializeField] string applyToastMessage = "설정이 저장되었습니다";
        [SerializeField] string resetToastTitle = "초기화";
        [SerializeField] string resetToastMessage = "기본값으로 초기화되었습니다";
        [SerializeField] string closeConfirmTitle = "알림";
        [SerializeField] string closeConfirmMessage = "변경사항을 적용하지 않고 닫을까요?";
        [SerializeField] string confirmYes = "예";
        [SerializeField] string confirmNo = "아니오";

        [SerializeField] SettingsSO settings; // 미바인딩 시 Shared

        SettingsSO S => settings != null ? settings : (settings = SettingsSO.Shared);
        IDisposable _showSub;
        IDisposable _gate; // OverlayGate 토큰(표시 중에만 non-null — 뒤로가기 CloseTop이 닫기 호출)
        bool _wiring; // 슬라이더 초기화 중 onValueChanged 발행 가드
        bool _visible; // OverlayGate 토큰 전이 추적
        bool _dirty;  // 열린 뒤 사용자 변경 발생 + 확인(Confirm) 미수행 → 닫기 시 확인 모달
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
            HookReleaseSfx(bgmSlider);
            HookReleaseSfx(sfxSlider);
            HookReleaseSfx(textSpeedSlider);
            HookReleaseSfx(autoSpeedSlider);
            Click(fullscreenButton, () => StageFullscreen(true));
            Click(windowButton, () => StageFullscreen(false));
            Click(leftArrow, () => StageResolution(_pendingResIndex - 1));
            Click(rightArrow, () => StageResolution(_pendingResIndex + 1));
            Click(applyButton, ApplyDisplayAndNotify);
            Click(confirmButton, OnConfirm);
            Click(resetButton, ResetAndNotify);
            Click(closeButton, RequestClose);
        }

        static void Click(Button b, UnityAction a) { if (b != null) b.onClick.AddListener(a); }
        void BindSlider(Slider s, Action<float> onChange)
        {
            if (s != null) s.onValueChanged.AddListener(v => { if (!_wiring) { onChange(v); _dirty = true; } });
        }

        // 슬라이더 드래그 종료(값 변동 시) 1회 알림음 — 드래그 중 매 프레임 연타와 분리(SliderReleaseNotifier).
        // SFX 이름은 UiSoundSO 중앙화(ADR-012), 빈값=무음. 미바인딩 시 런타임 부착(프리팹 수정 불필요).
        void HookReleaseSfx(Slider s)
        {
            if (s == null) return;
            var n = s.GetComponent<SliderReleaseNotifier>();
            if (n == null) n = s.gameObject.AddComponent<SliderReleaseNotifier>();
            n.onRelease.AddListener(PlaySliderChangeSfx);
        }

        void PlaySliderChangeSfx()
        {
            var name = UiSoundSO.Shared != null ? UiSoundSO.Shared.SettingsSliderChange : null;
            if (!string.IsNullOrEmpty(name)) Publish(new PlaySfxCommand(name));
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
            _dirty = false; // 설정값으로 재동기화한 깨끗한 상태(열기·초기화 직후)
            RefreshDisplayVisual();
        }

        void StageFullscreen(bool fs) { _pendingFullscreen = fs; _dirty = true; RefreshDisplayVisual(); }

        void StageResolution(int idx)
        {
            _pendingResIndex = Mathf.Clamp(idx, 0, GameConstants.Resolutions.Length - 1);
            _dirty = true;
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

        // 화면설정 적용(Apply Button): 스테이징된 해상도/전체화면 커밋 + 토스트. 변동 플래그는 유지한다 —
        // '전체 적용'은 확인(Confirm) 버튼이며, 닫기 확인 모달의 기준은 Confirm(아래 RequestClose).
        void ApplyDisplayAndNotify()
        {
            Publish(new ApplyDisplayCommand(_pendingResIndex, _pendingFullscreen));
            Publish(new ShowToastCommand(applyToastMessage, applyToastTitle));
        }

        // 전체 적용(확인 버튼): 화면설정 커밋 + 토스트 + 변동 플래그 해제(이후 닫으면 확인 모달 없이 닫힘).
        void OnConfirm()
        {
            ApplyDisplayAndNotify();
            _dirty = false;
        }

        // 초기화: 모달 없이 즉시 기본값 리셋 + 저장(컨트롤러) + 토스트. 저장되므로 그냥 닫아도 유지된다.
        void ResetAndNotify()
        {
            ResetAll();
            Publish(new ShowToastCommand(resetToastMessage, resetToastTitle));
        }

        // 닫기 요청(닫기 버튼·뒤로가기 공용): 변경 후 확인(Confirm) 미수행이면 확인 모달, 아니면 즉시 닫는다.
        // 기준은 확인 버튼 — 변경했어도 Confirm을 누르면 _dirty=false라 바로 닫힌다(재표시 시 RefreshFromSettings가 재동기화).
        void RequestClose()
        {
            if (!_dirty) { SetVisible(false); return; }
            Publish(new ShowModalCommand(
                closeConfirmTitle, closeConfirmMessage,
                new[] { new ModalButton(confirmNo, ModalButtonKind.No), new ModalButton(confirmYes, ModalButtonKind.Yes) },
                new ModalRequest(i => { if (i == 1) SetVisible(false); })));
        }

        void ResetAll()
        {
            Publish(new ResetSettingsCommand()); // 컨트롤러가 SettingsSO 리셋+재발행(EventBus 동기)
            RefreshFromSettings();               // 갱신된 SettingsSO로 컨트롤 갱신
        }

        void SetVisible(bool v)
        {
            if (canvasGroup == null) return;
            if (v)
            {
                // 재표시 포함 항상 시각·논리 최상단 동기화 — 형제 팝업 위로(SetAsLastSibling) +
                // 게이트 스택 맨 위로 재푸시(뒤로가기 CloseTop 대상 = 눈에 보이는 최상단 보장).
                transform.SetAsLastSibling();
                _gate?.Dispose();
                _gate = OverlayGate.Push(RequestClose); // 뒤로가기도 닫기 버튼과 동일한 확인 경로
                _visible = true;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                if (slide != null) slide.PlayShow(); // 알파는 연출이 구동
                else canvasGroup.alpha = 1f;
            }
            else
            {
                // 숨김은 입력을 즉시 차단(슬라이드 아웃 동안 클릭 통과 무해), 시각 퇴장만 연출에 맡긴다.
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                bool wasVisible = _visible;
                if (wasVisible) { _gate?.Dispose(); _gate = null; _visible = false; }
                if (slide != null) { if (wasVisible) slide.PlayHide(); else slide.ApplyHiddenInstant(); }
                else canvasGroup.alpha = 0f;
            }
        }
    }
}
