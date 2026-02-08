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
        [Header("애니메이션")]
        [SerializeField] RectTransform panelRect;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] float showDuration = 0.3f;
        [SerializeField] float hideDuration = 0.2f;
        [SerializeField] float slideOffset = 300f;

        [Header("화면 설정")]
        [SerializeField] Button fullscreenButton;
        [SerializeField] Button windowedButton;
        [SerializeField] Button resolutionPrevButton;
        [SerializeField] Button resolutionNextButton;
        [SerializeField] TMP_Text resolutionText;
        [SerializeField] Button applyButton;

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
        [SerializeField] Button confirmButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button closeButton;

        Vector2 originalPosition;
        
        // 해상도 목록
        readonly (int w, int h)[] resolutions = {
            (1280, 720),
            (1600, 900),
            (1920, 1080),
            (2560, 1440)
        };
        int currentResolutionIndex = 2; // 기본 1920x1080
        bool isFullscreen = true;

        // 변경사항 추적
        bool isDirty;
        float savedBgmVolume, savedSfxVolume;
        float savedTextSpeed, savedAutoSpeed;

        protected void Awake()
        {
            if (panelRect != null)
                originalPosition = panelRect.anchoredPosition;

            SetupButtons();
            SetupSliders();
        }

        void SetupButtons()
        {
            // 상단 버튼
            confirmButton?.onClick.AddListener(OnConfirmClick);
            resetButton?.onClick.AddListener(OnResetClick);
            closeButton?.onClick.AddListener(Close);

            // 화면 설정
            fullscreenButton?.onClick.AddListener(() => SetWindowMode(true));
            windowedButton?.onClick.AddListener(() => SetWindowMode(false));
            resolutionPrevButton?.onClick.AddListener(PrevResolution);
            resolutionNextButton?.onClick.AddListener(NextResolution);
            applyButton?.onClick.AddListener(ApplyResolution);
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

        #region Show/Hide (슬라이드 애니메이션)

        public override void Show()
        {
            LoadSettings();
            isDirty = false;  // 로드 직후 dirty 초기화
            gameObject.SetActive(true);
            PlayShowAnimation().Forget();
        }

        public override void Hide()
        {
            SaveSettings();
            PlayHideAnimation().Forget();
        }

        async UniTaskVoid PlayShowAnimation()
        {
            if (panelRect == null || canvasGroup == null)
            {
                base.Show();
                return;
            }

            canvasGroup.alpha = 0f;
            panelRect.anchoredPosition = originalPosition + new Vector2(slideOffset, 0);

            await DOTween.Sequence()
                .Append(canvasGroup.DOFade(1f, showDuration))
                .Join(panelRect.DOAnchorPos(originalPosition, showDuration).SetEase(Ease.OutQuad))
                .AsyncWaitForCompletion();
        }

        async UniTaskVoid PlayHideAnimation()
        {
            if (panelRect == null || canvasGroup == null)
            {
                base.Hide();
                return;
            }

            // 슬라이드 먼저 시작, 페이드는 후반부에만
            await DOTween.Sequence()
                .Append(panelRect.DOAnchorPos(originalPosition + new Vector2(slideOffset, 0), hideDuration).SetEase(Ease.InQuad))
                .Insert(hideDuration * 0.6f, canvasGroup.DOFade(0f, hideDuration * 0.4f))
                .AsyncWaitForCompletion();

            gameObject.SetActive(false);
        }

        #endregion

        #region 설정 로드/저장

        void LoadSettings()
        {
            // 화면 설정
            isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            currentResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 2);
            UpdateWindowModeUI();
            UpdateResolutionUI();

            // 메인 볼륨
            bgmSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("BGMVolume", 0.8f));
            sfxSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 1f));

            // 캐릭터별 음성
            voiceYeunSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Yeun", 1f));
            voiceDaeunSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Daeun", 1f));
            voiceBomSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Bom", 1f));
            voiceHeewonSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Heewon", 1f));
            voiceRoaSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("Voice_Roa", 1f));

            // 속도
            textSpeedSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("TextSpeed", 0.5f));
            autoSpeedSlider?.SetValueWithoutNotify(PlayerPrefs.GetFloat("AutoSpeed", 0.5f));
        }

        void SaveSettings()
        {
            // 화면 설정
            PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
            PlayerPrefs.SetInt("ResolutionIndex", currentResolutionIndex);

            // 메인 볼륨
            PlayerPrefs.SetFloat("BGMVolume", bgmSlider?.value ?? 0.8f);
            PlayerPrefs.SetFloat("SFXVolume", sfxSlider?.value ?? 1f);

            // 캐릭터별 음성
            PlayerPrefs.SetFloat("Voice_Yeun", voiceYeunSlider?.value ?? 1f);
            PlayerPrefs.SetFloat("Voice_Daeun", voiceDaeunSlider?.value ?? 1f);
            PlayerPrefs.SetFloat("Voice_Bom", voiceBomSlider?.value ?? 1f);
            PlayerPrefs.SetFloat("Voice_Heewon", voiceHeewonSlider?.value ?? 1f);
            PlayerPrefs.SetFloat("Voice_Roa", voiceRoaSlider?.value ?? 1f);

            // 속도
            PlayerPrefs.SetFloat("TextSpeed", textSpeedSlider?.value ?? 0.5f);
            PlayerPrefs.SetFloat("AutoSpeed", autoSpeedSlider?.value ?? 0.5f);

            PlayerPrefs.Save();
        }

        #endregion

        #region 화면 설정

        void SetWindowMode(bool fullscreen)
        {
            isFullscreen = fullscreen;
            UpdateWindowModeUI();
        }

        void UpdateWindowModeUI()
        {
            // 선택된 버튼 시각적 표시 (필요 시 색상 변경)
            if (fullscreenButton != null)
                fullscreenButton.interactable = !isFullscreen;
            if (windowedButton != null)
                windowedButton.interactable = isFullscreen;
        }

        void PrevResolution()
        {
            currentResolutionIndex = Mathf.Max(0, currentResolutionIndex - 1);
            UpdateResolutionUI();
        }

        void NextResolution()
        {
            currentResolutionIndex = Mathf.Min(resolutions.Length - 1, currentResolutionIndex + 1);
            UpdateResolutionUI();
        }

        void UpdateResolutionUI()
        {
            if (resolutionText != null)
            {
                var res = resolutions[currentResolutionIndex];
                resolutionText.text = $"{res.w} X {res.h}";
            }

            if (resolutionPrevButton != null)
                resolutionPrevButton.interactable = currentResolutionIndex > 0;
            if (resolutionNextButton != null)
                resolutionNextButton.interactable = currentResolutionIndex < resolutions.Length - 1;
        }

        void ApplyResolution()
        {
            var res = resolutions[currentResolutionIndex];
            Screen.SetResolution(res.w, res.h, isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            Debug.Log($"[SettingsPopup] 해상도 적용: {res.w}x{res.h}, 전체화면: {isFullscreen}");
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
            // 0 = 느림, 1 = 빠름 (DialogueUI에서 사용)
            // TODO: DialogueUI.Instance?.SetTextSpeed(value);
        }

        void OnAutoSpeedChanged(float value)
        {
            // 0 = 느림 (긴 대기), 1 = 빠름 (짧은 대기)
            // ScriptRunner의 autoDelay에 반영 (예: 4초 ~ 1초)
            // TODO: ScriptRunner와 연동
        }

        #endregion

        #region 버튼 핸들러

        void OnConfirmClick()
        {
            SaveSettings();
            ApplyResolution();
            isDirty = false;
            Close();
        }

        void OnResetClick()
        {
            // 기본값으로 초기화
            isFullscreen = true;
            currentResolutionIndex = 2; // 1920x1080
            UpdateWindowModeUI();
            UpdateResolutionUI();

            bgmSlider?.SetValueWithoutNotify(0.8f);
            sfxSlider?.SetValueWithoutNotify(1f);

            voiceYeunSlider?.SetValueWithoutNotify(1f);
            voiceDaeunSlider?.SetValueWithoutNotify(1f);
            voiceBomSlider?.SetValueWithoutNotify(1f);
            voiceHeewonSlider?.SetValueWithoutNotify(1f);
            voiceRoaSlider?.SetValueWithoutNotify(1f);

            textSpeedSlider?.SetValueWithoutNotify(0.5f);
            autoSpeedSlider?.SetValueWithoutNotify(0.5f);

            // 볼륨 즉시 적용
            AudioManager.Instance?.SetBGMVolume(0.8f);
            AudioManager.Instance?.SetSFXVolume(1f);

            isDirty = true;
            PopupManager.Instance?.Toast("초기화", "설정이 기본값으로 초기화되었습니다.");
        }

        #endregion

        #region 닫기 확인

        /// <summary>
        /// 변경사항이 있으면 확인 팝업 표시
        /// </summary>
        public override async UniTask<bool> TryCloseAsync()
        {
            if (!isDirty)
            {
                return true;  // 변경 없으면 바로 닫기
            }

            // 확인 팝업 표시
            bool confirm = await PopupManager.Instance.ConfirmAsync(
                "변경사항이 저장되지 않았습니다.\n저장하지 않고 닫으시겠습니까?");

            if (confirm)
            {
                // 저장 안 하고 닫기 - 이전 값 복원
                RevertSettings();
                isDirty = false;
                return true;
            }

            return false;  // 취소 - 닫지 않음
        }

        /// <summary>
        /// 변경 전 값으로 복원
        /// </summary>
        void RevertSettings()
        {
            LoadSettings();
            
            // 볼륨 복원
            AudioManager.Instance?.SetBGMVolume(bgmSlider?.value ?? 0.8f);
            AudioManager.Instance?.SetSFXVolume(sfxSlider?.value ?? 1f);
        }

        #endregion
    }
}
