# 🔑 HANDOFF — 재작성 세션 진입점 (LoveAlgorithm)

> 이 문서를 읽는 Claude에게: LoveAlgorithm 코드베이스를 **EventBus + ScriptableObject 단일 패턴으로 전체 재작성** 중이다.
> 대화를 재현하려 하지 말고, 아래 결론·금지선·다음 액션만 지켜라.
> 감독은 CS 전공 + Unity/C# 베테랑. Unity 기초 설명 금지, 동료 개발자처럼 설계 근거 중심으로.

---

## ⚡ 30초 요약

- **프로젝트**: LoveAlgorithm — Unity 6 + URP 2D 비주얼노벨/연애 시뮬. 5히로인·30일 루프·CSV 스토리 엔진.
- **지금**: **코드 전체 재작성**(아트/프리팹 유지). 아키텍처를 Service Locator → **EventBus + SO 단일**로 전환.
- **브랜치**: `rewrite/eventbus-so`(작업) / `wip/pre-rewrite-snapshot` @ 9ac3c9e(재작성 전 WIP 보존) / `main` b40964b.
- **기준 문서**: `docs/REWRITE_FEATURE_INVENTORY.md`(기능·공식·수치) · `REWRITE_CLASS_MANIFEST.csv`(클래스 처리 체크리스트) · `REWRITE_TUNING_VALUES.csv`(연출 수치 동결) · `docs/decisions.md` ADR-007~012.
- **환경**: Unity 에디터 + MCP(`mcp__UnityMCP__*`)가 작업트리를 본다. recompile/콘솔/테스트(`run_tests`) 가능.
  - ⚠️ **MCP는 세션 시작 시 에디터가 떠 있어야** 붙는다(stdio). 안 붙으면 `/mcp` 재연결 또는 헤드리스 배치(`Unity 6000.4.3f1 -batchmode -runTests -testPlatform EditMode`, 에디터 닫힌 상태에서만). 임시 산출물(log/xml)은 커밋 전 삭제.
  - ⚠️ **MCP `execute_code` 사용 불가**(Roslyn 미설치+DLL 수 커맨드라인 한계), **헤드리스 스크린샷 백지**(포커스 없는 Game View). 런타임 검증은 **PlayMode 테스트**로, 시각 확인은 감독이 직접 Play.

---

## 🚫 금지선

1. **추측 금지** — 기능·수치 모르면 인벤토리/코드 확인 또는 **질문**. (이번 세션 Shop 슬라이스2에서 적용: 동결값/스키마 인접 결정은 멈추고 질문.)
2. **호감도 공식·수치 변경 금지** — 인벤토리 §4 그대로(임계치 로아46/하예은32/서다은35/이봄39/도희원43 등).
3. **아트/프리팹/씬/SO GUID 보존** — `.meta` 건드리지 말 것(=`git mv`로 이동). 코드만 재작성.
4. **Service Locator / 인터페이스 계약(`I*`) 부활 금지** — EventBus + State SO만 (ADR-007).
5. **매니저 4개 초과 금지** — GameManager/AudioManager/SaveManager/UIManager (4개 골격 완성됨).
6. **SO 상태 영구화 금지** — 런타임 상태는 부팅 리셋 + 세이브 직렬화(Definition/Instance 분리).
7. **과설계 게이트** — "나중에 쓸지도"면 만들지 말 것.

---

## ✅ 확정 (ADR 근거)

- 아키텍처: EventBus(통지·명령) + State SO 직접 읽기(동기 GET) + 완료-이벤트(await 케이스). (ADR-007)
- 전체 재작성·아트 보존·`rewrite/eventbus-so`(ADR-008) / 내러티브 자체 CSV 엔진(ADR-009) / 위험도 게이트+마일스톤+커밋"왜"(ADR-010) / 코드 `_Project/Scripts/` 피처별 asmdef(ADR-011) / 연출 수치 SO화(ADR-012).
- **순수층 + 얇은 어댑터 패턴(정착됨)**: 공식/결정 로직 = 순수 static(`*Service`/`*Formula`/`*Interpreter`, GameStateSO 인자, EventBus 무관, EditMode 테스트). EventBus 연결 = 얇은 MonoBehaviour 어댑터(`*Controller`/`*Manager`, OnEnable 구독→순수 호출→통지 발행). 새 기능은 이 패턴을 따른다.
- **asmdef 9개**: `Core ← Data ← {Affinity, Schedule, Game}`, `Core ← {Save, Audio, Shop}`, `Narrative ← {Core, Affinity}`, `UI ← {Core, TMP}`. + 테스트 `LoveAlgo.Tests.EditMode/PlayMode`. **`Save`·`UI`만 autoReferenced=false**(구 동명 ns 충돌 회피), 나머지 true. 전부 옛 Assembly-CSharp과 공존.

---

## 위험도 게이트 (작업 착수 시 등급 선언)

| 등급 | 대상 | 리뷰 |
|---|---|---|
| 🔴 Critical | EventBus, SaveData 스키마, State SO, 씬 흐름 | 감독 정독+승인 |
| 🟠 High | 모듈 경계, 세이브 마이그레이션 | 설계+diff |
| 🟡 Medium | 모듈 내부 로직 | 작동증거+diff 훑기 |
| 🟢 Low | SO 에셋, UI 트윈 | 작동 테스트만 |

---

## 📍 현재 상태 (한눈에)

**엔드투엔드 시뮬레이션 루프가 섰다** — 골격(4매니저+순수/공식층+슬라이스1) 위에 실 게임 씬 `Assets/_Project/Scenes/Game.unity`가 신 매니저들로 돈다: 부팅→스케줄 선택(실 UI)→행동 소진→하루 전환→오토세이브→반복→30일 엔딩. 각 슬라이스 EventBus+SO 패턴, 항상 컴파일, EditMode+PlayMode 테스트 통과. **현재 EditMode 150 / PlayMode 16 그린, 컴파일 0에러.** (슬라이스별 상세 = git log.)
**+ M3 내러티브 런타임 슬라이스1(대사+선택지) 코드+씬 배선 완성** — CSV를 받아 대사 표시→선택지→효과→점프→종료까지 코루틴으로 구동(아래 표). `Game.unity`에 실제 배치됨(아래 ⚠️ 씬 구조).

| 영역 | 상태 | asmdef |
|---|---|---|
| Core 인프라 (EventBus·Log·MoneyFormat·NameValidator·Hangul·Headless) | ✅ | Core |
| 상태/세이브 스키마 (GameStateData/SO·SaveData·JsonSaveStore) | ✅ | Core |
| Data/공식 (GameBalanceSO·GameConstants·GameTimeline·DayLoop) | ✅ | Data |
| 호감도 공식 (AffinityFormula §4 1:1) | ✅ 순수 | Affinity |
| Schedule (Effects·Service·Controller) | ✅ 통합 | Schedule |
| Narrative 파서/검증/Flow (Parser·Validator·FlowCommandInterpreter·FlowCommandController) | ✅ 순수+컨트롤러 | Narrative |
| **Narrative 런타임 슬라이스1** (대사+선택지: ScriptCursor·ChoiceParser·ChoiceEffectInterpreter 순수 + NarrativeController 어댑터 + DialogueView·ChoiceView·ChoiceSlot) | ✅ **코드+테스트** | Narrative/UI |
| 매니저 GameManager(하루전환)·SaveManager(오토세이브)·AudioManager(재생)·UIManager(그룹) | ✅ 슬라이스1 | Game/Save/Audio/UI |
| HUD (Day/Money/Affinity/Stat/Status) | ✅ 슬라이스1·2 | UI |
| Shop (구매+Consumable+SessionBuff 즉시가산+중복50%페널티) | ✅ 슬라이스2 | Shop |
| 부팅 (GameBoot·GameBootstrap 컴포지션 루트) | ✅ 완성 | Game |
| **실 게임 씬 시뮬레이션 루프** (부팅+4매니저+ScheduleController+HUD+ScheduleView+EndingView) | ✅ **엔드투엔드** | `Assets/_Project/Scenes/Game.unity` |
| 스케줄 선택 UI (ScheduleView·ScheduleSlot, 슬롯 클릭→명령) | ✅ | Schedule(피처 응집) |
| 엔딩 화면 (EndingView, 30일 종료점) | ✅ 최소 | UI |
| 통합 dev 씬 (전 매니저 EventBus 협업 + HUD 시각화) | ✅ | `Assets/_Dev/Integration/IntegrationTest.unity` |

**아직 안 된 것 / 다음 우선순위 후보**:
- **✅ 엔드투엔드 시뮬레이션 루프 해소**(이번 세션): `Game.unity`가 신 매니저로 실제 플레이된다(부팅→스케줄선택→하루전환→오토세이브→30일 엔딩). 단 **내러티브(대사/선택지)는 제외** — 시뮬레이션 페이즈만. 구 `Main.unity`는 여전히 구 아키텍처로 공존(미폐기).
- **🟢 HUD/슬롯 시각 레이아웃 미조정**: 기능 배선만 됨(위치/폰트/스타일은 감독이 Play로 다듬는 영역). 엔딩 결과 디테일(최고 호감도 등)도 최소.
- **M3 내러티브 런타임 — 슬라이스1(대사+선택지) 완성 + 씬 배선 완료**: 완료-핸들 커맨드 패턴으로 엔진↔UI 디커플(ADR-007). 순수 ScriptCursor/ChoiceParser/ChoiceEffectInterpreter(EditMode 18) + NarrativeController 코루틴 어댑터 + DialogueView/ChoiceView(PlayMode 2). 선택지 `Love:`는 Affinity 카테고리(`Affinity:Point:Id:Dialogue:N`)로 위임→정본 단일화(감독 결정). `Game.unity`에 실배치(매니저 2 + Narrative UI 그룹 + dev 트리거 버튼). **남은 것**: 슬라이스2(스테이지 Char/BG/CG/SD/Overlay·FX·Sound·오토모드·인라인태그·점프페이드/스테이지합성/로그복원·선택지 조건/이력·LockScreen 계열 Flow) + **실 트리거**(이벤트→스크립트 매핑, dev 버튼 대체) + **엔진 포맷 스토리 CSV**(현재 기획 CSV만 존재).
- **Shop 슬라이스2(감독 결정 필요)**: SessionBuff 적용 경계(구 코드: 다음 스케줄 base효과 직후) / Gift 인벤토리(🔴 세이브 스키마) / 중복 50% 페널티(상태 위치).
- **GameManager 잔여 seam**: 저녁이벤트(M3)·페이드(M5 UI)·페이즈전환(GamePhase 상태머신). (오토세이브 seam은 완료.)
- **Settings**(볼륨↔AudioMixer = AudioManager 슬라이스2) / **구 모듈 폐기**(소비처 이식 완료 시 Service Locator·구 매니저 제거).

---

## ⚠️ 새 세션이 반드시 알 것 (안 깨뜨리려면)

- **전환기 공존**: 구 Assembly-CSharp 모듈 다수 잔존(ShopSystem/ShopUI·DialogueUI·ScriptRunner·구 GameManager/SaveManager/UIManager/AudioManager 등). 신규 asmdef는 `git mv`(ns/GUID 보존)+autoref로 공존. **항상 컴파일 가능** 유지가 절대 원칙 — 피처 하나 옮기고 옛 코드는 그때 삭제.
- **autoRef=false 2개**(`LoveAlgo.Save`·`LoveAlgo.UI`): 구 동명 네임스페이스/타입 충돌 회피용. 신규 코드가 이들을 참조하려면 **명시적 asmdef 참조 추가**(테스트 asmdef엔 이미 추가됨). 구 모듈 폐기 시 true 복귀 검토.
- **이름 충돌 패턴**: 신규 타입이 구 동명 타입과 겹치면 → 다른 ns(예: 구 Modules.Audio vs 신 LoveAlgo.Audio) 또는 asmdef 격리(autoRef=false, 예: UIManager). **MCP로 씬에 컴포넌트 부착 시 전체 타입명**(`LoveAlgo.Game.GameManager`)으로 모호성 주의.
- **MCP 씬 작업 팁**(이번 세션 시행착오): 신 asmdef 타입을 MCP로 부착/배선 시 **assembly-qualified 이름** 필요 — `"LoveAlgo.UI.UIManager, LoveAlgo.UI"` 형식(단순명은 "not found"). 컴포넌트 ref 배선(`set_property`)은 **GO instanceID**를 주면 해당 GO에서 컴포넌트(TMP_Text·Button 등) 자동 추출. 프리팹화는 `manage_prefabs create_from_gameobject`(target=이름). **프로젝트는 Input System 패키지** — EventSystem엔 `InputSystemUIInputModule`(구 `StandaloneInputModule`은 런타임 `Input` 예외로 전 PlayMode 오염).
- **내러티브 씬 구조(Game.unity)**: 캔버스 1개(`_UI`, Screen Space) 유지. `_UI/Narrative`=UIManager.narrativeRoot(부팅 시 **inactive** — 시뮬 클릭 차단 방지, ShowUiGroupCommand가 토글). 그 아래 `DialogueView`(전체화면 투명 Image=클릭캐처+화자/본문 TMP)·`ChoiceView`(Container=VerticalLayoutGroup, slotPrefab=`Prefabs/Narrative/ChoiceSlot.prefab`). 매니저 `_Bootstrap/NarrativeController`·`FlowCommandController`(State=GameState_Main). dev 진입 = `_UI/Simulation/DevNarrativeButton`(NarrativeDevTrigger, 인라인 데모 CSV). **PlayMode 테스트 격리 주의**: GameScene 테스트가 Game.unity를 Single 로드 후 언로드 안 해 잔존 — 신 매니저를 발행 이벤트로 검증하는 테스트는 잔존 인스턴스 제거 후 진행(NarrativeControllerPlayModeTests 참고). **⚠️ 씬 dirty 오염**: PlayMode 테스트가 `Game.unity`를 SceneManager 로드하면 씬 오브젝트 active를 실제로 토글한다(EndingRoot 켜짐·UIManager가 Narrative 켜짐). 그 상태로 dirty 저장되면 **부팅 UI 상태가 디스크에 오염**된다 — 테스트 후 `Game.unity`를 저장하기 전 **부팅 active 상태 확인 필수**(Narrative=inactive·EndingRoot=inactive·Simulation=active).
- **IVT 가교**: `Scripts/Narrative/AssemblyInfo.cs`가 `Assembly-CSharp`에 internal 노출(구 ScenarioEditor가 ScriptLine setter 접근). 해당 소비처 이식/삭제 시 제거.
- **테스트 = Test Runner 어셈블리만**(임시 dev 하니스/씬 금지): EditMode=순수/공식층, PlayMode=MonoBehaviour 라이프사이클·OnEnable 구독·씬 와이어링. 구 코드 테스트 3개(ScriptParser·ScriptValidator·SaveLoadRoundTrip)는 `Assets/Tests/Editor/`(Assembly-CSharp-Editor) 잔류.
- **State SO 바인딩**: 매니저/컨트롤러는 `State` 프로퍼티(GameStateSO)를 인스펙터/부팅으로 주입받음. 통합 dev 씬이 그 배선 예시. 런타임 초기화(공식 Configure + DayLoop.BeginRun)는 `GameBoot.NewGame`/`GameBootstrap`.

---

## ▶️ 다음 액션

이번 세션 **시뮬레이션 루프 엔드투엔드 완성**(구 #2 "부팅 씬 조립" 달성) + **아키텍처 문서 동기화**(ADR-007/011: dev_guide·CLAUDE.md의 Service Locator/Modules 잔재 제거). 감독이 다음 방향 택1:

1. **M3 슬라이스2** — 스테이지(Char/BG/CG/SD/Overlay)·FX·사운드·오토모드·인라인태그(`<emote>`/`<wait>`)·점프페이드/스테이지합성/로그복원·선택지 조건·이력·LockScreen 계열 Flow. 스테이지는 World Space 캔버스 또는 SpriteRenderer(sorting layer)로 대사 UI 뒤에(슬라이스1에서 캔버스 1개만 둔 이유). **실 트리거**: dev 버튼(NarrativeDevTrigger)을 이벤트→스크립트 매핑으로 대체 — 스토리 데이터(엔진 포맷 CSV) 필요(현재 기획 CSV만).
2. **화면 페이즈 상태머신**(🔴, 스펙=ADR-013) — Title↔Story↔Schedule↔Ending 전환을 단일 `PhaseController`로 일원화(현재 NarrativeController가 ShowUiGroupCommand 직접 토글). GamePhase enum(State SO) + 순수 PhaseService(FSM) + 의도 발행(RequestPhaseCommand). LockScreen은 Phase 아닌 Overlay(완료-핸들 복귀). 슬라이스2의 LockScreen/페이즈전환이 이 결정에 의존하므로 그 전에 구현 권장. 구현 시 확정: UIGroup↔GamePhase 매핑·씬 경계·Overlay 목록(ADR-013 末).
3. **시뮬레이션 루프 심화** — 카테고리 탭 UI 배선(현재 슬롯 동적생성만, 탭 버튼 미연결) / HUD·슬롯 시각 레이아웃 / 엔딩 결과 디테일(최고 호감도 등) / **Shop SessionBuff 복합효과 SO 데이터 보강**(코드 완성, ItemCatalog.asset에 SubEffect 부재 = 🟢 데이터). *(페이즈전환은 #2로 분리.)*
4. **Shop Gift 인벤토리(🔴 세이브 스키마)** — 선물 보관/소비. 단 소비처=내러티브 Event2/3라 M3 이후가 자연스럽다(지금은 죽은 코드).
5. **구 아키텍처 폐기 착수** — 소비처 이식 끝난 구 모듈·Service Locator 제거, `Main.unity` 신 씬으로 교체 검토.

### 워크플로우 규율 (directive)
- 무언가 만들 때마다 **작동 증거**: 순수/공식층=EditMode 테스트, MonoBehaviour 라이프사이클·구독·씬 와이어링=PlayMode 테스트. 임시 dev 하니스 금지 — Test Runner 어셈블리로. 위험도 등급 선언 + 커밋 "왜".
- **썸네일은 레이어 배제 캡처**가 필수 요구사항(옛 말썽: 안 잡혀야 할 UI 포함됨) — Save 썸네일 이식(M5) 시.

### 잔여 Common (파킹 — 소비처 이식 시점 처리)
미이식 구 Assembly-CSharp에서만 쓰임 → 지금 이동 불가. 소비처 피처 이식 때 함께 처리.
- `ListenerBag.cs`(UI 전용)→ UI 피처 이식 시 `LoveAlgo.UI`로. `SingletonMonoBehaviour.cs`(DOTween 의존)→ 구 매니저 폐기 시. `Services.cs`(폐기, 소비처 37곳)→ 마지막 구 모듈 이식 시.

### 마일스톤 (원안 — 실제론 감독이 슬라이스별 우선순위 지정)
M1 Core ✅ → M2 Data/공식 ✅ → M3 내러티브/스테이지(파서까지) → M4 기능모듈(Schedule·Shop 진행) → M5 UI/Save(매니저·HUD 진행). *순서는 엄격히 안 지켜졌고(감독 선택), broad-first로 골격을 먼저 세웠다 — 다음은 depth(플레이 루프/내러티브 런타임)로 좁히는 게 자연스럽다.*

---

*결론과 가드레일만 전달. 상세 규칙 = docs/dev_guide.md, 기능 = docs/REWRITE_FEATURE_INVENTORY.md, 슬라이스 이력 = git log. 막히면 감독에게 질문.*
