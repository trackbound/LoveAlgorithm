# 🔑 HANDOFF — 재작성 세션 진입점 (LoveAlgorithm)

> 이 문서를 읽는 Claude에게: LoveAlgorithm 코드베이스를 **EventBus + ScriptableObject 단일 패턴으로 전체 재작성** 중이다.
> 대화를 재현하려 하지 말고, 아래 결론·금지선·다음 액션만 지켜라.
> 감독은 CS 전공 + Unity/C# 베테랑. Unity 기초 설명 금지, 동료 개발자처럼 설계 근거 중심으로.

---

## ⚡ 30초 요약

- **프로젝트**: LoveAlgorithm — Unity 6 + URP 2D 비주얼노벨/연애 시뮬. 5히로인·30일 루프·CSV 스토리 엔진.
- **지금**: **코드 전체 재작성**(아트/프리팹 유지). 아키텍처를 Service Locator → **EventBus + SO 단일**로 전환.
- **브랜치**: `rewrite/eventbus-so`(작업) / `wip/pre-rewrite-snapshot` @ 9ac3c9e(재작성 전 미커밋 WIP 406파일 보존) / `main` b40964b.
- **기준 문서(3종 시트 + ADR)**: `docs/REWRITE_FEATURE_INVENTORY.md`(기능·공식·수치) · `REWRITE_CLASS_MANIFEST.csv`(전 클래스 처리/상태 체크리스트) · `REWRITE_TUNING_VALUES.csv`(연출 수치 동결). 결정 이유 = `docs/decisions.md` ADR-007~012.
- **환경**: Unity 에디터 + MCP(`mcp__mcp-unity__*`)가 **main 작업트리 = 현재 rewrite 브랜치**를 본다. 컴파일/콘솔 검증 가능.
  - ⚠️ **MCP 연결 주의(이번 세션 경험)**: `.mcp.json`의 `mcp-unity`는 stdio node 명령이라 **세션 시작 시 에디터가 떠 있어야** 도구가 붙는다. 에디터 닫힌 채 시작했으면 세션 중 `/mcp` 재연결 필요. 안 붙으면 **헤드리스 배치로 검증 대체** 가능(아래 레시피, 에디터 닫힌 상태에서만 — Library 잠금 회피).
  - **헤드리스 EditMode 검증 레시피**: `& "C:/Program Files/Unity/Hub/Editor/6000.4.3f1/Editor/Unity.exe" -batchmode -projectPath <proj> -runTests -testPlatform EditMode -testResults <xml> -logFile <log> -accept-apiupdate` → exit 0 + `<test-run … result="Passed">`. 임시 산출물(log/xml)은 커밋 전 삭제.

---

## 🚫 금지선

1. **추측 금지** — 기능·수치 모르면 `REWRITE_FEATURE_INVENTORY.md`/코드 확인 또는 질문.
2. **호감도 공식·수치 변경 금지** — 인벤토리 §4 그대로 재현(임계치 로아46/하예은32/서다은35/이봄39/도희원43 등).
3. **아트/프리팹/씬/SO GUID 보존** — `.meta` 건드리지 말 것. 코드만 재작성.
4. **Service Locator / 인터페이스 계약(`I*`) 부활 금지** — EventBus + State SO만 (ADR-007 supersede ADR-002/006).
5. **매니저 4개 초과 금지** — GameManager/AudioManager/SaveManager/UIManager.
6. **SO 상태 영구화 금지** — 런타임 상태는 부팅 리셋 + 세이브 직렬화(Definition/Instance 분리).
7. **과설계 게이트** — "나중에 쓸지도"면 만들지 말 것.

---

## ✅ 확정 (ADR 근거)

- 아키텍처: EventBus(통지·명령) + State SO 직접 읽기(동기 GET) + 완료-이벤트(await 케이스). (ADR-007)
- 전체 재작성, 아트 보존, `rewrite/eventbus-so`. (ADR-008)
- 내러티브: Ink 비채택, 자체 CSV 엔진 재구현. (ADR-009)
- 운영: 위험도 게이트 + 마일스톤 + 형태문서 금지 + 커밋 "왜". (ADR-010)
- 구조: 코드 `_Project/Scripts/`(피처별 asmdef) + 아트/프리팹 타입별 중앙화. (ADR-011)
- 재설계(전사 금지) + 세션 연속성 규율 + 연출 수치 SO화. (ADR-012)
- asmdef 도입 진행: 현재 `LoveAlgo.Core`·`LoveAlgo.Data`·`LoveAlgo.Affinity`·`LoveAlgo.Schedule` 4개(전부 autoReferenced; Data·Affinity·Schedule 모두 Core 의존, Affinity는 Data도 의존) + 옛 Assembly-CSharp 공존.

---

## 위험도 게이트 (작업 착수 시 등급 선언)

| 등급 | 대상 | 리뷰 |
|---|---|---|
| 🔴 Critical | EventBus, SaveData 스키마, State SO, 씬 흐름 | 감독 정독+승인 |
| 🟠 High | 모듈 경계, 세이브 마이그레이션 | 설계+diff |
| 🟡 Medium | 모듈 내부 로직 | 작동증거+diff 훑기 |
| 🟢 Low | SO 에셋, UI 트윈 | 작동 테스트만 |

---

## ▶️ 다음 액션

**베이스 = WIP 스냅샷 위로 rebase 완료(컴파일되는 상태). State SO = 단일 확정.**

### 지금 정확한 지점 (M1 진행 중)
- ✅ **M1 step1 커밋됨**: `Scripts/Core` + `LoveAlgo.Core` asmdef + 무의존 인프라 6개 이식(EventBus·Log·MoneyFormat·NameValidator·Hangul·Headless). 콘솔 클린 확인됨.
- ✅ **M1 step2 커밋됨 (`136d831`)**: `Scripts/Core/State/GameStateData.cs`·`GameStateSO.cs`(단일, [NonSerialized]+ResetRuntime), `Scripts/Core/Save/SaveData.cs`·`JsonSaveStore.cs`(JsonUtility, dict=엔트리리스트). 0에러 컴파일 확인.
  - **충돌 해소(전환기 한정, M5 구 Save 폐기 시 제거)**: 신규 `LoveAlgo.Core.SaveData`가 구 `LoveAlgo.Story.SaveSystem.SaveData`와 동명 → ISave/SaveModule/SaveManager는 `using` 별칭, 동일 네임스페이스라 별칭 무효인 SessionController는 타입 한정. 구 로직 무변경.
  - **작동 증거**: `GameStateSaveRoundTripTests`(EditMode 7케이스) 신설 — 전체 EditMode **27/27 통과**. (감독 결정: 세이브 검증은 프로젝트 관행대로 EditMode, 플레이모드 씬 대신.)
  - **썸네일 캡처(옛 step③)는 M5로 연기**: 캡처 코드는 Save 기능모듈 소관이고 현재 Core엔 `thumbnailFile` 필드 + 경로 헬퍼만 있음. 레이어 배제 캡처 요구사항은 아래 워크플로우 규율에 유지.
- ✅ **MCP for Unity 연결됨** — recompile/콘솔/테스트 실행 가능 확인.
- ✅ **M1 종료**: 잔여 Common 3개는 능동 이식 불가/시기상조로 **소비처(구 모듈) 이식 시점에 함께 처리**하기로 확정(파킹). 근거는 아래 "잔여 Common" 갱신 참조. Core 인프라 완료.
- ✅ **M2 slice1 커밋됨 (`918cdac` 호감도 엔진)**: ① Core `GameStateSO`에 스탯 API `GetStat/SetStat/AddStat`(0~100 클램프, "Int"→intel 매핑) 추가. ② `GameStateData`에 포인트추적 상태(`heroinePoints`/`eventChoices`) 직렬화 필드 추가 — 구 `HeroinePointTracker` static dict 대체. ③ 신규 asmdef `LoveAlgo.Affinity` + `AffinityFormula`(순수 함수): 총점·스탯보너스(+3/+1/0)·로아 피로(+3/+6/+10)·엔딩 판정(로아 히든 우선→마진 최대)·Event3 +2 재선택 = 인벤토리 §4 1:1 재현. 임계치/선호스탯은 검증된 폴백 상수표 내장.
  - **구 코드 무변경**: 구 `AffinityCalculator`/`HeroinePointTracker`(LoveAlgo.Modules.Affinity)는 옛 모듈이 아직 사용 → 그대로 공존, 매니페스트 상태 미변경.
- ✅ **M2 slice2 커밋됨 (`969c9cb` GameBalanceSO 이식 + Definition 연결)**: ① `DayType`/`StoryArc` enum을 `LoveAlgo.Core`(신규 `Scripts/Core/GameFlowTypes.cs`)로 이전 — 도메인 타입이라 이후 모듈이 Data 의존 없이 참조. 구 `GameTimeline.cs`는 enum 정의만 제거(테이블 무변경), asmdef auto-ref로 계속 사용. ② 신규 asmdef `LoveAlgo.Data`(refs Core) + `GameBalanceSO.cs`를 `git mv`로 `Scripts/Data`에 이동 — **스크립트 .meta GUID `d1b5ea…` 보존** → `GameBalance.asset` m_Script 재바인딩·직렬화 데이터 보존. ③ `AffinityFormula.Configure(GameBalanceSO)`/`ResetToFallback()` 추가, 하드코딩 상수표는 `FallbackHeroines` 폴백으로 유지(헤드리스/테스트), 채점 함수 순수성 유지.
  - **asmdef 의존 체인**: `LoveAlgo.Core ← LoveAlgo.Data ← LoveAlgo.Affinity`(전부 autoReferenced, 구 Assembly-CSharp과 공존).
  - **작동 증거**: MCP force recompile 콘솔 0에러, EditMode **63/63 통과**(M1 27 + M2 32 + Definition 연결 4). 실제 `GameBalance.asset` 로드로 §4 임계치(46/32/35/39/43) 무드리프트 동시 검증.
  - **보류(과설계 게이트)**: `Configure` 부팅 호출 주체(매니저)는 M4/M5 소관.
- ✅ **M2 slice3 커밋됨 (GameConstants·GameTimeline 이식)**: 구 `GameConstants.cs`/`GameTimeline.cs`(Assembly-CSharp의 마지막 밸런스 진입점)를 `git mv`로 `Scripts/Data`(=`LoveAlgo.Data` asmdef)로 이동. 둘 다 `GameBalanceSO`(Definition)에 의존 → Data 레이어가 정확한 소속(Core는 Data 참조 불가=순환). **새 asmdef 추가 없음**(체인 그대로 Core←Data←Affinity).
  - **무변경 이식**: `.meta` GUID·이력 보존(`git mv`, R100=내용 무변경), 네임스페이스(`LoveAlgo` / `LoveAlgo.Core`) 유지 → 구 모듈 57개 소비처 무변경 컴파일. `GameTimeline`(ns `LoveAlgo.Core`)은 부모 ns 탐색으로 `LoveAlgo.GameBalanceSO`를, Data→Core 참조로 `DayType`/`StoryArc`를 그대로 해소.
  - **폴백 유지(정리 결론)**: §4 폴백 상수(46/32/35/39/43, actionsPerDay/maxDay 등)는 헤드리스/테스트 격리용으로 보존 — `AffinityFormula` 폴백과 동일 근거. 삭제 시 SO 부재 상황에서 회귀 위험이라 미삭제가 정합적.
  - **작동 증거**: 에디터 미실행 → 헤드리스 배치(`Unity 6000.4.3f1 -batchmode -runTests -testPlatform EditMode`) 실행, 컴파일 0에러 + **EditMode 63/63 통과**(exit 0). 전 어셈블리 컴파일 확인.
- ✅ **M2 slice4 커밋됨 (데이루프 진행 공식)**: 🔴 GameStateData/SO 스키마 추가 + 순수 진행 모듈.
  - **스키마(세이브) 추가-온리**: `GameStateData.remainingActions`(int, §7 세이브 필드). `GameStateSO`에 `Day`/`RemainingActions` 카운터 접근자 + `Money`(long, 세터 0 바닥 클램프=구 `Mathf.Max(0,…)` 재현)/`AddMoney`. JsonUtility 누락 필드=기본값이라 기존 세이브 호환(현재 실세이브 없음).
  - **신규 `Scripts/Data/DayLoop.cs`(ns `LoveAlgo.Core`, Data asmdef)**: 순수 정적 함수 `BeginRun`(1일차+행동 풀충전)·`ConsumeAction`(소모, 0이하면 하루종료 신호)·`AdvanceDay`(일차+1·풀충전·`MaxDay` 초과 시 `EnteredEnding`)·`IsEndingReached`·`IsFreeDay`/`IsEventDay`(GameTimeline 위임). MaxDay30/ActionsPerDay2는 GameConstants에서 로드 → Data asmdef 소속.
  - **범위 결정(과설계 게이트)**: 구 `DayLoopController`의 페이드/UI/ScriptRunner/AutoSave/세션버프/인라인스케줄=오케스트레이션(M4/M5), 스케줄 효과표·투자 RNG=Schedule 모듈(M4)로 분리. slice4는 진행 공식 순수 코어만.
  - **구 코드 무변경**: 구 `DayLoopController`/`GameState`/`ScheduleTable`은 옛 모듈이 사용 → 공존, 매니페스트 미변경.
  - **작동 증거**: 헤드리스 배치(6000.4.3f1) 컴파일 0에러 + **EditMode 71/71 통과**(63 + slice4 8: BeginRun/Consume/Advance/엔딩경계 30→31/Money바닥/직렬화 라운드트립).
- ✅ **M4 slice1 커밋됨 (Schedule 데이터/공식층 이식)**: 감독이 다음 우선순위로 **M4 Schedule** 선택.
  - **신규 asmdef `LoveAlgo.Schedule`(refs Core)** at `Scripts/Schedule/`. `ScheduleType.cs`(ScheduleType/Category/Effect + ScheduleTable)·`ScheduleDataSO.cs`를 `git mv`로 이식 — 네임스페이스 `LoveAlgo.Schedule`·GUID 보존(R100), `MoneyFormat`(Core)만 의존이라 깨짐 없음. 구 UI(`ScheduleUI`/`ScheduleSlot`/`ScheduleModule` 등, namespace 동일·Assembly-CSharp 잔류)는 auto-ref로 무변경 컴파일.
  - **신규 순수 적용기 `ScheduleEffects.cs`**: `Apply(gs, effect)`(스탯/소지금 변화, 클램프)·`ApplyInvest(gs, multiplier)`(±50~100%, 배수=호출자 주입으로 RNG 분리, 0 바닥, 실반영액 반환). 구 `DayLoopController.OnScheduleSelected`의 순수 부분만 재현 — 토스트/세션버프/RNG/투자 게이트는 통합층(slice2)·Shop(별도) 소관.
  - **작동 증거**: 헤드리스 배치(6000.4.3f1) 컴파일 0에러 + **EditMode 81/81 통과**(71 + slice1 10: 적용/클램프/투자 바닥/카테고리·9종·Loading제한).
- ▶️ **다음 착수(다음 세션)**: M4 slice2 — 🔴 Schedule **통합층 재작성**. **설계안부터 감독 검토 후 구현** (위험도 게이트 Critical).
  - **대상**: 구 `Assets/_Project/Modules/Schedule/Code/ScheduleModule.cs` — `ISchedule`/`ISimulationSubMode` 구현 + `Services.Register<ISchedule>` (Service Locator = 금지선4 제거 대상).
  - **풀어야 할 설계 결정**:
    1. **스탯변경 통지 이벤트 구조체** 정의(구 `LoveAlgo.Contracts.StatChangedEvent` 대체). `Scripts/Core` 또는 신규 이벤트 모듈에 `readonly struct`로? `ScheduleEffects.Apply`는 순수 유지하고 **호출자(통합층)가 Apply 직후 `EventBus.Publish`** 하는 경계가 맞는지 확정.
    2. 스케줄 선택→적용 진입점을 어디가 소유하나(매니저 4개 중? GameManager 데이루프 측?). 구 `DayLoopController.OnScheduleSelected` 책임의 새 거처.
    3. 투자 게이트(소지금 ≥ `GameConstants.MinInvestMoney`)·1일1회 제한(`isLimited`)·세션버프(Shop, 별도)·토스트(UI, M5) 경계 분리.
  - **참고 구현물**: 순수층은 이미 있음 — `ScheduleEffects.Apply/ApplyInvest`(LoveAlgo.Schedule), `DayLoop.ConsumeAction/AdvanceDay`(LoveAlgo.Core). 통합층은 이 둘을 호출+이벤트 발행만.
  - **구 코드 현황**: `ScheduleModule`/`ScheduleUI`/`DayLoopController` 등 옛 모듈은 무변경 공존 중(매니페스트 상태 미변경). slice2에서 통합층 이식 시 구 `ScheduleModule` 처리.

### 워크플로우 규율 (directive)
- 무언가 만들 때마다 **전용 테스트 씬 + 플레이모드로 작동 증거**(dev_guide 증거우선).
- **썸네일은 레이어 배제 캡처**가 필수 요구사항(옛 개발 말썽: 안 잡혀야 할 UI 포함됨).

### 잔여 Common (소비처 이식 시점 처리로 확정 — 파킹)
세 파일 모두 **미이식 구 Assembly-CSharp(Core/Modules/UI)에서만** 쓰임 → "항상 컴파일 가능" 원칙상 지금 이동/삭제 불가. 마이그레이션 원칙(피처 하나씩 옮기고 옛 코드는 그때 삭제)에 따라 소비처 피처가 이식될 때 함께 처리한다.
- `ListenerBag.cs`(유지, UI 전용)→ UI 피처 이식(M5) 시 `LoveAlgo.UI` asmdef로. 지금 단독 이동은 과설계.
- `SingletonMonoBehaviour.cs`(유지, DOTween 의존)→ 매니저 재구축 시 재설계 후 배치.
- `Services.cs`(폐기, 소비처 37곳)→ 마지막 구 모듈 이식 시 소비처와 함께 삭제.

### 마이그레이션 전략 / 마일스톤
- 새 asmdef 코드는 옛 Assembly-CSharp과 공존(자동 참조). 피처 하나씩 새 구조로 옮기고 옛 코드는 그때 삭제(매니페스트 `상태` 갱신). 항상 컴파일 가능.
- M1 Core 인프라 → M2 Data(SO)+호감도/스탯/데이루프 공식 → M3 내러티브/스테이지 → M4 기능모듈 → M5 UI/Save.

---

*결론과 가드레일만 전달. 상세 규칙 = docs/dev_guide.md, 기능 = docs/REWRITE_FEATURE_INVENTORY.md. 막히면 감독에게 질문.*
