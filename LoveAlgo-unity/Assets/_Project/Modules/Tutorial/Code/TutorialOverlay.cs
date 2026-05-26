using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LoveAlgo.Story;
using LoveAlgo.Core;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 범용 튜토리얼 오버레이 — CSV 기반 스텝 진행, 클릭으로 넘기기
    ///
    /// 프리팹 1개 + CSV만 갈아끼우면 임의의 튜토리얼에 재사용 가능.
    /// dim 컬럼: 빈값=숨김, "keep"=이전 유지, 이미지명=새로 표시
    /// chr 컬럼(구 roa): 캐릭터 이미지 스프라이트 이름
    ///
    /// CSV 헤더 형식: dim,chr,text
    ///
    /// 사용:
    ///   var ov = UIManager.Instance.TutorialOverlay;
    ///   await ov.RunAsync("Story/ScheduleTutorial", "HasSeenScheduleTutorial", ct);
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        [Header("오버레이 레이어")]
        [SerializeField] CanvasGroup overlayGroup;
        [SerializeField] Image dimImage;
        [SerializeField] Image characterImage;
        [SerializeField] Image textboxImage;
        [SerializeField] TMP_Text dialogueText;

        [Header("스프라이트 바인딩 (모든 튜토리얼이 공용으로 사용)")]
        [Tooltip("dim 컬럼에서 이름으로 참조할 수 있는 배경/딤 스프라이트들")]
        [SerializeField] Sprite[] dimSprites;
        [Tooltip("chr 컬럼에서 이름으로 참조할 수 있는 캐릭터 스프라이트들")]
        [SerializeField] Sprite[] characterSprites;

        [Header("애니메이션")]
        [SerializeField] float fadeInDuration = 0.3f;
        [SerializeField] float fadeOutDuration = 0.25f;
        [SerializeField] float textFadeDuration = 0.15f;
        [Tooltip("첫 진입 시 안내 멘트(텍스트박스 + 텍스트) 등장 페이드 시간")]
        [SerializeField] float firstStepFadeDuration = 0.2f;

        [Header("뿅 등장 — 첫 스텝 펀치 애니메이션")]
        [Tooltip("0=비활성. 1.05~1.2 권장. 시작 스케일 0.8 → 펀치 → 1.0 으로 안착.")]
        [SerializeField] float popupPunchStrength = 0.15f;
        [SerializeField] float popupPunchDuration = 0.35f;
        [Tooltip("캐릭터 이미지에도 동일 펀치 적용 여부")]
        [SerializeField] bool popupPunchCharacter = true;

        [Header("텍스트박스 동적 리사이즈")]
        [Tooltip("텍스트박스 상하 패딩 (px)")]
        [SerializeField] float textboxPaddingVertical = 60f;
        [Tooltip("텍스트박스 좌우 패딩 (px)")]
        [SerializeField] float textboxPaddingHorizontal = 80f;
        [Tooltip("텍스트박스 최소 높이 (px)")]
        [SerializeField] float textboxMinHeight = 120f;
        [Tooltip("텍스트박스 최소 너비 (px)")]
        [SerializeField] float textboxMinWidth = 400f;
        [Tooltip("텍스트박스 최대 너비 (px) — 이 너비 넘으면 자동 줄바꿈")]
        [SerializeField] float textboxMaxWidth = 1100f;

        [Header("dim별 위치 프리셋 (선택)")]
        [Tooltip("dim 키 → textbox/character RectTransform 위치 매핑. 빈 배열이면 기본 위치 유지.")]
        [SerializeField] AnchorPreset[] anchorPresets;

        [System.Serializable]
        public struct AnchorPreset
        {
            [Tooltip("ApplyDim 의 dim 이름과 일치 (예: tutorial_dim_01)")]
            public string dimKey;
            [Tooltip("텍스트박스 anchoredPosition (RectTransform 기준)")]
            public Vector2 textboxAnchoredPos;
            [Tooltip("캐릭터 이미지 anchoredPosition")]
            public Vector2 characterAnchoredPos;
        }

        /// <summary>클릭 수신 플래그</summary>
        bool _clicked;

        /// <summary>ESC로 스킵 요청됨 (테스트용)</summary>
        bool _skipRequested;

        /// <summary>이름→스프라이트 룩업 (Awake에서 빌드)</summary>
        Dictionary<string, Sprite> _dimLookup;
        Dictionary<string, Sprite> _characterLookup;

        void Awake()
        {
            _dimLookup = BuildLookup(dimSprites);
            _characterLookup = BuildLookup(characterSprites);

            // 인스펙터 기본 텍스트(플레이스홀더)가 잠깐 보이는 것을 방지
            if (dialogueText != null)
            {
                dialogueText.text = "";
                dialogueText.alpha = 0f;
            }
            // 텍스트박스 — 9-slice 동적 리사이즈 보장 (prefab 설정 무시 강제):
            //  • Type=Sliced 가 아니면 늘릴 때 sprite 통째로 stretch → 모서리/꼬리 늘어남
            //  • Sprite Border 미설정이면 Sliced여도 Simple과 동일 → 경고
            if (textboxImage != null)
            {
                textboxImage.type = Image.Type.Sliced;
                textboxImage.preserveAspect = false;
                textboxImage.fillCenter = true;
                var sp = textboxImage.sprite;
                if (sp != null && sp.border == Vector4.zero)
                {
                    Debug.LogWarning(
                        $"[TutorialOverlay] textboxImage 스프라이트 '{sp.name}' 의 Sprite Border 가 0입니다. " +
                        "Project 창에서 스프라이트 선택 → Sprite Editor → Border 4값 설정 필요. " +
                        "(권장: 좌/하/우/상 ≥ 30, 꼬리 있으면 꼬리쪽 더 크게)", this);
                }

                // 처음에는 숨김 (첫 스텝에서 텍스트와 함께 페이드)
                var c = textboxImage.color;
                textboxImage.color = new Color(c.r, c.g, c.b, 0f);
            }
            if (overlayGroup != null) overlayGroup.alpha = 0f;
            if (dimImage != null) dimImage.enabled = false;

            gameObject.SetActive(false);
        }

        static Dictionary<string, Sprite> BuildLookup(Sprite[] sprites)
        {
            var dict = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            if (sprites == null) return dict;
            foreach (var s in sprites)
            {
                if (s != null)
                    dict[s.name] = s;
            }
            return dict;
        }

        /// <summary>
        /// 튜토리얼 실행 — CSV 파싱 → 스텝별 표시 → 클릭 진행 → 완료 시 플래그 설정
        /// </summary>
        /// <param name="csvResourcePath">Resources 폴더 기준 CSV 경로 (확장자 제외). 예: "Story/ScheduleTutorial"</param>
        /// <param name="seenFlagKey">완료 시 GameState에 설정할 플래그 키. 비어있으면 플래그 미설정.</param>
        public async UniTask RunAsync(string csvResourcePath, string seenFlagKey, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(csvResourcePath))
            {
                Debug.LogWarning("[TutorialOverlay] csvResourcePath가 비어있습니다.");
                return;
            }

            var csv = Resources.Load<TextAsset>(csvResourcePath);
            if (csv == null)
            {
                Debug.LogWarning($"[TutorialOverlay] CSV 파일을 찾을 수 없습니다: Resources/{csvResourcePath}");
                return;
            }

            var steps = ParseCSV(csv.text);
            if (steps.Count == 0) return;

            // 스킵 플래그 초기화
            _skipRequested = false;

            // 플레이어 이름 치환
            string playerName = GameState.Instance?.PlayerName ?? "유저";

            gameObject.SetActive(true);
            overlayGroup.alpha = 0f;
            dimImage.enabled = false;

            // 인스펙터 기본 텍스트가 1프레임 노출되는 것을 방지: 첫 스텝 적용 전에 비워두기
            if (dialogueText != null)
            {
                dialogueText.text = "";
                dialogueText.alpha = 0f;
            }
            // 텍스트박스 배경도 숨김 (첫 스텝 등장 시 함께 페이드)
            if (textboxImage != null)
            {
                var c = textboxImage.color;
                textboxImage.color = new Color(c.r, c.g, c.b, 0f);
            }

            // 페이드 인
            await overlayGroup.DOFade(1f, fadeInDuration)
                .SetEase(Ease.OutCubic)
                .ToUniTask(cancellationToken: ct);

            for (int i = 0; i < steps.Count; i++)
            {
                if (_skipRequested) break;

                var step = steps[i];
                string text = step.text.Replace("{name}", playerName);
                // CSV에서 \n 리터럴을 실제 줄바꿈으로 변환
                text = text.Replace("\\n", "\n");

                // 딤 이미지 처리
                ApplyDim(step.dim);

                // 캐릭터 이미지
                if (_characterLookup.TryGetValue(step.character, out var charSprite))
                    characterImage.sprite = charSprite;

                // 텍스트 표시 (페이드) + 텍스트박스 동적 리사이즈
                dialogueText.alpha = 0f;
                dialogueText.text = text;
                FitTextboxHeight();

                if (i == 0 && textboxImage != null)
                {
                    // 첫 진입: 텍스트박스 배경 + 텍스트를 같이 빠르게 페이드 인 + "뿅" 스케일 펀치
                    var boxRt = textboxImage.rectTransform;
                    var charRt = popupPunchCharacter && characterImage != null ? characterImage.rectTransform : null;

                    if (popupPunchStrength > 0f)
                    {
                        boxRt.localScale = Vector3.one * 0.8f;
                        if (charRt != null) charRt.localScale = Vector3.one * 0.85f;
                    }

                    var seq = DOTween.Sequence();
                    _ = seq.Join(textboxImage.DOFade(1f, firstStepFadeDuration).SetEase(Ease.OutCubic));
                    _ = seq.Join(DOTween.ToAlpha(
                        () => dialogueText.color, c => dialogueText.color = c,
                        1f, firstStepFadeDuration
                    ).SetEase(Ease.OutCubic));
                    if (popupPunchStrength > 0f)
                    {
                        // OutBack: 0.8 → 1.15 → 1.0 (탱탱 튀어나오는 느낌)
                        _ = seq.Join(boxRt.DOScale(1f, popupPunchDuration).SetEase(Ease.OutBack, 2.5f));
                        if (charRt != null)
                            _ = seq.Join(charRt.DOScale(1f, popupPunchDuration).SetEase(Ease.OutBack, 2.5f));
                    }
                    await seq.ToUniTask(cancellationToken: ct);
                }
                else
                {
                    await DOTween.ToAlpha(
                        () => dialogueText.color, c => dialogueText.color = c,
                        1f, textFadeDuration
                    ).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct);
                }

                // 클릭 대기
                await WaitForClickAsync(ct);

                // 텍스트 페이드 아웃 (마지막 스텝 제외)
                if (i < steps.Count - 1)
                {
                    await DOTween.ToAlpha(
                        () => dialogueText.color, c => dialogueText.color = c,
                        0f, textFadeDuration
                    ).SetEase(Ease.InCubic).ToUniTask(cancellationToken: ct);
                }
            }

            // 페이드 아웃
            await overlayGroup.DOFade(0f, fadeOutDuration)
                .SetEase(Ease.InCubic)
                .ToUniTask(cancellationToken: ct);

            gameObject.SetActive(false);

            // 플래그 설정 — 다음부터 표시 안 함.
            // 튜토리얼은 "유저가 한 번이라도 봤는가" 메타데이터라 PlayerPrefs(영구) + GameState(현재 세션) 둘 다 저장.
            // GameState만 쓰면 ResetAll(새 게임) 시 초기화되어 매번 다시 뜸.
            if (!string.IsNullOrEmpty(seenFlagKey))
            {
                GameState.Instance?.SetFlag(seenFlagKey, true);
                PlayerPrefs.SetInt("Tutorial_" + seenFlagKey, 1);
                PlayerPrefs.Save();
            }
        }

        /// <summary>이 튜토리얼을 이전에 본 적이 있는지 (PlayerPrefs 영구 확인).</summary>
        public static bool HasSeen(string seenFlagKey)
        {
            if (string.IsNullOrEmpty(seenFlagKey)) return false;
            return PlayerPrefs.GetInt("Tutorial_" + seenFlagKey, 0) == 1;
        }

        /// <summary>딤 이미지 적용 (빈값=숨김, keep=유지, 이름=새 스프라이트) + 위치 프리셋</summary>
        void ApplyDim(string dimValue)
        {
            if (string.IsNullOrEmpty(dimValue))
            {
                dimImage.enabled = false;
                ApplyAnchorPreset(""); // 기본 위치 복귀는 옵션 — 빈값일 땐 그대로 두는 편이 자연스러움
                return;
            }

            if (dimValue.Equals("keep", StringComparison.OrdinalIgnoreCase))
                return; // 이전 상태 유지

            if (_dimLookup.TryGetValue(dimValue, out var dimSprite))
            {
                dimImage.sprite = dimSprite;
                dimImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[TutorialOverlay] 딤 스프라이트 '{dimValue}'을 찾을 수 없습니다.");
                dimImage.enabled = false;
            }

            // dim 키에 매칭되는 위치 프리셋 적용 (있으면)
            ApplyAnchorPreset(dimValue);
        }

        /// <summary>dim 키에 매칭되는 anchor preset을 textbox/character 에 적용</summary>
        void ApplyAnchorPreset(string dimKey)
        {
            if (anchorPresets == null || anchorPresets.Length == 0) return;
            if (string.IsNullOrEmpty(dimKey)) return;

            foreach (var preset in anchorPresets)
            {
                if (!string.Equals(preset.dimKey, dimKey, StringComparison.OrdinalIgnoreCase)) continue;
                if (textboxImage != null)
                    textboxImage.rectTransform.anchoredPosition = preset.textboxAnchoredPos;
                if (characterImage != null)
                    characterImage.rectTransform.anchoredPosition = preset.characterAnchoredPos;
                return;
            }
        }

        /// <summary>텍스트 내용에 맞춰 텍스트박스 너비/높이 동적 조정 (9-slice 스프라이트 활용)</summary>
        void FitTextboxHeight()
        {
            if (textboxImage == null || dialogueText == null) return;

            // 1) 가로: preferredWidth → min/max 클램프
            //    텍스트가 maxWidth 안에서 자연 폭으로 끝나면 짧은 박스, 넘으면 max에서 wrap → 세로 늘림
            dialogueText.textWrappingMode = TextWrappingModes.NoWrap;
            dialogueText.ForceMeshUpdate();
            float naturalW = dialogueText.preferredWidth;
            float availableTextWidth = Mathf.Max(textboxMinWidth, textboxMaxWidth) - textboxPaddingHorizontal;
            float newTextW = Mathf.Clamp(naturalW, textboxMinWidth - textboxPaddingHorizontal, availableTextWidth);
            float newBoxW = Mathf.Clamp(naturalW + textboxPaddingHorizontal, textboxMinWidth, textboxMaxWidth);

            // 2) wrap 다시 켜고 가로 확정 → 그 결과 세로(preferredHeight) 계산
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            var textRt = dialogueText.rectTransform;
            textRt.sizeDelta = new Vector2(newTextW, textRt.sizeDelta.y);
            dialogueText.ForceMeshUpdate();
            float textH = dialogueText.preferredHeight;
            float newBoxH = Mathf.Max(textH + textboxPaddingVertical, textboxMinHeight);

            // 3) 박스 sizeDelta 갱신
            var rt = textboxImage.rectTransform;
            rt.sizeDelta = new Vector2(newBoxW, newBoxH);
        }

        /// <summary>화면 클릭/스페이스바 대기 (ESC 스킵). 인스펙터 OnClick 미바인딩 안전망으로 마우스 직접 폴링.</summary>
        async UniTask WaitForClickAsync(CancellationToken ct)
        {
            _clicked = false;
            // 직전 프레임 클릭이 잡혀 즉시 스킵되는 것 방지 — 1프레임 양보
            await UniTask.Yield(PlayerLoopTiming.LastUpdate, ct);

            await UniTask.WaitUntil(() =>
                _clicked
                || _skipRequested
                || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && (_skipRequested = true))
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame),
                cancellationToken: ct);
        }

        /// <summary>클릭 이벤트 수신 (Button OnClick 또는 EventTrigger에서 호출)</summary>
        public void OnClick()
        {
            _clicked = true;
        }

        #region CSV 파싱

        struct TutorialStep
        {
            public string dim;
            public string character;
            public string text;
        }

        /// <summary>CSV 파싱 — 첫 행 헤더 스킵, 쌍따옴표 지원</summary>
        static List<TutorialStep> ParseCSV(string csv)
        {
            var steps = new List<TutorialStep>();
            var lines = csv.Split('\n');

            for (int i = 1; i < lines.Length; i++) // 헤더 스킵
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var cols = SplitCSVLine(line);
                if (cols.Count < 3) continue;

                steps.Add(new TutorialStep
                {
                    dim = cols[0].Trim(),
                    character = cols[1].Trim(),
                    text = cols[2].Trim()
                });
            }

            return steps;
        }

        /// <summary>CSV 라인 분리 (쌍따옴표 안의 쉼표 무시)</summary>
        static List<string> SplitCSVLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            int start = 0;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                    inQuotes = !inQuotes;
                else if (line[i] == ',' && !inQuotes)
                {
                    result.Add(StripQuotes(line.Substring(start, i - start)));
                    start = i + 1;
                }
            }
            result.Add(StripQuotes(line.Substring(start)));
            return result;
        }

        static string StripQuotes(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                s = s.Substring(1, s.Length - 2);
            return s;
        }

        #endregion
    }
}
