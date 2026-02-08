using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        [SerializeField] Button extraButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        [Header("호버 텍스트 (자식 오브젝트)")]
        [SerializeField] GameObject hoverTextStart;
        [SerializeField] GameObject hoverTextContinue;
        [SerializeField] GameObject hoverTextLoad;
        [SerializeField] GameObject hoverTextExtra;
        [SerializeField] GameObject hoverTextSettings;
        [SerializeField] GameObject hoverTextExit;

        [Header("타이틀 BGM")]
        [SerializeField] string titleBGM = "Title";

        [Header("데코 오브젝트 (선택)")]
        [SerializeField] GameObject decoNormal;
        [SerializeField] GameObject decoStart;
        [SerializeField] GameObject decoContinue;
        [SerializeField] GameObject decoLoad;
        [SerializeField] GameObject decoExtra;
        [SerializeField] GameObject decoSettings;
        [SerializeField] GameObject decoExit;

        GameObject currentDeco;

        [Header("데코 크로스페이드")]
        [SerializeField] float decoCrossfadeDuration = 0.2f;

        void Start()
        {
            // 초기 호버 텍스트 비활성화
            SetHoverText(hoverTextStart, false);
            SetHoverText(hoverTextContinue, false);
            SetHoverText(hoverTextLoad, false);
            SetHoverText(hoverTextExtra, false);
            SetHoverText(hoverTextSettings, false);
            SetHoverText(hoverTextExit, false);

            SetupButtons();
            SetupHoverEvents();
            ShowDecoImmediate(decoNormal);
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
            extraButton?.onClick.AddListener(OnExtraClick);
            settingsButton?.onClick.AddListener(OnSettingsClick);
            exitButton?.onClick.AddListener(OnExitClick);
        }

        void SetupHoverEvents()
        {
            AddHoverEvent(startButton, decoStart, hoverTextStart);
            AddHoverEvent(continueButton, decoContinue, hoverTextContinue);
            AddHoverEvent(loadButton, decoLoad, hoverTextLoad);
            AddHoverEvent(extraButton, decoExtra, hoverTextExtra);
            AddHoverEvent(settingsButton, decoSettings, hoverTextSettings);
            AddHoverEvent(exitButton, decoExit, hoverTextExit);
        }

        void AddHoverEvent(Button button, GameObject deco, GameObject hoverText)
        {
            if (button == null) return;

            var trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            // Pointer Enter
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ => {
                ShowDeco(deco);
                SetHoverText(hoverText, true);
            });
            trigger.triggers.Add(enterEntry);

            // Pointer Exit
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener(_ => {
                ShowDeco(decoNormal);
                SetHoverText(hoverText, false);
            });
            trigger.triggers.Add(exitEntry);
        }

        void SetHoverText(GameObject hoverText, bool active)
        {
            if (hoverText == null) return;
            hoverText.SetActive(active);
        }

        #region 데코 관리

        void ShowDeco(GameObject deco)
        {
            if (currentDeco == deco) return;

            var oldDeco = currentDeco;
            currentDeco = deco;

            // 새 데코 페이드인
            if (deco != null)
            {
                deco.SetActive(true);
                var newCG = GetOrAddCanvasGroup(deco);
                newCG.alpha = 0f;
                newCG.DOKill();
                newCG.DOFade(1f, decoCrossfadeDuration).SetEase(Ease.OutQuad);
            }

            // 이전 데코 페이드아웃
            if (oldDeco != null && oldDeco != deco)
            {
                var oldCG = GetOrAddCanvasGroup(oldDeco);
                oldCG.DOKill();
                oldCG.DOFade(0f, decoCrossfadeDuration)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() => oldDeco.SetActive(false));
            }
        }

        CanvasGroup GetOrAddCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        /// <summary>
        /// 초기화 시 즉시 표시 (페이드 없음)
        /// </summary>
        void ShowDecoImmediate(GameObject deco)
        {
            // 모든 Deco 비활성화
            HideAllDecos();

            if (deco != null)
            {
                deco.SetActive(true);
                var cg = GetOrAddCanvasGroup(deco);
                cg.alpha = 1f;
            }
            currentDeco = deco;
        }

        void HideAllDecos()
        {
            GameObject[] decos = { decoNormal, decoStart, decoContinue, decoLoad, decoExtra, decoSettings, decoExit };
            foreach (var d in decos)
            {
                if (d != null)
                {
                    d.SetActive(false);
                    var cg = d.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 0f;
                }
            }
        }

        #endregion

        #region 버튼 핸들러

        void OnStartClick()
        {
            Debug.Log("[TitleUI] Start - 새 게임 시작");
            ConfirmNewGame().Forget();
        }

        async UniTaskVoid ConfirmNewGame()
        {
            // 세이브 데이터가 있으면 확인 팝업
            if (HasAnySaveData())
            {
                bool confirmed = await PopupManager.Instance.ConfirmAsync(
                    "새 게임을 시작하면\n저장되지 않은 진행이 사라집니다.\n계속하시겠습니까?");
                if (!confirmed) return;
            }

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

        void OnExtraClick()
        {
            Debug.Log("[TitleUI] Extra - 엑스트라");
            PopupManager.Instance?.ShowModal<ExtraPopup>();
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
