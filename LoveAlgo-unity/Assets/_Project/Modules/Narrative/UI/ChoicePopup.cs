using LoveAlgo.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using LoveAlgo.Modules.Audio;
using UnityEngine.UI;
using LoveAlgo.Common;
using LoveAlgo.Modules.Stats;
using LoveAlgo.UI;
using DG.Tweening;
using LoveAlgo.Core;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 선택지 UI
    /// </summary>
    public class ChoicePopup : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] Transform buttonContainer;
        [SerializeField] GameObject buttonPrefab;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("설정")]
        [SerializeField] float fadeDuration = 0.2f;

        [Header("선택지 애니메이션")]
        [SerializeField] float choiceAppearDuration = 0.3f;
        [SerializeField] Ease choiceAppearEase = Ease.OutQuad;

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
                Debug.LogWarning("[ChoicePopup] 표시할 선택지가 없습니다.");
                return null;
            }

            // 버튼 생성
            ClearButtons();
            CreateButtons(validOptions);

            // 버튼 초기 상태: 투명 (패널 페이드인 전에 세팅)
            foreach (var btn in spawnedButtons)
            {
                var cg = btn.GetComponent<CanvasGroup>();
                if (cg == null) cg = btn.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }

            // 패널 페이드인
            await ShowAsync(ct);

            // 전체 선택지 동시 페이드인 (스케일 없이 깔끔하게)
            foreach (var btn in spawnedButtons)
            {
                var cg = btn.GetComponent<CanvasGroup>();
                if (cg != null)
                    _ = cg.DOFade(1f, choiceAppearDuration).SetEase(choiceAppearEase);
            }
            await UniTask.Delay(TimeSpan.FromSeconds(choiceAppearDuration), cancellationToken: ct);

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
            var soundMgr = LoveAlgo.UI.UISoundManager.Instance;

            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var buttonObj = Instantiate(buttonPrefab, buttonContainer);
                buttonObj.SetActive(true);

                SetButtonText(buttonObj, option.ButtonText);

                // 클릭 이벤트
                int index = i;
                var button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    // 기본 UI 클릭/호버 사운드에서 제외
                    soundMgr?.ExcludeButton(button);

                    button.onClick.AddListener(() => {
                        soundMgr?.PlayChoiceSelect();
                        OnButtonClicked(index);
                    });

                    // 선택지 전용 호버 사운드
                    var trigger = buttonObj.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                    if (trigger == null)
                        trigger = buttonObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                    var hoverEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                        { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                    hoverEntry.callback.AddListener(_ => soundMgr?.PlayChoiceHover());
                    trigger.triggers.Add(hoverEntry);
                }

                spawnedButtons.Add(buttonObj);
            }
        }

        void SetButtonText(GameObject buttonObj, string text)
        {
            if (buttonObj == null) return;

            // ButtonEX의 ChildSwap 텍스트를 먼저 갱신
            var buttonEX = buttonObj.GetComponent<ButtonEX>();
            buttonEX?.SetText(text);

            // 프리팹 구조와 무관하게 모든 TMP_Text를 동일 값으로 맞춘다.
            // 상태별 텍스트가 여러 개인 버튼에서도 문구 불일치를 막는다.
            var texts = buttonObj.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                    texts[i].text = text;
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
                case "AddLove":    // Command 별칭
                    // Love:Character:Value
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int loveValue))
                    {
                        GameState.Instance.AddLove(parts[1], loveValue);
                    }
                    break;

                case "Stat":
                case "AddStat":    // Command 별칭
                    // Stat:StatName:Value
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int statValue))
                    {
                        var stats = Services.TryGet<IStats>();
                        if (stats != null) stats.Add(parts[1], statValue);
                        else GameState.Instance.AddStat(parts[1], statValue);
                    }
                    break;

                case "Flag":
                case "Set":        // Command 별칭
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
                case "AddMoney":   // Command 별칭
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
                if (btn != null) Destroy(btn);
            }
            spawnedButtons.Clear();
            LoveAlgo.UI.UISoundManager.Instance?.ClearExcludedButtons();
        }

        void OnDestroy()
        {
            ClearButtons();
            if (canvasGroup != null) DOTween.Kill(canvasGroup);
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

        /// <summary>
        /// 선택지 상태를 즉시 초기화 (로드/씬 전환 시 호출).
        /// 진행 중이던 ShowAndWaitAsync가 취소되어도 버튼/CanvasGroup이 화면에 남는 잔상 방지.
        /// </summary>
        public void ResetImmediate()
        {
            if (canvasGroup != null) DOTween.Kill(canvasGroup);
            isWaitingForChoice = false;
            selectedIndex = -1;
            ClearButtons();
            Hide();
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
