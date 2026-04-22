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

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 튜토리얼 오버레이 — CSV 기반 스텝 진행, 클릭으로 넘기기
    /// dim 컬럼: 빈값=숨김, "keep"=이전 유지, 이미지명=새로 표시
    /// </summary>
    public class ScheduleTutorialOverlay : MonoBehaviour
    {
        [Header("오버레이 레이어")]
        [SerializeField] CanvasGroup overlayGroup;
        [SerializeField] Image dimImage;
        [SerializeField] Image roaImage;
        [SerializeField] Image textboxImage;
        [SerializeField] TMP_Text dialogueText;

        [Header("스프라이트 바인딩")]
        [SerializeField] Sprite[] dimSprites;
        [SerializeField] Sprite[] roaSprites;

        [Header("애니메이션")]
        [SerializeField] float fadeInDuration = 0.3f;
        [SerializeField] float fadeOutDuration = 0.25f;
        [SerializeField] float textFadeDuration = 0.15f;
        [Tooltip("첫 진입 시 안내 멘트(텍스트박스 + 텍스트) 등장 페이드 시간")]
        [SerializeField] float firstStepFadeDuration = 0.2f;

        [Header("텍스트박스 동적 리사이즈")]
        [Tooltip("텍스트박스 상하 패딩 (px)")]
        [SerializeField] float textboxPaddingVertical = 60f;
        [Tooltip("텍스트박스 최소 높이 (px)")]
        [SerializeField] float textboxMinHeight = 120f;

        /// <summary>클릭 수신 플래그</summary>
        bool _clicked;

        /// <summary>이름→스프라이트 룩업 (Awake에서 빌드)</summary>
        Dictionary<string, Sprite> _dimLookup;
        Dictionary<string, Sprite> _roaLookup;

        void Awake()
        {
            _dimLookup = BuildLookup(dimSprites);
            _roaLookup = BuildLookup(roaSprites);

            // 인스펙터 기본 텍스트(플레이스홀더)가 잠깐 보이는 것을 방지
            if (dialogueText != null)
            {
                dialogueText.text = "";
                dialogueText.alpha = 0f;
            }
            // 텍스트박스도 처음에는 숨겨둔 상태로 시작 (첫 스텝에서 텍스트와 함께 페이드 인)
            if (textboxImage != null)
            {
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
        public async UniTask RunAsync(CancellationToken ct)
        {
            var csv = Resources.Load<TextAsset>("Story/ScheduleTutorial");
            if (csv == null)
            {
                Debug.LogWarning("[ScheduleTutorial] CSV 파일을 찾을 수 없습니다.");
                return;
            }

            var steps = ParseCSV(csv.text);
            if (steps.Count == 0) return;

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
                var step = steps[i];
                string text = step.text.Replace("{name}", playerName);
                // CSV에서 \n 리터럴을 실제 줄바꿈으로 변환
                text = text.Replace("\\n", "\n");

                // 딤 이미지 처리
                ApplyDim(step.dim);

                // 로아 표정
                if (_roaLookup.TryGetValue(step.roa, out var roaSprite))
                    roaImage.sprite = roaSprite;

                // 텍스트 표시 (페이드) + 텍스트박스 동적 리사이즈
                dialogueText.alpha = 0f;
                dialogueText.text = text;
                FitTextboxHeight();

                if (i == 0 && textboxImage != null)
                {
                    // 첫 진입: 텍스트박스 배경 + 텍스트를 같이 빠르게 페이드 인
                    var seq = DOTween.Sequence();
                    seq.Join(textboxImage.DOFade(1f, firstStepFadeDuration).SetEase(Ease.OutCubic));
                    seq.Join(DOTween.ToAlpha(
                        () => dialogueText.color, c => dialogueText.color = c,
                        1f, firstStepFadeDuration
                    ).SetEase(Ease.OutCubic));
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

            // 플래그 설정 — 다음부터 표시 안 함
            GameState.Instance?.SetFlag("HasSeenScheduleTutorial", true);
        }

        /// <summary>딤 이미지 적용 (빈값=숨김, keep=유지, 이름=새 스프라이트)</summary>
        void ApplyDim(string dimValue)
        {
            if (string.IsNullOrEmpty(dimValue))
            {
                dimImage.enabled = false;
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
                Debug.LogWarning($"[ScheduleTutorial] 딤 스프라이트 '{dimValue}'을 찾을 수 없습니다.");
                dimImage.enabled = false;
            }
        }

        /// <summary>텍스트 높이에 맞춰 텍스트박스 높이 조정 (9-slice 스프라이트 활용)</summary>
        void FitTextboxHeight()
        {
            if (textboxImage == null || dialogueText == null) return;

            dialogueText.ForceMeshUpdate();
            float textH = dialogueText.preferredHeight;
            float newH = Mathf.Max(textH + textboxPaddingVertical, textboxMinHeight);

            var rt = textboxImage.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, newH);
        }

        /// <summary>화면 클릭 또는 스페이스바 대기</summary>
        async UniTask WaitForClickAsync(CancellationToken ct)
        {
            _clicked = false;
            await UniTask.WaitUntil(() =>
                _clicked || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame),
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
            public string roa;
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
                    roa = cols[1].Trim(),
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
