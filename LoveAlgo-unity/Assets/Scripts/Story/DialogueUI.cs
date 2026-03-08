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
        [SerializeField] float typingSpeed = 0.03f;
        const float baseTypingSpeed = 0.044f;  // 포스트-포즈 스케일 기준 속도 (기본 슬라이더 0.3 시 실제 값)

        [Header("포스트-포즈 (문장부호/개행 뒤 일시정지 — baseTypingSpeed 기준값, 속도에 비례 스케일)")]
        [SerializeField] float newlinePostPause = 0.18f;      // 개행 후 정지
        [SerializeField] float periodPostPause = 0.12f;       // . ! ? ~ … 후 정지
        [SerializeField] float commaPostPause = 0.06f;        // , 후 정지
        [SerializeField] float ellipsisDotPause = 0.07f;      // 연속 점(.) 각각의 딜레이
        [SerializeField] float jitterAmount = 0.04f;          // 미세 리듬 변동 (±4%)

        // 인라인 태그 콜백
        public Action<string> OnEmoteTag;   // 표정 변경 요청

        bool isTyping;
        bool skipRequested;
        string fullText;
        bool isHidden;
        bool needsFadeIn;  // 다음 Show 시 페이드인 필요 여부

        // 타이핑 사운드 — 쿨다운은 UISoundManager에서 일괄 관리

        /// <summary>
        /// 마지막 표시된 텍스트 길이 (Auto 딜레이 계산용)
        /// </summary>
        public int LastDisplayedTextLength => fullText?.Length ?? 0;

        /// <summary>
        /// 텍스트 속도 설정 (0=느림, 1=빠름)
        /// </summary>
        public void SetTextSpeed(float normalized)
        {
            // 0=느림(0.068s/char), 0.4=기본(0.044s), 1=빠름(0.008s/char)
            typingSpeed = Mathf.Lerp(0.068f, 0.008f, normalized);
        }

        // 대사 로그
        readonly List<DialogueLogEntry> dialogueLog = new();
        public IReadOnlyList<DialogueLogEntry> DialogueLog => dialogueLog;

        // 인라인 태그 정규식 (값에 / 가 포함될 수 있으므로 [^>]+ 사용, trailing / 는 후처리로 제거)
        static readonly Regex InlineTagRegex = new(@"<(wait|sfx|emote|speed)=([^>]+)>", RegexOptions.Compiled);
        static readonly Regex SpeedEndRegex = new(@"</speed>", RegexOptions.Compiled);
        // 로그용: 모든 인라인 태그 제거
        static readonly Regex StripTagsRegex = new(@"<(wait|sfx|emote|speed)=[^>]+>|</speed>", RegexOptions.Compiled);

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
                originalY = rectTransform.anchoredPosition.y;

            // Auto 버튼 기본 색상을 즉시 캡처 (타이밍 문제 방지)
            if (autoButton != null)
            {
                var btnImage = autoButton.GetComponent<Image>();
                if (btnImage != null)
                    autoButtonNormalColor = btnImage.color;
            }

            HideNextIndicator();
            SetupButtons();
            LoadMonologueDotSprites();
            if (showButtonObject != null) showButtonObject.SetActive(false);

            // ScriptRunner의 Auto 모드 변경 이벤트 구독
            SubscribeAutoModeEvent();

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
            if (dialogueText == null) return;

            // 변수 치환 ({{PlayerName}} 등)
            speaker = SubstituteVariables(speaker);
            text = SubstituteVariables(text);

            // 로그에 추가 (인라인 태그 제거된 클린 텍스트)
            AddToLog(speaker, StripTagsRegex.Replace(text, ""));

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

            // 대사창 표시 — 페이드인과 타이핑을 동시 시작 (대기 제거)
            if (!isHidden)
            {
                Show();
            }

            float currentSpeed = typingSpeed;
            // 포스트-포즈 스케일: 현재 속도/기준 속도 → 빠르면 포즈도 짧게, 느리면 길게
            float pauseScale = Mathf.Clamp(typingSpeed / baseTypingSpeed, 0.3f, 2.5f);
            int visibleCount = 0;

            try
            {
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
                            // ── 상용 VN 표준: 일정 속도 타이핑 + 포스트-포즈 ──
                            // 글자는 일정하게 빠르게 찍고, 문장부호/개행 직후에만 잠시 멈춤
                            string content = seg.Content;
                            for (int i = 0; i < content.Length; i++)
                            {
                                if (skipRequested) { CompleteText(); return; }

                                char c = content[i];
                                visibleCount++;
                                dialogueText.maxVisibleCharacters = visibleCount;

                                // 개행: 글자 찍기만 하고 포스트-포즈로 처리
                                if (c == '\n' || c == '\r')
                                {
                                    if (!skipRequested)
                                        await UniTask.Delay(TimeSpan.FromSeconds(newlinePostPause * pauseScale), cancellationToken: ct);
                                    continue;
                                }

                                // 타이핑 사운드 (쿨다운은 UISoundManager에서 관리)
                                LoveAlgo.UI.UISoundManager.Instance?.PlayTyping();

                                // 기본 딜레이 (일정 속도) + 미세 지터
                                float charDelay = currentSpeed * (1f + UnityEngine.Random.Range(-jitterAmount, jitterAmount));
                                await UniTask.Delay(TimeSpan.FromSeconds(charDelay), cancellationToken: ct);

                                // ── 포스트-포즈: 글자 찍힌 직후 추가 정지 (속도 비례) ──
                                float postPause = GetPostPause(c, content, i) * pauseScale;
                                if (postPause > 0f && !skipRequested)
                                {
                                    await UniTask.Delay(TimeSpan.FromSeconds(postPause), cancellationToken: ct);
                                }
                            }
                            break;

                        case SegmentType.Wait:
                            if (!skipRequested && float.TryParse(seg.Content, out float waitTime))
                            {
                                // skipRequested 되면 즉시 탈출하도록 프레임 단위 대기
                                float elapsed = 0f;
                                while (elapsed < waitTime && !skipRequested)
                                {
                                    await UniTask.Yield(ct);
                                    elapsed += Time.deltaTime;
                                }
                            }
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

                dialogueText.maxVisibleCharacters = fullText.Length;  // 안전: 혹시 누락 방지
                ShowNextIndicator();
            }
            finally
            {
                // 로드/취소로 OperationCanceledException이 발생해도 반드시 isTyping 해제
                isTyping = false;
            }
        }

        /// <summary>
        /// 포스트-포즈 계산 — 글자 출력 직후 추가 정지 시간
        /// 상용 VN 표준: 일정 속도로 찍다가 문장부호 직후에만 잠시 멈춤
        /// </summary>
        float GetPostPause(char c, string text, int index)
        {
            // 쉼표
            if (c == ',')
                return commaPostPause;

            // 연속 문장부호 (... !! ?! 등)
            if (IsSentenceEndPunctuation(c))
            {
                // 뒤에 같은 종류가 더 오면 → 연속 점 딜레이 (각 점마다 약간)
                if (index + 1 < text.Length && IsSentenceEndPunctuation(text[index + 1]))
                    return ellipsisDotPause;

                // 마지막 문장부호 → 풀 포스트-포즈
                return periodPostPause;
            }

            return 0f;
        }

        bool IsSentenceEndPunctuation(char c)
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

                // trailing / 제거 (self-closing 태그: <emote=Default/> → "Default/" → "Default")
                if (tagValue.EndsWith("/"))
                    tagValue = tagValue.Substring(0, tagValue.Length - 1);

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
            var name = GameState.Instance.PlayerName;
            return text
                .Replace("{{PlayerName}}", name)
                .Replace("{{Player}}", name);
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
            StopBlinkAnimation();

            // DOTween: 이 오브젝트에 연결된 모든 트윈 정리
            DOTween.Kill(nextIndicator);

            // Auto 모드 이벤트 구독 해제
            if (ScriptRunner.IsAlive)
                ScriptRunner.Instance.OnAutoModeChanged -= UpdateAutoVisual;
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
                dialogueText.ForceMeshUpdate();
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
        /// 즉시 텍스트를 완성하고 인디케이터를 표시
        /// (플래그만 세우면 다음 await까지 지연되므로 CompleteText 직접 호출)
        /// </summary>
        public void RequestSkip()
        {
            CompleteText();
        }

        /// <summary>
        /// ScriptRunner의 Auto 모드 변경 이벤트 구독
        /// </summary>
        void SubscribeAutoModeEvent()
        {
            // Awake 시점에 ScriptRunner가 아직 없을 수 있으므로 지연 구독
            SubscribeAutoModeEventAsync().Forget();
        }

        async UniTaskVoid SubscribeAutoModeEventAsync()
        {
            // ScriptRunner가 준비될 때까지 대기 (최대 5초)
            float elapsed = 0f;
            while (!ScriptRunner.IsAlive && elapsed < 5f)
            {
                await UniTask.Yield();
                elapsed += Time.deltaTime;
            }

            if (ScriptRunner.IsAlive)
                ScriptRunner.Instance.OnAutoModeChanged += UpdateAutoVisual;
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

        public void UpdateAutoVisual(bool isAuto)
        {
            if (autoButton != null)
            {
                var btnImage = autoButton.GetComponent<Image>();
                if (btnImage != null)
                {
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
            if (dialogueText != null)
            {
                dialogueText.text = "";
                dialogueText.maxVisibleCharacters = 0;
            }
            HideNextIndicator();
        }
    }
}
