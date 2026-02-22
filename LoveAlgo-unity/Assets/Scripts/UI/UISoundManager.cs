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
        [SerializeField] float typingMinInterval = 0.03f; // 텍스트 속도와 무관하게 최소 간격 보장

        [Header("볼륨 프리뷰")]
        [SerializeField] float volumePreviewInterval = 0.08f;


        [Header("자동 바인딩")]
        [SerializeField] bool autoBindButtons = true;

        AudioSource audioSource;
        /// <summary>
        /// 타이핑 전용 AudioSource (피치 변경 시 다른 사운드 영향 방지)
        /// </summary>
        AudioSource typingSource;
        HashSet<Button> registeredButtons = new HashSet<Button>();
        float lastTypingPlayTime = -999f;
        float lastVolumePreviewTime = -999f;

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
        }

        #region 버튼 자동 바인딩

        /// <summary>
        /// 씬의 모든 버튼에 사운드 바인딩
        /// </summary>
        public void BindAllButtons()
        {
            // 비활성화된 오브젝트 포함해서 모든 버튼 찾기
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

            // 클릭 사운드
            button.onClick.AddListener(() => PlayClick());

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
                    entry.callback.AddListener(_ => PlayHover());
                    hasEnterEvent = true;
                    break;
                }
            }

            if (!hasEnterEvent)
            {
                var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener(_ => PlayHover());
                trigger.triggers.Add(enterEntry);
            }
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

            typingSource.clip = typingClip;
            typingSource.pitch = Random.Range(minTypingPitch, maxTypingPitch);
            typingSource.volume = Random.Range(minTypingVolume, maxTypingVolume);
            typingSource.Play();
        }

        /// <summary>
        /// 볼륨 슬라이더 조작 중 샘플 사운드 재생 (과도한 연속 재생 방지)
        /// </summary>
        public void PlayVolumePreview()
        {
            if (Time.unscaledTime - lastVolumePreviewTime < volumePreviewInterval) return;
            lastVolumePreviewTime = Time.unscaledTime;

            var clip = clickClip != null ? clickClip : dialogueNextClip;
            if (clip == null) return;
            PlayClip(clip, clickVolume);
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
        /// 선택지 등장 사운드 재생
        /// </summary>
        public void PlayChoiceAppear()
        {
            PlayClip(choiceAppearClip, clickVolume);
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
