using UnityEngine;
using LoveAlgo.Modules.Audio;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Save;
using LoveAlgo.Settings;
using LoveAlgo.Title;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 타이틀 UI
    /// </summary>
    public class TitlePanel : MonoBehaviour
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

        /// <summary>비동기 버튼 처리 중 재진입 방지</summary>
        bool isBusy;

        readonly ListenerBag _listeners = new();

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

        public void PlayTitleBGM()
        {
            if (!string.IsNullOrEmpty(titleBGM))
            {
                AudioManager.Instance?.PlayBGMAsync(titleBGM).Forget();  // 기본 3초 페이드인
            }
        }

        void SetupButtons()
        {
            _listeners.Bind(startButton, OnStartClick);
            _listeners.Bind(continueButton, OnContinueClick);
            _listeners.Bind(loadButton, OnLoadClick);
            _listeners.Bind(extraButton, OnExtraClick);
            _listeners.Bind(settingsButton, OnSettingsClick);
            _listeners.Bind(exitButton, OnExitClick);
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
            Debug.Log("[TitlePanel] Start - 새 게임 시작");
            ConfirmNewGame().Forget();
        }

        async UniTaskVoid ConfirmNewGame()
        {
            if (isBusy) return;
            isBusy = true;
            try
            {
                // 저장 데이터가 있으면 확인 팝업
                if (HasAnySaveData())
                {
                    bool confirmed = await PopupManager.Instance.ConfirmAsync(
                        "저장된 데이터가 있습니다.\n새 게임을 시작할까요?", "예", "아니오");
                    if (!confirmed) return;
                }

                GameManager.Instance?.StartNewGame();
            }
            finally
            {
                isBusy = false;
            }
        }

        void OnContinueClick()
        {
            Debug.Log("[TitlePanel] Continue - 이어하기");
            HandleContinue().Forget();
        }

        async UniTaskVoid HandleContinue()
        {
            if (isBusy) return;
            isBusy = true;
            try
            {
                bool hasAutoSave = SaveManager.Exists(SaveManager.AutoSaveSlot);

                if (hasAutoSave)
                {
                    // 자동저장 데이터 있음 → 확인 후 로드
                    bool confirmed = await PopupManager.Instance.ConfirmAsync(
                        "이 부분부터 시작할까요?", "예", "아니오");
                    if (!confirmed) return;

                    GameManager.Instance?.LoadGame(SaveManager.AutoSaveSlot);
                }
                else
                {
                    // 자동저장 데이터 없음 → 새 게임 안내
                    bool confirmed = await PopupManager.Instance.ConfirmAsync(
                        "저장된 데이터가 없습니다.\n새 게임을 시작할까요?", "예", "아니오");
                    if (!confirmed) return;

                    GameManager.Instance?.StartNewGame();
                }
            }
            finally
            {
                isBusy = false;
            }
        }

        void OnLoadClick()
        {
            Debug.Log("[TitlePanel] Load - 불러오기");
            Services.TryGet<ISave>()?.ShowLoadUI();
        }

        void OnSettingsClick()
        {
            Debug.Log("[TitlePanel] Settings - 설정");
            Services.TryGet<ISettings>()?.ShowSettingsUI();
        }

        void OnExtraClick()
        {
            Debug.Log("[TitlePanel] Extra - 엑스트라");
            Services.TryGet<ITitle>()?.ShowExtraUI();
        }

        void OnExitClick()
        {
            Debug.Log("[TitlePanel] Exit - 종료");
            ConfirmExit().Forget();
        }

        async UniTaskVoid ConfirmExit()
        {
            if (isBusy) return;
            isBusy = true;
            try
            {
                bool confirmed = await PopupManager.Instance.ConfirmAsync("게임을 종료하시겠습니까?");
                
                if (confirmed)
                {
                    QuitGame();
                }
            }
            finally
            {
                isBusy = false;
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
            // Continue 버튼은 항상 활성화 (자동저장 없으면 새 게임 안내)
            if (continueButton != null)
                continueButton.interactable = true;
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

        void OnDestroy()
        {
            _listeners.Dispose();

            // 데코 CanvasGroup DOTween 정리
            GameObject[] decos = { decoNormal, decoStart, decoContinue, decoLoad, decoExtra, decoSettings, decoExit };
            foreach (var d in decos)
            {
                if (d != null)
                {
                    var cg = d.GetComponent<CanvasGroup>();
                    if (cg != null) cg.DOKill();
                }
            }
        }
    }
}
