using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI;
using DG.Tweening;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 향상된 선택지 UI (기존 ChoiceUI를 대체하거나 병행 사용)
    /// - EnhancedChoiceButton 사용
    /// - 순차 등장 애니메이션
    /// - 선택 확정 시 다른 선택지 페이드아웃
    /// - 인터랙션 피드백 통합
    /// </summary>
    public class EnhancedChoiceUI : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] Transform buttonContainer;
        [SerializeField] GameObject buttonPrefab;  // EnhancedChoiceButton 컴포넌트 포함
        [SerializeField] CanvasGroup canvasGroup;

        [Header("설정")]
        [SerializeField] float fadeDuration = 0.2f;
        [SerializeField] float staggerDelay = 0.1f;  // 순차 등장 간격
        [SerializeField] float selectionFadeoutDuration = 0.4f;

        [Header("인터랙션")]
        [SerializeField] bool useInteractionFeedback = true;

        List<GameObject> spawnedButtons = new();
        List<EnhancedChoiceButton> enhancedButtons = new();
        int selectedIndex = -1;
        bool isWaitingForChoice;

        void Awake()
        {
            Hide();
        }

        /// <summary>
        /// 선택지 표시 및 선택 대기
        /// </summary>
        public async UniTask<ChoiceResult> ShowAndWaitAsync(List<OptionData> options, CancellationToken ct)
        {
            // 조건 필터링
            var validOptions = FilterOptions(options);

            if (validOptions.Count == 0)
            {
                Debug.LogWarning("[EnhancedChoiceUI] 표시할 선택지가 없습니다.");
                return null;
            }

            // 버튼 생성
            ClearButtons();
            CreateButtons(validOptions);

            // 패널 페이드인
            await ShowAsync(ct);

            // 버튼 순차 등장 (stagger)
            for (int i = 0; i < enhancedButtons.Count; i++)
            {
                var btn = enhancedButtons[i];
                btn.SetEntranceDelay(i * staggerDelay);
            }

            // 첫 번째 버튼 등장 시작 후 전체 등장 대기
            float totalEntranceTime = enhancedButtons.Count * staggerDelay + 0.4f;  // entrance duration
            await UniTask.Delay(TimeSpan.FromSeconds(totalEntranceTime), cancellationToken: ct);

            // 선택 대기
            selectedIndex = -1;
            isWaitingForChoice = true;

            await UniTask.WaitUntil(() => selectedIndex >= 0, cancellationToken: ct);

            isWaitingForChoice = false;

            // 선택된 버튼 애니메이션 + 나머지 페이드아웃
            await PlaySelectionAnimations(ct);

            // 숨김
            await HideAsync(ct);

            // 결과 반환
            var selectedOption = validOptions[selectedIndex];

            // 효과 적용
            ApplyEffects(selectedOption.Effects);

            return new ChoiceResult
            {
                SelectedIndex = selectedIndex,
                JumpTarget = selectedOption.JumpTarget
            };
        }

        /// <summary>
        /// 조건에 맞는 선택지만 필터링
        /// </summary>
        List<OptionData> FilterOptions(List<OptionData> options)
        {
            var result = new List<OptionData>();

            foreach (var option in options)
            {
                if (string.IsNullOrEmpty(option.Condition))
                {
                    result.Add(option);
                }
                else if (GameState.Instance != null && GameState.Instance.EvaluateCondition(option.Condition))
                {
                    result.Add(option);
                }
                else if (GameState.Instance == null)
                {
                    // GameState 없으면 일단 표시
                    result.Add(option);
                }
            }

            return result;
        }

        /// <summary>
        /// 버튼 생성
        /// </summary>
        void CreateButtons(List<OptionData> options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var buttonObj = Instantiate(buttonPrefab, buttonContainer);
                buttonObj.SetActive(true);

                // EnhancedChoiceButton 설정
                var enhancedBtn = buttonObj.GetComponent<EnhancedChoiceButton>();
                if (enhancedBtn != null)
                {
                    enhancedBtn.SetText(option.ButtonText);
                    enhancedButtons.Add(enhancedBtn);
                }
                else
                {
                    // 폴백: HoverButton
                    var hoverButton = buttonObj.GetComponent<HoverButton>();
                    if (hoverButton != null)
                    {
                        hoverButton.SetText(option.ButtonText);
                    }
                    else
                    {
                        var text = buttonObj.GetComponentInChildren<TMP_Text>();
                        if (text != null)
                            text.text = option.ButtonText;
                    }
                }

                // 클릭 이벤트
                int index = i;
                var button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => {
                        OnButtonClicked(index);
                    });
                }

                spawnedButtons.Add(buttonObj);
            }
        }

        /// <summary>
        /// 버튼 클릭 처리
        /// </summary>
        void OnButtonClicked(int index)
        {
            if (!isWaitingForChoice) return;

            // 인터랙션 피드백
            if (useInteractionFeedback && InteractionFeedbackManager.Instance != null)
            {
                // 클릭 위치 가져오기
                if (index < spawnedButtons.Count)
                {
                    var btnRect = spawnedButtons[index].GetComponent<RectTransform>();
                    if (btnRect != null)
                    {
                        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, btnRect.position);
                        InteractionFeedbackManager.Instance.PlayChoiceSelectionFeedback(screenPos);
                    }
                }
            }

            selectedIndex = index;
        }

        /// <summary>
        /// 선택 확정 애니메이션
        /// </summary>
        async UniTask PlaySelectionAnimations(CancellationToken ct)
        {
            if (selectedIndex < 0 || selectedIndex >= enhancedButtons.Count) return;

            var selectedButton = enhancedButtons[selectedIndex];

            // 선택된 버튼 애니메이션
            selectedButton.PlaySelectionAnimation();

            // 나머지 버튼 페이드아웃
            var fadeoutTasks = new List<UniTask>();
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                if (i == selectedIndex) continue;

                var btnObj = spawnedButtons[i];
                var cg = btnObj.GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = btnObj.AddComponent<CanvasGroup>();

                fadeoutTasks.Add(cg.DOFade(0f, selectionFadeoutDuration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct));
            }

            try
            {
                await UniTask.WhenAll(fadeoutTasks);
            }
            catch (OperationCanceledException)
            {
                // 취소됨
            }
        }

        /// <summary>
        /// 효과 적용
        /// </summary>
        void ApplyEffects(List<string> effects)
        {
            if (GameState.Instance == null || effects == null) return;

            foreach (var effect in effects)
            {
                ApplyEffect(effect);
            }
        }

        void ApplyEffect(string effect)
        {
            if (string.IsNullOrEmpty(effect)) return;

            var parts = effect.Split(':');
            if (parts.Length < 2) return;

            string type = parts[0];

            switch (type)
            {
                case "Love":
                case "AddLove":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int loveValue))
                    {
                        GameState.Instance.AddLove(parts[1], loveValue);
                    }
                    break;

                case "Stat":
                case "AddStat":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int statValue))
                    {
                        GameState.Instance.AddStat(parts[1], statValue);
                    }
                    break;

                case "Flag":
                case "Set":
                    if (parts.Length >= 3)
                    {
                        bool flagValue = parts[2].ToLower() == "true";
                        GameState.Instance.SetFlag(parts[1], flagValue);
                    }
                    else if (parts.Length >= 2)
                    {
                        GameState.Instance.SetFlag(parts[1], true);
                    }
                    break;

                case "Money":
                case "AddMoney":
                    if (int.TryParse(parts[1], out int moneyValue))
                    {
                        GameState.Instance.AddMoney(moneyValue);
                    }
                    break;

                case "SFX":
                    if (parts.Length >= 2)
                    {
                        AudioManager.Instance?.PlaySFX(parts[1]);
                    }
                    break;
            }
        }

        /// <summary>
        /// 버튼 정리
        /// </summary>
        void ClearButtons()
        {
            foreach (var btn in spawnedButtons)
            {
                Destroy(btn);
            }
            spawnedButtons.Clear();
            enhancedButtons.Clear();
        }

        #region 표시/숨김

        public void Show()
        {
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        async UniTask ShowAsync(CancellationToken ct)
        {
            gameObject.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                await canvasGroup.DOFade(1f, fadeDuration).ToUniTask(cancellationToken: ct);
            }
        }

        async UniTask HideAsync(CancellationToken ct)
        {
            if (canvasGroup != null)
            {
                await canvasGroup.DOFade(0f, fadeDuration).ToUniTask(cancellationToken: ct);
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                gameObject.SetActive(false);
            }

            ClearButtons();
        }

        #endregion
    }
}
