# 📋 결정 이력 (Decisions Log)

> 큰 구조적 결정을 "왜"와 함께 기록합니다. AI와 감독 모두 나중에 이 이유를 되짚어볼 수 있도록 돕습니다.
> 새로운 결정은 리스트 상단에 추가합니다 (최신순 정렬).

---

## ADR-006: UI 직접 접근 래퍼의 Deprecate 및 Service Locator 사용 일원화 (2026-05-31)
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
- **맥락**: UI가 많아지고 복잡해짐에 따라 각각의 컴포넌트의 라이프사이클(모달 차단 여부, 자동 소멸 여부 등)을 혼동하여 오작동이나 씬 내에서의 중복 렌더링 오버헤드가 발생함.
- **결정**: 
  - UI 컴포넌트명을 용도에 맞춰 `*Popup`(모달 다이얼로그, `PopupBase` 상속 필수), `*Panel`(진입/게이트 화면), `*Notification`(자동소멸 알림), `*UI`(인게임 모드 컨테이너), `*Widget`(UI 하위 구성요소) 등으로 네이밍 접미사 표준화.
  - 씬 내 Canvas 분리 적용: `_UI`(인게임 메인)와 `_Popup`(모달 팝업 및 알림) Canvas를 엄격히 분리하여 팝업이 다시 켜지고 꺼질 때 메인 UI의 Canvas Rebuild가 트리거되지 않도록 최적화.
  - 하이어라키를 `_Bootstrap`, `_Modules`, `_Stage`, `_Popup`, `_UI` 구조로 통일.
- **이유**: 네이밍 규칙만으로 UI의 성격을 보장하고 Unity의 Canvas Rebuild 성능 저하를 방지하기 위함.

## ADR-003: 매니저 싱글톤 최소화 및 4대 매니저 제한 (2026-05-31)
- **맥락**: 모듈별로 `{Feature}Manager`를 우후죽순 신설하여 싱글톤으로 띄울 경우, 싱글톤 간 복잡한 상호 참조가 꼬여 씬 로드/언로드 시 크래시나 라이프사이클 엉킴 현상이 빈번히 발생함.
- **결정**: 
  - 프로젝트 내 허용되는 글로벌 싱글톤 매니저는 오직 `GameManager`(씬 전환, 전역상태), `AudioManager`(사운드), `SaveManager`(세이브), `UIManager`(UI 인프라) 4개만 허용.
  - 모듈별 상태 및 로직 관리가 필요할 시 `{Feature}Manager` 대신 `{Feature}Module` 형태로 개발하여 Service Locator(`Services.cs`)에 등록하거나 ScriptableObject 레지스트리를 활용하도록 구조를 고정.
- **이유**: God Object 패턴의 증식을 차단하여 시스템의 결합도를 낮추고 안정적인 라이프사이클을 보장하기 위함.

## ADR-002: 모듈 간 통신 아키텍처 (Service Locator + EventBus 조합) (2026-05-31)
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
