using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 설정 팝업 (Modal)
    /// 화면, 볼륨, 캐릭터 음성, 속도 설정
    /// </summary>
    public class SettingsPopup : ModalPopupBase
    {
        [Header("화면 설정")]
        [SerializeField] Button fullscreenButton;
        [SerializeField] Button windowedButton;
        [SerializeField] Sprite modeOnSprite;   // btn_config_mode_on
        [SerializeField] Sprite modeOffSprite;  // btn_config_mode
        [SerializeField] Button resolutionPrevButton;
        [SerializeField] Button resolutionNextButton;
        [SerializeField] Sprite arrowLeftSprite;      // btn_config_arrow_left
        [SerializeField] Sprite arrowLeftDisabled;     // btn_config_arrow_left_disabled
        [SerializeField] Sprite arrowRightSprite;      // btn_config_arrow_right
        [SerializeField] Sprite arrowRightDisabled;    // btn_config_arrow_right_disabled
        [Tooltip("해상도 표시용 이미지(프리팹에 스프라이트 바인딩). 없으면 텍스트로 폴백")]
        [SerializeField] UnityEngine.UI.Image resolutionImage;
        [Tooltip("해상도별 스프라이트 목록: 인덱스는 resolutions 배열과 대응합니다")]
        [SerializeField] Sprite[] resolutionSprites;


        [Header("메인 볼륨")]
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
        [SerializeField] Button confirmButton; // 우측상단 적용 (해상도 제외 설정 저장 및 창닫기)
        [SerializeField] Button resetButton;   // 우측상단 리셋 (기본값으로 초기화)
        [SerializeField] Button closeButton;   // 우측상단 닫기 (변경사항 확인)
        
        // 해상도 전용 적용 버튼 (이미 존재)
        [Header("해상도 적용 버튼")]
        [SerializeField] Button applyButton; // 해상도 전용 즉시 적용 + 저장 (ResolutionIndex / Fullscreen)

        // 해상도 목록 (스프라이트 순서와 대응)
        readonly (int w, int h)[] resolutions = {
            (1920, 1080),
            (1440, 810),
            (1280, 720),
            (960, 540),
            (800, 450)
        };
        int currentResolutionIndex = 0; // 기본 1920x1080
        bool isFullscreen = true;

        // 변경사항 추적
        bool isDirty;               // 해상도 제외(볼륨/속도/캐릭터 음성 등)
        bool isResolutionDirty;     // 해상도/전체화면 변경 여부
        float savedBgmVolume, savedSfxVolume;
        float savedTextSpeed, savedAutoSpeed;

        protected override void Awake()
        {
            base.Awake();

            SetupButtons();
            SetupSliders();
        }

        void SetupButtons()
        {
            // 상단 버튼 (우측 상단: 리셋 / 적용 / 닫기)
            confirmButton?.onClick.AddListener(OnConfirmClick); // 해상도 제외 설정 저장 및 닫기
            resetButton?.onClick.AddListener(OnResetClick);
            closeButton?.onClick.AddListener(Close);

            // 화면 설정 (해상도 전용: 프리뷰 후 'Apply'로 즉시 적용)
            fullscreenButton?.onClick.AddListener(() => SetWindowMode(true));
            windowedButton?.onClick.AddListener(() => SetWindowMode(false));
            resolutionPrevButton?.onClick.AddListener(PrevResolution);
            resolutionNextButton?.onClick.AddListener(NextResolution);
            applyButton?.onClick.AddListener(ApplyResolutionButton_Click);
        }

        void SetupSliders()
        {
            // 메인 볼륨 (변경 추적)
            bgmSlider?.onValueChanged.AddListener(v => { AudioManager.Instance?.SetBGMVolume(v); MarkDirty(); });
            sfxSlider?.onValueChanged.AddListener(v => { AudioManager.Instance?.SetSFXVolume(v); MarkDirty(); });

            // 캐릭터별 음성
            voiceYeunSlider?.onValueChanged.AddListener(v => { SetCharacterVoice("Yeun", v); MarkDirty(); });
            voiceDaeunSlider?.onValueChanged.AddListener(v => { SetCharacterVoice("Daeun", v); MarkDirty(); });
            voiceBomSlider?.onValueChanged.AddListener(v => { SetCharacterVoice("Bom", v); MarkDirty(); });
            voiceHeewonSlider?.onValueChanged.AddListener(v => { SetCharacterVoice("Heewon", v); MarkDirty(); });
            voiceRoaSlider?.onValueChanged.AddListener(v => { SetCharacterVoice("Roa", v); MarkDirty(); });

            // 속도 (변경 추적)
            textSpeedSlider?.onValueChanged.AddListener(v => { OnTextSpeedChanged(v); MarkDirty(); });
            autoSpeedSlider?.onValueChanged.AddListener(v => { OnAutoSpeedChanged(v); MarkDirty(); });
        }

        void MarkDirty()
        {
            isDirty = true;
        }

        void MarkResolutionDirty()
        {
            isResolutionDirty = true;
        }

        #region Show/Hide

        public override void Show()
        {
            LoadSettings();
            isDirty = false;  // 로드 직후 dirty 초기화
            base.Show();
        }

        public override void Hide()
        {
            // 저장은 적용 버튼 또는 닫기 확인에서만 수행
            base.Hide();
        }

        #endregion

        #region 설정 로드/저장

        void LoadSettings()
        {
            // 화면 설정
            isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            currentResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 0);
            UpdateWindowModeUI();
            UpdateResolutionUI();

            // 메인 볼륨
            bgmSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("BGMVolume", 0.4f));
            sfxSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 0.4f));

            // 캐릭터별 음성
            voiceYeunSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Yeun", 0.4f));
            voiceDaeunSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Daeun", 0.4f));
            voiceBomSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Bom", 0.4f));
            voiceHeewonSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Heewon", 0.4f));
            voiceRoaSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Roa", 0.4f));

            // 속도
            textSpeedSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("TextSpeed", 0.4f));
            autoSpeedSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("AutoSpeed", 0.4f));
        }

        void SaveSettings()
        {
            // 해상도 관련 값(Fullscreen/ResolutionIndex)은 해상도 전용 Apply 버튼에서 저장합니다.

            // 메인 볼륨
            PlayerPrefs.SetFloat("BGMVolume", bgmSlider?.value ?? 0.4f);
            PlayerPrefs.SetFloat("SFXVolume", sfxSlider?.value ?? 0.4f);

            // 캐릭터별 음성
            PlayerPrefs.SetFloat("Voice_Yeun", voiceYeunSlider?.value ?? 0.4f);
            PlayerPrefs.SetFloat("Voice_Daeun", voiceDaeunSlider?.value ?? 0.4f);
            PlayerPrefs.SetFloat("Voice_Bom", voiceBomSlider?.value ?? 0.4f);
            PlayerPrefs.SetFloat("Voice_Heewon", voiceHeewonSlider?.value ?? 0.4f);
            PlayerPrefs.SetFloat("Voice_Roa", voiceRoaSlider?.value ?? 0.4f);

            // 속도
            PlayerPrefs.SetFloat("TextSpeed", textSpeedSlider?.value ?? 0.4f);
            PlayerPrefs.SetFloat("AutoSpeed", autoSpeedSlider?.value ?? 0.4f);

            PlayerPrefs.Save();
        }

        #endregion

        #region 화면 설정

        void SetWindowMode(bool fullscreen)
        {
            isFullscreen = fullscreen;
            UpdateWindowModeUI();
            MarkResolutionDirty();
        }

        void UpdateWindowModeUI()
        {
            if (modeOnSprite != null && modeOffSprite != null)
            {
                var fsImg = fullscreenButton?.GetComponent<UnityEngine.UI.Image>();
                var wdImg = windowedButton?.GetComponent<UnityEngine.UI.Image>();
                if (fsImg != null) fsImg.sprite = isFullscreen ? modeOnSprite : modeOffSprite;
                if (wdImg != null) wdImg.sprite = isFullscreen ? modeOffSprite : modeOnSprite;
            }
        }

        void PrevResolution()
        {
            currentResolutionIndex = Mathf.Max(0, currentResolutionIndex - 1);
            UpdateResolutionUI();
            MarkResolutionDirty();
        }
        
        void NextResolution()
        {
            currentResolutionIndex = Mathf.Min(resolutions.Length - 1, currentResolutionIndex + 1);
            UpdateResolutionUI();
            MarkResolutionDirty();
        }

        void UpdateResolutionUI()
        {
            if (resolutionImage != null && resolutionSprites != null && resolutionSprites.Length > currentResolutionIndex)
            {
                resolutionImage.sprite = resolutionSprites[currentResolutionIndex];
            }

            // 화살표 스프라이트 교체 (끝 지점에서 disabled 이미지)
            bool atFirst = currentResolutionIndex <= 0;
            bool atLast  = currentResolutionIndex >= resolutions.Length - 1;

            if (resolutionPrevButton != null)
            {
                var img = resolutionPrevButton.GetComponent<UnityEngine.UI.Image>();
                if (img != null && arrowLeftSprite != null && arrowLeftDisabled != null)
                    img.sprite = atFirst ? arrowLeftDisabled : arrowLeftSprite;
                resolutionPrevButton.interactable = !atFirst;
            }
            if (resolutionNextButton != null)
            {
                var img = resolutionNextButton.GetComponent<UnityEngine.UI.Image>();
                if (img != null && arrowRightSprite != null && arrowRightDisabled != null)
                    img.sprite = atLast ? arrowRightDisabled : arrowRightSprite;
                resolutionNextButton.interactable = !atLast;
            }
        }

        void ApplyResolution()
        {
            var res = resolutions[currentResolutionIndex];
            Screen.SetResolution(res.w, res.h, isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            Debug.Log($"[SettingsPopup] 해상도 적용: {res.w}x{res.h}, 전체화면: {isFullscreen}");
        }

        /// <summary>
        /// 해상도 전용 Apply 버튼 클릭 핸들러
        /// - 화면 해상도 즉시 적용 및 PlayerPrefs에 저장
        /// </summary>
        void ApplyResolutionButton_Click()
        {
            ApplyResolution();
            // 해상도 값 저장(별도 플로우)
            PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
            PlayerPrefs.SetInt("ResolutionIndex", currentResolutionIndex);
            PlayerPrefs.Save();
            isResolutionDirty = false;
            PopupManager.Instance?.Toast("해상도 적용", "해상도가 적용되었습니다.");
        }

        #endregion

        #region 캐릭터별 음성

        void SetCharacterVoice(string character, float volume)
        {
            AudioManager.Instance?.SetCharacterVoiceVolume(character, volume);
        }

        #endregion

        #region 속도 설정

        void OnTextSpeedChanged(float value)
        {
            UIManager.Instance?.DialogueUI?.SetTextSpeed(value);
        }

        void OnAutoSpeedChanged(float value)
        {
            ScriptRunner.Instance?.SetAutoDelay(value);
        }

        #endregion

        #region 버튼 핸들러

        void OnConfirmClick()
        {
            // '적용' 버튼: 슬라이더 + 해상도 일괄 저장 (창 닫지 않음)
            SaveSettings();
            ApplyResolution();
            PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
            PlayerPrefs.SetInt("ResolutionIndex", currentResolutionIndex);
            PlayerPrefs.Save();
            isDirty = false;
            isResolutionDirty = false;
            PopupManager.Instance?.Toast("적용", "설정이 저장되었습니다.");
        }

        void OnResetClick()
        {
            // 슬라이더만 기본값으로 초기화 — 해상도 영역은 건드리지 않음
            const float defaultVolume = 0.4f;
            const float defaultSpeed  = 0.4f;

            bgmSlider?.SetValueWithoutNotify(defaultVolume);
            sfxSlider?.SetValueWithoutNotify(defaultVolume);

            voiceYeunSlider?.SetValueWithoutNotify(defaultVolume);
            voiceDaeunSlider?.SetValueWithoutNotify(defaultVolume);
            voiceBomSlider?.SetValueWithoutNotify(defaultVolume);
            voiceHeewonSlider?.SetValueWithoutNotify(defaultVolume);
            voiceRoaSlider?.SetValueWithoutNotify(defaultVolume);

            textSpeedSlider?.SetValueWithoutNotify(defaultSpeed);
            autoSpeedSlider?.SetValueWithoutNotify(defaultSpeed);

            // 실시간 오디오 반영 (슬라이더 피드백)
            AudioManager.Instance?.SetBGMVolume(defaultVolume);
            AudioManager.Instance?.SetSFXVolume(defaultVolume);
            AudioManager.Instance?.SetCharacterVoiceVolume("Yeun", defaultVolume);
            AudioManager.Instance?.SetCharacterVoiceVolume("Daeun", defaultVolume);
            AudioManager.Instance?.SetCharacterVoiceVolume("Bom", defaultVolume);
            AudioManager.Instance?.SetCharacterVoiceVolume("Heewon", defaultVolume);
            AudioManager.Instance?.SetCharacterVoiceVolume("Roa", defaultVolume);
            UIManager.Instance?.DialogueUI?.SetTextSpeed(defaultSpeed);
            ScriptRunner.Instance?.SetAutoDelay(defaultSpeed);

            isDirty = true;
            PopupManager.Instance?.Toast("초기화", "슬라이더가 기본값으로 초기화되었습니다.");
        }

        #endregion

        #region 닫기 확인

        /// <summary>
        /// 변경사항이 있으면 확인 팝업 표시
        /// </summary>
        public override async UniTask<bool> TryCloseAsync()
        {
            // 해상도 변경은 Resolution 적용 버튼 전용 → 닫을 때 무시(확인팝업 없이 폐기)
            if (isResolutionDirty)
            {
                // UI만 저장값으로 복원 (실제 화면은 Apply 안 했으므로 변경 없음)
                isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
                currentResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 0);
                UpdateWindowModeUI();
                UpdateResolutionUI();
                isResolutionDirty = false;
            }

            // 슬라이더 변경사항 없으면 바로 닫기
            if (!isDirty)
            {
                return true;
            }

            // 슬라이더 변경사항이 있으면 확인 팝업
            bool save = await PopupManager.Instance.ConfirmAsync("변경된 정보가 있습니다.\n저장 하시겠습니까?",
                confirmText: "예", cancelText: "아니요");

            if (save)
            {
                SaveSettings();
            }
            else
            {
                RevertSettings();
            }

            isDirty = false;
            return true;  // 어느 쪽이든 닫기
        }

        /// <summary>
        /// 변경 전 값으로 복원
        /// </summary>
        void RevertSettings()
        {
            LoadSettings();
            
            // 볼륨 복원
            AudioManager.Instance?.SetBGMVolume(bgmSlider?.value ?? 0.4f);
            AudioManager.Instance?.SetSFXVolume(sfxSlider?.value ?? 0.4f);

            // NOTE: 해상도(실제 화면)는 ApplyResolutionButton_Click에서만 즉시 변경됩니다.
            // RevertSettings는 UI 값을 저장된 값으로 복원합니다.
        }

        #endregion
    }
}
