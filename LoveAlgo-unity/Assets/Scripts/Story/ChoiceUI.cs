using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 선택지 UI
    /// </summary>
    public class ChoiceUI : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] Transform buttonContainer;
        [SerializeField] GameObject buttonPrefab;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("설정")]
        [SerializeField] float fadeDuration = 0.2f;

        List<GameObject> spawnedButtons = new();
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
                Debug.LogWarning("[ChoiceUI] 표시할 선택지가 없습니다.");
                return null;
            }

            // 버튼 생성
            ClearButtons();
            CreateButtons(validOptions);

            // 표시
            await ShowAsync(ct);

            // 선택 대기
            selectedIndex = -1;
            isWaitingForChoice = true;

            await UniTask.WaitUntil(() => selectedIndex >= 0, cancellationToken: ct);

            isWaitingForChoice = false;

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

                // 텍스트 설정
                var text = buttonObj.GetComponentInChildren<TMP_Text>();
                if (text != null)
                {
                    text.text = option.ButtonText;
                }

                // 클릭 이벤트
                int index = i;
                var button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => {
                        LoveAlgo.UI.UISoundManager.Instance?.PlayChoiceSelect();
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
            selectedIndex = index;
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
                    // Love:Character:Value
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int loveValue))
                    {
                        GameState.Instance.AddLove(parts[1], loveValue);
                    }
                    break;

                case "Stat":
                    // Stat:StatName:Value
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int statValue))
                    {
                        GameState.Instance.AddStat(parts[1], statValue);
                    }
                    break;

                case "Flag":
                    // Flag:Name:true/false
                    if (parts.Length >= 3)
                    {
                        bool flagValue = parts[2].ToLower() == "true";
                        GameState.Instance.SetFlag(parts[1], flagValue);
                    }
                    else if (parts.Length >= 2)
                    {
                        // Flag:Name (기본 true)
                        GameState.Instance.SetFlag(parts[1], true);
                    }
                    break;

                case "Money":
                    // Money:Value
                    if (int.TryParse(parts[1], out int moneyValue))
                    {
                        GameState.Instance.AddMoney(moneyValue);
                    }
                    break;

                case "SFX":
                    // SFX:Name
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

    /// <summary>
    /// Option 파싱 데이터
    /// </summary>
    public class OptionData
    {
        public string ButtonText;
        public string JumpTarget;
        public List<string> Effects = new();
        public string Condition;

        /// <summary>
        /// Option Value 파싱
        /// 형식: 버튼텍스트|점프대상|효과1|효과2|...|if:조건
        /// </summary>
        public static OptionData Parse(string value)
        {
            var data = new OptionData();
            var parts = value.Split('|');

            if (parts.Length >= 1)
                data.ButtonText = parts[0];

            if (parts.Length >= 2)
                data.JumpTarget = parts[1];

            // 3번째부터: 효과 또는 조건
            for (int i = 2; i < parts.Length; i++)
            {
                string part = parts[i].Trim();

                if (part.StartsWith("if:", StringComparison.OrdinalIgnoreCase))
                {
                    // 조건
                    data.Condition = part.Substring(3);
                }
                else
                {
                    // 효과
                    data.Effects.Add(part);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// 선택 결과
    /// </summary>
    public class ChoiceResult
    {
        public int SelectedIndex;
        public string JumpTarget;
    }
}
