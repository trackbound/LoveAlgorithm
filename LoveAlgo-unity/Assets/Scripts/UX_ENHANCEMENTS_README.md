# 🎮 비주얼 노벨 UX 개선 가이드

## 📋 개요

이 문서는 LoveAlgo 비주얼 노벨 프로젝트에 추가된 UX 개선 시스템의 통합 가이드입니다.
**사용자가 재밌고 쫀득하게 플레이할 수 있도록** 다양한 인터랙션 피드백과 애니메이션을 제공합니다.

---

## 🆕 추가된 컴포넌트

### 1. **EnhancedChoiceButton**
선택지 전용 향상된 버튼 시스템

#### 주요 기능:
- ✨ 부드러운 호버 애니메이션 (스케일 + 글로우)
- 💥 클릭 시 펀치 효과
- 🌟 선택 확정 시 하이라이트 플래시
- 📊 순차 등장 애니메이션 (stagger)
- 🔊 사운드 피드백 자동 연동

#### 사용 방법:
```csharp
// Prefab에 EnhancedChoiceButton 컴포넌트 추가
// Inspector에서 설정:
// - Background Image
// - Button Text
// - Glow Image (선택적)
// - Hover/Click/Selection 애니메이션 파라미터 조정

// 코드에서 사용:
var choiceButton = buttonPrefab.GetComponent<EnhancedChoiceButton>();
choiceButton.SetText("선택지 텍스트");
choiceButton.SetEntranceDelay(0.1f * index);  // 순차 등장
```

#### 인스펙터 설정:
- **Hover Scale**: 1.05 (호버 시 5% 확대)
- **Click Punch Scale**: 0.95 (클릭 시 95%로 축소)
- **Selection Flash Color**: 노란색 (선택 시 플래시 색상)
- **Entrance Ease**: OutBack (등장 애니메이션 커브)

---

### 2. **InteractionFeedbackManager**
통합 인터랙션 피드백 시스템

#### 주요 기능:
- 🎆 클릭 위치 파티클 효과
- 🌊 리플 효과 (물결 퍼지는 효과)
- ⚡ 화면 플래시 (중요한 장면)
- 📳 햅틱 피드백 (모바일)
- 🔗 선택지/대화 통합 피드백

#### 사용 방법:
```csharp
// 씬에 InteractionFeedbackManager 추가 (싱글톤)

// 선택지 클릭 피드백
InteractionFeedbackManager.Instance.PlayChoiceSelectionFeedback(screenPosition);

// 화면 플래시 (충격적인 장면, 중요한 선택)
await InteractionFeedbackManager.Instance.PlayScreenFlash(Color.red, intensity: 0.5f);

// 리플 효과만
InteractionFeedbackManager.Instance.PlayRipple(screenPosition);

// 햅틱 피드백
InteractionFeedbackManager.Instance.PlayHaptic(HapticType.Selection);
```

#### 설정:
- **Click Particle Prefab**: 클릭 시 파티클 효과
- **Ripple Prefab**: UI Image (Radial Gradient)
- **Screen Flash Group**: FullScreen CanvasGroup + Image
- **Enable Haptics**: 모바일 진동 활성화

---

### 3. **CharacterReactionSystem**
캐릭터 생동감 시스템

#### 주요 기능:
- 🫁 미묘한 숨쉬기 애니메이션 (Idle)
- 💬 대화 중 화자 강조 (밝게 + 확대)
- 👁️ 깜빡임 애니메이션
- 🎭 감정 반응 (놀람, 기쁨, 슬픔, 고개 젓기)
- 🌊 미세한 흔들림 (Idle Sway)

#### 사용 방법:
```csharp
// CharacterSlot GameObject에 CharacterReactionSystem 추가

var reactionSystem = characterSlot.GetComponent<CharacterReactionSystem>();

// 대화 시작 시
reactionSystem.StartSpeaking();

// 대화 종료 시
reactionSystem.StopSpeaking();

// 감정 반응 재생
reactionSystem.PlayReaction(ReactionType.Surprise);  // 놀람
reactionSystem.PlayReaction(ReactionType.Joy);       // 기쁨
reactionSystem.PlayReaction(ReactionType.Sad);       // 슬픔
reactionSystem.PlayReaction(ReactionType.Shake);     // 고개 젓기
```

#### 인스펙터 설정:
- **Enable Breathing**: Idle 숨쉬기 활성화
- **Breathing Scale**: 1.02 (2% 확대/축소)
- **Breathing Duration**: 3초 (한 호흡 사이클)
- **Enable Speaking Highlight**: 대화 시 강조 활성화
- **Highlight Scale**: 1.05 (5% 확대)
- **Highlight Brightness**: 1.2 (20% 밝게)

---

### 4. **DialogueTransitionController**
부드러운 대화 전환 시스템

#### 주요 기능:
- 📛 이름박스 펄스 애니메이션 (화자 변경 시)
- ↔️ 대화창 위치 조정 (화자 위치에 따라)
- 👈👉 화자 인디케이터 표시
- 🌫️ 배경 흐림 효과 (선택적)

#### 사용 방법:
```csharp
// DialogueUI에 DialogueTransitionController 추가

var transitionController = dialogueUI.GetComponent<DialogueTransitionController>();

// 화자 변경 시
await transitionController.OnSpeakerChanged("다은", SlotPosition.L);

// 리셋 (장면 전환 시)
transitionController.Reset();
```

#### 인스펙터 설정:
- **Name Box Transform**: 이름박스 RectTransform
- **Enable Name Box Pulse**: 펄스 활성화
- **Enable Dialogue Box Shift**: 대화창 이동 활성화
- **Shift Amount**: 50px (이동 거리)
- **Speaker Indicators**: 화자별 화살표/아이콘

---

### 5. **EnhancedChoiceUI**
향상된 선택지 UI (기존 ChoiceUI 대체/병행 사용)

#### 주요 기능:
- 🎨 EnhancedChoiceButton 통합
- 📊 순차 등장 애니메이션
- ✅ 선택 확정 시 다른 선택지 페이드아웃
- 🔗 InteractionFeedbackManager 자동 연동

#### 사용 방법:
```csharp
// 기존 ChoiceUI를 EnhancedChoiceUI로 교체 또는 병행 사용

var enhancedChoiceUI = UIManager.Instance.GetComponent<EnhancedChoiceUI>();
var result = await enhancedChoiceUI.ShowAndWaitAsync(options, ct);

// 순차 등장 간격 조정
[SerializeField] float staggerDelay = 0.1f;  // Inspector에서 조정
```

---

### 6. **AdvancedTypingSoundController**
향상된 타이핑 사운드 시스템

#### 주요 기능:
- 🔤 글자별 다양한 사운드 (자음/모음/문장부호)
- 🗣️ 캐릭터별 타이핑 톤 변화
- 🎵 리듬감 있는 타이핑 (연속 글자 가속)
- 🇰🇷 한글 자모 분리 (초성/중성/종성)

#### 사용 방법:
```csharp
// DialogueUI에 AdvancedTypingSoundController 추가

var soundController = GetComponent<AdvancedTypingSoundController>();

// 화자 설정
soundController.SetCurrentCharacter("Daeun");

// 글자 타이핑 시
soundController.PlayTypingSound(character);
```

#### 인스펙터 설정:
- **Consonant Sounds**: 자음 사운드 배열
- **Vowel Sounds**: 모음 사운드 배열
- **Punctuation Sounds**: 문장부호 사운드
- **Character Profiles**: 캐릭터별 음성 프로필
  - Character ID
  - Pitch Multiplier (1.0 = 보통, 1.2 = 높은 목소리, 0.8 = 낮은 목소리)
  - Volume Multiplier

---

## 🔧 통합 가이드

### Step 1: 선택지 개선

#### 기존 ChoiceUI 업그레이드:
1. ChoiceUI Prefab의 버튼에 `EnhancedChoiceButton` 추가
2. Background Image, Button Text, Glow Image 연결
3. Hover/Click 애니메이션 파라미터 조정

#### EnhancedChoiceUI 사용:
1. 새 GameObject에 `EnhancedChoiceUI` 추가
2. Button Container, Button Prefab 설정
3. ScriptRunner 또는 UIManager에서 EnhancedChoiceUI 사용

```csharp
// ScriptRunner.cs에서 선택지 표시 시
var choiceUI = UIManager.Instance.EnhancedChoiceUI;  // 또는 ChoiceUI
var result = await choiceUI.ShowAndWaitAsync(options, ct);
```

---

### Step 2: 인터랙션 피드백 통합

1. 씬에 `InteractionFeedbackManager` GameObject 추가
2. Prefab 설정:
   - Click Particle Prefab (파티클 시스템)
   - Ripple Prefab (UI Image with Radial Gradient)
   - Screen Flash Group (FullScreen CanvasGroup)
3. EnhancedChoiceUI에서 자동 연동됨
4. 수동 호출 가능:

```csharp
// 중요한 대화 시 화면 플래시
InteractionFeedbackManager.Instance.PlayImportantDialogueFeedback(Color.yellow);
```

---

### Step 3: 캐릭터 생동감 추가

1. CharacterSlot Prefab에 `CharacterReactionSystem` 추가
2. Enable Breathing, Enable Speaking Highlight 활성화
3. ScriptRunner 또는 DialogueUI에서 연동:

```csharp
// DialogueUI.ShowTextAsync()에서:
var characterSlot = StageManager.Instance.Character.GetSlot(SlotPosition.C);
var reactionSystem = characterSlot.GetComponent<CharacterReactionSystem>();

// 대화 시작 시
reactionSystem?.StartSpeaking();

// 대화 종료 후 (다음 클릭 대기 전)
reactionSystem?.StopSpeaking();
```

---

### Step 4: 대화 전환 개선

1. DialogueUI GameObject에 `DialogueTransitionController` 추가
2. Name Box Transform, Dialogue Box Transform 연결
3. DialogueUI.ShowTextAsync()에서 화자 변경 시 호출:

```csharp
// DialogueUI.ShowTextAsync()에서:
var transitionController = GetComponent<DialogueTransitionController>();
if (transitionController != null && lastSpeaker != speaker)
{
    var speakerSlot = DetermineSpeakerSlot(speaker);
    await transitionController.OnSpeakerChanged(speaker, speakerSlot, ct);
    lastSpeaker = speaker;
}
```

---

### Step 5: 타이핑 사운드 개선

1. DialogueUI GameObject에 `AdvancedTypingSoundController` 추가
2. 자음/모음/문장부호 사운드 클립 추가
3. 캐릭터별 음성 프로필 설정
4. DialogueUI에서 연동:

```csharp
// DialogueUI.ShowTextAsync() - SetSpeaker()에서:
var soundController = GetComponent<AdvancedTypingSoundController>();
soundController?.SetCurrentCharacter(characterId);

// DialogueUI - PlayTypingSound()에서:
soundController?.PlayTypingSound(c);
```

---

## 🎨 추천 설정 (Best Practices)

### 선택지 애니메이션:
- **Stagger Delay**: 0.08~0.12초 (선택지 개수에 따라 조정)
- **Hover Scale**: 1.03~1.08 (너무 크면 어지러움)
- **Click Duration**: 0.10~0.20초 (빠른 피드백)

### 캐릭터 반응:
- **Breathing**: 항상 활성화 (Idle 생동감)
- **Speaking Highlight**: 다수 캐릭터 대화 시 활성화
- **Blink**: 선택적 (스프라이트 교체 필요)

### 인터랙션 피드백:
- **Ripple**: 선택지 클릭 시 사용
- **Screen Flash**: 중요한 선택, 충격적인 장면만 사용 (남발 금지)
- **Haptic**: 모바일 빌드 시 활성화

### 타이핑 사운드:
- **Base Volume**: 0.3~0.5 (너무 크면 방해됨)
- **Pitch Variation**: 0.05~0.15 (자연스러운 변화)
- **캐릭터 Pitch**: 차이를 크게 (1.3 vs 0.7) 또는 미묘하게 (1.05 vs 0.95)

---

## 🐛 문제 해결

### Q: EnhancedChoiceButton이 클릭되지 않아요
**A**: Button 컴포넌트가 있는지, CanvasGroup의 `Blocks Raycasts`가 true인지 확인하세요.

### Q: 순차 등장 애니메이션이 작동하지 않아요
**A**: EnhancedChoiceButton의 `OnEnable()`에서 PlayEntranceAnimation()이 호출되는지 확인하고, EntranceDelay가 설정되었는지 확인하세요.

### Q: InteractionFeedbackManager가 null이에요
**A**: 씬에 InteractionFeedbackManager가 있는지, Awake()에서 Instance가 설정되는지 확인하세요.

### Q: 캐릭터가 숨을 쉬지 않아요
**A**: CharacterReactionSystem의 `Enable Breathing`이 true인지, `OnEnable()`에서 StartIdleAnimations()이 호출되는지 확인하세요.

### Q: 타이핑 사운드가 너무 시끄러워요
**A**: AdvancedTypingSoundController의 `Base Volume`을 낮추거나, 개별 사운드 클립의 볼륨을 조정하세요.

---

## 📦 패키지 구조

```
Assets/Scripts/
├── UI/
│   ├── EnhancedChoiceButton.cs               (선택지 버튼)
│   ├── InteractionFeedbackManager.cs         (피드백 매니저)
│   └── AdvancedTypingSoundController.cs      (타이핑 사운드)
├── Story/
│   ├── EnhancedChoiceUI.cs                   (선택지 UI)
│   ├── CharacterReactionSystem.cs            (캐릭터 반응)
│   └── DialogueTransitionController.cs       (대화 전환)
└── UX_ENHANCEMENTS_README.md                 (본 문서)
```

---

## 🎯 다음 단계

### 추천 추가 개선:
1. **오토세이브 알림 UI** - 자동 저장 시 미묘한 아이콘 표시
2. **선택지 프리뷰** - 호버 시 결과 힌트 표시
3. **캐릭터 모션 프리셋** - 감정별 애니메이션 세트
4. **대화 히스토리 개선** - 캐릭터 썸네일, 하이라이트
5. **퀵 타임 이벤트** - 제한 시간 선택지

### 성능 최적화:
- DOTween 시퀀스 재사용
- 파티클 풀링
- 사운드 클립 로딩 최적화
- CanvasGroup 캐싱

---

## 📝 라이선스

이 개선 시스템은 LoveAlgo 프로젝트의 일부입니다.
자유롭게 수정하고 확장하여 사용하세요!

---

## 🙋 문의

개선 사항이나 버그 발견 시 이슈를 등록해주세요.
Happy Coding! 🎮✨
