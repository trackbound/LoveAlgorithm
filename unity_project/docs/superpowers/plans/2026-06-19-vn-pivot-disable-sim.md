# 시뮬레이션 → 순수 선형 VN 전환 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 스케줄/상점/스탯 시뮬레이션을 비활성하고 게임을 순수 선형 비주얼노벨로 전환한다(부팅→프롤로그→Flow 체인, 코드 보존·가역).

**Architecture:** Schedule/Shop/Tutorial 피처(각 독립 asmdef)를 `_Parked/`로 `git mv`(GUID 보존)해 씬 배선만 끊는다. 부팅 진입 페이즈를 Schedule→Story로 바꾸고, `NarrativeController`가 스크립트 종료 후 Schedule로 복귀하지 않고 Story를 유지하게 한다. 세이브 스키마·공식은 손대지 않는다.

**Tech Stack:** Unity 6 (URP 2D), C#, EventBus + ScriptableObject 단일 패턴, Unity Test Framework(EditMode/PlayMode), Unity MCP(씬 작업).

> 참조 스펙: `docs/superpowers/specs/2026-06-19-vn-pivot-disable-sim-design.md`

---

## File Structure

| 파일 | 책임 | 변경 |
|---|---|---|
| `Assets/_Project/Scripts/_Parked/{Schedule,Shop,Tutorial}/` | 비활성 피처 보관(코드 보존) | 폴더 이동(git mv) |
| `Assets/_Project/Scripts/Core/State/GameStateData.cs` | 런타임 상태 — 부팅 페이즈 기본값 | `phase` 기본값 Story |
| `Assets/_Project/Scripts/Narrative/NarrativeController.cs` | 스크립트 재생 어댑터 | 종료 시 Schedule 복귀 제거 |
| `Assets/Tests/PlayMode/NarrativeControllerPlayModeTests.cs` | 종료 페이즈 기대치 | Story 유지로 갱신 |
| `Assets/Tests/PlayMode/PhaseControllerPlayModeTests.cs` | 부팅 기본 페이즈 기대치 | Story로 갱신 |
| `Assets/_Project/Scenes/Game.unity` | 게임 씬 배선 | Schedule/Shop/HUD-stat 오브젝트 제거 |

---

## Task 1: 비활성 피처를 `_Parked/`로 이동

**Files:**
- Move: `Assets/_Project/Scripts/Schedule/` → `Assets/_Project/Scripts/_Parked/Schedule/`
- Move: `Assets/_Project/Scripts/Shop/` → `Assets/_Project/Scripts/_Parked/Shop/`
- Move: `Assets/_Project/Scripts/Tutorial/` → `Assets/_Project/Scripts/_Parked/Tutorial/`

세 피처는 각각 독립 asmdef(`LoveAlgo.Schedule/Shop/Tutorial`)이고 의존은 단방향(`Data ← Schedule/Shop`)이라 폴더 이동이 컴파일 참조를 깨지 않는다(asmdef 참조는 GUID 기반). `.meta`까지 함께 옮겨 GUID를 보존한다(금지선 #3).

- [ ] **Step 1: `_Parked` 폴더 생성 + git mv (폴더+`.meta` 통째)**

```bash
cd unity_project/Assets/_Project/Scripts
mkdir -p _Parked
git mv Schedule _Parked/Schedule
git mv Shop _Parked/Shop
git mv Tutorial _Parked/Tutorial
```

- [ ] **Step 2: `_Parked` 폴더의 Unity `.meta` 생성 확인**

Unity는 새 폴더에 `.meta`를 자동 생성한다. 에디터로 한 번 포커스(또는 MCP `refresh_unity`)해 `_Parked.meta`가 생기게 한 뒤 함께 스테이징한다. 하위 폴더(Schedule 등)는 기존 `.meta`가 git mv로 따라왔으므로 GUID 동일.

Run: `git status --short`
Expected: `Schedule/`·`Shop/`·`Tutorial/`가 `R`(rename)로, `_Parked.meta` 신규 `A`로 표시.

- [ ] **Step 3: 컴파일 검증 (콘솔 0에러)**

Unity 에디터 재컴파일 후 MCP `read_console`(또는 에디터 콘솔)로 에러 0 확인. asmdef 참조가 GUID라 폴더 이동만으로는 깨지지 않아야 한다.

Expected: 컴파일 에러 0. `LoveAlgo.Schedule/Shop/Tutorial` 어셈블리가 새 경로에서 동일하게 빌드.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor(parked): Schedule/Shop/Tutorial을 _Parked로 이동

왜: 순수 VN 전환 — 시뮬 메타게임 피처를 코드 보존한 채 격리(asmdef·GUID 유지)."
```

---

## Task 2: 부팅 진입 페이즈를 Story로

**Files:**
- Modify: `Assets/_Project/Scripts/Core/State/GameStateData.cs:33`

현재 부팅 리셋값 `phase = ScreenPhase.Schedule`. 순수 VN은 부팅 즉시 스토리에 있어야 하므로 Story로 바꾼다. 프롤로그가 같은 프레임에 `RequestPhaseCommand(Story)`를 발행하지만, 이어하기(스토리 위치 복원) 사이의 빈틈에서도 Schedule UI가 비치지 않게 기본값을 맞춘다.

- [ ] **Step 1: 기본값 변경**

`GameStateData.cs:33`:

```csharp
[NonSerialized] public ScreenPhase phase = ScreenPhase.Story;
```

(주석도 "부팅 리셋값 = Story(순수 VN 진입)"로 갱신.)

- [ ] **Step 2: `ScreenPhase` 주석 동기화**

`Assets/_Project/Scripts/Core/State/ScreenPhase.cs`의 "부팅 리셋값 = Schedule" 서술을 Story로 갱신(문서 정합).

- [ ] **Step 3: 컴파일 확인**

Expected: 컴파일 에러 0.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Core/State/GameStateData.cs Assets/_Project/Scripts/Core/State/ScreenPhase.cs
git commit -m "feat(vn): 부팅 진입 페이즈를 Story로

왜: 순수 선형 VN — 부팅 즉시 스토리. Schedule 자유행동 진입 제거."
```

---

## Task 3: 스크립트 종료 후 Story 유지 (Schedule 복귀 제거)

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/NarrativeController.cs:229-231`
- Test: `Assets/Tests/PlayMode/NarrativeControllerPlayModeTests.cs:129`

현재 `Run` 종료부(line 230)가 `RequestPhaseCommand(ScreenPhase.Schedule)`를 발행해 자유행동으로 복귀한다. 순수 VN에선 종료 후에도 Story를 유지(다음 스크립트는 Flow Jump/체인으로 연결)해야 하므로 이 발행을 제거한다. `NarrativeFinishedEvent`와 `ClearStoryPosition()`은 유지(세이브 정합·저녁이벤트 호환).

- [ ] **Step 1: 실패 테스트로 갱신**

`NarrativeControllerPlayModeTests.cs:129` 부근의 종료 페이즈 단정을 새 기대치로 바꾼다. 기존:

```csharp
Assert.AreEqual(ScreenPhase.Schedule, _phases[_phases.Count - 1], "종료 시 Schedule 복귀 요청");
```

신규(종료 후 Story 유지 — 마지막으로 요청된 페이즈는 재생 시작의 Story뿐, Schedule 요청이 없어야 함):

```csharp
Assert.AreEqual(ScreenPhase.Story, _phases[_phases.Count - 1], "종료 후 Story 유지(Schedule 복귀 없음)");
CollectionAssert.DoesNotContain(_phases, ScreenPhase.Schedule, "VN: Schedule 페이즈 요청 없음");
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

Run (MCP `run_tests` PlayMode, 필터 `NarrativeControllerPlayModeTests`):
Expected: FAIL — 아직 코드가 Schedule을 발행하므로 마지막 요청이 Schedule.

- [ ] **Step 3: 코드에서 Schedule 복귀 발행 제거**

`NarrativeController.cs:229-231`을 다음으로:

```csharp
            ClearStoryPosition(); // 정상 종료 = 스토리 밖 → 이후 세이브는 스토리 진입점에서 재개
            // 순수 VN: 종료 후에도 Story 페이즈 유지(다음 스크립트는 Flow Jump/체인으로 연결).
            // (구 시뮬: 여기서 RequestPhaseCommand(Schedule)로 자유행동 복귀 — 제거됨.)
            EventBus.Publish(new NarrativeFinishedEvent(scriptName));
```

(주석 line 18-20의 "종료 시 RequestPhaseCommand(Schedule)" 서술도 갱신.)

- [ ] **Step 4: 테스트 실행 — 통과 확인**

Run (MCP `run_tests` PlayMode, 필터 `NarrativeControllerPlayModeTests`):
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Narrative/NarrativeController.cs Assets/Tests/PlayMode/NarrativeControllerPlayModeTests.cs
git commit -m "feat(vn): 스크립트 종료 후 Story 유지(Schedule 복귀 제거)

왜: 순수 선형 VN — 스토리가 Flow 체인으로 이어지도록 자유행동 복귀를 없앤다."
```

---

## Task 4: PhaseController 부팅 기본 페이즈 테스트 갱신

**Files:**
- Test: `Assets/Tests/PlayMode/PhaseControllerPlayModeTests.cs:37`

`PhaseControllerPlayModeTests`가 부팅 기본 페이즈를 Schedule로 가정(line 37)한다. Task 2로 기본값이 Story가 됐으므로 이 테스트가 깨진다. 테스트가 PhaseController 전환 메커니즘을 검증하도록 시작 페이즈를 명시 설정하거나 기대치를 Story로 맞춘다.

- [ ] **Step 1: 테스트 점검 후 갱신**

`PhaseControllerPlayModeTests.cs`를 읽고, line 37의 `Assert.AreEqual(ScreenPhase.Schedule, from, "이전 = 부팅 기본 Schedule")`을 새 기본값에 맞춘다. PhaseService FSM이 Story↔Schedule이므로, 테스트가 유효 전환을 검증하려면 시작 페이즈를 테스트에서 명시 설정(`so.Phase = ScreenPhase.Story`)하고 Story→Schedule 또는 Story→Ending 전환을 검증하도록 조정한다(메커니즘 검증 의도 보존).

```csharp
// 부팅 기본값이 Story(순수 VN)로 바뀜 — 전환 메커니즘 검증을 위해 시작 페이즈를 명시.
so.Phase = ScreenPhase.Story;
EventBus.Publish(new RequestPhaseCommand(ScreenPhase.Ending)); // Story→Ending(유효)
...
Assert.AreEqual(ScreenPhase.Story, from, "이전 = 설정한 Story");
```

(구체 라인은 해당 테스트 파일 구조에 맞춰 최소 변경. PhaseService 자체 규칙은 미변경이라 `PhaseServiceTests`는 손대지 않는다.)

- [ ] **Step 2: 테스트 실행 — 통과 확인**

Run (MCP `run_tests` PlayMode, 필터 `PhaseControllerPlayModeTests`):
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Assets/Tests/PlayMode/PhaseControllerPlayModeTests.cs
git commit -m "test(vn): PhaseController 부팅 기본 페이즈 Story로 기대치 갱신

왜: 순수 VN 부팅 기본값 변경(Task 2)에 테스트 정합."
```

---

## Task 5: `Game.unity` 씬 재배선 (🔴, Unity MCP)

**Files:**
- Modify: `Assets/_Project/Scenes/Game.unity`

씬에서 시뮬레이션 UI/컨트롤러를 제거한다. EventBus 디커플이라 발행원이 사라지면 동작이 멈춘다. **삭제만**(`.meta`/프리팹/GUID 무변경, 금지선 #3). Unity 에디터가 떠 있고 MCP가 붙은 상태에서 수행.

- [ ] **Step 1: 씬 구조 파악**

MCP `manage_scene`(get_hierarchy) 또는 에디터로 `Game.unity`를 열어 다음 오브젝트 위치를 식별:
- `ScheduleView`/`ScheduleController` (스케줄 선택 UI + 컨트롤러)
- `ShopView`/`ShopOpenButton` (상점)
- HUD 내 Day/Money/Stat 표시 요소(`HudView` 바인딩 중 해당 TMP)
- schedule/shop 관련 dev 트리거(`DevScheduleButton` 등)

- [ ] **Step 2: 시뮬 오브젝트 제거**

MCP `manage_gameobject`(delete)로 ScheduleView/ScheduleController·ShopView/ShopOpenButton·관련 dev 트리거 오브젝트를 삭제. `_UI/Narrative`·`_Stage`·Messenger·Gacha·4매니저·`GameBootstrap`·`EndingView`는 유지.

- [ ] **Step 3: HUD Day/Money/Stat 표시 제거**

HUD에서 Day/Money/Stat 표시 요소만 제거(또는 비활성). Affinity 표시는 유지 판단은 감독 영역(🟢) — 기본은 시뮬 지표(Day/Money/Stat)만 제거.

- [ ] **Step 4: 부팅 active 상태 확인 (씬 오염 가드)**

저장 전 부팅 active 상태 확인: `Narrative`=inactive(ShowUiGroupCommand가 토글), `EndingRoot`=inactive, 스토리 진입이 정상. (HANDOFF "씬 dirty 오염" 주의.)

- [ ] **Step 5: 씬 저장 + Play 검증(감독)**

씬 저장 후 감독이 Play로 확인: 부팅 → 프롤로그 자동 재생 → 스토리 진행, 스케줄/상점 UI 미표시, 종료 후 Story 유지. (헤드리스 스크린샷은 백지라 시각 확인은 감독 Play.)

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scenes/Game.unity
git commit -m "feat(vn): Game 씬에서 스케줄/상점/스탯HUD 배선 제거

왜: 순수 VN — 시뮬 UI를 씬에서 제거(코드/프리팹 보존, 부활 시 재배선)."
```

---

## Task 6: 전체 검증

**Files:** (없음 — 검증 전용)

- [ ] **Step 1: EditMode 전체 실행**

Run (MCP `run_tests` EditMode):
Expected: 전부 그린(파킹/페이즈 변경 후에도 회귀 0).

- [ ] **Step 2: PlayMode 전체 실행**

Run (MCP `run_tests` PlayMode):
Expected: 전부 그린.

- [ ] **Step 3: 콘솔 0에러 확인**

MCP `read_console`로 에러/예외 0 확인.

- [ ] **Step 4: HANDOFF.md 갱신 커밋**

HANDOFF.md의 "현재 상태"에 VN 전환 결과(시뮬 비활성·`_Parked` 격리·부팅 Story 진입)를 1절 추가.

```bash
git add unity_project/HANDOFF.md
git commit -m "docs(handoff): 순수 VN 전환 완료 기록(시뮬 _Parked 격리·부팅 Story)"
```

---

## 알려진 한계 / 후속 (스펙 §9 + 발견 사항)

- **스토리 종료형 엔딩 트리거**: 마지막 스크립트 종료 → 엔딩 진입은 후속 슬라이스(현재 `EndingView` 미트리거).
- **QuickMenuView 노출**: `QuickMenuView.cs:110`이 `Phase==Schedule`일 때만 표시 → 순수 VN에선 (메신저 열림 외) 안 뜸. 인게임 세이브/타이틀복귀 접근은 내러티브 인포 바 세이브 버튼으로 대체 가능하나, 빠른메뉴 노출 조건 재설계는 **후속 결정 필요**(스코프 밖).
- **PhaseService Schedule 분기 데드코드**: Story↔Schedule FSM의 Schedule 경로 미사용 — 정리는 선택적 후속(YAGNI, 이번엔 미변경).
- **Affinity HUD 피드백**: 상시 표시 제거 시 호감도 피드백 UI는 필요 시 VN 맥락(메신저 등)으로 재설계 — 후속.
