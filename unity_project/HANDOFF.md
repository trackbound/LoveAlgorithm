# 🔑 HANDOFF — 다음 세션 진입점 (LoveAlgorithm)

> 이 문서를 읽는 Claude에게: 당신은 이전 설계 및 구현 결과를 이어받는 새 세션의 AI 수석 아키텍트입니다.
> 이전 대화 기록을 재현할 필요 없이, 아래 **진척 상황, 금지선, 그리고 즉시 수행할 다음 액션**을 숙지하고 작업을 시작하십시오.
> 감독(사용자)은 CS 전공의 베테랑 개발자입니다. 동작 증거 제시와 설계 근거 중심의 대화가 요구됩니다.

---

## ⚡ 30초 요약
- **프로젝트**: LoveAlgorithm (가칭) — Unity 6 + URP 2D + C#, 비주얼노벨 + 스탯 시뮬레이션 게임.
- **아키텍처**: 모듈 결합을 최소화하기 위해 **Service Locator (`Services.cs`) + EventBus** 패턴을 조합하여 개발 진행 중.
- **현재 상황**: 핵심 모듈(Stats, Affinity, DayLoop, Audio)과 PC잠금화면(`LockScreen`) 로직 본구현 완료. AI 협업 파이프라인 정착 및 2차 정리(NAMING.md 병합, 레거시 삭제, MCP 연동 경로 수정 등) 완료.
- **다음 타겟**: `LockScreen` 씬 와이어링/에셋 생성 지원 및 `DayLoopController` 이주 마스터, 이후 `Item/Inventory` 시스템 설계 돌입.

---

## 🚫 절대 금지선 (가드레일)
1. **과설계 및 무거운 종속성 금지**: VContainer 등 외부 DI 컨테이너나 ECS/DOTS 계층화 아키텍처 제안 금지 (1인 개발 스코프 제약).
2. **모듈 직접 참조 금지**: 모듈끼리 서로 `using`하여 직접 결합을 만들지 마십시오. 요청-응답은 `Services`, 상태 전파는 `EventBus`를 이용합니다.
3. **SO 상태 보존 금지 (SO 최대 함정)**: ScriptableObject 에셋에 런타임 상태값을 직접 저장하지 마십시오. 불변 정보는 SO(Definition), 가변 상태는 일반 C# 직렬화 클래스(Instance)로 완전히 분리해야 합니다.
4. **UI 내 비즈니스 로직 금지**: UI 클래스(`*UI`, `*Popup`, `*Panel`)는 표시와 인터랙션만 담당해야 합니다. 연산이나 상태 변경은 IService 인터페이스로 위임하십시오.

---

## ✅ 확정된 진척 사항 (Done)
- **모듈 인프라**: [EventBus.cs](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/Assets/_Project/Core/Common/EventBus.cs), [Services.cs](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/Assets/_Project/Core/Common/Services.cs)
- **Stats 모듈**: `IStats` 구현, 스탯 변경 시 `StatChangedEvent` 발행 및 세이브 연동 완료.
- **Affinity 모듈**: `IAffinity` 구현, 호감도 임계치 계산기 및 CSV `Affinity:` 연동 완성.
- **DayLoop 모듈**: `IDayLoop`로 날짜별 페이즈(EventPhase) 매핑 및 쿼리 구현 완료.
- **Audio 모듈**: `IAudio`로 BGM/SFX 호출 추상화 및 완전 이관 성공.
- **LockScreen 모듈**: `LockScreenController` (비번 해싱, 33 ToDo 랜덤 위젯, 오류 카운터, 씬 페이드 등) 코드 구현 완료.

---

## ▶️ 다음 액션 (Immediate Actions)

1. **LockScreen 씬 및 에셋 작업 지원**:
   - `docs/HANDOFF_NOTES.md`에 기술된 씬 GameObject 구조(`LockScreenModule` 구성) 및 `LockScreenPanel.prefab` 구성에 맞춰 에디터 세팅 가이드를 제시하거나 33 ToDo ScriptableObject 에셋 생성 작업을 지원합니다.
2. **DayLoopController 이주**:
   - 기존의 `DayLoopController`가 구버전 스키마에서 `IDayLoop` 인터페이스 기반으로 날짜(하루 낮/밤 2회) 및 이벤트일 분기 처리하도록 마이그레이션 작업을 착수합니다.
3. **아이템 / 인벤토리 시스템 설계**:
   - `ItemDataSO`(분류/효과/가격) 정의 및 플레이어가 상점 팝업에서 구매한 뒤 세션 내 보유/사용 시 효과가 적용되는 인벤토리 데이터 구조를 설계합니다 (Definition/Instance 분리 및 세이브 연동 필수).

---

*작업 시작 시 이 HANDOFF.md와 [docs/_index.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/_index.md)를 먼저 로드하여 버전 및 라우팅을 파악하십시오.*
