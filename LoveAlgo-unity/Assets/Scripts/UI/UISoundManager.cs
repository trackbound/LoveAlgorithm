using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace LoveAlgo.UI
{
    /// <summary>
    /// UI 사운드 매니저 - UI 전용 AudioSource로 분리
    /// 게임 SFX와 충돌 없이 독립적으로 재생
    /// </summary>
    public class UISoundManager : MonoBehaviour
    {
        public static UISoundManager Instance { get; private set; }

        [Header("UI 사운드 클립")]
        [SerializeField] AudioClip hoverClip;
        [SerializeField] AudioClip clickClip;
        [SerializeField] AudioClip typingClip;
        [SerializeField] AudioClip dialogueNextClip;
        [SerializeField] AudioClip choiceSelectClip;
        [SerializeField] AudioClip choiceAppearClip;

        [Header("사운드 이름 (Resources/Audio/SFX/)")]
        [SerializeField] string hoverSFXName = "Hover";
        [SerializeField] string clickSFXName = "Click";
        [SerializeField] string typingSFXName = "Type";

        [Header("볼륨 설정")]
        [SerializeField] [Range(0f, 1f)] float uiVolume = 1f;
        [SerializeField] float hoverVolume = 0.5f;
        [SerializeField] float clickVolume = 0.7f;
        [SerializeField] float typingVolume = 0.5f;

        [Header("타이핑 피치 설정")]
        [SerializeField] float minTypingPitch = 0.9f;
        [SerializeField] float maxTypingPitch = 1.1f;

        [Header("자동 바인딩")]
        [SerializeField] bool autoBindButtons = true;

        AudioSource audioSource;
        HashSet<Button> registeredButtons = new HashSet<Button>();

        /// <summary>
        /// UI 전체 볼륨 (설정에서 조절용)
        /// </summary>
        public float UIVolume
        {
            get => uiVolume;
            set
            {
                uiVolume = Mathf.Clamp01(value);
                if (audioSource != null)
                    audioSource.volume = uiVolume;
            }
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                SetupAudioSource();
                LoadClips();
            }
            else
            {
                Destroy(gameObject);
            }
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
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.volume = uiVolume;
        }

        void LoadClips()
        {
            if (hoverClip == null && !string.IsNullOrEmpty(hoverSFXName))
                hoverClip = Resources.Load<AudioClip>($"Audio/SFX/{hoverSFXName}");
            if (clickClip == null && !string.IsNullOrEmpty(clickSFXName))
                clickClip = Resources.Load<AudioClip>($"Audio/SFX/{clickSFXName}");
            if (typingClip == null && !string.IsNullOrEmpty(typingSFXName))
                typingClip = Resources.Load<AudioClip>($"Audio/SFX/{typingSFXName}");
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
        /// 타이핑 사운드 재생 (피치 랜덤)
        /// </summary>
        public void PlayTyping()
        {
            if (typingClip == null || audioSource == null) return;
            
            audioSource.pitch = Random.Range(minTypingPitch, maxTypingPitch);
            audioSource.PlayOneShot(typingClip, typingVolume * uiVolume);
            audioSource.pitch = 1f;  // 다른 사운드를 위해 복원
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
                audioSource.PlayOneShot(clip, volume * uiVolume);
            }
        }

        #endregion

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
