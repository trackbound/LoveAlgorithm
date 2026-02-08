using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 대사 로그 항목
    /// </summary>
    public struct DialogueLogEntry
    {
        public string Speaker;       // 표시 이름 (한글)
        public string Text;
        public string CharacterId;   // 영문 ID (썸네일 로드용)
    }

    /// <summary>
    /// 대사창 UI
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [Header("필수 바인딩")]
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text dialogueText;

        [Header("다음 인디케이터 (별도 오브젝트)")]
        [SerializeField] RectTransform nextIndicator;     // 인디케이터 오브젝트 (Image/TMP Sprite)
        [SerializeField] Vector2 indicatorOffset = new(10f, 0f);  // 마지막 글자 기준 오프셋

        [Header("버튼")]
        [SerializeField] Button titleButton;
        [SerializeField] Button saveButton;
        [SerializeField] Button loadButton;
        [SerializeField] Button configButton;
        [SerializeField] Button autoButton;
        [SerializeField] Button logButton;
        [SerializeField] Button hideButton;
        [SerializeField] GameObject showButtonObject;  // Hide 시 표시될 Show 버튼

        [Header("선택적 바인딩")]
        [SerializeField] GameObject nameBox;        // 없으면 nameText만 사용
        [SerializeField] CanvasGroup canvasGroup;   // 없으면 SetActive로 제어
        [SerializeField] AudioSource typingAudioSource;
        [SerializeField] AudioClip typingSFX;       // 없으면 타이핑 사운드 없음
        [SerializeField] CharacterDatabase characterDatabase;  // 캐릭터 색상용

        [Header("독백 Dots 애니메이션")]
        [SerializeField] Image monologueDotsImage;  // 독백 시 이름 대신 표시할 Image
        [SerializeField] float dotsFrameRate = 0.15f;  // 프레임 간격 (초)
        Sprite[] monologueDotSprites;  // Resources에서 로드
        CancellationTokenSource dotsAnimCts;

        [Header("대사창 애니메이션")]
        [SerializeField] float fadeDuration = 0.25f;       // Show/Hide 페이드 속도
        [SerializeField] float slideDuration = 0.25f;      // 버튼 슬라이드 속도
        [SerializeField] float slideDistance = 200f;       // 슬라이드 거리 (px)

        Tweener showHideTween;     // 현재 fade 애니메이션
        Sequence slideSequence;    // 슬라이드 시퀀스
        RectTransform rectTransform;
        float originalY;

        [Header("설정")]
        [SerializeField] float typingSpeed = 0.04f;
        [SerializeField] float punctuationDelay = 0.12f;  // 문장부호 최대 추가 딜레이
        [SerializeField] float jitterAmount = 0.15f;       // 타이핑 리듬 랜덤 변동 (±15%)
        [SerializeField] int punctuationLookahead = 2;     // 문장부호 N글자 전부터 감속 시작

        // 인라인 태그 콜백
        public Action<string> OnEmoteTag;   // 표정 변경 요청

        bool isTyping;
        bool skipRequested;
        string fullText;
        bool isHidden;
        bool needsFadeIn;  // 다음 Show 시 페이드인 필요 여부

        /// <summary>
        /// 마지막 표시된 텍스트 길이 (Auto 딜레이 계산용)
        /// </summary>
        public int LastDisplayedTextLength => fullText?.Length ?? 0;

        /// <summary>
        /// 텍스트 속도 설정 (0=느림, 1=빠름)
        /// </summary>
        public void SetTextSpeed(float normalized)
        {
            // 0=느림(0.08s/char), 1=빠름(0.01s/char)
            typingSpeed = Mathf.Lerp(0.08f, 0.01f, normalized);
        }

        // 대사 로그
        readonly List<DialogueLogEntry> dialogueLog = new();
        public IReadOnlyList<DialogueLogEntry> DialogueLog => dialogueLog;

        // 인라인 태그 정규식
        static readonly Regex InlineTagRegex = new(@"<(wait|sfx|emote|speed)=([^/>]+)(/?)>", RegexOptions.Compiled);
        static readonly Regex SpeedEndRegex = new(@"</speed>", RegexOptions.Compiled);

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
                originalY = rectTransform.anchoredPosition.y;

            HideNextIndicator();
            SetupButtons();
            LoadMonologueDotSprites();
            if (showButtonObject != null) showButtonObject.SetActive(false);

            // 저장된 텍스트 속도 복원
            float savedSpeed = PlayerPrefs.GetFloat("TextSpeed", 0.4f);
            SetTextSpeed(savedSpeed);
        }

        void SetupButtons()
        {
            titleButton?.onClick.AddListener(OnTitleClick);
            saveButton?.onClick.AddListener(OnSaveClick);
            loadButton?.onClick.AddListener(OnLoadClick);
            configButton?.onClick.AddListener(OnConfigClick);
            autoButton?.onClick.AddListener(OnAutoClick);
            logButton?.onClick.AddListener(OnLogClick);
            hideButton?.onClick.AddListener(OnHideClick);
            
            // Show 버튼 (Hide 상태에서 표시됨)
            if (showButtonObject != null)
            {
                var showButton = showButtonObject.GetComponent<Button>();
                showButton?.onClick.AddListener(OnShowClick);
            }
        }

        /// <summary>
        /// 대사 표시 (타이핑 효과 + 인라인 태그)
        /// maxVisibleCharacters 방식으로 리치텍스트 호환 + 부드러운 리듬
        /// </summary>
        public async UniTask ShowTextAsync(string speaker, string text, CancellationToken ct)
        {
            // 변수 치환 ({{PlayerName}} 등)
            speaker = SubstituteVariables(speaker);
            text = SubstituteVariables(text);

            // 로그에 추가
            AddToLog(speaker, text);

            // 화자 설정 (Show 전에 호출 → 이전 텍스트 잔상 방지)
            SetSpeaker(speaker);

            // 인라인 태그 파싱
            var segments = ParseInlineTags(text);

            // 타이핑 준비
            fullText = GetCleanText(segments);
            isTyping = true;
            skipRequested = false;
            HideNextIndicator();

            // maxVisibleCharacters 방식: 전체 텍스트를 미리 세팅하고 글자만 드러냄
            dialogueText.text = fullText;
            dialogueText.ForceMeshUpdate();
            dialogueText.maxVisibleCharacters = 0;

            // 대사창 표시 (Hide 상태가 아닐 때만, 텍스트가 비워진 후 보여줄)
            if (!isHidden)
            {
                bool shouldWaitFade = needsFadeIn;
                Show();
                if (shouldWaitFade)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(fadeDuration), cancellationToken: ct);
            }

            float currentSpeed = typingSpeed;
            int visibleCount = 0;

            foreach (var seg in segments)
            {
                ct.ThrowIfCancellationRequested();

                if (skipRequested)
                {
                    CompleteText();
                    break;
                }

                switch (seg.Type)
                {
                    case SegmentType.Text:
                        // 글자별 타이핑 — 부드러운 리듬
                        string content = seg.Content;
                        for (int i = 0; i < content.Length; i++)
                        {
                            if (skipRequested) { CompleteText(); goto done; }

                            char c = content[i];
                            visibleCount++;
                            dialogueText.maxVisibleCharacters = visibleCount;

                            // 줄바꿈·공백은 사운드/딜레이 생략
                            if (c == '\n' || c == '\r') continue;

                            PlayTypingSound();

                            // ── 부드러운 딜레이 계산 ──
                            float charDelay = currentSpeed;

                            // 문장부호 감속: 문장부호 자체 + N글자 전부터 점진 감속
                            float punctWeight = GetPunctuationWeight(c, content, i);
                            if (punctWeight > 0f)
                            {
                                // EaseOutQuad 커브로 부드럽게 감속
                                float easedWeight = punctWeight * punctWeight;  // InQuad: 점진적으로 느려짐
                                charDelay += punctuationDelay * easedWeight;
                            }

                            // 자연스러운 리듬 변동 (미세한 랜덤 지터)
                            float jitter = 1f + UnityEngine.Random.Range(-jitterAmount, jitterAmount);
                            charDelay *= jitter;

                            await UniTask.Delay(TimeSpan.FromSeconds(charDelay), cancellationToken: ct);
                        }
                        break;

                    case SegmentType.Wait:
                        if (!skipRequested && float.TryParse(seg.Content, out float waitTime))
                            await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: ct);
                        break;

                    case SegmentType.SFX:
                        AudioManager.Instance?.PlaySFX(seg.Content);
                        break;

                    case SegmentType.Emote:
                        OnEmoteTag?.Invoke(seg.Content);
                        break;

                    case SegmentType.SpeedStart:
                        if (float.TryParse(seg.Content, out float speedMult))
                            currentSpeed = typingSpeed * speedMult;
                        break;

                    case SegmentType.SpeedEnd:
                        currentSpeed = typingSpeed;
                        break;
                }
            }

            done:
            isTyping = false;
            dialogueText.maxVisibleCharacters = fullText.Length;  // 안전: 혹시 누락 방지
            ShowNextIndicator();
        }

        /// <summary>
        /// 문장부호 가중치 계산 — 문장부호 N글자 전부터 점진 감속
        /// 0.0 = 일반 글자, 1.0 = 문장부호 자체
        /// </summary>
        float GetPunctuationWeight(char c, string text, int index)
        {
            // 현재 글자가 문장부호면 최대 가중치
            if (IsMajorPunctuation(c)) return 1.0f;
            if (c == ',') return 0.4f;  // 쉼표는 약한 감속

            // 다음 N글자 안에 문장부호가 있으면 점진 감속
            int lookahead = punctuationLookahead;
            for (int ahead = 1; ahead <= lookahead && index + ahead < text.Length; ahead++)
            {
                char nextC = text[index + ahead];
                if (IsMajorPunctuation(nextC))
                {
                    // ahead=1이면 weight 높고, ahead=lookahead이면 낮음
                    // 선형 보간: 가까울수록 더 느려짐
                    float t = 1f - ((float)ahead / (lookahead + 1));
                    return t * 0.5f;  // 최대 0.5 (문장부호 자체보다는 약하게)
                }
            }

            return 0f;
        }

        bool IsMajorPunctuation(char c)
        {
            return c == '.' || c == '!' || c == '?' || c == '~' || c == '\u2026';  // \u2026 = …
        }

        #region 인라인 태그 파싱

        enum SegmentType { Text, Wait, SFX, Emote, SpeedStart, SpeedEnd }

        struct TextSegment
        {
            public SegmentType Type;
            public string Content;
        }

        List<TextSegment> ParseInlineTags(string text)
        {
            var segments = new List<TextSegment>();
            int lastIndex = 0;

            // 태그 찾기
            var matches = InlineTagRegex.Matches(text);
            foreach (Match m in matches)
            {
                // 태그 전 텍스트
                if (m.Index > lastIndex)
                {
                    string before = text.Substring(lastIndex, m.Index - lastIndex);
                    before = SpeedEndRegex.Replace(before, ""); // </speed> 제거
                    if (!string.IsNullOrEmpty(before))
                        segments.Add(new TextSegment { Type = SegmentType.Text, Content = before });
                }

                // 태그 처리
                string tagName = m.Groups[1].Value.ToLower();
                string tagValue = m.Groups[2].Value;
                bool isSelfClosing = m.Groups[3].Value == "/";

                switch (tagName)
                {
                    case "wait":
                        segments.Add(new TextSegment { Type = SegmentType.Wait, Content = tagValue });
                        break;
                    case "sfx":
                        segments.Add(new TextSegment { Type = SegmentType.SFX, Content = tagValue });
                        break;
                    case "emote":
                        segments.Add(new TextSegment { Type = SegmentType.Emote, Content = tagValue });
                        break;
                    case "speed":
                        segments.Add(new TextSegment { Type = SegmentType.SpeedStart, Content = tagValue });
                        break;
                }

                lastIndex = m.Index + m.Length;
            }

            // 나머지 텍스트
            if (lastIndex < text.Length)
            {
                string remaining = text.Substring(lastIndex);
                remaining = SpeedEndRegex.Replace(remaining, "");

                // </speed> 위치에서 속도 복귀 삽입
                if (text.Substring(lastIndex).Contains("</speed>"))
                    segments.Add(new TextSegment { Type = SegmentType.SpeedEnd, Content = "" });

                if (!string.IsNullOrEmpty(remaining))
                    segments.Add(new TextSegment { Type = SegmentType.Text, Content = remaining });
            }

            return segments;
        }

        string GetCleanText(List<TextSegment> segments)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var seg in segments)
            {
                if (seg.Type == SegmentType.Text)
                    sb.Append(seg.Content);
            }
            return sb.ToString();
        }

        string SubstituteVariables(string text)
        {
            if (GameState.Instance == null) return text;
            return text.Replace("{{PlayerName}}", GameState.Instance.PlayerName);
        }

        #endregion

        /// <summary>
        /// 화자 설정
        /// </summary>
        void SetSpeaker(string speaker)
        {
            bool hasName = !string.IsNullOrEmpty(speaker);
            bool isMonologue = !hasName;  // Speaker 없으면 독백

            // 독백 dots 애니메이션 처리
            if (monologueDotsImage != null)
            {
                if (isMonologue && monologueDotSprites != null && monologueDotSprites.Length > 0)
                {
                    // 독백: dots 이미 재생 중이면 유지 (연속 독백 시 끊김 방지)
                    monologueDotsImage.gameObject.SetActive(true);
                    if (!IsDotsAnimating)
                        StartDotsAnimation();
                    
                    // 이름 텍스트 숨김
                    if (nameText != null) nameText.gameObject.SetActive(false);
                }
                else
                {
                    // 대화: dots 숨기고 이름 표시
                    StopDotsAnimation();
                    monologueDotsImage.gameObject.SetActive(false);
                    
                    if (nameText != null)
                    {
                        nameText.text = hasName ? speaker : "";
                        nameText.gameObject.SetActive(hasName);
                        
                        // CharacterDatabase에서 색상 가져오기
                        if (hasName && characterDatabase != null)
                        {
                            nameText.color = Color.white;
                        }
                    }
                }
            }
            else
            {
                // monologueDotsImage 없으면 기존 동작
                if (nameText != null)
                {
                    nameText.text = hasName ? speaker : "";
                    nameText.gameObject.SetActive(hasName);
                    
                    // CharacterDatabase에서 색상 가져오기
                    if (hasName && characterDatabase != null)
                    {
                        nameText.color = Color.white;
                    }
                }
            }

            // nameBox가 있으면 nameBox로 제어 (독백이든 대화든 nameBox는 항상 표시)
            if (nameBox != null)
            {
                nameBox.SetActive(true);  // 독백이어도 nameBox는 유지 (dots 표시용)
            }
        }

        #region 독백 Dots 애니메이션

        /// <summary>
        /// Dots 애니메이션이 재생 중인지
        /// </summary>
        public bool IsDotsAnimating => dotsAnimCts != null && !dotsAnimCts.IsCancellationRequested;

        /// <summary>
        /// 독백 dots 스프라이트 로드
        /// </summary>
        void LoadMonologueDotSprites()
        {
            monologueDotSprites = Resources.LoadAll<Sprite>("UI/MonologueDots");
            if (monologueDotSprites == null || monologueDotSprites.Length == 0)
            {
                Debug.LogWarning("[DialogueUI] 독백 dots 스프라이트를 찾을 수 없습니다: Resources/UI/MonologueDots");
            }
            else
            {
                // 이름순 정렬 (00, 01, 02...)
                System.Array.Sort(monologueDotSprites, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Dots 애니메이션 시작
        /// </summary>
        void StartDotsAnimation()
        {
            StopDotsAnimation();
            dotsAnimCts = new CancellationTokenSource();
            PlayDotsAnimationAsync(dotsAnimCts.Token).Forget();
        }

        /// <summary>
        /// Dots 애니메이션 정지
        /// </summary>
        void StopDotsAnimation()
        {
            dotsAnimCts?.Cancel();
            dotsAnimCts?.Dispose();
            dotsAnimCts = null;
        }

        /// <summary>
        /// Dots 애니메이션 루프
        /// </summary>
        async UniTaskVoid PlayDotsAnimationAsync(CancellationToken ct)
        {
            if (monologueDotSprites == null || monologueDotSprites.Length == 0) return;
            if (monologueDotsImage == null) return;

            int frameIndex = 0;
            while (!ct.IsCancellationRequested)
            {
                monologueDotsImage.sprite = monologueDotSprites[frameIndex];
                frameIndex = (frameIndex + 1) % monologueDotSprites.Length;
                
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(dotsFrameRate), cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        void OnDestroy()
        {
            StopDotsAnimation();
            KillAnimations();
        }

        /// <summary>
        /// 독백 상태 즉시 초기화 (세이브/로드/점프 시 호출)
        /// </summary>
        public void ResetMonologueState()
        {
            StopDotsAnimation();
            if (monologueDotsImage != null)
                monologueDotsImage.gameObject.SetActive(false);
        }

        #endregion

        /// <summary>
        /// 즉시 전체 표시 (스킵)
        /// </summary>
        public void CompleteText()
        {
            if (isTyping)
            {
                skipRequested = true;
                dialogueText.text = fullText;
                dialogueText.maxVisibleCharacters = fullText?.Length ?? 0;
                isTyping = false;
                ShowNextIndicator();
            }
        }

        /// <summary>
        /// 타이핑 중인지
        /// </summary>
        public bool IsTyping => isTyping;

        /// <summary>
        /// 스킵 요청 (외부에서 클릭 시 호출)
        /// </summary>
        public void RequestSkip()
        {
            if (isTyping)
            {
                skipRequested = true;
            }
        }

        #region 표시/숨김

        /// <summary>
        /// 대사창 페이드 인 (빠른 알파 애니메이션)
        /// </summary>
        public void Show()
        {
            if (canvasGroup == null) { gameObject.SetActive(true); needsFadeIn = false; return; }

            KillAnimations();
            needsFadeIn = false;

            // 슬라이드 위치 복원
            if (rectTransform != null)
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, originalY);

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            showHideTween = canvasGroup.DOFade(1f, fadeDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        /// <summary>
        /// 대사창 페이드 아웃 (빠른 알파 애니메이션)
        /// </summary>
        public void Hide()
        {
            StopDotsAnimation();
            needsFadeIn = true;

            if (canvasGroup == null) { gameObject.SetActive(false); return; }

            KillAnimations();

            showHideTween = canvasGroup.DOFade(0f, fadeDuration)
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                });
        }

        /// <summary>
        /// 즉시 표시 (로드/복원 시 사용)
        /// </summary>
        public void ShowImmediate()
        {
            KillAnimations();
            needsFadeIn = false;

            if (rectTransform != null)
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, originalY);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 즉시 숨김 (초기화/로드 시 사용)
        /// </summary>
        public void HideImmediate()
        {
            StopDotsAnimation();
            KillAnimations();
            needsFadeIn = true;

            if (rectTransform != null)
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, originalY);

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
        /// 진행 중인 Show/Hide/Slide 애니메이션 정리
        /// </summary>
        void KillAnimations()
        {
            showHideTween?.Kill();
            showHideTween = null;
            slideSequence?.Kill();
            slideSequence = null;
        }

        #endregion

        #region Next Indicator

        bool indicatorShown;
        Tweener indicatorBlink;

        [Header("인디케이터 애니메이션")]
        [SerializeField] float blinkInterval = 0.6f;  // 깜빡임 간격
        [SerializeField] float bounceAmplitude = 4f;  // 상하 바운스 크기 (px)
        [SerializeField] float indicatorPopDuration = 0.25f;  // 등장 애니메이션 시간
        Color indicatorOriginalColor = Color.white;   // 원래 색상 저장
        Tweener indicatorBounce;
        float indicatorBaseY;

        void ShowNextIndicator()
        {
            if (nextIndicator == null || indicatorShown) return;

            // TMP_Text 메시 업데이트 강제
            dialogueText.ForceMeshUpdate();

            var textInfo = dialogueText.textInfo;
            
            // maxVisibleCharacters 기준으로 마지막 보이는 글자 인덱스 계산
            int maxVisible = dialogueText.maxVisibleCharacters;
            int totalChars = textInfo.characterCount;
            
            if (totalChars == 0 || maxVisible == 0)
            {
                nextIndicator.gameObject.SetActive(false);
                return;
            }

            // 마지막 보이는 글자 찾기 (maxVisibleCharacters와 isVisible 모두 고려)
            int lastVisibleIdx = -1;
            int searchEnd = Mathf.Min(maxVisible, totalChars);
            
            for (int i = searchEnd - 1; i >= 0; i--)
            {
                if (textInfo.characterInfo[i].isVisible)
                {
                    lastVisibleIdx = i;
                    break;
                }
            }

            if (lastVisibleIdx < 0)
            {
                // 보이는 글자가 없으면 텍스트 시작 위치 사용
                nextIndicator.anchoredPosition = Vector2.zero + indicatorOffset;
                indicatorBaseY = nextIndicator.anchoredPosition.y;
                nextIndicator.localScale = Vector3.zero;
                nextIndicator.gameObject.SetActive(true);
                nextIndicator.DOScale(Vector3.one, indicatorPopDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(StartBlinkAnimation);
                indicatorShown = true;
                return;
            }

            // 마지막 글자의 오른쪽 아래 위치 (baseline 기준으로 일관된 높이)
            var charInfo = textInfo.characterInfo[lastVisibleIdx];
            
            // baseline 사용: bottomRight.y를 사용하여 글자 높이에 관계없이 일관된 위치
            Vector3 charRight = new Vector3(charInfo.topRight.x, charInfo.bottomRight.y, 0);

            // 텍스트 로컬 좌표 → 월드 → 인디케이터 부모 로컬 좌표
            Vector3 worldPos = dialogueText.transform.TransformPoint(charRight);
            Vector3 localPos = nextIndicator.parent.InverseTransformPoint(worldPos);

            nextIndicator.anchoredPosition = new Vector2(localPos.x + indicatorOffset.x, localPos.y + indicatorOffset.y);
            indicatorBaseY = nextIndicator.anchoredPosition.y;

            // 팝인 등장: 스케일 0 → 1 (OutBack: 살짝 튕기는 느낌)
            nextIndicator.localScale = Vector3.zero;
            nextIndicator.gameObject.SetActive(true);
            nextIndicator.DOScale(Vector3.one, indicatorPopDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(StartBlinkAnimation);

            indicatorShown = true;
        }

        void StartBlinkAnimation()
        {
            StopBlinkAnimation();
            
            var img = nextIndicator.GetComponent<Image>();
            if (img != null)
            {
                // 원래 색상 저장 및 alpha 리셋
                indicatorOriginalColor = img.color;
                img.color = new Color(indicatorOriginalColor.r, indicatorOriginalColor.g, indicatorOriginalColor.b, 1f);
                
                // alpha 깜빡임
                indicatorBlink = DOTween.To(
                    () => img.color.a,
                    a => img.color = new Color(indicatorOriginalColor.r, indicatorOriginalColor.g, indicatorOriginalColor.b, a),
                    0.4f,
                    blinkInterval
                )
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);

                // 상하 바운스
                indicatorBounce = nextIndicator
                    .DOAnchorPosY(indicatorBaseY - bounceAmplitude, blinkInterval)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }
            else
            {
                // CanvasGroup 폴백
                var cg = nextIndicator.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    indicatorBlink = cg.DOFade(0.3f, blinkInterval)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo);
                }
            }
        }

        void StopBlinkAnimation()
        {
            indicatorBlink?.Kill();
            indicatorBlink = null;
            indicatorBounce?.Kill();
            indicatorBounce = null;
            
            // 원래 색상으로 복원
            if (nextIndicator != null)
            {
                var img = nextIndicator.GetComponent<Image>();
                if (img != null)
                {
                    img.color = new Color(indicatorOriginalColor.r, indicatorOriginalColor.g, indicatorOriginalColor.b, 1f);
                }
                else
                {
                    var cg = nextIndicator.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
                }
            }
        }

        void HideNextIndicator()
        {
            StopBlinkAnimation();
            
            if (nextIndicator != null)
            {
                nextIndicator.DOKill();  // 팝인 스케일 애니메이션도 정리
                nextIndicator.localScale = Vector3.one;
                nextIndicator.gameObject.SetActive(false);
            }
            indicatorShown = false;
        }

        #endregion

        #region Typing Sound

        [Header("타이핑 피치 설정")]
        [SerializeField] float minPitch = 0.9f;
        [SerializeField] float maxPitch = 1.1f;
        [SerializeField] bool useUISoundManager = true;  // UISoundManager 사용 여부

        void PlayTypingSound()
        {
            // UISoundManager 사용 시 위임
            if (useUISoundManager && LoveAlgo.UI.UISoundManager.Instance != null)
            {
                LoveAlgo.UI.UISoundManager.Instance.PlayTyping();
                return;
            }

            // 기존 방식 (fallback)
            if (typingSFX == null) return;

            if (typingAudioSource != null)
            {
                typingAudioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
                typingAudioSource.PlayOneShot(typingSFX);
            }
            else
            {
                AudioSource.PlayClipAtPoint(typingSFX, Camera.main.transform.position, 0.5f);
            }
        }

        #endregion

        #region 버튼 핸들러

        void OnTitleClick()
        {
            // 확인 팝업 후 타이틀로 이동
            PopupManager.Instance?.Confirm("타이틀로 돌아가시겠습니까?", 
                () => GameManager.Instance.GoToTitle(),
                null);
        }

        void OnSaveClick()
        {
            PopupManager.Instance?.ShowSave();
        }

        void OnLoadClick()
        {
            PopupManager.Instance?.ShowLoad();
        }

        void OnConfigClick()
        {
            PopupManager.Instance?.ShowSettings();
        }

        [Header("Auto 모드 표시")]
        [SerializeField] GameObject autoModeIndicator;  // "AUTO" 아이콘/텍스트
        [SerializeField] Color autoButtonActiveColor = Color.yellow;
        Color autoButtonNormalColor;

        void OnAutoClick()
        {
            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                runner.ToggleAutoMode();
                UpdateAutoVisual(runner.IsAutoMode);
            }
        }

        void UpdateAutoVisual(bool isAuto)
        {
            if (autoButton != null)
            {
                var btnImage = autoButton.GetComponent<Image>();
                if (btnImage != null)
                {
                    if (autoButtonNormalColor == default)
                        autoButtonNormalColor = btnImage.color;
                    btnImage.color = isAuto ? autoButtonActiveColor : autoButtonNormalColor;
                }
            }
            if (autoModeIndicator != null)
                autoModeIndicator.SetActive(isAuto);
        }

        void OnLogClick()
        {
            PopupManager.Instance?.ShowLog(dialogueLog);
        }

        void OnHideClick()
        {
            isHidden = true;
            StopDotsAnimation();
            KillAnimations();

            // 슬라이드 다운 + 페이드 아웃
            slideSequence = DOTween.Sequence();
            if (canvasGroup != null)
                slideSequence.Join(canvasGroup.DOFade(0f, slideDuration).SetEase(Ease.InCubic));
            if (rectTransform != null)
                slideSequence.Join(rectTransform.DOAnchorPosY(originalY - slideDistance, slideDuration).SetEase(Ease.InCubic));
            slideSequence.SetUpdate(true);
            slideSequence.OnComplete(() =>
            {
                if (canvasGroup != null)
                {
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
                if (showButtonObject != null) showButtonObject.SetActive(true);
            });
        }

        void OnShowClick()
        {
            isHidden = false;
            if (showButtonObject != null) showButtonObject.SetActive(false);
            KillAnimations();

            // 시작 위치: 아래에서
            if (rectTransform != null)
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, originalY - slideDistance);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            // 슬라이드 업 + 페이드 인
            slideSequence = DOTween.Sequence();
            if (canvasGroup != null)
                slideSequence.Join(canvasGroup.DOFade(1f, slideDuration).SetEase(Ease.OutCubic));
            if (rectTransform != null)
                slideSequence.Join(rectTransform.DOAnchorPosY(originalY, slideDuration).SetEase(Ease.OutCubic));
            slideSequence.SetUpdate(true);
        }

        #endregion

        #region Log

        /// <summary>
        /// 대사를 로그에 추가
        /// </summary>
        void AddToLog(string speaker, string text)
        {
            // Speaker 이름을 CharacterId로 변환 (썸네일 로드용)
            string charId = null;
            if (!string.IsNullOrEmpty(speaker) && characterDatabase != null)
            {
                charId = characterDatabase.SpeakerToCharacterId(speaker);
            }

            dialogueLog.Add(new DialogueLogEntry
            {
                Speaker = speaker,
                Text = text,
                CharacterId = charId
            });
        }

        /// <summary>
        /// 로그 클리어
        /// </summary>
        public void ClearLog()
        {
            dialogueLog.Clear();
        }

        #endregion

        /// <summary>
        /// 텍스트 클리어
        /// </summary>
        public void Clear()
        {
            if (nameText != null) nameText.text = "";
            if (dialogueText != null) dialogueText.text = "";
            HideNextIndicator();
        }
    }
}
