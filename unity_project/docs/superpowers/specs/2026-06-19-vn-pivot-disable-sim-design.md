# 시뮬레이션 → 순수 선형 VN 전환 설계 (2026-06-19)

> 위험도: 🔴 Critical (씬 흐름·부팅 페이즈 변경) + 🟡 Medium (피처 폴더 이동)
> 결정 근거: 게임 방향성을 "스토리+스케줄+상점+스탯 시뮬레이션" → "순수 선형 비주얼노벨"로 전환.
> 스케줄/상점/스탯의 시뮬레이션 메타게임을 제거하고 내러티브 엔진 단독으로 진행한다.

---

## 1. 목적 / 배경

기존 게임은 30일 루프 시뮬레이션(부팅→스케줄 선택→행동 소진→하루 전환→오토세이브→30일 엔딩)
위에 내러티브가 "저녁 이벤트"로 끼어드는 구조였다. 방향을 **순수 선형 VN**으로 바꾼다:

- 스케줄/상점/스탯 시뮬레이션 메타게임을 **현 개발에서 사용 안 함** (코드는 보존, 추후 부활 가능).
- 진행 동력은 **내러티브 엔진 자체 체인**(Flow `Jump`/스크립트 종료→다음 스크립트). 새 매니저·새 세이브 스키마 없음.
- 날짜 루프(DayLoop)·행동력·일자별 선택 개념 제거.

## 2. 스코프 (피처 처리)

| 처리 | 피처 | 방법 |
|---|---|---|
| **`_Parked/`로 이동** | Schedule, Shop, Tutorial | `git mv`로 폴더 통째 이동(asmdef·.cs·.meta, GUID 보존). 코드 무변경. |
| **표시 비활성** | Stat·Money·Day | `Game.unity` HUD에서 해당 표시 제거. `GameStateData` 필드는 휴면(삭제 안 함). |
| **유지** | Narrative, Gacha, Messenger, MessageStack, Core 4매니저 골격, Affinity(데이터) | 손대지 않음. |

- **유지 근거**: 감독 결정 — Gacha/Messenger/MessageStack은 VN 구성 요소로 남긴다. Schedule/Shop/Stat만 시뮬 메타게임.
- **Affinity**: 데이터·공식 유지(VN 분기 조건 `Chose:`/호감도에 활용 가능). 단 HUD 상시 표시는 제거.

## 3. 폴더 구조

```
Assets/_Project/Scripts/_Parked/
  Schedule/   ← 기존 Schedule/ 통째 (asmdef LoveAlgo.Schedule 포함)
  Shop/       ← 기존 Shop/ 통째 (asmdef LoveAlgo.Shop 포함)
  Tutorial/   ← 기존 Tutorial/ 통째
```

- asmdef는 **그대로 유지** → 코드는 계속 컴파일되고 해당 피처 테스트도 계속 그린(코드 무변경).
- 의존 방향이 단방향(`Data ← Schedule/Shop`)이고 asmdef 참조는 GUID 기반이라 폴더 위치 이동이 참조를 깨지 않는다.
- "비활성화"의 실질은 **씬 배선 해제**: EventBus로 디커플돼 있어 발행원(스케줄 확정 버튼 등)이 씬에서 빠지면 자연히 동작이 멈춘다.
- 부활 절차: `_Parked/`에서 `git mv`로 원위치 + `Game.unity` 재배선.

## 4. 부팅 · 페이즈 흐름 변경 (🔴 씬 흐름)

현재: `state.Phase` 기본값=Schedule로 부팅 → `NarrativeController.Run`이 재생 중 Story로 전환,
**종료 시 Schedule로 복귀** → 스케줄 UI 표시.

변경:

1. **기본 진입 페이즈를 Story로**: 부팅 시 Story 페이즈에서 시작. (`ScreenPhase` 기본값 조정 또는 부팅 동기화 발행 — 구현 플랜에서 최소 변경 경로 확정.)
2. **`NarrativeController` 종료 후 Schedule 복귀 제거**: 스크립트 종료 후 Story 페이즈를 유지한다.
   다음 스토리 연결은 Flow `Jump`/스크립트 자체 체인으로(EventScriptCatalog 재사용 가능).
3. **`GameManager` 하루 전환(DayLoop) 비활성**: `DayEndRequestedEvent` 발행원(스케줄 확정)이 park돼
   사라지므로 자연 정지. GameManager 코드·저녁이벤트 seam은 유지(호출되지 않을 뿐).
4. **`PhaseService` FSM**: Story↔Schedule 규칙에서 Schedule 분기는 미사용(데드코드화). FSM 자체는
   손대지 않아도 무해 — 단순화는 선택 사항(YAGNI: 이번엔 건드리지 않음).

## 5. 씬 재배선 (`Game.unity`, 🔴)

- **제거 대상 오브젝트**: `ScheduleView`/`ScheduleController`, `ShopView`/`ShopOpenButton`,
  HUD의 Day/Money/Stat 표시, schedule/shop 관련 dev 트리거.
- **유지**: `_Stage`, `_UI/Narrative`, Messenger·Gacha 그룹, 4매니저, `GameBootstrap`, `EndingView`(미트리거).
- 부팅 → 프롤로그 재생 → Story 유지(Flow 체인으로 진행).
- 금지선 #3 준수: `.meta` 건드리지 않음, 오브젝트는 **삭제만**(GUID 무변경). HUD는 표시 요소만 제거.

## 6. 엔딩 처리

- 30일 DayLoop 엔딩은 동력(스케줄) 제거로 자연 비활성.
- **스토리 종료형 엔딩**(마지막 스크립트 종료 → `RequestPhaseCommand(Ending)`)은 **이번 범위 밖 후속**.
  지금은 부팅→프롤로그→Flow 체인 진행까지가 목표. `EndingView`는 씬에 유지하되 미트리거.

## 7. 테스트 영향

- park 피처 asmdef 테스트(Schedule/Shop/Tutorial): 코드 무변경 → 계속 그린.
- **수정 대상**: `NarrativeController`(종료 후 페이즈 유지)·부팅 페이즈 기본값과 관련된 PlayMode 테스트.
  "종료 시 Schedule 복귀"를 단정하는 테스트가 있으면 새 기대치(Story 유지)로 갱신.
- 구현 후 검증: EditMode/PlayMode 전체 그린 + 컴파일/콘솔 0에러.

## 8. 되돌리기 (Reversibility)

- 모든 변경은 가역: 폴더는 `git mv`로 복귀, 씬 오브젝트는 코드/프리팹이 보존돼 재배선 가능,
  부팅 페이즈/종료 동작은 소수 코드 라인.
- 세이브 스키마 **무변경**(필드 휴면만) → 마이그레이션 불필요.

## 9. 알려진 한계 / 후속

- 스토리 종료형 엔딩 트리거(§6) — 후속 슬라이스.
- `PhaseService` Schedule 분기 데드코드 정리 — 선택적 후속.
- Affinity HUD 상시 표시 제거에 따른 호감도 피드백 UI는, 필요 시 VN 맥락(메신저 등)으로 재설계 — 후속.
