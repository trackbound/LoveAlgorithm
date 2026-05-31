using LoveAlgo.Common;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Story;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사창이 사용자에 의해 Hide된 상태에서만 표시되는 복귀 버튼.
    /// DialogueUI와 분리되어 UIManager Story 그룹에 lazy spawn됨.
    /// DialogueUI.OnHiddenChanged 이벤트를 구독해 자동 토글.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class DialogueShowButton : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;

        Button button;
        DialogueUI dialogueUI;

        readonly ListenerBag _listeners = new();

        void Awake()
        {
            button = GetComponent<Button>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            _listeners.Bind(button, OnClick);
            ApplyVisible(false); // 시작은 숨김
        }

        void OnDestroy()
        {
            _listeners.Dispose();
            if (dialogueUI != null) dialogueUI.OnHiddenChanged -= OnHiddenChanged;
        }

        void OnEnable()
        {
            // 이미 Bind된 경우 구독 재개. UIManager.DialogueUI를 직접 호출하면
            // (이 버튼이 DialogueUI 동반 spawn으로 만들어졌을 때) 무한 재귀가 발생하므로 금지.
            if (dialogueUI != null)
            {
                dialogueUI.OnHiddenChanged -= OnHiddenChanged;
                dialogueUI.OnHiddenChanged += OnHiddenChanged;
                ApplyVisible(dialogueUI.IsHidden);
            }
        }

        void OnDisable()
        {
            if (dialogueUI != null)
                dialogueUI.OnHiddenChanged -= OnHiddenChanged;
        }

        /// <summary>UIManager가 DialogueUI 동반 spawn 시 주입.</summary>
        public void Bind(DialogueUI ui)
        {
            if (ui == null || ui == dialogueUI) return;
            if (dialogueUI != null)
                dialogueUI.OnHiddenChanged -= OnHiddenChanged;
            dialogueUI = ui;
            dialogueUI.OnHiddenChanged += OnHiddenChanged;
            ApplyVisible(dialogueUI.IsHidden);
        }

        void OnHiddenChanged(bool hidden)
        {
            ApplyVisible(hidden);
        }

        void ApplyVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        void OnClick()
        {
            dialogueUI?.RequestShow();
        }
    }
}
