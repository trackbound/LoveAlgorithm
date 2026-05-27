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

        [Header("D10: stagger / hover / important")]
        [Tooltip("선택지가 위에서 아래로 등장할 때 버튼 간 지연(초). 0이면 동시 등장 (옛 동작).")]
        [SerializeField] float staggerDelaySec = 0.06f;

        [Tooltip("호버 시 버튼 스케일 (1.0=고정). 모바일 터치에서는 잠깐 적용되고 사라짐 — 무해.")]
        [SerializeField] float hoverScale = 1.05f;

        [Tooltip("호버 중인 버튼 외 나머지 버튼들의 dim alpha. 1=dim 안 함.")]
        [Range(0f, 1f)]
        [SerializeField] float dimSiblingsAlpha = 0.55f;

        [Tooltip("호버 트랜지션 시간(초).")]
        [SerializeField] float hoverTransitionSec = 0.12f;

        [Tooltip("중요 선택지 펄스 — 최대 스케일.")]
        [SerializeField] float importantPulseScale = 1.04f;

        [Tooltip("중요 선택지 펄스 1싸이클(왕복) 시간.")]
        [SerializeField] float importantPulseDuration = 1.1f;

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

            // D10: stagger 등장 — 위에서 아래로 0.06s 간격 페이드인
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                var btn = spawnedButtons[i];
                var cg = btn != null ? btn.GetComponent<CanvasGroup>() : null;
                if (cg == null) continue;
                float delay = i * staggerDelaySec;
                _ = cg.DOFade(1f, choiceAppearDuration).SetEase(choiceAppearEase).SetDelay(delay);
            }
            float staggerTotal = (spawnedButtons.Count - 1) * staggerDelaySec + choiceAppearDuration;
            if (staggerTotal < 0) staggerTotal = 0;
            await UniTask.Delay(TimeSpan.FromSeconds(staggerTotal), cancellationToken: ct);

            // D10: 모든 fadein 완료 후 important 펄스 시작 (등장 도중 펄스 안 보이게)
            for (int i = 0; i < spawnedButtons.Count && i < validOptions.Count; i++)
            {
                if (validOptions[i].IsImportant)
                    StartImportantPulse(spawnedButtons[i]);
            }

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

                    // 선택지 전용 호버 사운드 + D10 시각 호버(스케일 + 형제 dim)
                    var trigger = buttonObj.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                    if (trigger == null)
                        trigger = buttonObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

                    int capturedIndex = i;
                    var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                        { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                    enterEntry.callback.AddListener(_ =>
                    {
                        soundMgr?.PlayChoiceHover();
                        OnButtonHoverEnter(capturedIndex);
                    });
                    trigger.triggers.Add(enterEntry);

                    var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                        { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                    exitEntry.callback.AddListener(_ => OnButtonHoverExit(capturedIndex));
                    trigger.triggers.Add(exitEntry);
                }

                spawnedButtons.Add(buttonObj);
            }
        }

        // ── D10: 호버 / 중요 펄스 ─────────────────────────────────

        /// <summary>호버 진입 — 본인 스케일업 + 형제 dim.</summary>
        void OnButtonHoverEnter(int index)
        {
            if (index < 0 || index >= spawnedButtons.Count) return;
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                var btn = spawnedButtons[i];
                if (btn == null) continue;
                var rt = btn.transform as RectTransform;
                var cg = btn.GetComponent<CanvasGroup>();
                if (i == index)
                {
                    if (rt != null)
                    {
                        DOTween.Kill(rt, complete: false);
                        _ = rt.DOScale(hoverScale, hoverTransitionSec).SetEase(Ease.OutQuad).SetId(rt);
                    }
                    if (cg != null) _ = cg.DOFade(1f, hoverTransitionSec).SetId(cg);
                }
                else
                {
                    if (cg != null)
                    {
                        DOTween.Kill(cg);
                        _ = cg.DOFade(dimSiblingsAlpha, hoverTransitionSec).SetId(cg);
                    }
                }
            }
        }

        /// <summary>호버 이탈 — 전부 원복.</summary>
        void OnButtonHoverExit(int index)
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                var btn = spawnedButtons[i];
                if (btn == null) continue;
                var rt = btn.transform as RectTransform;
                var cg = btn.GetComponent<CanvasGroup>();
                if (rt != null)
                {
                    DOTween.Kill(rt, complete: false);
                    _ = rt.DOScale(1f, hoverTransitionSec).SetEase(Ease.OutQuad).SetId(rt);
                }
                if (cg != null)
                {
                    DOTween.Kill(cg);
                    _ = cg.DOFade(1f, hoverTransitionSec).SetId(cg);
                }
            }
        }

        /// <summary>중요 선택지 펄스 — 무한 yoyo 스케일.</summary>
        void StartImportantPulse(GameObject buttonObj)
        {
            if (buttonObj == null) return;
            var rt = buttonObj.transform as RectTransform;
            if (rt == null) return;
            // 호버 시 OnButtonHoverEnter가 같은 transform 대상 트윈을 kill → 펄스 일시 정지.
            // 호버 해제 후엔 펄스 재시작 안 함 (한 번 끊기면 그대로) — 단순화 정책.
            _ = rt.DOScale(importantPulseScale, importantPulseDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetId(rt);
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
        /// 버튼 정리 — D10 펄스/호버 트윈도 함께 kill (DOTween orphan 경고 방지).
        /// </summary>
        void ClearButtons()
        {
            foreach (var btn in spawnedButtons)
            {
                if (btn == null) continue;
                var rt = btn.transform as RectTransform;
                var cg = btn.GetComponent<CanvasGroup>();
                if (rt != null) DOTween.Kill(rt, complete: false);
                if (cg != null) DOTween.Kill(cg);
                Destroy(btn);
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
        /// D10: 중요 선택지 마커. 기획자가 ButtonText 앞에 '*' 또는 안에 '[important]'를
        /// 적으면 true. 파싱 시 마커는 ButtonText에서 strip됨.
        /// </summary>
        public bool IsImportant;

        /// <summary>
        /// Option Value 파싱
        /// 형식: 버튼텍스트|점프대상|효과1|효과2|...|if:조건
        /// 중요 마커: 버튼텍스트 앞 '*' 또는 텍스트 내 '[important]'
        /// </summary>
        public static OptionData Parse(string value)
        {
            var data = new OptionData();
            var parts = value.Split('|');

            if (parts.Length >= 1)
                data.ButtonText = ExtractImportantMarker(parts[0], out data.IsImportant);

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

        /// <summary>
        /// D10: 버튼텍스트에서 중요 마커('*' 접두사 또는 '[important]' 토큰)를 추출.
        /// 마커가 발견되면 isImportant=true, 마커 strip된 텍스트 반환. 양쪽 공백 trim.
        /// </summary>
        internal static string ExtractImportantMarker(string raw, out bool isImportant)
        {
            isImportant = false;
            if (string.IsNullOrEmpty(raw)) return raw ?? "";

            string s = raw;

            // [important] 토큰 (대소문자 무시) — 텍스트 어디든
            const string TokenLower = "[important]";
            int idx = s.IndexOf(TokenLower, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                isImportant = true;
                s = s.Remove(idx, TokenLower.Length);
            }

            // '*' 접두사 (공백 허용)
            string trimmed = s.TrimStart();
            if (trimmed.StartsWith("*"))
            {
                isImportant = true;
                trimmed = trimmed.Substring(1);
            }

            return trimmed.Trim();
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
