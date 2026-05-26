# 모듈 경계 ADR — Contracts 추출 + .asmdef 도입

날짜: 2026-05-22  
상태: 채택 (C1, 이번 라운드)  
스코프: Phase C 큰 구조 리팩토링의 청사진. C2~C4 실행 가이드.

## 컨텍스트

N5(.asmdef 도입 시도)는 보류로 끝났다. 의존성 그래프 분석(N5 보고) 결과 **양방향 순환 의존**이 광범위해, `.asmdef`를 단순 추가하면 Unity의 일방향 참조 규칙 때문에 컴파일이 깨진다.

### 발견된 양방향 순환

```
Narrative ↔ Audio       (SoundLineExecutor → IAudio, AudioModule → Story namespace)
Narrative ↔ Stage       (BG/CG/CharLineExecutor → IStage, Stage → Story)
Narrative ↔ Settings    (DialogueUI → ISettings, SettingsModule → INarrative)
Narrative ↔ Affinity    (AffinityFlowCommand → IAffinity, Affinity → Story)
Narrative ↔ Stats       (ChoicePopup → IStats, Stats → Story)
Narrative ↔ Title       (Title → Story)
UI ↔ 거의 모든 모듈    (UIManager wrapper + DialogueUI/ChoicePopup이 LoveAlgo.UI인데 다른 모듈도 LoveAlgo.UI 의존)
```

### 근본 원인

**인터페이스(`I*`)와 그 구현 클래스가 같은 namespace 안에 공존.** 예:

```csharp
// LoveAlgo.Modules.Audio
public interface IAudio { ... }
public class AudioModule : MonoBehaviour, IAudio { ... }
```

`using LoveAlgo.Modules.Audio;`를 하면 인터페이스도 구현도 다 따라옴. Narrative가 `IAudio`만 쓰고 싶어도 namespace 전체를 import — 양방향 의존 형성이 자연스럽게 일어난다.

## 결정

**`LoveAlgo.Contracts`라는 새 namespace/폴더**를 만들고, **cross-module로 노출되는 인터페이스와 EventBus struct만** 거기로 옮긴다. 구현 클래스는 옛 namespace 유지.

### 폴더 구조

```
Assets/_Project/Contracts/         새 폴더
  Modules/                         모듈별 인터페이스
    IAudio.cs
    INarrative.cs
    IStage.cs
    ...
  Events/                          크로스-모듈 이벤트 struct
    DayChangedEvent.cs
    AffinityChangedEvent.cs
    BGMChangedEvent.cs
    StatChangedEvent.cs
    GamePhaseChangedEvent.cs       (Core에서 이동)
```

### 추출 대상 — 인터페이스 16개

| 인터페이스 | 현재 namespace | 외부 타입 의존 (using으로 유지) |
|---|---|---|
| `IAffinity` | `LoveAlgo.Modules.Affinity` | 없음 |
| `IAudio` | `LoveAlgo.Modules.Audio` | 없음 |
| `IDayLoop` | `LoveAlgo.Modules.DayLoop` | 없음 |
| `ILockScreen` | `LoveAlgo.LockScreen` | `LoveAlgo.LockScreen.Data` |
| `IMiniGame` | `LoveAlgo.MiniGame` | 없음 (UniTask만) |
| `INarrative` | `LoveAlgo.Narrative` | `LoveAlgo.Story`, `LoveAlgo.UI` |
| `IPhone` | `LoveAlgo.Phone` | 없음 |
| `ISave` | `LoveAlgo.Save` | `LoveAlgo.Core`, `LoveAlgo.Story.SaveSystem` |
| `ISchedule` | `LoveAlgo.Schedule` | 없음 |
| `ISettings` | `LoveAlgo.Settings` | 없음 |
| `IShop` | `LoveAlgo.Shop` | 없음 |
| `ISimulation` | `LoveAlgo.Simulation` | `LoveAlgo.UI` |
| `IStage` | `LoveAlgo.Stage` | `LoveAlgo.Core`, `LoveAlgo.Story` |
| `IStats` | `LoveAlgo.Modules.Stats` | 없음 |
| `ITitle` | `LoveAlgo.Title` | `LoveAlgo.UI` |
| `ITutorial` | `LoveAlgo.Tutorial` | `LoveAlgo.UI` |

**모두 `LoveAlgo.Contracts` 단일 namespace로** 통일. 일관성/검색성 우선 — Sub-namespace로 쪼개지 않음.

### 추출 대상 — EventBus struct 6개

- `GamePhaseChangedEvent` (Core/Events → Contracts/Events)
- `DayChangedEvent` (DayLoop/Events → Contracts/Events)
- `BGMChangedEvent` (Audio/Events → Contracts/Events)
- `AffinityChangedEvent` (Affinity/Events → Contracts/Events)
- `StatChangedEvent` (Stats/Events → Contracts/Events)
- LockScreen 이벤트 4개 (`LockScreenOpenedEvent`, `PasswordFailedEvent`, `PasswordSetEvent`, `UnlockedEvent`): 검토 후 결정 — 외부 모듈이 구독하지 않는다면 모듈 내부에 유지.

### 외부 타입 의존 처리

`INarrative`가 `LoveAlgo.Story`의 `DialogueUI`를 시그니처로 받는 경우 — 그 타입은 Contracts로 옮기지 않고, **Contracts 안의 인터페이스 파일에 `using LoveAlgo.Story;`만 추가**한다. 즉 Contracts는 일부 모듈 namespace에 의존하지만 (`Core`, `Story`, `UI`, `Story.SaveSystem`, `LockScreen.Data`) 단방향이라 .asmdef 도입 후에도 깨지지 않는다.

문제는 `LoveAlgo.UI` 의존 — UI 모듈이 거의 모든 모듈을 참조하면서 Contracts도 UI에 의존하면 순환 위험. **C3 단계에서 UI wrapper 정리**를 통해 해소. C2에서는 그대로 둔다.

## 도미노 순서

```
C1  ADR (이번 라운드)
  ↓
C2  인터페이스 + 이벤트를 Contracts로 이동 (이번 라운드)
    · 타입 재배치 + namespace 변경 + using 추가만
    · 동작 변경 0, 컴파일 통과 유지
    · 외부 모듈 타입 의존은 그대로 (using만 추가)
  ↓
C3  cross-module 구체 호출을 Services 경유로 전환 (다음 라운드)
    · 예: AudioManager.Instance.PlayBGMAsync → Services.Get<IAudio>().PlayBGM
    · UIManager wrapper 호출처를 Services.TryGet<I*>() 직접 사용으로 마이그레이션
    · 의존 방향이 단방향이 됨 — .asmdef 도입 가능 상태
  ↓
C4  모듈별 .asmdef 도입 (다음 다음 라운드)
    · leaf부터 점진 — Common → Contracts → Core → 의존 적은 모듈 순
    · 각 모듈 asmdef는 Contracts와 Common, 필요한 다른 모듈 asmdef만 참조
    · 빌드 시간 단축, internal 가시성 강제, 모듈 분리 보장
    · A1 (Editor 어셈블리 분리)도 이때 함께
```

### leaf-first asmdef 순서 (C4 단계)

1. `LoveAlgo.Common.asmdef` (이미 의존 0)
2. `LoveAlgo.Contracts.asmdef` (Common + 일부 외부 namespace만)
3. `LoveAlgo.Core.asmdef` (Common + Contracts)
4. **의존 적은 모듈부터**: Stats, Affinity, Audio, MiniGame, Tutorial, Phone
5. **중간 의존 모듈**: DayLoop, Schedule, Shop, Settings, Save, LockScreen, Simulation, Title
6. **가장 무거운 모듈**: Stage, Narrative
7. `LoveAlgo.UI.asmdef` (or 모듈 안으로 흡수)
8. **Editor 어셈블리들** — A1 Editor .asmdef 단독 도입 부활

## 비채택 안

### A) 인터페이스를 모듈 내부 sub-namespace로 두기

예: `LoveAlgo.Modules.Audio.Contracts.IAudio`. 격리되지만 `.asmdef` 한 어셈블리 안에서 namespace만 다른 거라 외부에선 같은 어셈블리 참조 — 의존 끊김 효과 0.

### B) Contracts를 모듈별로 쪼개기

예: `Audio.Contracts.dll`, `Narrative.Contracts.dll`. 더 깔끔한 의존 그래프지만 .asmdef 수가 2배 → 컴파일 시간 부담. 단일 Contracts asmdef로 충분.

### C) 정적 호출만 쓰고 인터페이스 없애기

Singleton-only 패턴. 테스트 어려움 + DI 불가 + 이미 H2에서 `Services.Get<I>()` 도입 후 30+곳 사용 — 회귀가 큼.

## 회귀 안전 (이번 라운드 C2)

- **동작 변경 0**: 순수 namespace 이동 + using 추가만.
- **컴파일 통과 유지**: 각 인터페이스 이동 후 grep으로 옛 namespace 참조처를 전수 검색하고 `using LoveAlgo.Contracts;` 추가.
- **무리한 이동 회피**: namespace 이동만으로 컴파일이 깨지는 인터페이스(예: 외부 타입 의존이 도미노로 번지는 경우)가 발견되면 그 인터페이스는 C2에서 빼고 C3로 미룬다. 보고 후 사용자 판단.
- **B2 EditMode 테스트**가 직전 라운드에서 들어왔으므로, C2 적용 후 Unity Test Runner 실행으로 회귀 1차 검증 가능 (SaveLoadRoundTrip, ScriptParser, ScriptValidator 19 test).

## 롤백 계획

각 단계 별도 커밋. 깨지면 `git revert <sha>` 단일 호출로 그 단계만 되돌림.

- C2-2 (인터페이스 이동)이 깨지면 → 한 커밋 revert → 옛 상태 그대로.
- C3 (구체 호출 → Services)는 더 위험. 모듈 단위로 별도 커밋. 한 모듈에서 깨지면 그 모듈만 revert.
- C4 .asmdef는 leaf 모듈부터 한 .asmdef당 한 커밋. 도미노로 번지지 않도록 leaf 우선.

## 검증 포인트 (사용자가 Unity에서)

1. C2 이후: 에디터 컴파일 통과 (콘솔 에러 0)
2. EditMode 테스트 19개 모두 통과 (Test Runner → EditMode)
3. 일반 플레이로 프롤로그 한 번 진행 — 헤드리스 도입 후 회귀 가드
4. 자동저장 → 로드 한 사이클 — Save round-trip 회귀 가드
