# LoveAlgo — AI-Optimized Project Architecture

> **목표**: AI 코딩 도구가 효율적으로 코드를 생성·수정·검증할 수 있는 프로젝트 구조 재설계
> **원칙**: "AI가 읽기 쉬운 코드 = 인간이 읽기 쉬운 코드"

---

## 1. 현재 구조의 근본적 문제 (AI 관점)

### 1.1 God Object가 AI에게 주는 치명적 문제

| 파일 | 줄 수 | AI가 겪는 문제 |
|------|-------|----------------|
| [`ScriptRunner.cs`](Assets/Scripts/Story/ScriptRunner.cs) | 1,352 | **컨텍스트 윈도우 초과**: LLM이 한 번에 전체 파일을 이해할 수 없음. 수정 시 사이드 이펙트 예측 불가 |
| [`GameManager.cs`](Assets/Scripts/Core/GameManager.cs) | 887 | **책임 혼재**: Phase 전환 + Day 루프 + 스케줄 + 세이브 + 오디오가 단일 파일. AI가 "스케줄만 수정"해도 다른 영역 영향 분석 불가 |
| [`SaveManager.cs`](Assets/Scripts/Story/SaveManager.cs) | 660 | **도메인 혼재**: 직렬화 + 도메인 복원 + 스크린샷 캡처가 단일 클래스. AI가 세이브 포맷만 수정하려다 UI 로직 건드림 |
| [`PopupManager.cs`](Assets/Scripts/UI/PopupManager.cs) | 531 | **UI 로직 혼재**: 모달 관리 + Save/Load UI + Settings UI + Log UI가 단일 클래스 |

### 1.2 AI 코딩 자동화의 3대 적

1. **컨텍스트 윈도우 한계**: LLM은 한 번에 ~8K~128K 토큰만 처리 가능. 1,352줄 파일은 전체 분석 시 컨텍스트 소진
2. **의존성 파악 불가**: `GameManager`가 `ScriptRunner`, `SaveManager`, `UIManager`, `AudioManager`, `ScreenFX`, `StageManager` 모두 참조 → AI가 "작은 수정"을 해도 영향 범위 파악 불가
3. **테스트 격리 불가**: God Object는 단위 테스트 작성 시 목(mock)이 10개 이상 필요 → AI가 테스트 생성 포기

---

## 2. 재설계 철학: AI가 좋아하는 코드 패턴

### 2.1 핵심 원칙

| 원칙 | 설명 | AI 이점 |
|------|------|---------|
| **단일 책임** | 한 파일 = 한 역할 | AI가 수정 범위 명확히 파악 |
| **인터페이스 분리** | 의존은 인터페이스로 | AI가 구현 교체 시 영향 최소화 |
| **이벤트 기반 통신** | 직접 호출 대신 이벤트 | AI가 모듈 추가 시 기존 코드 수정 불필요 |
| **데이터 파이프라인** | 데이터 흐름이 단방향 | AI가 데이터 변환 로직 추적 용이 |

### 2.2 "좋은 사례" 패턴: [`GameState.cs`](Assets/Scripts/Story/GameState.cs)

```
GameState.cs (~300줄) — 순수 도메인, UI 의존 없음
✅ 한 눈에 파악 가능한 크기
✅ 외부 의존성 최소화
✅ 직렬화/복원 로직 명확
```

**이 패턴을 모든 도메인에 적용한다.**

---

## 3. 목표 아키텍처

### 3.1 계층 구조 (의존성 방향 명시)

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation Layer (UI)                                     │
│  LoveAlgo.UI                                                 │
│  ├── TitleUI, UsernameUI, ScheduleUI                         │
│  ├── PopupManager, ModalPopupBase                            │
│  └── ChoiceUI, LogPopup, SettingsPopup                       │
│         ▲                                                    │
│         │  (이벤트 구독, 명령 발행)                            │
├─────────┼────────────────────────────────────────────────────┤
│  Application Layer (Use Cases)                               │
│  LoveAlgo.Application                                        │
│  ├── GameFlowController  — Phase 전환 오케스트레이션          │
│  ├── DayLoopController   — 하루 진행 루프                    │
│  ├── SaveLoadController  — 세이브/로드 오케스트레이션         │
│  └── ScheduleController  — 스케줄 선택 → 효과 적용            │
│         ▲                                                    │
│         │  (도메인 객체 사용)                                  │
├─────────┼────────────────────────────────────────────────────┤
│  Domain Layer (Business Logic)                               │
│  LoveAlgo.Domain                                             │
│  ├── GameState          — 순수 게임 상태 (POCO)               │
│  ├── HeroinePointTracker — 호감도 계산                       │
│  ├── DayEventTable      — 이벤트 스케줄링                    │
│  ├── GameTimeline       — 타임라인 데이터                     │
│  └── GameBalanceSO      — 밸런스 설정                        │
│         ▲                                                    │
│         │  (외부 의존 없음)                                    │
├─────────┼────────────────────────────────────────────────────┤
│  Infrastructure Layer (Technical)                            │
│  LoveAlgo.Infrastructure                                     │
│  ├── StoryEngine        — CSV 파싱 + 스크립트 실행           │
│  ├── SaveRepository     — JSON 직렬화/역직렬화               │
│  ├── ThumbnailCapture   — 스크린샷 캡처                      │
│  ├── AudioManager       — 오디오 재생                        │
│  └── StageManager       — 배경/캐릭터/이펙트 관리             │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 네임스페이스 재편

| 기존 | 변경 | 이유 |
|------|------|------|
| `LoveAlgo.Core` | `LoveAlgo.Domain` + `LoveAlgo.Application` | 도메인 로직과 애플리케이션 로직 분리 |
| `LoveAlgo.Story` | `LoveAlgo.Infrastructure.Story` + `LoveAlgo.Domain.Story` | 스토리 엔진을 인프라로 격리 |
| `LoveAlgo.UI` | `LoveAlgo.Presentation` | UI를 프레젠테이션 레이어로 명확화 |
| (신규) | `LoveAlgo.Application` | 유스케이스 오케스트레이션 전담 |

> **참고**: 네임스페이스 변경 시 [`AGENTS.md`](../../AGENTS.md)의 네임스페이스 표도 함께 업데이트해야 합니다.

---

## 4. Phase별 상세 리팩토링 계획

### Phase 1: ScriptRunner → StoryEngine 분리 (우선순위: 최상)

**현재 문제**: [`ScriptRunner.cs`](Assets/Scripts/Story/ScriptRunner.cs) 1,352줄 — switch 분기 15개, 매크로 5개, 선택지 처리, 로그 복원, Auto 모드 모두 단일 파일

#### 4.1 Before (현재)

```
ScriptRunner.cs (1,352줄)
├── 스크립트 로드/파싱 (100줄)
├── 실행 루프 (50줄)
├── Type별 실행 (switch 15개, 400줄)
│   ├── Text, Char, BG, CG, SD, Overlay, Sound, FX, Flow, Choice, Option, Place
├── Next 처리 (50줄)
├── Auto 모드 (50줄)
├── 클릭 입력 (30줄)
├── 되감기 (80줄)
├── 매크로 (400줄)
│   ├── DayEnd, DayStart, SceneEnd, SceneStart, Setup
├── Flow 명령 (100줄)
│   ├── Jump, End, Save, If, MiniGame, LoadingScene
└── 로그 복원 (50줄)
```

#### 4.2 After (목표)

```
StoryEngine/
├── ScriptEngine.cs (150줄) — 실행 루프 + 라인 디스패치만
│   └── ILineHandler 인터페이스로 위임
│
├── Handlers/
│   ├── ILineHandler.cs (20줄) — 인터페이스
│   ├── TextLineHandler.cs (80줄) — 대사/나레이션
│   ├── CharLineHandler.cs (60줄) — 캐릭터 제어
│   ├── BGLineHandler.cs (80줄) — 배경 전환
│   ├── CGLineHandler.cs (50줄) — CG 표시
│   ├── SDLineHandler.cs (50줄) — SD 컷씬
│   ├── OverlayLineHandler.cs (40줄) — 오버레이
│   ├── SoundLineHandler.cs (50줄) — 오디오
│   ├── FXLineHandler.cs (60줄) — 시각 효과
│   ├── PlaceLineHandler.cs (40줄) — 장소 배너
│   └── ChoiceLineHandler.cs (80줄) — 선택지 처리
│
├── Macros/
│   ├── IMacroHandler.cs (20줄) — 인터페이스
│   ├── DayEndMacro.cs (60줄)
│   ├── DayStartMacro.cs (60줄)
│   ├── SceneEndMacro.cs (50줄)
│   ├── SceneStartMacro.cs (50줄)
│   └── SetupMacro.cs (80줄)
│
├── Flow/
│   ├── FlowCommand.cs (20줄) — 인터페이스
│   ├── JumpCommand.cs (30줄)
│   ├── IfCommand.cs (60줄)
│   ├── MiniGameCommand.cs (30줄)
│   └── LoadingSceneCommand.cs (40줄)
│
├── ScriptParser.cs (기존 유지, ~200줄)
├── ScriptLine.cs (기존 유지, ~70줄)
└── AutoModeController.cs (60줄) — Auto 모드 전용
```

**산출물**: ScriptRunner 1,352줄 → ScriptEngine 150줄 + Handler 10개 × 40~80줄 + Macro 5개 × 50~80줄 + Flow 4개 × 30~60줄

#### 4.3 AI 이점

| 항목 | Before | After |
|------|--------|-------|
| AI가 수정 시 읽어야 할 파일 크기 | 1,352줄 | 40~80줄 |
| 새 Type 추가 시 수정 범위 | ScriptRunner 내부 switch | 새 Handler 1개 추가 + 등록 |
| OCP 준수 | ❌ switch 수정 필요 | ✅ 새 Handler 추가만 |
| 단위 테스트 가능성 | ❌ mocking 10개 이상 | ✅ Handler별 독립 테스트 |

---

### Phase 2: GameManager → GameFlowController + DayLoopController

**현재 문제**: [`GameManager.cs`](Assets/Scripts/Core/GameManager.cs) 887줄 — Phase 전환 + Day 루프 + 스케줄 + 세이브 + 오디오 + 데모 로직 혼재

#### 4.4 Before (현재)

```
GameManager.cs (887줄)
├── Phase 전환 (ChangePhase, EnterXxx) — 150줄
├── 게임 흐름 (GoToTitle, StartNewGame, OnNameConfirmed) — 200줄
├── Day 루프 (EnterDayLoop, EndDay, OnScheduleCompleted) — 200줄
├── 스케줄 처리 (OnScheduleSelected, BuildScheduleFeedback) — 100줄
├── 세이브/로드 (AutoSave, Save, LoadGame, LoadFromSaveData) — 200줄
├── 장면 정리 (CleanupStage) — 20줄
└── 데모 모드 (ShouldReturnToDemoEnd, MarkDemoScheduleComplete) — 30줄
```

#### 4.5 After (목표)

```
Application/
├── GameFlowController.cs (200줄) — Phase 전환 오케스트레이션
│   ├── ChangePhase(GamePhase)
│   ├── GoToTitleAsync()
│   ├── StartNewGame()
│   ├── ContinueGame()
│   └── LoadFromSaveData(SaveData)
│
├── DayLoopController.cs (180줄) — 하루 진행 루프
│   ├── EnterDayLoop()
│   ├── EndDayAsync()
│   ├── OnScheduleCompleted()
│   └── RunDayEventInline()
│
├── ScheduleController.cs (120줄) — 스케줄 선택 → 효과 적용
│   ├── OnScheduleSelected(ScheduleType)
│   ├── BuildScheduleFeedback()
│   └── ApplyScheduleEffect()
│
├── SaveLoadController.cs (150줄) — 세이브/로드 오케스트레이션
│   ├── AutoSave()
│   ├── Save(slot)
│   ├── LoadGame(slot)
│   └── RestoreStageState()
│
Domain/
└── GameManager.cs (80줄) — Phase 상태만 관리
    ├── CurrentPhase, CurrentDay, RemainingActions
    ├── PlayerName
    └── AdvanceDay()
```

#### 4.6 통신 패턴: 이벤트 기반

```csharp
// GameManager는 상태만 발행
public event Action<GamePhase> OnPhaseChanged;
public event Action<int> OnDayChanged;

// GameFlowController가 구독하여 UI 전환 처리
GameManager.Instance.OnPhaseChanged += phase => HandlePhaseTransition(phase);
```

---

### Phase 3: SaveManager → SaveRepository + SaveDataMapper + ThumbnailCapture

**현재 문제**: [`SaveManager.cs`](Assets/Scripts/Story/SaveManager.cs) 660줄 — 직렬화 + 도메인 복원 + 스크린샷 + 텍스처 가공 혼재

#### 4.7 Before (현재)

```
SaveManager.cs (660줄)
├── JSON 직렬화/역직렬화 — 100줄
├── 도메인 복원 (ApplyToGameState) — 150줄
├── 스크린샷 캡처 (CaptureScreenshotAsTexture) — 100줄
├── 텍스처 가공 (CropAndScale, DetectContentRect) — 150줄
├── UI 숨김/복원 (HideUIForThumbnailCapture) — 100줄
└── 세이브 슬롯 관리 — 60줄
```

#### 4.8 After (목표)

```
Infrastructure/
├── SaveRepository.cs (120줄) — 순수 JSON 직렬화
│   ├── SaveToJson(slot, SaveData)
│   ├── LoadFromJson(slot) → SaveData
│   ├── Exists(slot)
│   └── GetAllUserSaves()
│
├── SaveDataMapper.cs (150줄) — 도메인 ↔ SaveData 변환
│   ├── CaptureAll() → SaveData
│   ├── ApplyAll(SaveData)
│   └── ApplyToGameState(SaveData)
│
└── ThumbnailCapture.cs (120줄) — 스크린샷 전용
    ├── CaptureAsync(slot) → Texture2D
    ├── CropAndScale()
    └── HideUIForCapture() / RestoreUIFromSnapshot()
```

---

### Phase 4: PopupManager → ModalManager + TopPopupController

**현재 문제**: [`PopupManager.cs`](Assets/Scripts/UI/PopupManager.cs) 531줄 — 모달 관리 + Save/Load UI + Settings UI + Log UI 혼재

#### 4.9 After (목표)

```
Presentation/
├── ModalManager.cs (150줄) — 모달 팝업 레이어 관리
│   ├── ShowModal<T>()
│   ├── CloseModalAsync()
│   └── CloseAll()
│
├── TopPopupController.cs (120줄) — Top 레이어 팝업
│   ├── ShowConfirm()
│   ├── ShowAlert()
│   ├── ShowToast()
│   └── ShowLog()
│
└── PopupManager.cs (80줄) — 레이어 관리 + ESC 처리만
    ├── layerModal, layerTop, dimmer
    └── HandleEscapeKey()
```

---

## 5. Assembly Definition 도입 (Phase 5)

### 5.1 목표 구조

```
Assets/Scripts/
├── Domain/           LoveAlgo.Domain.asmdef        → 외부 의존 없음
├── Application/      LoveAlgo.Application.asmdef   → Domain 참조
├── Infrastructure/   LoveAlgo.Infrastructure.asmdef → Domain, Application 참조
├── Presentation/     LoveAlgo.Presentation.asmdef  → Domain, Application 참조
├── MiniGame/         LoveAlgo.MiniGame.asmdef      → Domain 참조
├── Phone/            LoveAlgo.Phone.asmdef         → Domain 참조
└── Shop/             LoveAlgo.Shop.asmdef          → Domain 참조
```

### 5.2 효과

| 항목 | Before | After |
|------|--------|-------|
| 순환 참조 가능성 | ❌ 제한 없음 | ✅ 컴파일러가 방지 |
| 컴파일 시간 | 전체 리컴파일 | 변경된 모듈만 |
| AI가 의존성 파악 | 코드 분석 필요 | asmdef 파일 확인만 |
| 모듈 경계 | 네임스페이스 관례 | 컴파일러 강제 |

---

## 6. AI 코딩 자동화를 위한 추가 개선사항

### 6.1 의존성 주입 패턴 도입

**현재 문제**: 모든 싱글톤이 `Instance?.`로 직접 접근 → AI가 의존성 파악 불가

> **Unity DI 참고**: Unity의 `MonoBehaviour`는 생성자를 직접 호출할 수 없습니다. 생성자 주입을 사용하려면 **Zenject**, **VContainer**, 또는 **Unity DI** 프레임워크 도입이 필요합니다. 대안으로 `ScriptableObject` 기반 DI 또는 `IServiceLocator` 패턴을 고려할 수 있습니다.

```csharp
// ❌ 현재: 직접 의존
var runner = ScriptRunner.Instance;
var gs = GameState.Instance;
var fx = ScreenFX.Instance;

// ✅ 목표: 생성자 주입 (DI 프레임워크 필요)
public class TextLineHandler : ILineHandler
{
    readonly IDialoguePresenter _dialogue;
    readonly IMonologueDimController _monologueDim;
    
    public TextLineHandler(IDialoguePresenter dialogue, IMonologueDimController monologueDim)
    {
        _dialogue = dialogue;
        _monologueDim = monologueDim;
    }
}
```

### 6.2 명령/쿼리 분리 (CQRS 라이트)

> **참고**: CQRS 패턴은 대규모 프로젝트에 적합합니다. 비주얼 노벨 게임에는 과할 수 있으므로, 필요 시 더 간단한 Command 패턴(인터페이스 1개 + 핸들러 딕셔너리)으로 대체를 고려하세요.

```csharp
// 명령: 상태 변경 (반환값 없음)
public record StartNewGameCommand(string PlayerName);
public record EndDayCommand();
public record SaveGameCommand(int Slot);

// 쿼리: 상태 조회 (부수작용 없음)
public record GetGameStateQuery();
public record GetDayInfoQuery(int Day);

// 핸들러: 명령/쿼리 처리
public interface ICommandHandler<TCommand> { UniTask Handle(TCommand cmd, CancellationToken ct); }
public interface IQueryHandler<TQuery, TResult> { UniTask<TResult> Handle(TQuery query, CancellationToken ct); }
```

**AI 이점**: AI가 "새 게임 시작" 로직을 수정할 때 `StartNewGameCommand` 핸들러만 보면 됨

### 6.3 상태 머신 명시화

현재 `GamePhase` enum + `isTransitioning` 플래그로 암시적 상태 관리 → 명시적 상태 머신 도입

```csharp
public sealed class GameStateMachine
{
    readonly Dictionary<GamePhase, IPhaseState> _states;
    IPhaseState _currentState;
    
    public async UniTask TransitionTo(GamePhase next, CancellationToken ct)
    {
        if (_currentState != null) await _currentState.ExitAsync(ct);
        _currentState = _states[next];
        await _currentState.EnterAsync(ct);
    }
}

interface IPhaseState
{
    UniTask EnterAsync(CancellationToken ct);
    UniTask ExitAsync(CancellationToken ct);
}
```

### 6.4 CSV 스크립트 명령 레지스트리

현재 `ScriptRunner`의 switch 문 → 명령 레지스트리 패턴

```csharp
// 명령 등록 (어플리케이션 시작 시 한 번)
LineHandlerRegistry.Register(LineType.Text, new TextLineHandler());
LineHandlerRegistry.Register(LineType.BG, new BGLineHandler());

// 실행 시 디스패치
if (LineHandlerRegistry.TryGet(line.Type, out var handler))
    await handler.ExecuteAsync(line, ct);
```

---

## 7. 마이그레이션 전략

### 7.1 점진적 접근 원칙

```
Week 1-2: Phase 1 (ScriptRunner 분리)
  └── AI가 가장 자주 수정하는 파일, 즉시 효과
  └── 기존 ScriptRunner는 deprecated 마크, 새 StoryEngine과 병행

Week 3-4: Phase 2 (GameManager 분리)
  └── Phase 1 완료 후
  └── GameFlowController, DayLoopController 점진적 도입

Week 5-6: Phase 3 (SaveManager 분리) + Phase 4 (PopupManager 분리)
  └── 병렬 진행 가능
  └── 기존 API 유지, 내부만 분리

Week 7-8: Phase 5 (Assembly Definition)
  └── Phase 1~4 안정화 후
  └── 순환 참조 검증, 컴파일 시간 측정
```

### 7.2 검증 체크리스트

각 Phase 완료 시:

- [ ] `unity-cli editor refresh --compile` — 컴파일 오류 없음
- [ ] `unity-cli console --type error` — 런타임 오류 없음
- [ ] 프롤로그 → DayLoop → 스케줄 → DayEnd → 엔딩 전체 플로우 정상 동작
- [ ] 세이브/로드 정상 동작 (기존 세이브 데이터 호환성 유지)
- [ ] Git 커밋: "refactor: Phase X — 기능 변경 없음, 구조만 분리"

---

## 8. AI 코딩 가이드라인 (재설계 후)

### 8.1 AI가 새 기능 추가 시 따라야 할 패턴

```
1. 도메인 레이어에 데이터 모델 정의 (Domain/)
2. 애플리케이션 레이어에 유스케이스 정의 (Application/)
3. 인프라 레이어에 기술 구현 (Infrastructure/)
4. 프레젠테이션 레이어에 UI 구현 (Presentation/)
5. 의존성 방향: Presentation → Application → Domain ← Infrastructure
```

### 8.2 AI가 수정 시 확인해야 할 것

| 수정 유형 | 확인할 파일 | 최대 줄 수 |
|-----------|------------|-----------|
| 새 대사 Type 추가 | `Handlers/XXXLineHandler.cs` | 80줄 |
| 새 매크로 추가 | `Macros/XXXMacro.cs` | 80줄 |
| 새 Flow 명령 추가 | `Flow/XXXCommand.cs` | 60줄 |
| Phase 전환 로직 변경 | `GameFlowController.cs` | 200줄 |
| Day 루프 변경 | `DayLoopController.cs` | 180줄 |
| 세이브 포맷 변경 | `SaveData.cs` + `SaveDataMapper.cs` | 150줄 |

### 8.3 AI가 절대 수정하면 안 되는 것

- [`GameState.cs`](Assets/Scripts/Story/GameState.cs) — 세이브 포맷 호환성 (AGENTS.md 준수)
- [`ScriptParser.cs`](Assets/Scripts/Story/ScriptParser.cs) — CSV 파싱 로직 (스토리 전체 영향)
- `GameConstants.cs` — 하드코딩 상수 (ScriptableObject로 이동 대상)

---

## 9. 비교: Before vs After

### 9.1 파일 크기 분포

| 구분 | Before (Top 5) | After (Top 5) |
|------|----------------|---------------|
| 최대 파일 | ScriptRunner.cs 1,352줄 | GameFlowController.cs 200줄 |
| 2위 | GameManager.cs 887줄 | DayLoopController.cs 180줄 |
| 3위 | DialogueUI.cs 875줄 | SaveDataMapper.cs 150줄 |
| 4위 | SaveManager.cs 660줄 | ModalManager.cs 150줄 |
| 5위 | AudioManager.cs 617줄 | TextLineHandler.cs 80줄 |
| **평균** | **~350줄** | **~90줄** |

### 9.2 AI 컨텍스트 효율

| 항목 | Before | After | 개선율 |
|------|--------|-------|--------|
| 수정 시 읽어야 할 최대 파일 | 1,352줄 | 200줄 | **85% 감소** |
| 의존성 개수 (GameManager 기준) | 12개 | 3개 | **75% 감소** |
| 단위 테스트 목 개수 | 10+ | 2~3 | **70% 감소** |
| 새 Type 추가 시 수정 파일 수 | 1개 (1,352줄) | 1개 (80줄) | **94% 감소** |

---

## 10. 결론

이 재설계는 **리팩토링을 넘어 AI가 효율적으로 코딩할 수 있는 환경 조성**이 목표다.

- **God Object 분해** → AI가 컨텍스트 윈도우 내에서 전체 파일 이해 가능
- **인터페이스 분리** → AI가 구현 교체 시 영향 범위 최소화
- **이벤트 기반 통신** → AI가 모듈 추가 시 기존 코드 수정 불필요
- **Assembly Definition** → AI가 의존성 방향을 컴파일러가 검증받음

**핵심**: "AI가 읽기 쉬운 코드 = 인간이 읽기 쉬운 코드" — 이 원칙에서 벗어나는 모든 설계는 거부된다.
