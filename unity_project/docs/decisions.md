# 📋 결정 이력 (Decisions Log)

> 큰 구조적 결정을 "왜"와 함께 기록합니다. AI와 감독 모두 나중에 이 이유를 되짚어볼 수 있도록 돕습니다.
> 새로운 결정은 리스트 상단에 추가합니다 (최신순 정렬).

---

## ADR-012: 재설계 원칙(전사 금지) + 세션 연속성 규율 (2026-06-01)
- **맥락**: 재작성은 기존 클래스를 그대로 베끼면 같은 문제가 재발. 또 다세션에 걸쳐 진행되므로 컨텍스트가 끊겨도 일관돼야 함.
- **결정**:
  - **재설계(전사 아님)**: 클래스 매니페스트(`REWRITE_CLASS_MANIFEST.csv`)는 "재현할 기능"의 참조일 뿐, 1:1 복제 금지. 구조·기능을 파악해 더 단순·자동화된 형태로. "유지"=동작 보존이지 파일 그대로가 아님.
  - **연출 수치 동결**: 하드코딩된 페이드/타이핑/FX 수치는 `REWRITE_TUNING_VALUES.csv`에 기록 후 SO(Definition)로 분리. 코드 매직넘버 금지.
  - **세션 규율**: 단일 진실=docs. HANDOFF는 델타 갱신(통째 재작성 금지), 큰 결정=ADR, 커밋 메시지에 "왜", 한 작업=한 커밋(atomic), 형태 문서 금지(코드가 진실). 작업 종료 시 HANDOFF "다음 액션" 갱신.
- **이유**: 문서를 잘 다뤄야 새 세션이 매끄럽게 이어받음(감독 지시). 재설계로 누적 결함 차단.

## ADR-011: 폴더 구조 — 코드 Scripts/ 집중(피처별 asmdef) + 아트/프리팹 타입별 중앙화 (2026-06-01)
- **맥락**: 기존은 `_Project/Modules/<X>/`에 코드+UI+Data+Prefabs 공동배치. 재작성에서 코드(휘발)와 자산(보존)을 가르는 게 안전·명료.
- **결정**:
  - **코드 = `_Project/Scripts/` 하위, 피처별 군집**: `Scripts/Core`(의존성0) · `Scripts/Data`(→Core) · `Scripts/Features/<X>`(→Core,Data) · `Scripts/UI`(→Core,Data) · `Scripts/DevTools`(Editor). 완전 타입별 분류는 지양(피처별 asmdef 경계 유지).
  - **피처별 asmdef** → feature간 직접참조는 컴파일 에러로 자동 차단. 교차통신은 Core의 EventBus + State SO 경유. (현재 asmdef 0개 → 도입이 재작성의 일부.)
  - **아트/오디오/프리팹 = 타입별 중앙화**, 코드 트리 밖: `_Project/Art` · `Audio` · `Prefabs/<X>`. GUID 폴더무관이라 안전. 단 우리 MonoBehaviour 단 프리팹은 재작성 후 재바인딩 필요 → 제자리 재작성 시 `.cs.meta` GUID 보존으로 최소화.
  - SO `.asset` 인스턴스는 `Resources/Data` 유지(Resources.Load 경로 보존).
- **이유**: 삭제할 코드와 보존 자산의 깨끗한 경계 + asmdef로 경계 자동 강제(감독 직관 채택). 코드만 Scripts/ 집중은 Mortise(`Scripts/Core`·`Scripts/<feature>`)와 정합.

## ADR-010: 협업 운영 규율 도입 (Mortise 문서 차용) (2026-06-01)
- **맥락**: 코드베이스 전체 재작성은 긴 다세션 작업. 표류·리뷰 병목 방지 장치 필요.
- **결정**: 타 프로젝트(Mortise) 운영 문서에서 비판적으로 차용 —
  - `HANDOFF.md`(세션 진입점: 직전 결론·금지선·다음 액션) 도입.
  - **위험도 4단계 게이트**(🔴Critical/🟠High/🟡Medium/🟢Low) — 작업 착수 시 등급 먼저 선언.
  - 마일스톤 분할(M1/M2…, 각 끝에 작동 증거+컨펌), 디렉토리 README, 형태 문서 금지(코드가 진실), 커밋 메시지에 "왜".
- **비채택**: Mortise의 EventBus-전용(아래 ADR-007에서 별도 판단), eval-first "한 방 슬라이스"(LoveAlgo는 검증된 설계), 게임 픽션.
- **이유**: 베테랑 감독의 리뷰를 위험도로 차등해 병목 제거 + 누적 표류 구조적 차단.

## ADR-009: 내러티브 엔진 — Ink 비채택, 자체 CSV 스토리 엔진 재작성 (2026-06-01)
- **맥락**: 재작성에 Ink(inkle) 도입 검토.
- **결정**: **Ink 비채택.** 기존 CSV 명령 체계(REWRITE_FEATURE_INVENTORY.md §2)를 EventBus 명령 기반으로 재구현.
- **이유**: ①연출·모듈호출 글루 비용이 Ink로도 동일(무대/오디오/FX/모듈호출은 결국 태그+디스패치) ②분기 로직이 C#/호감도 공식 기반이라 Ink 분기 강점 저활용 ③Ink 자체 상태 ↔ 호감도/세이브 이중화 회피(과설계 게이트).
- **뒤집힐 조건**: 비개발자 작가 합류 또는 분기 복잡도가 스크립트로 크게 이동 시 재검토.

## ADR-008: 코드베이스 전체 재작성 (아트/프리팹 유지) (2026-06-01)
- **맥락**: 누적 문제 + 아키텍처 전환(ADR-007). 점진 리팩토링보다 깨끗한 재출발 선택.
- **결정**: `Assets/_Project`·`Scripts`의 C#를 처음부터 재작성. **아트·프리팹·씬·SO 에셋 GUID는 보존**(자산 가치). 기능은 `REWRITE_FEATURE_INVENTORY.md` 기준 재현(특히 §4 호감도 공식·수치 그대로). main 미커밋 WIP는 `wip/pre-rewrite-snapshot`에 보존. 작업 브랜치 `rewrite/eventbus-so`.
- **이유**: EventBus+SO 전환은 통신 골격을 전부 바꾸므로 점진 이주보다 재작성이 빠르고 깨끗.

## ADR-007: 아키텍처 패턴 전환 — EventBus + ScriptableObject 단일 (2026-06-01)
- **맥락**: 기존 ADR-002는 Service Locator + EventBus 조합. 재작성 기회에 더 단순한 단일 패턴으로 전환(감독 결정).
- **결정**: 모듈 통신 = **EventBus**(일방 통지·명령) + **State SO 직접 읽기**(동기 GET). `Services`·인터페이스 계약(`I*`) 전면 폐기. 매니저 4개(GameManager/AudioManager/SaveManager/UIManager)만. 동기 결과가 필요한 소수(미니게임 점수, 인라인 Schedule/Username/LockScreen)는 **완료 핸들(UniTaskCompletionSource) 실은 이벤트**로.
- **데이터**: Definition SO(불변, 런타임 읽기 전용) + State SO(런타임 상태 컨테이너, 부팅 리셋·세이브 직렬화). dev_guide §4-1a Definition/Instance 분리 준수.
- **이유**: 17모듈 인터페이스 그물 제거, 1인 유지보수 단순화. 동기 요청-응답은 State SO 읽기 + 완료-이벤트로 충분.
- **supersede**: ADR-002(Service Locator+EventBus 조합), ADR-006(Services 일원화). ADR-003(매니저 4개)은 유지하되 "Module을 Services에 등록" 항목만 무효.

## ADR-006: UI 직접 접근 래퍼의 Deprecate 및 Service Locator 사용 일원화 (2026-05-31)
> ⚠️ **ADR-007로 폐기**: `Services` 일원화 자체가 폐기됨. UI는 State SO 직접 읽기 + EventBus 명령 발행으로 접근.
- **맥락**: `UIManager.Instance.DialogueUI`와 같은 방식은 UIManager 클래스가 프로젝트의 거의 모든 모듈 인터페이스를 참조 및 보관하게 하여 강한 결합을 발생시키고 모듈 분리를 방해함.
- **결정**: 
  - `UIManager` 내부의 직접적인 UI 래퍼 프로퍼티 접근을 소프트 Deprecate 처리.
  - 신규 코드는 `Services.TryGet<I*>()` 또는 `Services.Get<I*>()`을 이용해 독립적인 인터페이스에 직접 질의하여 UI 인스턴스를 가져오도록 강제.
  - 예: `LoveAlgo.Common.Services.TryGet<INarrative>()?.DialogueUI`
- **이유**: UI 시스템의 의존성 그물을 차단하여 모듈의 자급자족 및 유연성을 높이기 위함.

## ADR-005: 전역 로깅 규칙 (`LoveAlgo.Common.Log` 사용 강제) (2026-05-31)
- **맥락**: Unity 표준 `Debug.Log` / `Debug.LogWarning`은 릴리즈 빌드에서도 호출 자체가 남아 문자열 보간 및 I/O 호출이 이루어지며 성능 핫패스(Update, 코루틴) 내에서 대량의 가비지와 오버헤드를 유발함.
- **결정**: 
  - 신규 생성하는 모든 개발용 로그는 `LoveAlgo.Common.Log` 헬퍼 클래스를 우선 사용하도록 의무화.
  - `Log.Info(...)` 및 `Log.Warn(...)`은 `[Conditional]` 어트리뷰트가 적용되어 있어, Editor/Development Build가 아닌 릴리즈 빌드 시 컴파일러 단에서 호출 코드 자체가 제거됨 (가비지 및 실행 비용 0).
  - 정말 사용자나 QA에게 보여야 하는 핵심 시스템 에러/예외만 `Log.Error(...)` 또는 `Debug.LogError(...)`를 활용.
- **이유**: 빌드 성능을 최적화하고 릴리즈 시 불필요한 로그 노이즈를 완전 차단하기 위함.

## ADR-004: UI 분류 매트릭스 및 씬 하이어라키 표준화 (2026-05-31)
> ⚠️ **ADR-007/011로 부분 갱신**: 씬 `_Modules` 노드(글로벌 IService 등록 오브젝트)·구 모듈 폴더 구조는 폐기. UI 명명 매트릭스(`*Popup`/`*UI`/`*Panel`…)와 `_UI`/`_Popup` Canvas 분리 정책은 유효. 최신 하이어라키 = dev_guide §3-5.
- **맥락**: UI가 많아지고 복잡해짐에 따라 각각의 컴포넌트의 라이프사이클(모달 차단 여부, 자동 소멸 여부 등)을 혼동하여 오작동이나 씬 내에서의 중복 렌더링 오버헤드가 발생함.
- **결정**: 
  - UI 컴포넌트명을 용도에 맞춰 `*Popup`(모달 다이얼로그, `PopupBase` 상속 필수), `*Panel`(진입/게이트 화면), `*Notification`(자동소멸 알림), `*UI`(인게임 모드 컨테이너), `*Widget`(UI 하위 구성요소) 등으로 네이밍 접미사 표준화.
  - 씬 내 Canvas 분리 적용: `_UI`(인게임 메인)와 `_Popup`(모달 팝업 및 알림) Canvas를 엄격히 분리하여 팝업이 다시 켜지고 꺼질 때 메인 UI의 Canvas Rebuild가 트리거되지 않도록 최적화.
  - 하이어라키를 `_Bootstrap`, `_Modules`, `_Stage`, `_Popup`, `_UI` 구조로 통일.
- **이유**: 네이밍 규칙만으로 UI의 성격을 보장하고 Unity의 Canvas Rebuild 성능 저하를 방지하기 위함.

## ADR-003: 매니저 싱글톤 최소화 및 4대 매니저 제한 (2026-05-31)
> ⚠️ **ADR-007로 부분 무효**: 매니저 4개 제한은 유지. 단 "`{Feature}Module`을 `Services`에 등록" 항목은 폐기(Service Locator 제거) — 도메인 로직은 순수 static + 얇은 어댑터로.
- **맥락**: 모듈별로 `{Feature}Manager`를 우후죽순 신설하여 싱글톤으로 띄울 경우, 싱글톤 간 복잡한 상호 참조가 꼬여 씬 로드/언로드 시 크래시나 라이프사이클 엉킴 현상이 빈번히 발생함.
- **결정**: 
  - 프로젝트 내 허용되는 글로벌 싱글톤 매니저는 오직 `GameManager`(씬 전환, 전역상태), `AudioManager`(사운드), `SaveManager`(세이브), `UIManager`(UI 인프라) 4개만 허용.
  - 모듈별 상태 및 로직 관리가 필요할 시 `{Feature}Manager` 대신 `{Feature}Module` 형태로 개발하여 Service Locator(`Services.cs`)에 등록하거나 ScriptableObject 레지스트리를 활용하도록 구조를 고정.
- **이유**: God Object 패턴의 증식을 차단하여 시스템의 결합도를 낮추고 안정적인 라이프사이클을 보장하기 위함.

## ADR-002: 모듈 간 통신 아키텍처 (Service Locator + EventBus 조합) (2026-05-31)
> ⚠️ **ADR-007로 폐기**: Service Locator(`Services`)+EventBus 조합 → EventBus + State SO 단일로 전환. 동기 결과는 완료-이벤트로.
- **맥락**: 여러 모듈(Stats, Affinity, Narrative, Save 등)이 서로의 클래스를 직접 참조하거나 인스펙터 상에서 씬 결합을 할 경우, 기능 수정 시 파급 효과가 너무 크고 이관 작업이 극도로 복잡해짐.
- **결정**: 
  - 모든 모듈의 결합은 직접 호출을 절대 금지하고 `Service Locator` 인터페이스 조회 또는 `EventBus` 발행-구독 패턴으로만 구현.
  - **요청-응답 (동기 결과 필요 시)**: `Services.Register<I*>()`로 등록 후 `Services.Get<I*>().Method()`로 요청.
  - **이벤트 전파 (느슨한 통지)**: `EventBus.Publish(new StructEvent())`로 발행하고 관심 있는 타 모듈이 `EventBus.Subscribe<StructEvent>()`로 구독.
- **이유**: 컴포넌트 단위의 자급자족을 가능하게 하여 한 폴더 안에서 완결되는 기능 모듈을 제작할 수 있도록 하기 위함.

## ADR-001: LoveAlgorithm 기술 스택 및 대상 플랫폼 확정 (2026-05-31)
- **맥락**: 솔로 개발 진행 시 무리한 플랫폼 확장이나 최신 기술의 무분별한 도입은 개발 기간 연장 및 스코프 과잉의 주범이 됨.
- **결정**: 
  - 엔진: Unity 6 LTS (6000.4.9f1) / URP 2D Renderer / C#
  - 환경: Windows (주 개발 환경), JetBrains Rider IDE
  - 대상 플랫폼: PC (Steam) & Mobile (Android 1차 타겟, iOS 추후 대응)
- **이유**: 개발 생산성을 극대화하고 개발 기간을 6개월 이내로 유지하여 실제 출시 확률을 높이기 위함.

---

## 🚧 미결정 사항 (Open Questions)

1. **상점 및 인벤토리 마이그레이션**:
   - `Inventory` 시스템을 `Shop` 모듈 하위의 구조적 데이터(Definition SO vs Instance 런타임 클래스)로 어떻게 유기적으로 연동하고 영속화할 것인지 세부 설계 필요.
2. **이벤트 흐름 컨트롤러 이주**:
   - 기존의 `DayLoopController`를 `IDayLoop`로 이주 및 마이그레이션하면서 생길 수 있는 UI 생명주기 및 씬 동기화 엣지 케이스 확인 대기.
