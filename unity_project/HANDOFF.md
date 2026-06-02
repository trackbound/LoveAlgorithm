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
- asmdef 도입 진행: 현재 `LoveAlgo.Core`·`LoveAlgo.Data`·`LoveAlgo.Affinity`·`LoveAlgo.Schedule`·`LoveAlgo.Game`·`LoveAlgo.Narrative`·`LoveAlgo.Save` 7개 + 옛 Assembly-CSharp 공존. 체인: `Core ← Data ← {Affinity, Schedule, Game}`, `Core ← Save`. `Narrative`=refs `{Core, Affinity}`(파서/모델/검증기 순수층 + Flow 인터프리터+라우터). `Audio`=refs `{Core}`. **`Save`만 autoReferenced=false**(구 `LoveAlgo.Save`(SaveModule)·`LoveAlgo.Story.SaveManager`와 단순명 충돌 회피 — 구 Save 폐기 시 true 복귀), 나머지는 autoReferenced=true(`LoveAlgo.Audio`는 신규 ns라 구 `LoveAlgo.Modules.Audio`와 무충돌). 매니저 **3/4 완성**(GameManager·SaveManager·AudioManager). 테스트 어셈블리: `LoveAlgo.Tests.EditMode`·`LoveAlgo.Tests.PlayMode`(autoRef=false).

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
- ✅ **M4 slice2 커밋됨 (Schedule 통합층 재작성)**: 🔴 구 `ScheduleModule`+`DayLoopController.OnScheduleSelected`의 Service Locator 경로를 EventBus+순수층으로 대체. 설계안 감독 승인 후 구현(일일제한=상태직렬화, 하루종료=통지까지).
  - **순수 코어 `Scripts/Schedule/ScheduleService.cs`**: `Execute(gs, type, investMultiplier)` → 게이트(투자 자금<`MinInvestMoney`/`isLimited` 1일1회) 검사 → `ScheduleEffects.Apply`/`ApplyInvest` + `DayLoop.ConsumeAction` 호출 → 변경 전/후 스냅샷으로 `StatChangedEvent[]` 산출 → `ScheduleResult`(거부사유·스탯변화·소지금델타·dayEnded) 반환. RNG/EventBus 모름 = 결정적.
  - **얇은 어댑터 `ScheduleController.cs`**(MonoBehaviour): `ScheduleSelectedCommand` 구독 → 투자면 RNG 배수 주입 → `Execute` → 결과를 EventBus 통지(`StatChangedEvent`×변경분 / `ScheduleAppliedEvent` / 거부 시 `ScheduleRejectedEvent` / 소진 시 `DayEndRequestedEvent`). `State` 프로퍼티로 부팅 와이어링. **Service Locator 제거(금지선4)**.
  - **이벤트 구조체**: Core(`Scripts/Core/Events/`)에 범용 `StatChangedEvent`(구 Contracts 1:1, ns→`LoveAlgo.Events`)·`DayEndRequestedEvent`. Schedule asmdef에 도메인 `ScheduleSelectedCommand`/`ScheduleAppliedEvent`/`ScheduleRejectedEvent`+`ScheduleRejection`.
  - **🔴 스키마(추가-온리)**: `GameStateData.usedLimitedToday`(List<string>) + `GameStateSO.HasUsedLimited/MarkLimitedUsed/ClearDailyLimits`·`StatIds` SSOT. `DayLoop.BeginRun/AdvanceDay`가 하루 전환 시 `ClearDailyLimits`. JsonUtility 호환=기존 세이브 무영향.
  - **asmdef**: `LoveAlgo.Schedule`에 `LoveAlgo.Data` 참조 추가(DayLoop·MinInvestMoney 소속). 순환 없음.
  - **범위 밖(구 코드 무변경 공존)**: 세션버프(Shop), 토스트/피드백문자열·UI 스폰·`ISimulationSubMode`(M5), 하루전환 오케스트레이션(저녁이벤트·페이드·오토세이브·`AdvanceDay` 실행=GameManager 구독자). 구 `ScheduleModule`/`ScheduleUI`/`DayLoopController` 그대로, 매니페스트 미변경.
  - **작동 증거**: MCP force recompile **0에러/0경고** + EditMode **92/92 통과**(81 + slice2 11: 효과적용·StatChange 변경분만·투자게이트 3종·1일1회·AdvanceDay리셋·하루종료신호·null·컨트롤러 발행경로 3종).
- ✅ **M5 slice1 커밋됨 (GameManager 하루전환 코어)**: 🔴 감독이 다음 우선순위로 **M5 GameManager 거처** 선택, 설계안 승인 후 구현. dangling `DayEndRequestedEvent` 구독자 해소.
  - **신규 asmdef `LoveAlgo.Game`(refs Core, Data)** at `Scripts/Game/`. Schedule는 참조 안 함(Schedule가 Core 이벤트만 발행 → 디커플). 체인: `Core ← Data ← Game`.
  - **`GameManager : MonoBehaviour`**(승인된 4매니저 중 1번): `[SerializeField] GameStateSO state` + `State` 프로퍼티. `OnEnable`에서 `DayEndRequestedEvent` 구독 → `OnDayEndRequested`: `DayLoop.AdvanceDay` → 정상 진행이면 `DayChangedEvent(prev,new)` 발행 / `EnteredEnding`이면 `EnteredEndingEvent(day)` 발행 후 return(구 EndDayAsync도 엔딩 시 오토세이브 전 return). 직접 호출 가능(라이프사이클 비의존).
  - **신규 Core 이벤트**(`Scripts/Core/Events/`, ns `LoveAlgo.Events`): `DayChangedEvent`(prev/new) · `EnteredEndingEvent`(day=MaxDay+1).
  - **명시적 deferred seam(주석만, 빈 인프라 X)**: 저녁이벤트 인라인 실행(M3 내러티브)·페이드/로딩(M5 UI)·슬롯0 오토세이브(Save 슬라이스)·페이즈전환(GamePhase 상태머신). 무존재 시스템용 await 훅 인프라는 과설계 게이트로 미작성 — 구 EndDayAsync 13단계는 이들 의존이라 지금 전체 재현 불가.
  - **전환기 공존**: 구 `LoveAlgo.Core.GameManager`(레거시, 페이즈/세이브/컨트롤러 허브)는 옛 씬이 사용 → 그대로 둠, 매니페스트 행 `대기` 유지(신규는 교체 아닌 책임 일부 신규 구현). 단순명 충돌은 테스트에서 `using GameManager = LoveAlgo.Game.GameManager;` 별칭으로 해소.
  - **작동 증거**: MCP force recompile **0에러/0경고** + EditMode **95/95 통과**(92 + slice1 3: 정상 하루전환+DayChanged·엔딩경계 30→31 EnteredEnding+DayChanged 미발행·null state 가드).
- ✅ **테스트 인프라 정립 커밋됨 (Tests asmdef + PlayMode)**: 🟡 프로덕션 코드 무변경. 그간 테스트가 asmdef 없이 암묵적 `Assembly-CSharp-Editor`에 얹혀 PlayMode 테스트를 못 돌리던 갭 해소.
  - **제약(핵심)**: asmdef 테스트 어셈블리는 구 모놀리식 `Assembly-CSharp`를 참조 불가 → 구 코드 테스트는 asmdef로 못 옮김. 그래서 **신규(asmdef) 대상 / 구(모놀리식) 대상**으로 분리.
  - **신규 `LoveAlgo.Tests.EditMode` asmdef**(`Assets/Tests/EditMode/`, refs Core·Data·Affinity·Schedule·Game + TestRunner, autoRef=false): 신규 코드 테스트 6개를 `git mv`로 이동(메타 GUID·이력 보존) — Affinity/DayLoop/ScheduleEffects/ScheduleService/GameStateSave/GameManager. 부수효과로 **신규 테스트가 구 모놀리식에서 격리**(아키텍처 이득).
  - **신규 `LoveAlgo.Tests.PlayMode` asmdef**(`Assets/Tests/PlayMode/`): `GameManagerPlayModeTests` 3종([UnityTest]) — EditMode가 못 덮던 `OnEnable` 구독 경로를 실제 런타임에서 검증(구독→DayEndRequested→DayChanged / 풀체인 ScheduleSelect→하루전환 / 엔딩경계). dev 하니스 우회를 Test Runner 정식 테스트로 승격.
  - **구 코드 테스트 3개 잔류**(`Assets/Tests/Editor/`, Assembly-CSharp-Editor): ScriptParser·ScriptValidator(`LoveAlgo.Story`)·SaveLoadRoundTrip(`LoveAlgo.Story.SaveSystem`). 해당 구 코드 이식 시 함께 정리.
  - **작동 증거**: EditMode **95/95**(이동 후 총합 불변=손실 없음) + PlayMode **3/3**, 컴파일 0에러.
  - **참고(execute_code 막힘)**: MCP `execute_code`는 Roslyn 미설치→CodeDom 폴백 + 전체 어셈블리 `/r:` 커맨드라인 한계로 이 프로젝트에선 사용 불가. 런타임 검증은 PlayMode 테스트 어셈블리로 한다(서드파티 cruft 정리는 별도 백로그).
- ✅ **M3 slice1 커밋됨 (내러티브 파서/모델/검증기 순수층 이식)**: 🟡 감독이 추천순 1위로 M3 선택. Schedule slice1과 동일 패턴(네임스페이스·GUID 보존 + autoref로 구 소비처 무변경).
  - **신규 asmdef `LoveAlgo.Narrative`(refs 없음=자기완결 리프)** at `Scripts/Narrative/`. 순수 6파일을 `git mv`로 이식 — `ScriptLine`(model: ScriptLine/LineType/NextType, ns `LoveAlgo.Story`)·`CsvUtility`·`ScriptParser`(ns `LoveAlgo.Story`)·`CommandAliases`·`FXCommandSignatures`·`ScriptValidator`(ns `LoveAlgo.Story.StoryEngine`). 의존 폐포가 System+UnityEngine뿐이라 깨짐 없음(UnityEngine.Debug/TextAsset/RuntimeInitializeOnLoadMethod는 asmdef 정상).
  - **범위 밖(구 Assembly-CSharp 잔류)**: runtime/오케스트레이션 = `ScriptRunner`·`ScriptEngine`·`Engine/Handlers/*`·`Engine/Flow/*`·`Engine/Macros/*`·UI(`DialogueUI`/`ChoicePopup` 등)·Editor 도구·`ScriptCsvSerializer`/`StoryMappings` 등. 구 소비처 14곳은 ns 보존+autoref로 무변경 컴파일.
  - **🟠 전환기 가교(IVT)**: `ScriptLine` 속성이 `internal set`이라, 모놀리식에 함께 있던 `DevTools/ScenarioEditor/ScenarioEditorIMGUI`(유일한 writer)가 asmdef 분리 후 막힘(CS0200) → `Scripts/Narrative/AssemblyInfo.cs`에 `[assembly:InternalsVisibleTo("Assembly-CSharp"/"Assembly-CSharp-Editor")]` 추가. 해당 소비처 이식/삭제 시 제거.
  - **테스트 이식**: 기존 `ScriptParserTests`(6)·`ScriptValidatorTests`(9)를 `Assets/Tests/EditMode/`(asmdef)로 `git mv`, EditMode asmdef에 `LoveAlgo.Narrative` 참조 추가. 구 `SaveLoadRoundTripTests`만 `Assets/Tests/Editor/`(Assembly-CSharp-Editor) 잔류(구 SaveSystem 의존).
  - **작동 증거**: MCP force recompile 0에러 + EditMode **95/95**(이동 후 총합 불변).
  - **다음 narrative 슬라이스 후보**: ① 명령 파이프(ILineExecutor/handlers)와 Flow 커맨드(`Affinity:`/`Schedule:`/`Day:` 등)를 EventBus+State로 재작성 → AffinityFormula·ScheduleService·GameManager 연결. ② ScriptRunner→내러티브 진행을 GameManager seam(저녁이벤트)과 연결. UI(DialogueUI)는 M5.
- ✅ **Save 슬라이스 커밋됨 (SaveManager + GameManager 오토세이브 seam)**: 🔴 설계안 감독 승인(트리거=GameManager가 SaveRequested 발행 / asmdef=신규 LoveAlgo.Save) 후 구현.
  - **신규 asmdef `LoveAlgo.Save`(refs Core, autoRef=false)** at `Scripts/Save/`. ScheduleController 패턴 미러: 순수 static + 얇은 MonoBehaviour 어댑터.
  - **순수 `SaveService`(static)**: `Capture(gs,label)→SaveData` / `Save(slot,gs,label)→bool`(JsonSaveStore 위임) / `Load(slot,gs)→bool`(`gs.Load(data.state)`). Core 타입만 의존 = 결정적·EditMode 테스트.
  - **`SaveManager : MonoBehaviour`**: `SaveRequestedEvent` 구독 → `SaveService.Save` → `SaveCompletedEvent` 발행. `State` 프로퍼티 부팅 와이어링. 라벨=`Day N`(임시).
  - **신규 Core 이벤트**: `SaveRequestedEvent(slot,reason)` · `SaveCompletedEvent(slot,success)`(`LoveAlgo.Events`).
  - **GameManager 오토세이브 seam 채움**: 하루전환 시 `AdvanceDay` 직후 `SaveRequestedEvent(AutoSaveSlot,"day-end")` 발행 → `DayChangedEvent`(구 EndDayAsync day++→오토세이브 순서 재현).
  - **범위 밖**: 썸네일 캡처(M5 UI)·로드 트리거(타이틀/이어하기=M5, SaveService.Load는 있으나 미배선)·슬롯 메타 라벨 확장. 구 `LoveAlgo.Save`(SaveModule)·`LoveAlgo.Story.SaveManager`는 공존(autoRef=false로 충돌 회피).
  - **작동 증거**: 컴파일 0에러 + EditMode **101/101**(95+SaveService 6) + PlayMode **4/4**(GameManager 3 + 하루전환→오토세이브 슬롯0 파일생성·재로드 일차 영구화 1).
- ✅ **M3 slice2 커밋됨 (Flow 커맨드 Affinity:/Day: 순수 인터프리터)**: 🟠 설계안 감독 승인(순수 인터프리터+결과반환, 발행은 엔진 슬라이스로 연기). 떠 있던 `AffinityFormula`를 CSV 문법에 연결(공식 무변경=금지선2).
  - **`LoveAlgo.Narrative` asmdef refs +`{Core, Affinity}`**(무의존 리프→런타임 동작 획득, 순환 없음).
  - **순수 `FlowCommandInterpreter.Apply(gs, command)→FlowCommandResult`**(ns 보존 `LoveAlgo.Story.StoryEngine.Flow`): `Affinity:EventChoice:{hid}:{tag}:{pts}`→`AffinityFormula.RecordEventChoice`(Event3 +2 재선택 포함) / `Affinity:Point:{hid}:{cat}:{amt}`→`AddPoint` / `Day:{N}`→`gs.Day=N`(표시용, 전환 아님). 구 `AffinityFlowCommand`/`DayFlowCommand` 문법 1:1. EventBus/Services 모름=결정적.
  - **발행 연기**: 호출자(내러티브 엔진=미이식)가 `FlowCommandResult` 보고 통지 발행. 그래서 `AffinityChangedEvent`도 지금 미추가(dangling 회피). 구 Flow 커맨드는 공존(구 엔진 사용).
  - **범위 밖**: 제어흐름 Flow(Jump/If/LoadingScene/MiniGame/Message/LockScreen/Username)=엔진 내부, ScriptEngine 이식, UI.
  - **작동 증거**: 컴파일 0에러 + EditMode **107/107**(101 + slice2 6: EventChoice·Event3재선택+2·Point·Day·악성입력 거부·null).
- ✅ **M3 slice3 커밋됨 (FlowCommandRouter — 인터프리터 EventBus 어댑터)**: 🟠 구 `ScriptEngine` 전체는 UI/async 결합으로 미이식, 대신 인터프리터의 **런타임 호출자**를 ScheduleController 패턴으로 구현(slice2의 "발행 연기" 충족).
  - **`FlowCommandRouter : MonoBehaviour`**(LoveAlgo.Narrative, ns `LoveAlgo.Story.StoryEngine.Flow`): `FlowCommandRequestedEvent` 구독 → `FlowCommandInterpreter.Apply` → Affinity 계열이면 `AffinityChangedEvent`, Day면 `DayChangedEvent` 발행. `State` 부팅 와이어링.
  - **신규 Core 이벤트**: `FlowCommandRequestedEvent(command)`(명령) · `AffinityChangedEvent(heroineId, newScore)`(통지, HUD용).
  - **연결 완성**: 이제 누구든(엔진 이식 시 ScriptEngine, 현재는 테스트/디버그) `FlowCommandRequestedEvent` 발행으로 CSV Flow→호감도 경로가 런타임에 동작. 제어흐름 Flow·ScriptEngine·UI는 여전히 범위 밖.
  - **작동 증거**: 컴파일 0에러 + EditMode **111/111**(107+4) + PlayMode **5/5**(4 + 라우터 OnEnable 구독 1).
- ✅ **AudioManager 슬라이스1 커밋됨 (핵심 재생 EventBus화)**: 🟠 설계 승인(BGM/SFX/Voice 재생만 + 코루틴 페이드, DOTween 제거). 구 970줄 AudioManager(Singleton+IAudio+DOTween+UI버튼바인딩+믹서)의 핵심 재생만 이식.
  - **신규 `LoveAlgo.Audio` asmdef(refs Core, autoRef=true, ns `LoveAlgo.Audio`)**. Services/IAudio/Singleton 제거(3번째 매니저).
  - **Core 명령 이벤트**(`Scripts/Core/Events/AudioEvents.cs`): `PlayBgmCommand(name,fade)`·`StopBgmCommand(fade)`·`PlaySfxCommand`·`PlayVoiceCommand`·`StopVoiceCommand` + `BgmChangedEvent`(구 BGMChangedEvent 이식).
  - **`AudioManager : MonoBehaviour`**: 5명령 구독 → AudioSource 재생. AudioSource 미바인딩 시 자동생성(`EnsureSources`), clip 해석은 주입형 `ClipLoader`(기본 `Resources.Load<AudioClip>("Audio/{cat}/{name}")`, 카탈로그/테스트 대체 가능). 페이드=자체 코루틴(DOTween 결합 제거). `CurrentBgm` 노출.
  - **범위 밖(후속)**: AudioMixer 볼륨(Settings)·UI사운드/버튼자동바인딩/타이핑(M5)·캐릭터 BGM 자동전환(Stage)·캐릭터 entry SFX·StoryMappings BGM alias·앱 포커스 pause.
  - **작동 증거**: 컴파일 0에러 + EditMode **116/116**(111+5: BGM설정/중복무시/정지/클립없음/SFX·Voice 로더호출) + PlayMode **6/6**(5 + AudioManager OnEnable 구독 1).
- ▶️ **다음 착수(다음 세션)**: 감독이 다음 마일스톤 선택. 남은 연결고리:
  - **`DayChangedEvent`/`EnteredEndingEvent` 구독자**: HUD·페이즈 UI(M5 UI), 엔딩 화면(M5).
  - **GameManager 잔여 seam 채우기**: 저녁이벤트(M3 내러티브 이식 후)·페이드(M5 UI)·페이즈전환(GamePhase). ~~오토세이브~~=Save 슬라이스에서 완료. 부팅 와이어링(GameStateSO를 ScheduleController/SaveManager.State 등에 주입)도 GameManager 소관(후속).
  - **`SaveCompletedEvent` 구독자**: 저장 토스트 UI(M5). 로드 트리거(`SaveService.Load`)는 타이틀/이어하기 흐름(M5)에서 배선.
  - **세션버프**: Shop 슬라이스에서 `ScheduleAppliedEvent` 구독으로 적용(적용 순서=구 코드 base효과 직후 → 경계 확정 필요).
  - **UI**: M5에서 ScheduleUI가 `ScheduleSelectedCommand` 발행 + `ScheduleApplied/Rejected/StatChanged` 구독(토스트·HUD). `ScheduleTable` 정적 질의 직접 사용(ISchedule 불필요).

### 워크플로우 규율 (directive)
- 무언가 만들 때마다 **작동 증거**(dev_guide 증거우선): 순수/공식층은 **EditMode 테스트**(`LoveAlgo.Tests.EditMode`), MonoBehaviour 라이프사이클·구독·씬 와이어링은 **PlayMode 테스트**(`LoveAlgo.Tests.PlayMode`). 임시 dev 하니스/씬 금지 — Test Runner 어셈블리로.
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
