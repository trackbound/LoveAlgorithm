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
        [SerializeField] Button confirmButton; // 우측상단 적용 (해상도 제외 설정 저장 및 창닫기)
        [SerializeField] Button resetButton;   // 우측상단 리셋 (기본값으로 초기화)
        [SerializeField] Button closeButton;   // 우측상단 닫기 (변경사항 확인)
        
        // 해상도 전용 적용 버튼 (이미 존재)
        [Header("해상도 적용 버튼")]
        [SerializeField] Button applyButton; // 해상도 전용 즉시 적용 + 저장 (ResolutionIndex / Fullscreen)

        // 해상도 목록 (오름차순: ← 작아짐 / → 커짐)
        readonly (int w, int h)[] resolutions = {
            (800, 450),
            (960, 540),
            (1280, 720),
            (1440, 810),
            (1920, 1080)
        };
        int currentResolutionIndex = 4; // 기본 1920x1080
        bool isFullscreen = true;

        // 변경사항 추적
        bool isDirty;               // 해상도 제외(볼륨/속도/캐릭터 음성 등)
        bool isResolutionDirty;     // 해상도/전체화면 변경 여부

        // ── 열기 시점 스냅샷 (되돌리기용) ──
        struct SettingsSnapshot
        {
            public float masterVolume;
            public float bgmVolume, sfxVolume;
            public float voiceYeun, voiceDaeun, voiceBom, voiceHeewon, voiceRoa;
            public float textSpeed, autoSpeed;
        }
        SettingsSnapshot snapshot;

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
            // 마스터 볼륨
            masterSlider?.onValueChanged.AddListener(v => { AudioManager.Instance?.SetMasterVolume(v); MarkDirty(); });

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
            TakeSnapshot();
            isDirty = false;
            isResolutionDirty = false;
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
            currentResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 4);
            UpdateWindowModeUI();
            UpdateResolutionUI();

            // 마스터 볼륨
            masterSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("MasterVolume", 0.8f));

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

            // 마스터 볼륨
            PlayerPrefs.SetFloat("MasterVolume", masterSlider?.value ?? 0.8f);

            // 메인 볼륨
            PlayerPrefs.SetFloat("BGMVolume", bgmSlider?.value ?? 0.4f);
            PlayerPrefs.SetFloat("SFXVolume", sfxSlider?.value ?? 0.4f);

            // 캐릭터별 음성 (런타임 딕셔너리 + PlayerPrefs 동기화)
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
            ConfirmReset().Forget();
        }

        async UniTaskVoid ConfirmReset()
        {
            bool confirmed = await PopupManager.Instance.ConfirmAsync("모든 설정을 기본값으로\n초기화하시겠습니까?");
            if (!confirmed) return;

            ApplyResetValues();
        }

        void ApplyResetValues()
        {
            // 슬라이더만 기본값으로 초기화 — 해상도 영역은 건드리지 않음
            const float defaultMaster = 0.8f;
            const float defaultVolume = 0.4f;
            const float defaultSpeed  = 0.4f;

            masterSlider?.SetValueWithoutNotify(defaultMaster);
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
            AudioManager.Instance?.SetMasterVolume(defaultMaster);
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
                currentResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 4);
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
        /// 현재 저장값(PlayerPrefs) 기준 스냅샷 저장
        /// </summary>
        void TakeSnapshot()
        {
            snapshot = new SettingsSnapshot
            {
                masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.8f),
                bgmVolume   = PlayerPrefs.GetFloat("BGMVolume", 0.4f),
                sfxVolume   = PlayerPrefs.GetFloat("SFXVolume", 0.4f),
                voiceYeun   = PlayerPrefs.GetFloat("Voice_Yeun", 0.4f),
                voiceDaeun  = PlayerPrefs.GetFloat("Voice_Daeun", 0.4f),
                voiceBom    = PlayerPrefs.GetFloat("Voice_Bom", 0.4f),
                voiceHeewon = PlayerPrefs.GetFloat("Voice_Heewon", 0.4f),
                voiceRoa    = PlayerPrefs.GetFloat("Voice_Roa", 0.4f),
                textSpeed   = PlayerPrefs.GetFloat("TextSpeed", 0.4f),
                autoSpeed   = PlayerPrefs.GetFloat("AutoSpeed", 0.4f),
            };
        }

        /// <summary>
        /// 스냅샷 값으로 모든 런타임 설정 복원 (저장 안 하고 닫기 시)
        /// </summary>
        void RevertSettings()
        {
            // 마스터 볼륨 복원
            AudioManager.Instance?.SetMasterVolume(snapshot.masterVolume);

            // 볼륨 복원
            AudioManager.Instance?.SetBGMVolume(snapshot.bgmVolume);
            AudioManager.Instance?.SetSFXVolume(snapshot.sfxVolume);

            // 캐릭터 음성 복원
            AudioManager.Instance?.SetCharacterVoiceVolume("Yeun", snapshot.voiceYeun);
            AudioManager.Instance?.SetCharacterVoiceVolume("Daeun", snapshot.voiceDaeun);
            AudioManager.Instance?.SetCharacterVoiceVolume("Bom", snapshot.voiceBom);
            AudioManager.Instance?.SetCharacterVoiceVolume("Heewon", snapshot.voiceHeewon);
            AudioManager.Instance?.SetCharacterVoiceVolume("Roa", snapshot.voiceRoa);

            // 속도 복원
            UIManager.Instance?.DialogueUI?.SetTextSpeed(snapshot.textSpeed);
            ScriptRunner.Instance?.SetAutoDelay(snapshot.autoSpeed);
        }

        #endregion
    }
}
