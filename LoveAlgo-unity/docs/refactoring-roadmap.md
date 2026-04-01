# 리팩토링 로드맵

현재 코드의 구조적 문제와 분리 계획.

---

## 현재 문제: 4개의 God Object

```
GameManager (900줄) ─── 게임 흐름 + 세이브 + Day루프 + 스케줄 + 오디오 + 데모로직
ScriptRunner (1200줄) ── 파싱 + 실행루프 + 10개 Type별 핸들러 + 선택지 + 오토모드 + 로그복원
SaveManager (1400줄) ── 직렬화 + 도메인복원 + 스크린샷 + 텍스처가공 + UI숨김
PopupManager (600줄) ── 모달계층 + Save/Load UI + Settings UI + Log UI
```

**좋은 사례**: `GameState.cs` (~300줄) — 순수 도메인, UI 의존 없음. 이 패턴을 따를 것.

---

## Phase 1: ScriptRunner 분리 (우선순위 최고)

**이유**: 새 연출 Type 추가 시마다 ScriptRunner 수정 필요 (OCP 위반). AI가 가장 자주 건드릴 파일.

### Before
```csharp
// ScriptRunner.cs — 1200줄, switch 분기 15개
async UniTask ExecuteLineAsync(ScriptLine line, CancellationToken ct)
{
    switch (line.Type)
    {
        case LineType.Text: /* 50줄 */ break;
        case LineType.Char: /* 40줄 */ break;
        case LineType.BG:   /* 60줄 */ break;
        // ... 15개 더
    }
}
```

### After
```csharp
// ILineExecutor.cs — 인터페이스
public interface ILineExecutor
{
    LineType Type { get; }
    UniTask ExecuteAsync(ScriptLine line, CancellationToken ct);
}

// TextExecutor.cs, CharExecutor.cs, BGExecutor.cs ... 각각 분리
public class TextExecutor : ILineExecutor
{
    public LineType Type => LineType.Text;
    public async UniTask ExecuteAsync(ScriptLine line, CancellationToken ct) { ... }
}

// ScriptRunner.cs — 200줄로 축소
private Dictionary<LineType, ILineExecutor> _executors;

async UniTask ExecuteLineAsync(ScriptLine line, CancellationToken ct)
{
    if (_executors.TryGetValue(line.Type, out var executor))
        await executor.ExecuteAsync(line, ct);
}
```

**산출물**: ScriptRunner 1200줄 → ScriptRunner 200줄 + Executor 10개 × 50~80줄

---

## Phase 2: GameManager 분리

### Before
```
GameManager (900줄)
├── Phase 전환 로직
├── DayLoop 로직 (EnterDayLoop, EndDay, HandleDayEvents)
├── Schedule 결과 처리 (OnScheduleCompleted)
├── Save/Load 호출 (Save, AutoSave, LoadFromSaveData)
├── 스테이지 정리 (CleanupStage)
├── 데모 모드 플래그
└── 오디오 제어 (BGM 정지)
```

### After
```
GameManager (200줄) — Phase 상태만 관리
├── ChangePhase(GamePhase)
├── CurrentPhase, CurrentDay
└── 이벤트 발행만 (OnPhaseChanged)

DayLoopController (별도) — Day 진행 로직
├── EnterDayLoop()
├── EndDay()
└── HandleDayEvents()

GameSessionManager (별도) — 세이브/로드 오케스트레이션
├── StartNewGame()
├── ContinueGame()
├── Save() / Load()
└── RestoreStageState()
```

---

## Phase 3: SaveManager 분리

### Before (1400줄)
```
SaveManager
├── JSON 직렬화 (Save/Load)
├── 도메인 복원 (ApplyToGameState → Shop, Phone, Events 복원)
├── 스크린샷 캡처 (CaptureScreenshotAsTexture)
├── 텍스처 가공 (CropAndScale, DetectContentRect)
└── UI 숨김/복원 (HideUIForThumbnailCapture)
```

### After
```
SaveManager (200줄) — 순수 직렬화
├── SaveToJson(slot, SaveData)
└── LoadFromJson(slot) → SaveData

SaveDataMapper (150줄) — 도메인 ↔ SaveData 변환
├── CaptureAll() → SaveData
└── ApplyAll(SaveData)

ThumbnailCapture (200줄) — 스크린샷 전용
├── CaptureAsync() → Texture2D
└── CropAndScale()
```

---

## Phase 4: Assembly Definition 도입

현재: 모든 코드가 단일 Assembly-CSharp에 컴파일 → 의존성 제한 없음

### 목표 구조
```
Assets/Scripts/
├── Core/       LoveAlgo.Core.asmdef       → 외부 의존 없음
├── Story/      LoveAlgo.Story.asmdef      → Core 참조
├── UI/         LoveAlgo.UI.asmdef         → Core, Story 참조
├── Schedule/   LoveAlgo.Schedule.asmdef   → Core, UI 참조
├── MiniGame/   LoveAlgo.MiniGame.asmdef   → Core 참조
├── Shop/       LoveAlgo.Shop.asmdef       → Core 참조
└── Phone/      LoveAlgo.Phone.asmdef      → Core 참조
```

**효과**: 순환 참조 방지, 컴파일 시간 단축, 모듈 경계 명확화

---

## 실행 순서

```
Phase 1 (ScriptRunner 분리) → AI가 가장 자주 수정하는 파일, 즉시 효과
Phase 2 (GameManager 분리)  → Phase 1 완료 후
Phase 3 (SaveManager 분리)  → Phase 2와 병렬 가능
Phase 4 (Assembly Definition)→ Phase 1~3 안정화 후
```

> 각 Phase는 독립 커밋. 기능 변경 없이 구조만 분리 (행동 보존 리팩토링).
