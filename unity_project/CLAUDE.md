# CLAUDE.md — LoveAlgorithm 작업 원칙

> 최상위 규칙 문서. Claude Code가 이 세션에서 작업할 때 항상 최우선으로 준수하는 규칙입니다.
> (토큰 절약을 위해 180줄 이하 유지)

---

## 🚪 세션 진입 및 라우팅 (SSOT)

새 세션 시작 시 Claude는 반드시 다음 문서들을 이 순서로 읽어 흐름을 동기화합니다:
1. [HANDOFF.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/HANDOFF.md) (프로젝트 루트) : 직전 진행 사항, 금지선, 즉시 수행할 다음 액션 요약
2. [docs/_index.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/_index.md) (문서 폴더) : 정본 문서 인덱스 및 세부 작업별 라우팅 가이드
*모든 소스 코드 및 문서 내에서는 버전 번호가 없는 파일명(`dev_guide.md` 등)으로만 파일을 참조합니다.*

---

## 🔴 위험도 기반 4단계 리뷰 게이트

Claude는 코드를 제안하거나 구현할 때 **작업 위험도 등급**을 선언하고 그에 상응하는 증거를 동봉합니다.

| 등급 | 대상 영역 | 리뷰 및 작동 증거 |
|---|---|---|
| 🔴 Critical | 핵심 골격 (EventBus, Services Locator, 세이브 스키마) | 감독 정독 필수, 에디터 및 컴파일 동작 검증 증거 제시 |
| 🟠 High | 모듈 간 인터페이스 변경, 세이브 데이터 포맷 수정, 입력 시스템 | Git Diff 검토 필요, 엣지 케이스 자가 검증 동봉 |
| 🟡 Medium | 모듈 내부 세부 로직, 신규 기능 클래스 구현 | 동작 결과 로그 또는 에디터 테스트 방법 명시 |
| 🟢 Low | SO 데이터 에셋 추가, 단순 UI 바인딩/애니메이션 수치 튜닝 | 에디터 동작 확인 요약으로 코드 리뷰 대체 가능 |

---

## 🎯 게임 정체성 & 아키텍처 규칙

### 1. 피처 독립성 (피처별 asmdef)
- 코드 = `Assets/_Project/Scripts/{Feature}/` 피처별 군집 + asmdef. 아트/프리팹/SO는 코드 트리 밖 타입별 중앙화(`_Project/Prefabs`, `Art`, `Resources/Data`). 구 `_Project/Modules/{ModuleName}/{Code,Data,UI,Prefabs}` 자급자족 구조는 폐기 (ADR-011).
- **피처 간 직접 참조 절대 금지**: 피처별 asmdef가 컴파일 단에서 차단. 교차통신은 Core의 EventBus + State SO만 경유 (ADR-007).
- **동기 상태 조회**: State SO 직접 읽기(`gameState.Day` 등). `Services`(Service Locator)·인터페이스 계약(`I*`)은 폐기 — 부활 금지. 동기 결과가 꼭 필요한 소수만 완료-이벤트(완료 핸들 실은 이벤트).
- **비동기 상태 전파**: C# 구조체 이벤트를 정의해 `EventBus.Publish(new StructEvent())`로 느슨한 연동.

### 2. Obsolete API 금지
- Unity 6 LTS 기준 obsolete 마크된 API는 절대 새로 쓰지 않음 (예: `FindObjectOfType` -> `FindAnyObjectByType`, `enableWordWrapping` -> `textWrappingMode`). strikethrough 발견 시 즉시 대체 검색 후 적용.

### 3. 로깅 규칙 (`LoveAlgo.Common.Log` 사용)
- 일반 디버그용 로그는 `Log.Info(...)` / `Log.Warn(...)`을 디폴트로 사용 (릴리즈 컴파일 시 호출 자동 제거).
- 진짜 사용자 보고용 에러/예외만 `Log.Error(...)` 또는 `Debug.LogError(...)` 사용.

### 4. UI 접근 규칙
- `UIManager.Instance.DialogueUI` 등 직접 참조 wrapper 프로퍼티 사용 금지.
- UI는 도메인 상태를 State SO에서 읽고, 동작은 EventBus 명령으로 발행한다. `Services.TryGet<I*>()` 등 서비스 조회도 폐기(ADR-007) — 쓰지 않음.

---

## 🧭 선택지 제시 규칙
- 감독에게 둘 이상의 선택지를 제시할 때는 **항상 가장 합리적인 안을 첫 번째에 두고 "(추천)" 표시**를 붙인다. 추천 근거 한 줄 동봉. 그냥 "어떻게 할까요"로 판단을 떠넘기지 말 것.

---

## 📝 커밋 및 변경
- 한 기능 = 한 커밋 (Atomic Commit) 준수. 자동 커밋 금지, 감독 승인 시 커밋.
- 커밋 메시지 본문에 **"왜(Why)"** 이 변경을 적용했는지 이유 명시.
- 에러 발생 및 병목 시 억지로 수정하지 말고 직전 좋은 지점으로 `git restore` 후 재시도.
