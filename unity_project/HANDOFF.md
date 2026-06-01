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
- ⚠️ 현재 asmdef 0개(Assembly-CSharp 단일) → asmdef 도입이 M1의 일부.

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

**구조 확정(ADR-011). M1 착수.**

- **M1 (🔴 Critical) — Core 인프라 + 구조 도입**:
  1. `_Project/Scripts/{Core,Data,Features,UI,DevTools}` 골격 + Core asmdef(`LoveAlgo.Core`) 생성.
  2. 의존성0 "유지" 인프라 이식: EventBus·Log·MoneyFormat·NameValidator·Hangul·ListenerBag·Singleton·Headless → `Scripts/Core`. **네임스페이스 보존**(공존 컴파일).
  3. `GameStateSO`(런타임 상태) + `SaveData` 스키마(`REWRITE_FEATURE_INVENTORY.md §7`).
  4. `recompile_scripts`로 0에러 확인 → atomic 커밋.
  - 열린 질문: State SO 단일(`GameStateSO`) vs 도메인별 분할 — 감독 결정(미정 시 단일로 진행).
- **마이그레이션 전략(컴파일 유지)**: 새 asmdef 코드는 기존 Assembly-CSharp과 공존(예: Assembly-CSharp이 새 asmdef 자동 참조). 피처를 하나씩 새 구조로 옮기고 **그 피처 옛 코드는 그때 삭제**(매니페스트 상태 갱신). 항상 컴파일 가능.
- **마일스톤(잠정)**: M1 Core 인프라 → M2 Data(SO 정의)+호감도/스탯/데이루프 공식 → M3 내러티브/스테이지 → M4 기능모듈 → M5 UI/Save.

---

*결론과 가드레일만 전달. 상세 규칙 = docs/dev_guide.md, 기능 = docs/REWRITE_FEATURE_INVENTORY.md. 막히면 감독에게 질문.*
