# 🚀 UX 개선 퀵 스타트 가이드

5분 안에 비주얼 노벨 UX를 쫀득하게 만들어보세요!

---

## ⚡ 1분 퀵 셋업

### Step 1: InteractionFeedbackManager 추가 (30초)

1. 씬에 빈 GameObject 생성 → "InteractionFeedbackManager"로 이름 변경
2. `InteractionFeedbackManager` 컴포넌트 추가
3. 완료! (Prefab은 선택사항)

### Step 2: EnhancedChoiceButton 사용 (2분)

1. ChoiceUI의 Button Prefab 열기
2. `EnhancedChoiceButton` 컴포넌트 추가
3. Inspector에서 연결:
   - Background Image: 버튼 배경 Image
   - Button Text: TMP_Text 컴포넌트
   - Glow Image (선택): 호버 글로우용 Image
4. Prefab 저장

### Step 3: 캐릭터 숨쉬기 추가 (1분)

1. CharacterSlot Prefab 열기
2. `CharacterReactionSystem` 컴포넌트 추가
3. Inspector에서 설정:
   - Enable Breathing: ✅
   - Enable Speaking Highlight: ✅
4. Prefab 저장

**완료! 🎉**

---

## 🎨 5분 풀 셋업 (모든 기능)

### 1. 선택지 개선 (1분)

```
1. ChoiceUI Prefab 열기
2. EnhancedChoiceButton 추가 (위 Step 2 참조)
3. Hover Scale: 1.05
4. Click Punch Scale: 0.95
5. Selection Flash Color: Yellow
6. 저장
```

### 2. 인터랙션 피드백 (1분)

```
1. InteractionFeedbackManager GameObject 생성 (위 Step 1 참조)
2. (선택) Prefab 추가:
   - Ripple Prefab: UI Image (Radial Gradient)
   - Click Particle Prefab: Particle System
   - Screen Flash Group: FullScreen CanvasGroup + White Image
3. 저장
```

### 3. 캐릭터 생동감 (1분)

```
1. CharacterSlot Prefab 열기
2. CharacterReactionSystem 추가
3. 설정:
   - Enable Breathing: ✅
   - Breathing Scale: 1.02
   - Breathing Duration: 3초
   - Enable Speaking Highlight: ✅
   - Highlight Scale: 1.05
4. 저장
```

### 4. 대화 전환 (1분)

```
1. DialogueUI GameObject 선택
2. DialogueTransitionController 추가
3. 연결:
   - Name Box Transform: 이름박스 RectTransform
   - Dialogue Box Transform: 대화창 RectTransform
4. 설정:
   - Enable Name Box Pulse: ✅
   - Enable Dialogue Box Shift: (선택)
5. 저장
```

### 5. 타이핑 사운드 (1분)

```
1. DialogueUI GameObject 선택
2. AdvancedTypingSoundController 추가
3. (선택) 사운드 클립 추가:
   - Consonant Sounds: 자음 사운드 배열
   - Vowel Sounds: 모음 사운드 배열
   - Punctuation Sounds: 문장부호 사운드
4. (선택) Character Profiles 설정
5. 저장
```

---

## 🔗 코드 통합 (3분)

### 최소 통합 (선택지만)

기존 ChoiceUI Prefab에 EnhancedChoiceButton만 추가했다면 **추가 코드 없이 바로 작동**합니다!

### 권장 통합 (모든 기능)

`ScriptRunner.cs` 또는 `DialogueUI.cs`에 다음 코드 추가:

```csharp
// DialogueUI.ShowTextAsync()에서:

// 1. 화자 전환 효과
var transitionController = GetComponent<DialogueTransitionController>();
if (transitionController != null && lastSpeaker != speaker)
{
    await transitionController.OnSpeakerChanged(speaker, speakerSlot, ct);
}

// 2. 타이핑 사운드 캐릭터 설정
var soundController = GetComponent<AdvancedTypingSoundController>();
soundController?.SetCurrentCharacter(characterId);

// 3. 캐릭터 강조 시작
var reactionSystem = characterSlot?.GetComponent<CharacterReactionSystem>();
reactionSystem?.StartSpeaking();

// 4. 대화 표시 (기존 코드)
await ShowTextAsync(speaker, text, ct);

// 5. 캐릭터 강조 종료
reactionSystem?.StopSpeaking();
```

**더 자세한 통합은 `UXEnhancementIntegrationExample.cs` 참조!**

---

## ✅ 체크리스트

### 선택지:
- [ ] EnhancedChoiceButton 컴포넌트 추가됨
- [ ] Background Image, Button Text 연결됨
- [ ] 호버 시 확대되는지 테스트
- [ ] 클릭 시 펀치 효과 확인

### 인터랙션 피드백:
- [ ] InteractionFeedbackManager 씬에 있음
- [ ] EnhancedChoiceUI 사용 중 (자동 연동)
- [ ] (선택) 리플/플래시 Prefab 설정됨

### 캐릭터:
- [ ] CharacterReactionSystem 추가됨
- [ ] Idle 시 숨쉬기 확인
- [ ] (선택) 대화 시 강조 확인
- [ ] (선택) 감정 반응 테스트

### 대화 전환:
- [ ] DialogueTransitionController 추가됨
- [ ] Name Box 펄스 확인
- [ ] (선택) 대화창 이동 테스트

### 타이핑 사운드:
- [ ] AdvancedTypingSoundController 추가됨
- [ ] (선택) 사운드 클립 추가됨
- [ ] (선택) 캐릭터 프로필 설정됨

---

## 🎯 즉시 테스트하기

### 씬 재생 → 선택지 확인:
1. 게임 플레이
2. 선택지가 나올 때까지 진행
3. 마우스 호버 → 확대되는지 확인
4. 클릭 → 펀치 효과 확인
5. 선택 후 → 플래시/페이드아웃 확인

### 대화 확인:
1. 대화 장면 재생
2. 캐릭터가 숨쉬는지 확인 (미묘한 확대/축소)
3. 화자 변경 시 이름박스 펄스 확인
4. (선택) 타이핑 사운드 확인

---

## 🐛 문제 해결

### "호버 효과가 안 돼요!"
- Button 컴포넌트가 있는지 확인
- CanvasGroup의 Blocks Raycasts가 true인지 확인

### "캐릭터가 안 움직여요!"
- CharacterReactionSystem이 있는지 확인
- Enable Breathing이 체크되었는지 확인
- 캐릭터가 화면에 보이는지 확인 (Inactive면 작동 안 함)

### "사운드가 안 나와요!"
- AudioSource 컴포넌트가 있는지 확인
- 사운드 클립이 추가되었는지 확인
- 볼륨이 0이 아닌지 확인

---

## 📚 다음 단계

### 기본 마스터했다면:
1. **UX_ENHANCEMENTS_README.md** 읽기 (전체 기능 설명)
2. **UXEnhancementIntegrationExample.cs** 참조 (고급 통합)
3. 프로젝트에 맞게 커스터마이징
4. 플레이테스트 → 피드백 수집 → 파라미터 조정

### 추천 커스터마이징:
- 선택지 색상/크기 조정
- 캐릭터별 타이핑 톤 설정
- 중요한 선택 시 화면 플래시 활용
- 감정별 캐릭터 반응 추가

---

## 🎉 완성!

이제 여러분의 비주얼 노벨은 **훨씬 더 쫀득하고 재밌어졌습니다!**

Happy Developing! 🎮✨

---

**문의사항이 있으면 이슈 등록해주세요!**
