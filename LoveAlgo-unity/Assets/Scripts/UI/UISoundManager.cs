using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI 사운드 매니저 - UI 전용 AudioSource로 분리
    /// 게임 SFX와 충돌 없이 독립적으로 재생
    /// </summary>
    public class UISoundManager : SingletonMonoBehaviour<UISoundManager>
    {

        [Header("UI 사운드 클립")]
        [SerializeField] AudioClip hoverClip;
        [SerializeField] AudioClip clickClip;
        [SerializeField] AudioClip typingClip;
        [SerializeField] AudioClip dialogueNextClip;
        [SerializeField] AudioClip choiceSelectClip;
        [SerializeField] AudioClip choiceAppearClip;
        [SerializeField] AudioClip choiceHoverClip;
        [SerializeField] AudioClip popupOpenClip;
        [SerializeField] AudioClip popupCloseClip;
        [SerializeField] AudioClip saveCompleteClip;
        [SerializeField] AudioClip loadCompleteClip;
        [Tooltip("볼륨 슬라이더 프리뷰 전용 사운드 (미할당 시 clickClip 사용)")]
        [SerializeField] AudioClip volumePreviewClip;

        [Header("오디오 믹서")]
        [SerializeField] AudioMixerGroup sfxMixerGroup;  // AudioManager와 동일한 SFX 그룹 할당

        [Header("볼륨 설정")]
        [SerializeField] float hoverVolume = 0.5f;
        [SerializeField] float clickVolume = 0.7f;

        [Header("타이핑 피치/볼륨 랜덤 설정")]
        [SerializeField] float minTypingPitch = 0.9f;
        [SerializeField] float maxTypingPitch = 1.1f;
        [SerializeField] float minTypingVolume = 0.35f;
        [SerializeField] float maxTypingVolume = 0.5f;

        [Header("타이핑 재생 간격")]
        [SerializeField] float typingMinInterval = 0.035f; // 기본 속도(0.039)에서 매 글자 소리, 빠른 속도에서는 자연 솎아짐

        [Header("볼륨 프리뷰")]
        [SerializeField] float volumePreviewDebounce = 0.08f;  // 슬라이더 조작 정지 후 재생까지 대기 시간


        [Header("자동 바인딩")]
        [SerializeField] bool autoBindButtons = true;

        AudioSource audioSource;
        /// <summary>
        /// 타이핑 전용 AudioSource (피치 변경 시 다른 사운드 영향 방지)
        /// </summary>
        AudioSource typingSource;
        HashSet<Button> registeredButtons = new HashSet<Button>();
        /// <summary>
        /// 기본 클릭/호버 사운드에서 제외할 버튼 (선택지 등 전용 사운드 사용)
        /// </summary>
        HashSet<Button> excludedButtons = new HashSet<Button>();
        float lastTypingPlayTime = -999f;
        float volumePreviewScheduledTime = -1f;  // 디바운스 예약 시각 (-1 = 예약 없음)
        float volumePreviewScale = 1f;           // 예약된 프리뷰 볼륨 스케일 (1.0 = 기본 0.8)

        protected override void OnSingletonAwake()
        {
            SetupAudioSource();
            WarmUpClips();
        }

        void Start()
        {
            if (autoBindButtons)
            {
                BindAllButtons();
            }
        }

        void SetupAudioSource()
        {
            // 메인 UI AudioSource (클릭, 호버 등)
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            // UI SFX는 매우 짧은 소리이므로 priority를 높게 설정 (0=최고)
            audioSource.priority = 0;

            // 타이핑 전용 AudioSource (피치 변경이 다른 사운드에 영향 주지 않도록 분리)
            typingSource = gameObject.AddComponent<AudioSource>();
            typingSource.playOnAwake = false;
            typingSource.priority = 0;

            // AudioMixer SFX 그룹에 라우팅 (설정의 SFX 볼륨이 UI 사운드에도 적용됨)
            if (sfxMixerGroup != null)
            {
                audioSource.outputAudioMixerGroup = sfxMixerGroup;
                typingSource.outputAudioMixerGroup = sfxMixerGroup;
            }
        }

        // Pre-warm: 첫 PlayOneShot overhead 제거를 위해 무음으로 미리 재생
        void WarmUpClips()
        {
            if (hoverClip != null) audioSource.PlayOneShot(hoverClip, 0f);
            if (clickClip != null) audioSource.PlayOneShot(clickClip, 0f);
            if (typingClip != null) typingSource.PlayOneShot(typingClip, 0f);
        }

        #region 버튼 자동 바인딩

        /// <summary>
        /// 씬의 모든 버튼에 사운드 바인딩
        /// </summary>
        public void BindAllButtons()
        {
            // 비활성화된 오브젝트 포함해서 모든 버튼 찾기
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (var button in buttons)
            {
                RegisterButton(button);
            }
            Debug.Log($"[UISoundManager] {registeredButtons.Count}개 버튼에 사운드 적용");
        }

        /// <summary>
        /// 개별 버튼 등록
        /// </summary>
        public void RegisterButton(Button button)
        {
            if (button == null || registeredButtons.Contains(button)) return;

            registeredButtons.Add(button);

            // 클릭 사운드 — 제외 목록에 있으면 재생하지 않음
            button.onClick.AddListener(() => { if (!excludedButtons.Contains(button)) PlayClick(); });

            // 호버 사운드 (EventTrigger 사용)
            var trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            // PointerEnter 이벤트가 없으면 추가
            bool hasEnterEvent = false;
            foreach (var entry in trigger.triggers)
            {
                if (entry.eventID == EventTriggerType.PointerEnter)
                {
                    entry.callback.AddListener(_ => { if (!excludedButtons.Contains(button)) PlayHover(); });
                    hasEnterEvent = true;
                    break;
                }
            }

            if (!hasEnterEvent)
            {
                var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener(_ => { if (!excludedButtons.Contains(button)) PlayHover(); });
                trigger.triggers.Add(enterEntry);
            }
        }

        /// <summary>
        /// 기본 클릭/호버 사운드에서 제외 (선택지 등 전용 사운드 사용 버튼용)
        /// </summary>
        public void ExcludeButton(Button button)
        {
            if (button != null) excludedButtons.Add(button);
        }

        /// <summary>
        /// 제외 목록 초기화 (선택지 UI 닫힐 때 호출)
        /// </summary>
        public void ClearExcludedButtons()
        {
            excludedButtons.Clear();
        }

        /// <summary>
        /// 새로 생성된 UI에 버튼 바인딩 (동적 UI용)
        /// </summary>
        public void BindButtonsInTransform(Transform root)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                RegisterButton(button);
            }
        }

        #endregion

        #region 사운드 재생

        /// <summary>
        /// 호버 사운드 재생
        /// </summary>
        public void PlayHover()
        {
            PlayClip(hoverClip, hoverVolume);
        }

        /// <summary>
        /// 클릭 사운드 재생
        /// </summary>
        public void PlayClick()
        {
            PlayClip(clickClip, clickVolume);
        }

        /// <summary>
        /// 타이핑 사운드 재생 (피치+볼륨 랜덤, 전용 AudioSource 사용)
        /// Play()로 글자마다 재시작 → 키보드 타자 질감 (PlayOneShot 누적 방지)
        /// </summary>
        public void PlayTyping()
        {
            if (typingClip == null || typingSource == null) return;
            if (Time.unscaledTime - lastTypingPlayTime < typingMinInterval) return;
            lastTypingPlayTime = Time.unscaledTime;

            // 안전장치: mute/비활성 상태 복구
            if (typingSource.mute) typingSource.mute = false;

            typingSource.clip = typingClip;
            typingSource.pitch = Random.Range(minTypingPitch, maxTypingPitch);
            typingSource.volume = Random.Range(minTypingVolume, maxTypingVolume);
            typingSource.Play();
        }

        /// <summary>
        /// 볼륨 슬라이더 조작 중 샘플 사운드 예약 (디바운스)
        /// 슬라이더가 멈춘 후 volumePreviewDebounce 시간이 지나면 1회만 재생.
        /// </summary>
        /// <param name="volumeScale">0~1 볼륨 배율 (기본 1.0 = 0.8배 재생). 캐릭터 음성 슬라이더 등 개별 볼륨 미리듣기에 사용.</param>
        public void PlayVolumePreview(float volumeScale = 1f)
        {
            volumePreviewScheduledTime = Time.unscaledTime + volumePreviewDebounce;
            volumePreviewScale = Mathf.Clamp01(volumeScale);
        }

        void Update()
        {
            // 볼륨 프리뷰 디바운스 처리
            if (volumePreviewScheduledTime > 0f && Time.unscaledTime >= volumePreviewScheduledTime)
            {
                volumePreviewScheduledTime = -1f;

                var clip = volumePreviewClip != null ? volumePreviewClip
                         : clickClip != null ? clickClip
                         : dialogueNextClip;
                if (clip != null)
                    PlayClip(clip, 0.8f * volumePreviewScale);

                volumePreviewScale = 1f;
            }
        }

        /// <summary>
        /// 대사 넘김 사운드 재생
        /// </summary>
        public void PlayDialogueNext()
        {
            PlayClip(dialogueNextClip, clickVolume);
        }

        /// <summary>
        /// 선택지 선택 사운드 재생
        /// </summary>
        public void PlayChoiceSelect()
        {
            PlayClip(choiceSelectClip, clickVolume);
        }

        /// <summary>
        /// 선택지 호버 사운드 재생 (전용 클립, 없으면 기본 호버)
        /// </summary>
        public void PlayChoiceHover()
        {
            PlayClip(choiceHoverClip != null ? choiceHoverClip : hoverClip, hoverVolume);
        }

        /// <summary>
        /// 선택지 등장 사운드 재생
        /// </summary>
        public void PlayChoiceAppear()
        {
            PlayClip(choiceAppearClip, clickVolume);
        }

        /// <summary>
        /// 팝업 열기 사운드 재생
        /// </summary>
        public void PlayPopupOpen()
        {
            PlayClip(popupOpenClip, clickVolume);
        }

        /// <summary>
        /// 팝업 닫기 사운드 재생
        /// </summary>
        public void PlayPopupClose()
        {
            PlayClip(popupCloseClip, hoverVolume);
        }

        /// <summary>
        /// 세이브 완료 사운드 재생
        /// </summary>
        public void PlaySaveComplete()
        {
            PlayClip(saveCompleteClip, clickVolume);
        }

        /// <summary>
        /// 로드 완료 사운드 재생
        /// </summary>
        public void PlayLoadComplete()
        {
            PlayClip(loadCompleteClip, clickVolume);
        }

        void PlayClip(AudioClip clip, float volume)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        #endregion

        #region 앱 포커스/일시정지 처리

        void OnApplicationFocus(bool hasFocus)
        {
            // UI SFX(클릭/호버)는 매우 짧아서 포커스 복귀 시 재개할 필요 없음
            // Pause → UnPause 사이의 타이밍 갭이 오히려 latency처럼 느껴질 수 있어 Stop 사용
            if (!hasFocus)
            {
                if (audioSource != null) audioSource.Stop();
                if (typingSource != null) typingSource.Stop();
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                if (audioSource != null) audioSource.Stop();
                if (typingSource != null) typingSource.Stop();
            }
        }

        #endregion
    }
}
