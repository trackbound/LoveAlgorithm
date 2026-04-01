# LoveAlgo - AI Coding Agent Instructions

## 프로젝트 개요
**LoveAlgo**는 대학 캠퍼스 배경의 연애 어드벤처 시뮬레이션 게임입니다. Unity 기반으로 개발되며, 5명의 히로인(하예은, 서다은, 이봄, 도희원, 로아)과의 연애 루트를 구현합니다.

> 📖 **전체 아키텍처**: [ARCHITECTURE.md](ARCHITECTURE.md) 참조
> 📖 **CSV 스크립트 문법**: [script-commands.md](script-commands.md) 참조

---

## 🤖 AI 에이전트 작업 규칙

### 작업 전 필수 확인
1. **ARCHITECTURE.md 먼저 참조** - 클래스 관계, 싱글톤 접근법 확인
2. **기존 패턴 따르기** - 비슷한 기능이 이미 있으면 그 방식 복제
3. **네임스페이스 확인** - Core, Story, Schedule, UI, MiniGame 중 적절한 곳에 배치

### 코드 생성 규칙
```csharp
// ✅ 올바른 패턴
await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
await tween.ToUniTask(cancellationToken: ct);
GameState.Instance?.AddLove("Roa", 5);

// ❌ 금지 패턴
yield return new WaitForSeconds(1);  // 코루틴 금지
StartCoroutine(SomeMethod());        // 코루틴 금지
tween.OnComplete(() => { });         // 콜백 대신 await 사용
```

### 싱글톤 접근 패턴
```csharp
// 항상 null 체크 또는 ?. 사용
GameManager.Instance?.ChangePhase(GamePhase.DayLoop);
UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
ScriptRunner.Instance?.OnClick();
GameState.Instance?.AddStat("Int", 5);
AudioManager.Instance?.PlaySFX("Click");
StageManager.Instance?.Character.ExecuteAsync("C:Enter:Roa", ct);
ScreenFX.Instance?.FadeOutAsync(1f, ct);
PopupManager.Instance?.Toast("알림", "저장 완료!");
```

### 새 기능 추가 시 체크리스트
- [ ] 기존에 비슷한 기능 있는지 검색 (`grep_search` 활용)
- [ ] 적절한 네임스페이스에 배치
- [ ] 인스펙터 바인딩 활용 (코드 최소화)
- [ ] UniTask 반환, CancellationToken 지원
- [ ] 한국어 주석 작성

### 수정 작업 시 주의
- **ScriptRunner 수정 시**: 스토리 실행 전체에 영향, 신중히
- **GameState 수정 시**: 세이브/로드 호환성 확인
- **UI 수정 시**: UIManager/PopupManager 통해 접근

---

## CSV 스크립트 구조 (요약)

```csv
LineID,Type,Speaker,Value,Next
```

| Type | 용도 | Value 예시 |
|------|------|------------|
| `Text` | 대사/나레이션 | `안녕!` |
| `Char` | 캐릭터 제어 | `C:Enter:Roa:Happy` |
| `BG` | 배경 전환 | `School_Day:Fade:1.5` |
| `CG` | CG 이미지 | `CG/Roa_FirstMeet:Fade:1.0`, `Exit` |
| `Overlay` | 보조 배경 | `Roa_Theme:FadeIn:0.5`, `FadeOut` |
| `Sound` | 오디오 | `BGM:Morning`, `SFX:Knock` |
| `FX` | 시각 효과 | `FadeOut:1.0`, `CamShake:0.5` |
| `Flow` | 흐름 제어 | `Jump:LineID`, `End` |
| `Choice` | 선택지 시작 | (빈 값) |
| `Option` | 선택지 항목 | `버튼텍스트\|점프대상\|효과` |

### 스크립트 실행 흐름
```
CSV 로드 → ScriptParser.Parse() → ScriptRunner.RunAsync()
    ↓
한 줄씩 순차 실행: ExecuteLineAsync(line) → HandleNextAsync(line)
    ↓
Type별 분기: Text→DialogueUI, Char→CharacterLayer, BG→BackgroundLayer...
```

---

## 코드 패턴

### 비동기 처리 (UniTask)
```csharp
// 딜레이
await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);

// 조건 대기
await UniTask.WaitUntil(() => condition, cancellationToken: ct);

// DOTween 연동
await tween.ToUniTask(cancellationToken: ct);

// 병렬 실행
await UniTask.WhenAll(task1, task2, task3);
```

### 새 UI 팝업 추가 패턴
```csharp
// 1. ModalPopupBase 상속
public class MyPopup : ModalPopupBase
{
    public override void Show() { /* 표시 로직 */ }
    public override void Hide() { /* 숨김 로직 */ }
}

// 2. PopupManager에 프리팹 등록 후 사용
PopupManager.Instance.ShowModal<MyPopup>();
```

### 새 미니게임 추가 패턴
```csharp
// MiniGameBase 상속
public class MyGame : MiniGameBase
{
    protected override void OnGameStart() { /* 게임 시작 */ }
    protected override void EndGame() { /* 게임 종료 */ }
}
```

---

## 플레이어 스탯

| 코드명 | 한글 | 설명 |
|--------|------|------|
| `Str` | 체력 | 운동으로 증가 |
| `Int` | 지성 | 공부로 증가 |
| `Soc` | 사교성 | 스터디그룹 등 |
| `Per` | 끈기 | 운동으로 증가 |
| `Fatigue` | 피로 | 행동 시 누적, 휴식으로 감소 |

### 히로인별 선호 스탯
| 히로인 | 선호 | 난이도 |
|--------|------|--------|
| 하예은 | 체력 | ★☆☆ |
| 서다은 | 지성 | ★★☆ |
| 이봄 | 사교성 | ★★★ |
| 도희원 | 끈기 | ★★★★ |
| 로아 | 피로≥70 | 히든 |

---

## 코드 스타일
- **한국어 주석** 표준 (`/// <summary>` 포함)
- **간결하고 직관적인 네이밍**
- **인스펙터 바인딩** 최대 활용 (코드량 최소화)
- **코루틴 사용 금지** - UniTask만 사용

## 외부 라이브러리
| 라이브러리 | 용도 |
|-----------|------|
| **UniTask** | 비동기 (코루틴 대체) |
| **DOTween Pro** | 트위닝 애니메이션 |
| **TextMesh Pro** | UI 텍스트 |
| **Newtonsoft JSON** | 세이브/로드 직렬화 |
| **unity-cli** | AI 에이전트 ↔ Unity Editor 연동 |

---

## 🔧 unity-cli (AI Agent ↔ Unity 연동)

프로젝트에 `unity-cli-connector` 패키지가 설치되어 있으며, 터미널에서 `unity-cli` 명령으로 Unity Editor를 직접 제어할 수 있습니다.

### 주요 명령어
```bash
# Unity 상태 확인
unity-cli status

# 컴파일 에러 확인
unity-cli console --type error

# 임의 C# 코드 실행 (가장 강력한 도구)
unity-cli exec "return Application.dataPath;"
unity-cli exec "return EditorSceneManager.GetActiveScene().name;"

# 플레이모드 제어
unity-cli editor play --wait
unity-cli editor stop

# 에셋 리프레시 + 리컴파일
unity-cli editor refresh --compile

# 스크린샷 캡처
unity-cli screenshot --view scene
unity-cli screenshot --view game

# 프로파일러 데이터 조회
unity-cli profiler hierarchy --depth 3

# 메뉴 아이템 실행
unity-cli menu "File/Save Project"

# 등록된 도구 목록 확인
unity-cli list
```

### AI 에이전트 사용 가이드라인

> ⚠️ **Auto Refresh가 비활성화**되어 있습니다. 코드 수정 후 Unity에 자동 반영되지 않습니다.

- **코드 수정 후 반드시**: `unity-cli editor refresh --compile`로 수동 리프레시 + 리컴파일 실행 (여러 파일 수정 시 모든 수정 완료 후 한 번만 실행)
- **컴파일 확인**: 리프레시 후 `unity-cli console --type error`로 컴파일 에러 점검
- **런타임 상태 검사**: `unity-cli exec`로 게임 오브젝트 상태, 싱글톤 값, 씬 구조 등 확인
- **에셋 수정 후**: `unity-cli reserialize <path>`로 YAML 직렬화 정리
- **stdin 파이프**: 복잡한 코드는 `echo '...' | unity-cli exec`로 셸 이스케이프 회피
