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
        public string Speaker;
        public string Text;
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

        [Header("설정")]
        [SerializeField] float typingSpeed = 0.04f;
        [SerializeField] float punctuationDelay = 0.15f;  // 문장부호 추가 딜레이

        // 인라인 태그 콜백
        public Action<string> OnEmoteTag;   // 표정 변경 요청

        bool isTyping;
        bool skipRequested;
        string fullText;
        bool isHidden;

        /// <summary>
        /// 마지막 표시된 텍스트 길이 (Auto 딜레이 계산용)
        /// </summary>
        public int LastDisplayedTextLength => fullText?.Length ?? 0;

        // 대사 로그
        readonly List<DialogueLogEntry> dialogueLog = new();
        public IReadOnlyList<DialogueLogEntry> DialogueLog => dialogueLog;

        // 인라인 태그 정규식
        static readonly Regex InlineTagRegex = new(@"<(wait|sfx|emote|speed)=([^/>]+)(/?)>", RegexOptions.Compiled);
        static readonly Regex SpeedEndRegex = new(@"</speed>", RegexOptions.Compiled);

        void Awake()
        {
            HideNextIndicator();
            SetupButtons();
            LoadMonologueDotSprites();
            if (showButtonObject != null) showButtonObject.SetActive(false);
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
        /// </summary>
        public async UniTask ShowTextAsync(string speaker, string text, CancellationToken ct)
        {
            // 대사창 표시 (Hide 상태가 아닐 때만)
            if (!isHidden) Show();
            
            // 변수 치환 ({{PlayerName}} 등)
            speaker = SubstituteVariables(speaker);
            text = SubstituteVariables(text);

            // 로그에 추가
            AddToLog(speaker, text);

            // 화자 설정
            SetSpeaker(speaker);

            // 인라인 태그 파싱
            var segments = ParseInlineTags(text);

            // 타이핑 시작
            fullText = GetCleanText(segments);
            isTyping = true;
            skipRequested = false;
            HideNextIndicator();

            dialogueText.text = "";
            float currentSpeed = typingSpeed;

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
                        // 글자별 타이핑
                        foreach (char c in seg.Content)
                        {
                            if (skipRequested) { CompleteText(); break; }
                            dialogueText.text += c;
                            // 줄바꿈·공백은 사운드/딜레이 생략
                            if (c == '\n' || c == '\r') continue;
                            PlayTypingSound();

                            // 문장부호에서 추가 딜레이
                            float charDelay = currentSpeed;
                            if (c == '.' || c == '!' || c == '?' || c == '~' || c == '\u2026')  // \u2026 = …
                            {
                                charDelay += punctuationDelay;
                            }
                            else if (c == ',')
                            {
                                charDelay += punctuationDelay * 0.5f;
                            }

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

            isTyping = false;
            // 독백 dots는 계속 재생 (클릭 시까지)
            ShowNextIndicator();
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
                    // 독백: dots 애니메이션 시작
                    monologueDotsImage.gameObject.SetActive(true);
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

        public void Show()
        {
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

        public void Hide()
        {
            StopDotsAnimation();  // 독백 dots 정지
            
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

        public async UniTask FadeInAsync(float duration, CancellationToken ct)
        {
            if (canvasGroup == null)
            {
                Show();
                return;
            }

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                await UniTask.Yield(ct);
            }
            canvasGroup.alpha = 1f;
        }

        public async UniTask FadeOutAsync(float duration, CancellationToken ct)
        {
            if (canvasGroup == null)
            {
                Hide();
                return;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                await UniTask.Yield(ct);
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        #endregion

        #region Next Indicator

        bool indicatorShown;
        Tweener indicatorBlink;

        [Header("인디케이터 애니메이션")]
        [SerializeField] float blinkInterval = 0.6f;  // 깜빡임 간격
        [SerializeField] float bounceAmplitude = 4f;  // 상하 바운스 크기 (px)
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
                nextIndicator.gameObject.SetActive(true);
                StartBlinkAnimation();
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
            nextIndicator.gameObject.SetActive(true);
            
            StartBlinkAnimation();
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
                nextIndicator.gameObject.SetActive(false);
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
            Hide();
            if (showButtonObject != null) showButtonObject.SetActive(true);
        }

        void OnShowClick()
        {
            isHidden = false;
            Show();
            if (showButtonObject != null) showButtonObject.SetActive(false);
        }

        #endregion

        #region Log

        /// <summary>
        /// 대사를 로그에 추가
        /// </summary>
        void AddToLog(string speaker, string text)
        {
            dialogueLog.Add(new DialogueLogEntry
            {
                Speaker = speaker,
                Text = text
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
