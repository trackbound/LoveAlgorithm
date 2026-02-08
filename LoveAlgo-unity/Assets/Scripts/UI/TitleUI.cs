using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;
using LoveAlgo.Core;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 타이틀 UI
    /// </summary>
    public class TitleUI : MonoBehaviour
    {
        [Header("메뉴 버튼")]
        [SerializeField] Button startButton;
        [SerializeField] Button continueButton;
        [SerializeField] Button loadButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        [Header("타이틀 BGM")]
        [SerializeField] string titleBGM = "Title";

        [Header("데코 오브젝트 (선택)")]
        [SerializeField] GameObject decoNormal;
        [SerializeField] GameObject decoStart;
        [SerializeField] GameObject decoContinue;
        [SerializeField] GameObject decoLoad;
        [SerializeField] GameObject decoSettings;
        [SerializeField] GameObject decoExit;

        GameObject currentDeco;

        void Start()
        {
            SetupButtons();
            SetupHoverEvents();
            ShowDeco(decoNormal);
            UpdateContinueButton();
            PlayTitleBGM();
        }

        void OnEnable()
        {
            // 타이틀로 돌아올 때 Continue 버튼 상태 갱신
            UpdateContinueButton();
            PlayTitleBGM();
        }

        void PlayTitleBGM()
        {
            if (!string.IsNullOrEmpty(titleBGM))
            {
                AudioManager.Instance?.PlayBGMAsync(titleBGM).Forget();  // 기본 3초 페이드인
            }
        }

        void SetupButtons()
        {
            startButton?.onClick.AddListener(OnStartClick);
            continueButton?.onClick.AddListener(OnContinueClick);
            loadButton?.onClick.AddListener(OnLoadClick);
            settingsButton?.onClick.AddListener(OnSettingsClick);
            exitButton?.onClick.AddListener(OnExitClick);
        }

        void SetupHoverEvents()
        {
            AddHoverEvent(startButton, decoStart);
            AddHoverEvent(continueButton, decoContinue);
            AddHoverEvent(loadButton, decoLoad);
            AddHoverEvent(settingsButton, decoSettings);
            AddHoverEvent(exitButton, decoExit);
        }

        void AddHoverEvent(Button button, GameObject deco)
        {
            if (button == null) return;

            var trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            // Pointer Enter
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ => ShowDeco(deco));
            trigger.triggers.Add(enterEntry);

            // Pointer Exit
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ => ShowDeco(decoNormal));
            trigger.triggers.Add(exitEntry);
        }

        #region 데코 관리

        void ShowDeco(GameObject deco)
        {
            if (currentDeco == deco) return;

            // 모든 Deco 비활성화
            decoNormal?.SetActive(false);
            decoStart?.SetActive(false);
            decoContinue?.SetActive(false);
            decoLoad?.SetActive(false);
            decoSettings?.SetActive(false);
            decoExit?.SetActive(false);

            // 현재 Deco 활성화
            deco?.SetActive(true);
            currentDeco = deco;
        }

        #endregion

        #region 버튼 핸들러

        void OnStartClick()
        {
            Debug.Log("[TitleUI] Start - 새 게임 시작");
            GameManager.Instance?.StartNewGame();
        }

        void OnContinueClick()
        {
            Debug.Log("[TitleUI] Continue - 이어하기");
            GameManager.Instance?.ContinueGame();
        }

        void OnLoadClick()
        {
            Debug.Log("[TitleUI] Load - 불러오기");
            
            PopupManager.Instance?.ShowLoadPopup(slot =>
            {
                GameManager.Instance?.LoadGame(slot);
                PopupManager.Instance?.Toast("로드 완료", $"슬롯 {slot}");
            });
        }

        void OnSettingsClick()
        {
            Debug.Log("[TitleUI] Settings - 설정");
            PopupManager.Instance?.ShowSettings();
        }

        void OnExitClick()
        {
            Debug.Log("[TitleUI] Exit - 종료");
            ConfirmExit().Forget();
        }

        async UniTaskVoid ConfirmExit()
        {
            bool confirmed = await PopupManager.Instance.ConfirmAsync("게임을 종료하시겠습니까?");
            
            if (confirmed)
            {
                QuitGame();
            }
        }

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region Continue 버튼 상태

        void UpdateContinueButton()
        {
            bool hasSave = HasAnySaveData();

            if (continueButton != null)
                continueButton.interactable = hasSave;
        }

        bool HasAnySaveData()
        {
            // 슬롯 0~29 중 하나라도 세이브가 있는지
            for (int i = 0; i < 30; i++)
            {
                if (SaveManager.Exists(i))
                    return true;
            }
            return false;
        }

        int FindLatestSaveSlot()
        {
            // 가장 최근 세이브 슬롯 찾기
            int latestSlot = -1;
            System.DateTime latestTime = System.DateTime.MinValue;

            for (int i = 0; i < 30; i++)
            {
                if (!SaveManager.Exists(i)) continue;

                var data = SaveManager.Load(i);
                if (data != null && data.SaveTime > latestTime)
                {
                    latestTime = data.SaveTime;
                    latestSlot = i;
                }
            }

            return latestSlot;
        }

        #endregion
    }
}
