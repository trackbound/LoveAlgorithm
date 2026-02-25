# 비주얼 노벨 게임 UI/UX 개선 분석 및 제안

## 📋 분석 개요
데모 출시를 앞두고 상용 퀄리티를 목표로 한 UI/UX 섬세화 작업

---

## 🎯 주요 발견 사항

### 1. CSV 스크립트 타이밍 이슈

#### 1.1 캐릭터 등장/퇴장 타이밍
**현재 문제점:**
- `CharacterSlot.EnterAsync()`에서 **이미 같은 캐릭터가 있으면 스킵** (line 71-74)
- 연속된 대화에서 캐릭터가 "등장한 채로" 유지되어 자연스럽지만,
- 새로운 씬으로 전환 시 캐릭터가 사라졌다가 다시 나타나는 것이 부자연스러울 수 있음

**개선 제안:**
```csharp
// CharacterSlot.cs - EnterAsync 수정
public async UniTask EnterAsync(string characterName, string emote = "Default", CancellationToken ct = default)
{
    // 이미 같은 캐릭터 + 같은 표정이면 스킵
    if (currentCharacter == characterName && currentEmote == emote && !IsEmpty)
    {
        return;
    }
    
    // 같은 캐릭터지만 표정이 다르면 EmoteAsync 호출
    if (currentCharacter == characterName && currentEmote != emote && !IsEmpty)
    {
        await EmoteAsync(emote, ct);
        return;
    }
    
    // 완전히 새로운 등장일 때만 Enter 애니메이션
    // ... 기존 코드
}
```

#### 1.2 BGM 전환 타이밍
**현재 구현:**
- BGM 변경 시 3초 기본 크로스페이드 (line 159 in AudioManager.cs)
- 캐릭터 등장 시 자동 BGM 전환 기능 있음 (line 118, 255-262)

**문제점:**
1. **BGM 전환이 너무 느림** - 3초는 상용 VN 기준으로 길다
2. CSV 스크립트에서 `await` 사용 시 BGM이 완전히 전환될 때까지 다음 라인 대기
3. Prologue.csv line 8-9: BGM과 캐릭터 등장이 분리되어 있어 부자연스러움

**개선 제안:**
```csv
# 현재 (부자연스러움)
,BG,,BG_Roa_FirstMeet:Cross,await
,Sound,,BGM:Roa,>                   # BGM만 즉시 진행

# 개선안 1: BGM을 먼저 시작하고 캐릭터 등장과 동시에 진행
,BG,,BG_Roa_FirstMeet:Cross,>       # 배경 전환 시작 (대기 안함)
,Sound,,BGM:Roa:Fade:1.5,>           # BGM 1.5초로 빠르게 전환
,Char,,C:Enter:Roa,await             # 캐릭터 등장 대기

# 개선안 2: 모두 동시 진행 (더 부드러움)
,BG,,BG_Roa_FirstMeet:Cross:0.8,>
,Sound,,BGM:Roa:Fade:1.0,>
,Char,,C:Enter:Roa,await
```

**코드 수정:**
```csharp
// AudioManager.cs
[SerializeField] float defaultFadeDuration = 1.5f; // 3f → 1.5f로 단축
```

#### 1.3 대사창 숨김 타이밍
**Prologue.csv line 69-73:**
```csv
,Char,,C:Exit,await
,Overlay,,FadeOut:0.3,>
,FX,,FadeOut:1.0,await
,Text,로아,내일 다시 만나.,click  # ← 대사창이 안보임!
,FX,,FadeIn:0.5,await
```

**문제:** 화면이 페이드아웃된 상태에서 텍스트가 나오면 보이지 않음

**개선안:**
```csv
,Char,,C:Exit,await
,Text,로아,내일 다시 만나.,click    # 대사창 보이는 상태에서 텍스트
,Overlay,,FadeOut:0.3,>
,FX,,FadeOut:1.0,await
,FX,,FadeIn:0.5,await
```

---

### 2. 타이핑 효과 & 텍스트 표시

#### 2.1 타이핑 속도
**DialogueUI.cs line 62:**
```csharp
[SerializeField] float typingSpeed = 0.03f;
```

**문제:** 
- 0.03초/글자 = 약 33자/초
- 한국어 기준으로 약간 빠른 편 (일본 VN은 보통 20-25자/초)

**개선 제안:**
```csharp
[SerializeField] float typingSpeed = 0.04f;  // 25자/초 (더 자연스러움)
[SerializeField] float punctuationDelay = 0.15f;  // 마침표, 느낌표 등에서 약간 쉬기
```

**추가 기능:**
```csharp
// DialogueUI.cs - ShowTextAsync 내부
foreach (char c in seg.Content)
{
    if (skipRequested) { CompleteText(); break; }
    dialogueText.text += c;
    
    // 줄바꿈·공백은 사운드/딜레이 생략
    if (c == '\n' || c == '\r') continue;
    
    PlayTypingSound();
    
    // 문장부호에서 추가 딜레이
    float charDelay = currentSpeed;
    if (c == '.' || c == '!' || c == '?' || c == '~')
    {
        charDelay += punctuationDelay;
    }
    else if (c == ',' || c == '…')
    {
        charDelay += punctuationDelay * 0.5f;
    }
    
    await UniTask.Delay(TimeSpan.FromSeconds(charDelay), cancellationToken: ct);
}
```

#### 2.2 다음 표시 인디케이터
**현재 구현 (DialogueUI.cs line 527-584):**
- 마지막 글자 위치를 계산하여 인디케이터 배치
- 깜빡임 애니메이션 (alpha 0.3 ↔ 1.0, 0.5초 간격)

**개선 제안:**
```csharp
// DialogueUI.cs
[Header("인디케이터 애니메이션")]
[SerializeField] float blinkInterval = 0.6f;      // 0.5f → 0.6f (약간 느리게)
[SerializeField] float blinkMinAlpha = 0.4f;     // 0.3f → 0.4f (더 잘 보이게)
[SerializeField] AnimationCurve blinkCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

void StartBlinkAnimation()
{
    StopBlinkAnimation();
    var img = nextIndicator.GetComponent<Image>();
    if (img != null)
    {
        indicatorOriginalColor = img.color;
        img.color = new Color(indicatorOriginalColor.r, indicatorOriginalColor.g, indicatorOriginalColor.b, 1f);
        
        indicatorBlink = DOTween.To(
            () => img.color.a,
            a => img.color = new Color(indicatorOriginalColor.r, indicatorOriginalColor.g, indicatorOriginalColor.b, a),
            blinkMinAlpha,
            blinkInterval
        )
        .SetEase(blinkCurve)  // 커스텀 이징 커브
        .SetLoops(-1, LoopType.Yoyo);
    }
}
```

---

### 3. 화면 전환 & 연출

#### 3.1 배경 전환
**BackgroundLayer.cs - FadeAsync (line 115-136):**

**문제:**
- Fade 전환 시 화면이 완전히 검게 변했다가 다시 밝아짐
- 일부 씬에서는 Cross 전환이 더 자연스러울 수 있음

**개선 제안:**
```csv
# Prologue.csv line 30 - 배경 전환 개선
,Char,,C:Exit,await
,BG,,MyRoom/BG_MyRoom_Interior_Night_LightOn:Cross,await  # Fade → Cross로 변경

# line 88 - 아침 기상
,BG,,MyRoom/BG_MyRoom_Bed_Day:Cross,await  # 침실 → 침실 전환은 Cross가 자연스러움
,FX,,EyeOpen:1.5,await
```

**추가 전환 효과:**
```csharp
// BackgroundLayer.cs - 새로운 전환 타입 추가
public enum BGTransition
{
    Cut,
    Fade,
    Cross,
    SlideLeft,    // 왼쪽으로 슬라이드
    SlideRight,   // 오른쪽으로 슬라이드
    Zoom          // 줌 인/아웃
}
```

#### 3.2 FX 효과 타이밍
**ScreenFX.cs - EyeOpen/Close:**

**Prologue.csv line 86-90:**
```csv
,BG,,MyRoom/BG_MyRoom_Bed_Day:Cross,await
,FX,,EyeOpen:1.5,await
,Sound,,BGM:Roa,>
,Overlay,,Roa_Theme:FadeIn:0.5,await
,Char,,C:Enter:Roa,await
```

**개선안 - 더 자연스러운 연출:**
```csv
# 눈을 뜨면서 동시에 모든 요소가 나타남
,BG,,MyRoom/BG_MyRoom_Bed_Day:Cut,>          # 먼저 배경만 세팅
,FX,,EyeClose,>                              # 눈 감은 상태로 시작
,Sound,,BGM:Roa:Fade:0.8,>                    # BGM 빠르게 페이드인
,FX,,EyeOpen:2.0,>                           # 눈 뜨는 효과 (await 제거)
,Overlay,,Roa_Theme:FadeIn:1.5,>             # 오버레이도 동시에
,Char,,C:Enter:Roa,await                     # 캐릭터만 마지막에 대기
```

#### 3.3 카메라 흔들림
**ScreenFX.cs line 271-299:**

**개선 제안:**
```csharp
// ScreenFX.cs
[Header("Shake 프리셋")]
[SerializeField] float shakePresetWeak = 10f;      // 약한 흔들림
[SerializeField] float shakePresetMedium = 25f;    // 중간
[SerializeField] float shakePresetStrong = 50f;    // 강한 흔들림

// CSV에서 사용:
// FX,,CamShake:0.3:Weak
// FX,,CamShake:0.5:Medium
// FX,,CamShake:0.8:Strong

public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
{
    // ...
    case "CamShake":
        float shakeDuration = parts.Length > 1 && float.TryParse(parts[1], out float sd) ? sd : 0.3f;
        float shakeStrength;
        
        if (parts.Length > 2)
        {
            // 문자열 프리셋 지원
            switch (parts[2].ToLower())
            {
                case "weak": shakeStrength = shakePresetWeak; break;
                case "medium": shakeStrength = shakePresetMedium; break;
                case "strong": shakeStrength = shakePresetStrong; break;
                default: float.TryParse(parts[2], out shakeStrength); break;
            }
        }
        else
        {
            shakeStrength = shakePresetMedium;
        }
        
        await CamShakeAsync(shakeDuration, shakeStrength, ct);
        break;
}
```

---

### 4. Auto 모드 & 사용성

#### 4.1 Auto 모드 딜레이
**ScriptRunner.cs line 34:**
```csharp
float autoDelay = 2f;
```

**문제:**
- 2초는 VN 기준으로 적당하지만, 텍스트 길이에 따라 조절되어야 함

**개선 제안:**
```csharp
// ScriptRunner.cs
[SerializeField] float autoDelayBase = 1.5f;           // 기본 딜레이
[SerializeField] float autoDelayPerCharacter = 0.05f;  // 글자당 추가 시간
[SerializeField] float autoDelayMin = 1.0f;            // 최소
[SerializeField] float autoDelayMax = 5.0f;            // 최대

async UniTask WaitForClickAsync(CancellationToken ct)
{
    waitingForClick = true;
    clickReceived = false;

    if (autoMode)
    {
        // 텍스트 길이에 따라 딜레이 조절
        float textLength = fullText?.Length ?? 0;
        float dynamicDelay = autoDelayBase + (textLength * autoDelayPerCharacter);
        dynamicDelay = Mathf.Clamp(dynamicDelay, autoDelayMin, autoDelayMax);
        
        var delayTask = UniTask.Delay(TimeSpan.FromSeconds(dynamicDelay), cancellationToken: ct);
        var clickTask = UniTask.WaitUntil(() => clickReceived, cancellationToken: ct);
        await UniTask.WhenAny(delayTask, clickTask);
    }
    else
    {
        await UniTask.WaitUntil(() => clickReceived, cancellationToken: ct);
    }

    waitingForClick = false;
    clickReceived = false;
}
```

#### 4.2 UI 피드백
**DialogueUI.cs - Auto 버튼:**

**개선 제안:**
```csharp
// DialogueUI.cs
[Header("Auto 모드 표시")]
[SerializeField] GameObject autoModeIndicator;  // "AUTO" 표시
[SerializeField] Color autoButtonColorNormal = Color.white;
[SerializeField] Color autoButtonColorActive = Color.yellow;

void OnAutoClick()
{
    var runner = ScriptRunner.Instance;
    if (runner != null)
    {
        runner.ToggleAutoMode();
        UpdateAutoButtonVisual(runner.IsAutoMode);
    }
}

void UpdateAutoButtonVisual(bool isAuto)
{
    if (autoButton != null)
    {
        var colors = autoButton.colors;
        colors.normalColor = isAuto ? autoButtonColorActive : autoButtonColorNormal;
        autoButton.colors = colors;
    }
    
    if (autoModeIndicator != null)
    {
        autoModeIndicator.SetActive(isAuto);
    }
}
```

---

### 5. 독백 표현 (Monologue Dots)

**DialogueUI.cs line 284-315:**

**현재 문제:**
- dots 애니메이션이 계속 재생됨
- 독백일 때 nameBox가 항상 표시됨 (line 335)

**개선 제안:**
```csharp
// DialogueUI.cs - SetSpeaker 수정
void SetSpeaker(string speaker)
{
    bool hasName = !string.IsNullOrEmpty(speaker);
    bool isMonologue = !hasName;

    if (monologueDotsImage != null)
    {
        if (isMonologue && monologueDotSprites != null && monologueDotSprites.Length > 0)
        {
            // 독백: dots 애니메이션 시작
            monologueDotsImage.gameObject.SetActive(true);
            StartDotsAnimation();
            
            if (nameText != null) nameText.gameObject.SetActive(false);
            
            // nameBox 크기를 dots에 맞게 조정
            if (nameBox != null)
            {
                var nameBoxRect = nameBox.GetComponent<RectTransform>();
                if (nameBoxRect != null)
                {
                    nameBoxRect.sizeDelta = new Vector2(100f, nameBoxRect.sizeDelta.y); // dots용 작은 크기
                }
            }
        }
        else
        {
            StopDotsAnimation();
            monologueDotsImage.gameObject.SetActive(false);
            
            if (nameText != null)
            {
                nameText.text = hasName ? speaker : "";
                nameText.gameObject.SetActive(hasName);
                
                if (hasName && characterDatabase != null)
                {
                    var charData = characterDatabase.GetCharacterById(speaker);
                    if (charData != null)
                    {
                        nameText.color = charData.nameColor;  // 캐릭터별 색상
                    }
                }
            }
            
            // nameBox 크기 복원
            if (nameBox != null)
            {
                var nameBoxRect = nameBox.GetComponent<RectTransform>();
                if (nameBoxRect != null)
                {
                    nameBoxRect.sizeDelta = new Vector2(200f, nameBoxRect.sizeDelta.y); // 일반 크기
                }
            }
        }
    }

    if (nameBox != null)
    {
        nameBox.SetActive(hasName || isMonologue);  // 이름 있거나 독백일 때만 표시
    }
}
```

---

### 6. CG 표시 & 숨김

**Prologue.csv line 542-556 (하예은 CG):**
```csv
,CG,,CG/Roa_FirstMeet:Fade:1.0,await
,FX,,DialogueShow,>

,Text,,예은이 덥석 내 왼쪽 무릎...
```

**문제:**
1. CG 이름이 잘못됨 (`Roa_FirstMeet` → `Yeun_LegDesk` 등)
2. DialogueShow 명령이 FX로 처리되는 것이 비직관적

**개선 제안:**
```csv
# CG 표시 전 캐릭터 퇴장은 ScriptRunner에서 자동 처리
,Char,,C:Exit,await                           # 캐릭터 먼저 퇴장
,CG,,Yeun/CG_LegDesk:Fade:1.0,await           # CG 표시 (대사창은 ScriptRunner가 자동 숨김)

# CG 위에 대사 표시
,Text,,예은이 덥석 내 왼쪽 무릎 뒤편에...  # 대사창 자동 표시 (ScriptRunner)

# CG 종료
,CG,,Exit:Fade:0.8,await                      # CG 종료 (대사창 자동 복원)
,Char,,C:Enter:Yeun,await                     # 캐릭터 다시 등장
```

**코드 수정:**
```csharp
// ScriptRunner.cs - ExecuteCGAsync 개선
async UniTask ExecuteCGAsync(ScriptLine line, CancellationToken ct)
{
    var parts = line.Value.Split(':');
    bool isExit = parts[0].Equals("Exit", System.StringComparison.OrdinalIgnoreCase);
    
    if (!isExit)
    {
        // CG 표시 시: 캐릭터 자동 퇴장 (이미 구현됨)
        var character = StageManager.Instance?.Character;
        if (character != null)
        {
            await character.ExitAllAsync(ct);
        }
        
        // 대사창 부드럽게 숨김 (Hide 대신 FadeOut)
        var dialogueUI = UIManager.Instance?.DialogueUI;
        if (dialogueUI != null)
        {
            await dialogueUI.FadeOutAsync(0.3f, ct);  // ✨ Hide() → FadeOutAsync()
        }
    }
    else
    {
        // CG 종료 시: 대사창 부드럽게 표시
        var dialogueUI = UIManager.Instance?.DialogueUI;
        if (dialogueUI != null)
        {
            await dialogueUI.FadeInAsync(0.3f, ct);   // ✨ Show() → FadeInAsync()
        }
    }
    
    // CG 레이어 제어
    var cg = StageManager.Instance?.CG;
    if (cg != null)
    {
        await cg.ExecuteAsync(line.Value, ct);
    }
}
```

---

## 🎨 상용 퀄리티를 위한 추가 제안

### 7. 사운드 디자인

#### 7.1 타이핑 사운드 다양화
```csharp
// DialogueUI.cs
[Header("타이핑 사운드 변주")]
[SerializeField] AudioClip[] typingSounds;  // 여러 사운드 준비
[SerializeField] int soundChangeInterval = 3;  // 3글자마다 사운드 변경

int typingSoundIndex = 0;

void PlayTypingSound()
{
    if (useUISoundManager && UISoundManager.Instance != null)
    {
        UISoundManager.Instance.PlayTyping();
        return;
    }

    if (typingSounds == null || typingSounds.Length == 0) return;

    if (typingAudioSource != null)
    {
        // 사운드 변주
        typingSoundIndex = (typingSoundIndex + 1) % soundChangeInterval;
        if (typingSoundIndex == 0)
        {
            int soundIdx = UnityEngine.Random.Range(0, typingSounds.Length);
            AudioClip clip = typingSounds[soundIdx];
            
            typingAudioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            typingAudioSource.PlayOneShot(clip);
        }
    }
}
```

#### 7.2 캐릭터별 타이핑 사운드
```csharp
// CharacterDatabase.cs에 추가
[System.Serializable]
public class CharacterTypingSound
{
    public string characterId;
    public AudioClip[] typingSounds;
    public float pitchVariation = 0.1f;
}

// DialogueUI.cs
public async UniTask ShowTextAsync(string speaker, string text, CancellationToken ct)
{
    // 캐릭터별 타이핑 사운드 로드
    if (characterDatabase != null && !string.IsNullOrEmpty(speaker))
    {
        var charData = characterDatabase.GetCharacterById(speaker);
        if (charData != null && charData.typingSounds != null)
        {
            currentTypingSounds = charData.typingSounds;
        }
    }
    
    // ... 기존 코드
}
```

### 8. 선택지 표시 개선

**ChoiceUI 개선:**
```csharp
// ChoiceUI.cs
[Header("선택지 애니메이션")]
[SerializeField] float choiceAppearDelay = 0.1f;  // 선택지 간 딜레이
[SerializeField] float choiceAppearDuration = 0.3f;
[SerializeField] Ease choiceAppearEase = Ease.OutBack;

public async UniTask ShowAndWaitAsync(List<OptionData> options, CancellationToken ct)
{
    // ... 기존 코드
    
    // 선택지 하나씩 나타나기
    for (int i = 0; i < buttonList.Count; i++)
    {
        var button = buttonList[i];
        button.transform.localScale = Vector3.zero;
        
        // 순차적으로 나타남
        await UniTask.Delay(TimeSpan.FromSeconds(choiceAppearDelay * i), cancellationToken: ct);
        _ = button.transform.DOScale(1f, choiceAppearDuration).SetEase(choiceAppearEase);
    }
    
    // ... 나머지 코드
}
```

### 9. 로딩 & 전환 효과

```csharp
// 새 파일: Assets/Scripts/Core/TransitionManager.cs
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace LoveAlgo.Core
{
    public class TransitionManager : MonoBehaviour
    {
        public static TransitionManager Instance { get; private set; }
        
        [SerializeField] CanvasGroup loadingPanel;
        [SerializeField] Image loadingIcon;
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public async UniTask ShowLoadingAsync(float duration = 0.3f)
        {
            loadingPanel.gameObject.SetActive(true);
            await loadingPanel.DOFade(1f, duration).ToUniTask();
            
            // 로딩 아이콘 회전
            loadingIcon.transform.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear);
        }
        
        public async UniTask HideLoadingAsync(float duration = 0.3f)
        {
            await loadingPanel.DOFade(0f, duration).ToUniTask();
            loadingPanel.gameObject.SetActive(false);
            loadingIcon.transform.DOKill();
        }
    }
}
```

---

## 📊 CSV 스크립트 모범 사례

### 기본 원칙
1. **동시 진행할 것은 `>`로** - BGM, 짧은 효과
2. **완료 대기할 것은 `await`로** - 캐릭터 등장, 중요한 화면 전환
3. **연출은 겹쳐서** - 여러 효과를 동시에 시작해 자연스럽게

### 예시 1: 캐릭터 첫 등장
```csv
# ❌ 나쁜 예 - 모든 것이 순차적
,BG,,School_Gate:Fade,await
,Sound,,BGM:Daily,await         # ← BGM이 완전히 전환될 때까지 대기 (3초)
,Char,,C:Enter:Roa,await

# ✅ 좋은 예 - 자연스러운 흐름
,BG,,School_Gate:Fade:0.8,>     # 배경 페이드 시작
,Sound,,BGM:Daily:Fade:1.2,>    # BGM 빠르게 전환 (동시 진행)
,Char,,C:Enter:Roa,await        # 캐릭터 등장 대기
```

### 예시 2: 장면 전환
```csv
# ❌ 나쁜 예 - 끊김이 느껴짐
,Char,,C:Exit,await
,FX,,FadeOut:0.5,await
,BG,,Night_Street,>
,FX,,FadeIn:0.5,await
,Char,,C:Enter:Yeun,await

# ✅ 좋은 예 - 매끄러운 전환
,Char,,C:Exit,>                      # 캐릭터 퇴장 시작 (대기 안함)
,FX,,FadeOut:0.8,await               # 화면 페이드아웃 대기
,BG,,Night_Street,>                  # 배경 즉시 교체
,Sound,,BGM:Night:Fade:1.0,>         # BGM 전환 시작
,FX,,FadeIn:0.8,>                    # 화면 페이드인 시작
,Char,,C:Enter:Yeun,await            # 캐릭터 등장 대기
```

### 예시 3: 감정적인 순간
```csv
# 강한 감정 표현 - 복합 연출
,Char,,C:Emote:Surprised,>           # 표정 변화 시작
,FX,,CamShake:0.3:Weak,>             # 약한 흔들림
,Sound,,SFX:Gasp,>                   # 효과음
,Text,로아,{{PlayerName}}!,click     # 대사

# 충격적인 순간
,FX,,Flash:0.2,>                     # 플래시
,FX,,CamShake:0.5:Strong,>           # 강한 흔들림
,Sound,,SFX:Impact,>                 # 충격음
,Char,,C:Emote:Shocked,await         # 표정 변화 대기
,Text,로아,이럴 수가...!,click
```

---

## 🚀 우선순위 구현 로드맵

### Phase 1: 핵심 타이밍 개선 (1-2일)
- [ ] BGM 전환 속도 단축 (3초 → 1.5초)
- [ ] 타이핑 속도 조정 + 문장부호 딜레이
- [ ] Auto 모드 텍스트 길이 반영
- [ ] CSV 스크립트 타이밍 수정 (Prologue.csv)

### Phase 2: 연출 퀄리티 (2-3일)
- [ ] 캐릭터 Enter 스킵 로직 개선
- [ ] CG 표시 시 Fade 효과
- [ ] 인디케이터 애니메이션 개선
- [ ] 독백 표현 nameBox 크기 조정

### Phase 3: 상용 폴리싱 (3-4일)
- [ ] 선택지 순차 애니메이션
- [ ] 캐릭터별 타이핑 사운드
- [ ] 타이핑 사운드 변주
- [ ] TransitionManager 구현
- [ ] 카메라 효과 프리셋

### Phase 4: 최종 테스트 (1-2일)
- [ ] 전체 시나리오 플레이테스트
- [ ] 타이밍 미세 조정
- [ ] 버그 수정
- [ ] 데모 빌드 최적화

---

## 📝 체크리스트

### 연출 체크리스트
- [ ] 모든 BGM 전환이 1.5초 이내인가?
- [ ] 캐릭터 등장 시 부자연스러운 대기 시간이 없는가?
- [ ] 대사창이 보이지 않는 상태에서 텍스트가 나오지 않는가?
- [ ] CG 표시/숨김이 부드러운가?
- [ ] 화면 전환 시 끊김이 느껴지지 않는가?

### 사용성 체크리스트
- [ ] Auto 모드가 자연스럽게 동작하는가?
- [ ] Skip 기능이 즉각 반응하는가?
- [ ] 인디케이터가 명확하게 보이는가?
- [ ] 선택지가 읽기 쉬운가?
- [ ] 로그 기능이 완전한가?

### 기술 체크리스트
- [ ] 모든 await/> 사용이 적절한가?
- [ ] 메모리 누수가 없는가?
- [ ] 프레임 드롭이 없는가?
- [ ] 모바일에서도 부드럽게 동작하는가?

---

## 🎮 참고: 상용 VN 벤치마크

### 타이밍 기준 (일본 주요 VN)
- 타이핑 속도: 20-30자/초
- BGM 크로스페이드: 1-2초
- 캐릭터 등장: 0.3-0.5초
- 배경 전환 (Fade): 0.5-1초
- Auto 모드 딜레이: 텍스트 길이 + 1-2초

### UI 디자인 원칙
1. **가독성 최우선** - 텍스트가 가장 중요
2. **부드러운 전환** - 모든 것이 fade/slide로 나타남
3. **즉각적인 피드백** - 클릭/선택 시 즉시 반응
4. **명확한 상태 표시** - Auto/Skip 등 현재 모드 표시

---

이 문서를 기반으로 단계적으로 개선하시면 상용 퀄리티에 도달하실 수 있습니다!
