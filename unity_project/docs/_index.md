# 📚 LoveAlgorithm 문서 인덱스 (단일 진실 소스)

> 이 파일은 `unity_project/docs/` 폴더 내 문서들의 메타데이터 단일 소스(SSOT)입니다.
> 모든 문서의 버전 정보 및 최종 업데이트는 여기서만 추적됩니다.
> AI와 에이전트, 스킬, 세션 내에서는 항상 **버전 없는 파일명**(`dev_guide.md` 등)으로 참조합니다.

---

## 🚪 세션 진입점 (docs/ 바깥, 프로젝트 루트)

새로운 개발 세션을 시작할 때 Claude는 반드시 **이 순서로** 파일을 읽고 진입해야 합니다:
1. [HANDOFF.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/HANDOFF.md) (루트) ← 직전 결론, 주의 사항, 금지선, 다음 액션. **(가장 먼저 읽을 것)**
2. [CLAUDE.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/CLAUDE.md) (루트) ← 컨텍스트/규칙 요약 및 위험도 게이트 정의 (Claude Code 자동 로드)
3. 아래 활성 문서 중 현재 작업 종류에 맞는 것

---

## 🎯 활성 문서 (Active)

이 파일들이 현재의 정본이며, 항상 가장 최신 상태를 반영합니다.

| 파일명 | 역할 | 현재 버전 | 마지막 갱신 |
|---|---|---|---|
| [dev_guide.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/dev_guide.md) | 개발 룰북, UI 네이밍, 하이어라키 및 C# 아키텍처 가이드 | v1.2 | 2026-06-08 |
| [vn_conventions.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/vn_conventions.md) | VN 표준 정본 레퍼런스 — 재작성 시 베낄 대상(렌더타깃 분류축·네이밍·CSV vs C# 경계·안티패턴·모범 예시) | v1 | 2026-06-03 |
| [decisions.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/decisions.md) | 결정 이력 (누적 ADR) | v1 | 2026-06-02 |
| [WORK_PLAN.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/WORK_PLAN.md) | 동적 작업 백로그 및 우선순위 리스트 (⚠️구 아키텍처 기준 stale) | v1 | 2026-06-02 |
| [STORY_CSV_GUIDE.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/STORY_CSV_GUIDE.md) | 기획자용 스토리 CSV 연출 및 대사 엔진 구문 가이드 | v1 | 2026-05-31 |
| [STORY_COMMANDS.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/STORY_COMMANDS.md) | 대사 엔진 내 사용 가능한 커맨드 목록 | v1 | 2026-06-08 |
| [ASSET_NAMING.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/ASSET_NAMING.md) | 아트 및 오디오 에셋 네이밍 룰북 (UI 버튼 §10 포함) | v1 | 2026-06-08 |
| [ASSET_REQUESTS.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/ASSET_REQUESTS.md) | 리소스 요청 리스트 (아트/사운드) | v1 | 2026-05-31 |
| [HANDOFF_NOTES.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/HANDOFF_NOTES.md) | 씬 바인딩 및 인스펙터 세팅 가이드 (임시 유지) | v1 | 2026-06-02 |

---

## 📦 누적 문서 (Cumulative)

버전 개념이 없으며 시간의 흐름에 따라 누적 및 갱신되는 문서입니다.

| 파일명 | 역할 |
|---|---|
| [decisions.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/decisions.md) | 아키텍처 결정 로그 (ADR) |

---

## 🗄️ 아카이브 (Archive)

이전 버전 및 완료된 과거 내역 보존 폴더 `docs/_archive/` 하위 항목입니다.

| 파일/디렉토리 | 내용 |
|---|---|
| [_archive/migrations/](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/_archive/migrations) | 완료된 과거 데이터/에셋 명칭 마이그레이션 계획 및 이력 로그 |

---

## 📝 문서 갱신 프로토콜

### 작은 갱신 (섹션 내 세부 사항 수정, 단순 백로그 체크 등)
1. 관련 문서를 직접 수정합니다.
2. `_index.md` 내 해당 문서의 "마지막 갱신" 날짜를 오늘로 변경합니다.
3. Git 커밋 메시지: `docs(feature): {간단 설명}`

### 큰 갱신 (아키텍처의 대대적인 구조 변경, 메이저 버전 업 등)
1. 기존 문서를 `docs/_archive/` 하위로 이동합니다. (파일명 예: `docs/_archive/dev_guide_v1_20260531.md`)
2. 새 버전 문서를 작성하여 갱신합니다.
3. `_index.md`에서 해당 문서의 "현재 버전" 번호를 올리고 "마지막 갱신" 날짜를 오늘로 변경합니다.
4. Git 커밋 메시지: `docs(feature): v{이전} -> v{신규} 메이저 갱신`

---

## 🤖 Claude/에이전트 참조 규칙

**절대 규칙**: 모든 소스 코드, 아티팩트, CLAUDE.md 및 룰셋에서는 문서를 참조할 때 **버전 번호가 없는 파일명**(`dev_guide.md` 등)으로만 참조해야 합니다.
- **이유**: 파일명에 버전 번호를 직접 하드코딩할 경우 메이저 업데이트 시 연관된 모든 스키마 및 링크를 수십 군데 넘게 수동 갱신해야 하므로 파편화와 링크 깨짐의 원인이 됩니다.
- 현재 사용 중인 정확한 문서를 찾거나 버전을 조회하고자 할 때는 언제나 이 `_index.md`를 로드하여 파악합니다.

---

## 🔍 작업별 라우팅 가이드 (Claude용)

| 작업 영역 | 1차 참조 문서 | 2차 참조 문서 |
|---|---|---|
| **재작성 슬라이스 착수 (신규 코드)** | [vn_conventions.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/vn_conventions.md) | [STORY_COMMANDS.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/STORY_COMMANDS.md) |
| **C# 코딩 / 리팩토링** | [dev_guide.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/dev_guide.md) | [vn_conventions.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/vn_conventions.md) · [decisions.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/decisions.md) |
| **새로운 기능 추가 / 백로그 확인** | [WORK_PLAN.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/WORK_PLAN.md) | [decisions.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/decisions.md) |
| **UI 명명 및 계층 설계** | [dev_guide.md §3-4/3-5](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/dev_guide.md#L148) | [HANDOFF_NOTES.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/HANDOFF_NOTES.md) |
| **스토리 작성 및 연출 스크립팅** | [STORY_CSV_GUIDE.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/STORY_CSV_GUIDE.md) | [STORY_COMMANDS.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/STORY_COMMANDS.md) |
| **아트 및 사운드 에셋 제작 및 네이밍** | [ASSET_NAMING.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/ASSET_NAMING.md) | [ASSET_REQUESTS.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/ASSET_REQUESTS.md) |
| **의사결정 이력 확인** | [decisions.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/docs/decisions.md) | - |
