using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Settings;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 설정 팝업 (Modal).
    /// 상태/저장소 접근은 모두 ISettings 경유. UI는 슬라이더/버튼 입력 처리만 담당.
    /// 화면, 볼륨, 캐릭터 음성, 속도 설정.
    /// </summary>
    public class SettingsPopup : PopupBase
    {
        [Header("화면 설정")]
        [SerializeField] Button fullscreenButton;
        [SerializeField] Button windowedButton;
        [SerializeField] Sprite modeOnSprite;
        [SerializeField] Sprite modeOffSprite;
        [SerializeField] Color modeTextSelectedColor = new Color(1f, 0.6f, 0.75f, 1f);
        [SerializeField] Color modeTextDeselectedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] Button resolutionPrevButton;
        [SerializeField] Button resolutionNextButton;
        [SerializeField] Sprite arrowLeftSprite;
        [SerializeField] Sprite arrowLeftDisabled;
        [SerializeField] Sprite arrowRightSprite;
        [SerializeField] Sprite arrowRightDisabled;
        [Tooltip("해상도 표시용 이미지(프리팹에 스프라이트 바인딩). 없으면 텍스트로 폴백")]
        [SerializeField] Image resolutionImage;
        [Tooltip("해상도별 스프라이트 목록: 인덱스는 ISettings 해상도 목록과 대응")]
        [SerializeField] Sprite[] resolutionSprites;

        [Header("메인 볼륨")]
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider bgmSlider;
        [SerializeField] Slider sfxSlider;

        [Header("캐릭터별 음성")]
        [SerializeField] Slider voiceYeunSlider;
        [SerializeField] Slider voiceDaeunSlider;
        [SerializeField] Slider voiceBomSlider;
        [SerializeField] Slider voiceHeewonSlider;
        [SerializeField] Slider voiceRoaSlider;

        [Header("속도")]
        [SerializeField] Slider textSpeedSlider;
        [SerializeField] Slider autoSpeedSlider;

        [Header("버튼")]
        [SerializeField] Button confirmButton; // 슬라이더 일괄 저장
        [SerializeField] Button resetButton;   // 기본값 초기화
        [SerializeField] Button closeButton;   // 닫기 (변경 확인)

        [Header("해상도 적용 버튼")]
        [SerializeField] Button applyButton;   // 해상도 전용 즉시 적용 + 저장

        // 변경사항 추적
        bool isDirty;            // 슬라이더 변경
        bool isResolutionDirty;  // 해상도/전체화면 변경

        ISettings settings;

        protected override void Awake()
        {
            base.Awake();
            settings = Services.Get<ISettings>();

            SetupButtons();
            SetupSliders();
        }

        void SetupButtons()
        {
            confirmButton?.onClick.AddListener(OnConfirmClick);
            resetButton?.onClick.AddListener(OnResetClick);
            closeButton?.onClick.AddListener(Close);

            fullscreenButton?.onClick.AddListener(() => SetWindowMode(true));
            windowedButton?.onClick.AddListener(() => SetWindowMode(false));
            resolutionPrevButton?.onClick.AddListener(PrevResolution);
            resolutionNextButton?.onClick.AddListener(NextResolution);
            applyButton?.onClick.AddListener(ApplyResolutionButton_Click);
        }

        void SetupSliders()
        {
            masterSlider?.onValueChanged.AddListener(v =>
            {
                if (settings != null) settings.MasterVolume = v;
                UISoundManager.Instance?.PlayVolumePreview();
                MarkDirty();
            });
            bgmSlider?.onValueChanged.AddListener(v =>
            {
                if (settings != null) settings.BGMVolume = v;
                UISoundManager.Instance?.PlayVolumePreview();
                MarkDirty();
            });
            sfxSlider?.onValueChanged.AddListener(v =>
            {
                if (settings != null) settings.SFXVolume = v;
                UISoundManager.Instance?.PlayVolumePreview();
                MarkDirty();
            });

            voiceYeunSlider?.onValueChanged.AddListener(v => { settings?.SetCharacterVoice("Yeun", v); UISoundManager.Instance?.PlayVolumePreview(v); MarkDirty(); });
            voiceDaeunSlider?.onValueChanged.AddListener(v => { settings?.SetCharacterVoice("Daeun", v); UISoundManager.Instance?.PlayVolumePreview(v); MarkDirty(); });
            voiceBomSlider?.onValueChanged.AddListener(v => { settings?.SetCharacterVoice("Bom", v); UISoundManager.Instance?.PlayVolumePreview(v); MarkDirty(); });
            voiceHeewonSlider?.onValueChanged.AddListener(v => { settings?.SetCharacterVoice("Heewon", v); UISoundManager.Instance?.PlayVolumePreview(v); MarkDirty(); });
            voiceRoaSlider?.onValueChanged.AddListener(v => { settings?.SetCharacterVoice("Roa", v); UISoundManager.Instance?.PlayVolumePreview(v); MarkDirty(); });

            textSpeedSlider?.onValueChanged.AddListener(v =>
            {
                if (settings != null) settings.TextSpeed = v;
                UISoundManager.Instance?.PlayVolumePreview();
                MarkDirty();
            });
            autoSpeedSlider?.onValueChanged.AddListener(v =>
            {
                if (settings != null) settings.AutoSpeed = v;
                UISoundManager.Instance?.PlayVolumePreview();
                MarkDirty();
            });
        }

        void MarkDirty() => isDirty = true;
        void MarkResolutionDirty() => isResolutionDirty = true;

        #region Show/Hide

        public override void Show()
        {
            SyncUIFromSettings();
            settings?.TakeSnapshot();
            isDirty = false;
            isResolutionDirty = false;
            base.Show();
        }

        public override void Hide() => base.Hide();

        #endregion

        #region UI ↔ Settings 동기화

        /// <summary>현재 settings 값으로 슬라이더/해상도 UI 갱신.</summary>
        void SyncUIFromSettings()
        {
            if (settings == null) return;

            UpdateWindowModeUI();
            UpdateResolutionUI();

            masterSlider?.SetValueWithoutNotify(settings.MasterVolume);
            bgmSlider?.SetValueWithoutNotify(settings.BGMVolume);
            sfxSlider?.SetValueWithoutNotify(settings.SFXVolume);

            voiceYeunSlider?.SetValueWithoutNotify(settings.GetCharacterVoice("Yeun"));
            voiceDaeunSlider?.SetValueWithoutNotify(settings.GetCharacterVoice("Daeun"));
            voiceBomSlider?.SetValueWithoutNotify(settings.GetCharacterVoice("Bom"));
            voiceHeewonSlider?.SetValueWithoutNotify(settings.GetCharacterVoice("Heewon"));
            voiceRoaSlider?.SetValueWithoutNotify(settings.GetCharacterVoice("Roa"));

            textSpeedSlider?.SetValueWithoutNotify(settings.TextSpeed);
            autoSpeedSlider?.SetValueWithoutNotify(settings.AutoSpeed);
        }

        #endregion

        #region 화면 설정

        void SetWindowMode(bool fullscreen)
        {
            if (settings != null) settings.IsFullscreen = fullscreen;
            UpdateWindowModeUI();
            MarkResolutionDirty();
        }

        void UpdateWindowModeUI()
        {
            bool fs = settings?.IsFullscreen ?? true;
            if (modeOnSprite != null && modeOffSprite != null)
            {
                var fsImg = fullscreenButton?.GetComponent<Image>();
                var wdImg = windowedButton?.GetComponent<Image>();
                if (fsImg != null) fsImg.sprite = fs ? modeOnSprite : modeOffSprite;
                if (wdImg != null) wdImg.sprite = fs ? modeOffSprite : modeOnSprite;
            }
            var fsTxt = fullscreenButton?.GetComponentInChildren<TMP_Text>();
            var wdTxt = windowedButton?.GetComponentInChildren<TMP_Text>();
            if (fsTxt != null) fsTxt.color = fs ? modeTextSelectedColor : modeTextDeselectedColor;
            if (wdTxt != null) wdTxt.color = fs ? modeTextDeselectedColor : modeTextSelectedColor;
        }

        void PrevResolution()
        {
            if (settings == null) return;
            settings.ResolutionIndex = Mathf.Max(0, settings.ResolutionIndex - 1);
            UpdateResolutionUI();
            MarkResolutionDirty();
        }

        void NextResolution()
        {
            if (settings == null) return;
            settings.ResolutionIndex = Mathf.Min(settings.ResolutionCount - 1, settings.ResolutionIndex + 1);
            UpdateResolutionUI();
            MarkResolutionDirty();
        }

        void UpdateResolutionUI()
        {
            int idx = settings?.ResolutionIndex ?? 0;
            int count = settings?.ResolutionCount ?? 0;

            if (resolutionImage != null && resolutionSprites != null && resolutionSprites.Length > idx)
                resolutionImage.sprite = resolutionSprites[idx];

            bool atFirst = idx <= 0;
            bool atLast = idx >= count - 1;
            if (resolutionPrevButton != null)
            {
                var img = resolutionPrevButton.GetComponent<Image>();
                if (img != null && arrowLeftSprite != null && arrowLeftDisabled != null)
                    img.sprite = atFirst ? arrowLeftDisabled : arrowLeftSprite;
                resolutionPrevButton.interactable = !atFirst;
            }
            if (resolutionNextButton != null)
            {
                var img = resolutionNextButton.GetComponent<Image>();
                if (img != null && arrowRightSprite != null && arrowRightDisabled != null)
                    img.sprite = atLast ? arrowRightDisabled : arrowRightSprite;
                resolutionNextButton.interactable = !atLast;
            }
        }

        void ApplyResolutionButton_Click()
        {
            settings?.ApplyResolution();
            isResolutionDirty = false;
            PopupManager.Instance?.Toast("해상도 적용", "해상도가 적용되었습니다.");
        }

        #endregion

        #region 버튼 핸들러

        void OnConfirmClick()
        {
            if (settings != null)
            {
                settings.Save();
                settings.ApplyResolution(); // 해상도도 함께 저장/적용
            }
            isDirty = false;
            isResolutionDirty = false;
            PopupManager.Instance?.Toast("적용", "설정이 저장되었습니다.");
        }

        void OnResetClick() => ConfirmReset().Forget();

        async UniTaskVoid ConfirmReset()
        {
            bool confirmed = await PopupManager.Instance.ConfirmAsync("모든 설정을 기본값으로\n초기화하시겠습니까?");
            if (!confirmed) return;

            settings?.ResetToDefaults();
            SyncUIFromSettings();
            isDirty = true;
            PopupManager.Instance?.Toast("초기화", "기본값으로 변경되었습니다.\n적용 버튼을 눌러 저장하세요.");
        }

        #endregion

        #region 닫기 확인

        public override async UniTask<bool> TryCloseAsync()
        {
            // 해상도는 Apply 버튼 외에는 무시(저장된 값으로 UI만 복원)
            if (isResolutionDirty)
            {
                settings?.Load(); // PrefsKeys 값으로 메모리/UI 복원
                SyncUIFromSettings();
                isResolutionDirty = false;
            }

            if (!isDirty) return true;

            bool save = await PopupManager.Instance.ConfirmAsync("변경된 정보가 있습니다.\n저장 하시겠습니까?");
            if (save) settings?.Save();
            else settings?.RevertToSnapshot();

            SyncUIFromSettings();
            isDirty = false;
            return true;
        }

        #endregion
    }
}
