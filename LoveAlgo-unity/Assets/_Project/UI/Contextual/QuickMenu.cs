using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Phone;
using LoveAlgo.Save;
using LoveAlgo.Settings;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 스케줄/상점 화면 우측 퀵 메뉴.
    /// ButtonEX(Toggle 모드) + 슬라이드 패널.
    /// </summary>
    public class QuickMenu : MonoBehaviour
    {
        [Header("토글 버튼 (ButtonEX Toggle 모드)")]
        [SerializeField] Button toggleButton;
        [SerializeField] ButtonEX toggleButtonEX;

        [Header("메뉴 패널")]
        [SerializeField] RectTransform menuPanel;
        [SerializeField] CanvasGroup menuCanvasGroup;
        [SerializeField] float slideDuration = 0.25f;

        [Header("메뉴 버튼")]
        [SerializeField] Button titleButton;
        [SerializeField] Button phoneButton;
        [SerializeField] Button saveButton;
        [SerializeField] Button loadButton;
        [SerializeField] Button configButton;
        [SerializeField] Button exitButton;

        [Header("돌아가기")]
        [SerializeField] Button backButton;

        /// <summary>돌아가기 콜백 (ScheduleUI가 등록)</summary>
        public event System.Action OnBackRequested;

        bool isOpen;
        float hiddenY;   // 접힌 상태 Y (아래로 내려감)
        float shownY;    // 펼친 상태 Y
        Sequence activeSeq;

        readonly ListenerBag _listeners = new();

        void Awake()
        {
            if (menuPanel != null)
            {
                shownY = menuPanel.anchoredPosition.y;
                hiddenY = shownY - menuPanel.rect.height;
            }

            SetClosed(immediate: true);

            _listeners.Bind(toggleButton, Toggle);
            _listeners.Bind(backButton, CloseAndBack);
            _listeners.Bind(titleButton, CloseAndTitle);
            _listeners.Bind(phoneButton, CloseAndPhone);
            _listeners.Bind(saveButton, CloseAndSave);
            _listeners.Bind(loadButton, CloseAndLoad);
            _listeners.Bind(configButton, CloseAndConfig);
            _listeners.Bind(exitButton, CloseAndExit);
        }

        void OnDestroy()
        {
            _listeners.Dispose();
            activeSeq?.Kill();
            menuPanel?.DOKill();
        }

        void CloseAndBack() { Close(); OnBackRequested?.Invoke(); }
        void CloseAndTitle() { Close(); OnTitle(); }
        void CloseAndPhone() { Close(); OnPhone(); }
        void CloseAndSave() { Close(); OnSave(); }
        void CloseAndLoad() { Close(); OnLoad(); }
        void CloseAndConfig() { Close(); OnConfig(); }
        void CloseAndExit() { Close(); OnExit(); }

        // ── 토글 ──────────────────────────────────

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;
            toggleButtonEX?.SetToggle(true);

            activeSeq?.Kill();
            menuPanel?.DOKill();

            if (menuPanel != null)
            {
                menuPanel.gameObject.SetActive(true);
                menuPanel.localScale = new Vector3(1f, 0.3f, 1f);
                var pos = menuPanel.anchoredPosition;
                pos.y = hiddenY;
                menuPanel.anchoredPosition = pos;
            }
            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 0f;
                menuCanvasGroup.interactable = true;
                menuCanvasGroup.blocksRaycasts = true;
            }

            var seq = DOTween.Sequence();
            if (menuPanel != null)
            {
                seq.Join(menuPanel.DOAnchorPosY(shownY, slideDuration).SetEase(Ease.OutBack, 1.1f));
                seq.Join(menuPanel.DOScale(Vector3.one, slideDuration).SetEase(Ease.OutBack, 1.1f));
            }
            if (menuCanvasGroup != null)
                seq.Join(menuCanvasGroup.DOFade(1f, slideDuration * 0.6f).SetEase(Ease.OutQuad));
            activeSeq = seq;
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            toggleButtonEX?.SetToggle(false);

            activeSeq?.Kill();
            menuPanel?.DOKill();

            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.interactable = false;
                menuCanvasGroup.blocksRaycasts = false;
            }

            var seq = DOTween.Sequence();
            if (menuPanel != null)
            {
                seq.Join(menuPanel.DOAnchorPosY(hiddenY, slideDuration).SetEase(Ease.InBack, 1.1f));
                seq.Join(menuPanel.DOScale(new Vector3(1f, 0.3f, 1f), slideDuration).SetEase(Ease.InBack, 1.1f));
            }
            if (menuCanvasGroup != null)
                seq.Join(menuCanvasGroup.DOFade(0f, slideDuration).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                if (menuPanel != null) menuPanel.gameObject.SetActive(false);
            });
            activeSeq = seq;
        }

        void SetClosed(bool immediate)
        {
            isOpen = false;
            toggleButtonEX?.SetToggle(false);
            activeSeq?.Kill();

            if (immediate && menuPanel != null)
            {
                var pos = menuPanel.anchoredPosition;
                pos.y = hiddenY;
                menuPanel.anchoredPosition = pos;
                menuPanel.localScale = new Vector3(1f, 0.3f, 1f);
                menuPanel.gameObject.SetActive(false);
            }
            if (immediate && menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 0f;
                menuCanvasGroup.interactable = false;
                menuCanvasGroup.blocksRaycasts = false;
            }
        }

        void OnEnable()
        {
            SetClosed(immediate: true);
        }

        // ── 버튼 핸들러 ───────────────────────────

        void OnTitle()
        {
            PopupManager.Instance?.Confirm("타이틀로 돌아가시겠습니까?",
                () => GameManager.Instance?.GoToTitle(),
                null);
        }

        void OnPhone()
        {
            Services.TryGet<IPhone>()?.ShowPhoneUI();
        }

        void OnSave()
        {
            Services.TryGet<ISave>()?.ShowSaveUI();
        }

        void OnLoad()
        {
            Services.TryGet<ISave>()?.ShowLoadUI();
        }

        void OnConfig()
        {
            Services.TryGet<ISettings>()?.ShowSettingsUI();
        }

        void OnExit()
        {
            OnExitAsync();
        }

        async void OnExitAsync()
        {
            bool confirmed = await PopupManager.Instance.ConfirmAsync("게임을 종료하시겠습니까?");
            if (confirmed)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
