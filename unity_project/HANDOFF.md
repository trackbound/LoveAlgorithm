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
- asmdef 도입 진행: 현재 `LoveAlgo.Core`·`LoveAlgo.Affinity` 2개(둘 다 autoReferenced) + 옛 Assembly-CSharp 공존.

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
- ✅ **M2 slice1 커밋대기 (호감도 엔진)**: ① Core `GameStateSO`에 스탯 API `GetStat/SetStat/AddStat`(0~100 클램프, "Int"→intel 매핑) 추가. ② `GameStateData`에 포인트추적 상태(`heroinePoints`/`eventChoices`) 직렬화 필드 추가 — 구 `HeroinePointTracker` static dict 대체. ③ 신규 asmdef `LoveAlgo.Affinity` + `AffinityFormula`(순수 함수): 총점·스탯보너스(+3/+1/0)·로아 피로(+3/+6/+10)·엔딩 판정(로아 히든 우선→마진 최대)·Event3 +2 재선택 = 인벤토리 §4 1:1 재현. 임계치/선호스탯은 검증된 폴백 상수표 내장(2차에서 GameBalanceSO로 대체 예정).
  - **작동 증거**: `AffinityFormulaTests` 신설(§4·§5 전 케이스) — 전체 EditMode **59/59 통과**(M1 27 + M2 32), 0에러 컴파일.
  - **구 코드 무변경**: 구 `AffinityCalculator`/`HeroinePointTracker`(LoveAlgo.Modules.Affinity)는 옛 모듈이 아직 사용 → 그대로 공존, 매니페스트 상태 미변경.
- ▶️ **다음 착수**: M2 slice2 — `GameBalanceSO.cs`를 새 `Scripts/Data` asmdef로 이식(.meta GUID 보존, DayType/StoryArc enum 의존 정리, `GameBalance.asset` 재바인딩 검증) → Definition 소스로 `AffinityFormula` 상수표 대체. 이후 스탯/데이루프 모듈.

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
