# WORK_PLAN.md

> 동적 작업 리스트. 작업 추가/완료 시 갱신.
> 기획서: `게임 복합시스템 / 내부 콘텐츠 / PC잠금 연출` 기반.

## 진행 우선순위

1. **A1 호감도 엔진** + B 1차 이벤트 CSV (검증 가능 최소단위)
2. **A2 EventPhase 흐름** (시뮬 ↔ 이벤트 전환)
3. **A3 아이템 + C 상점 UI** (2차 이벤트 전 필수)
4. **A5 PC잠금 연출** (첫인상, 병렬 가능)
5. **A4 가챠** (보상, 후순위)

---

## A. 시스템 (코드)

### A1. 호감도/스코어링 엔진 ⚠️ 핵심
- [x] `AffinityCalculator` 확장: 비율 가중 (이벤트60·대화20·선물15·미니5) — 기존 구조에서 가중치 합산
- [x] 선호 스탯 보너스 (1등+3 / 공동1등+1) — `AffinityCalculator.CalcStatBonus`
- [x] 3차 재선택 +2 보정 (로아 제외) — `HeroinePointTracker.RecordEventChoice`에서 자동
- [x] 로아 피로 보정 (70/80/90 → +3/+6/+10) — `AffinityCalculator.CalcRoaFatigueBonus`
- [x] 고백 임계치 → 10엔딩 분기 — `IAffinity.GetEnding(confessedHeroineId)` + `EndingType` enum
- [x] 로아 고백 게이트 (피로<70 → 흑백) — `IAffinity.IsRoaConfessionUnlocked()`

### A2. 이벤트 흐름 컨트롤러
- [x] `EventPhase` enum (Opening/Event1/AfterEvent1/Festival/.../Confession/Ending) — `Modules/DayLoop/EventPhase.cs`
- [x] `IDayLoop` 모듈 — day → phase 매핑, IsEventDay/IsFreeActionDay 쿼리, DayChanged/PhaseChanged 이벤트 발행
- [ ] 기존 `DayLoopController`가 `IDayLoop` 결과 기반으로 자유행동일↔이벤트일 분기 (이주)
- [ ] 자유행동 낮/밤 2회 슬롯 (현재 구현 확인)

### A3. 아이템 / 인벤토리
- [ ] `ItemDataSO` (분류·대상·계층·효과·가격)
- [ ] 세션 효과 (자유행동 1회 후 소멸)
- [ ] 중복 50% 페널티 (같은 날 같은 태그)
- [ ] 선물 사용 게이트 (2차/3차만)
- [ ] 상점 오픈 게이트 (이벤트 전날 풀림)

### A4. 가챠 시스템 (신규)
- [ ] `GachaController` (30조각, 레어도 1~5 가중치 10·7·6·5·2)
- [ ] 모은 조각 인덱스 저장
- [ ] 호칭 업적 (퍼즐콜렉터 +5 / 마스터 +10)

### A5. PC잠금 시스템 (신규)
- [ ] `LockScreenController` (첫시작 / 일반진입 / 재설정 모드)
- [ ] 비밀번호 저장 (PrefsKeys, 단방향 해시)
- [ ] 3회 오류 카운터 → 열쇠 아이콘
- [ ] 투두리스트 33개 SO + 랜덤 3개 표출

---

## B. 콘텐츠 (CSV / Data)
- [ ] 1차 이벤트 CSV × 5히로인
- [ ] 축제 CSV
- [ ] 2차 이벤트 CSV × 5히로인
- [ ] MT CSV
- [ ] 3차 이벤트 CSV × 5히로인
- [ ] 고백 CSV × 5히로인 + 노고백
- [ ] 일상 대화 CSV (15회 대화 포인트)
- [ ] 메신저 대사 (개인이벤트 약속용)
- [ ] 아이템 마스터 SO (선물15 + 소모품/버프, 시트 기반)
- [ ] 투두리스트 33개 SO

---

## C. UI / Prefab
- [ ] 자유행동 명칭: 헬스→운동, 외출→아이템구매, 코인투자→투자
- [ ] 자유행동 클릭 → dim+팝업 ("스탯 증가 / 진행?")
- [ ] 로아 자유행동 튜토리얼 (33단계, 첫 진입 1회)
- [ ] 상점 UI (장바구니·수량·구매팝업·잔액부족)
- [ ] 가챠 UI (5×6 퍼즐, 조각 등장 애니, 컨페티 + 전체화면)
- [ ] PC잠금 UI (시계, 투두 위젯, 로아메시지 4개, 비번 입력 + 눈/열쇠)
- [ ] 엔딩 분기 화면 (10종)

---

## E. UI 리팩토링 (기능 작업 시 함께)

> UI 코드에 핵심 로직 혼재 — 기능 작업 시 분리 필요

- [ ] `ScheduleUI.cs` — 스케줄 선택 로직 + 표시 + 바인딩 혼재. 로직 → `ScheduleModule`로 분리
- [ ] `SettingsPopup.cs` — 볼륨/설정 저장 로직 직접 보유. → `IAudio` / `ISave` 경유로
- [ ] `SaveLoadPopup.cs` — 세이브 포맷 + 썸네일 + UI 혼재. → `ISave` 모듈로 분리

**원칙**: UI는 표시만. 상태 변경·데이터 접근은 IService 경유.

## D. 메모 / 문서
- [ ] `project_love_algorithm.md` 갱신: "30일 루프" → 이벤트 기반 흐름
- [ ] 이벤트 점수 테이블 명시 (1차+3·축제+4·2차+6·MT+5·3차+9 = 27)

---

## 완료 (Done)

- [x] **모듈 인프라 기반** — `Common/EventBus.cs`, `Common/Services.cs`, `Core/Bootstrap.cs`
- [x] **Stats 모듈** (Modules/Stats) — `IStats`, `StatsModule`, `StatChangedEvent`. GameState 래퍼.
- [x] **Affinity 모듈** (Modules/Affinity) — `IAffinity`, `AffinityModule`, `AffinityChangedEvent`, `EndingType` enum, 로아 게이트.
- [x] **CSV Flow 명령 `Affinity:...`** — `StoryEngine/Flow/AffinityFlowCommand.cs`. CSV에서 IAffinity 호출 가능.
- [x] **Event1.csv 검증 스캐폴드** — 5히로인 분기 + Affinity:EventChoice 호출 패턴 확립.
- [x] **DayLoop 모듈** (Modules/DayLoop) — `IDayLoop`, `EventPhase`, `DayLoopModule`, `DayChangedEvent`, `PhaseChangedEvent`. 일자→페이즈 매핑 + 이벤트일/자유행동일 판별.
- [x] **Stats 모듈 마이그레이션** — 게임플레이 코드(`DayLoopController`, `ShopManager`, `ChoiceUI`)가 IStats 경유. StatChangedEvent 실제 발행 시작. Save/Debug는 GameState 직접 유지.
- [x] **Audio 모듈** (Modules/Audio) — `IAudio`, `AudioModule`, `BGMChangedEvent`. AudioManager 싱글톤 래퍼. 최소 표면 (PlayBGM/Stop/SFX/Voice/StopVoice). 볼륨 설정은 기존 AudioManager 직접 유지.
- [x] **Audio 완전 이주** (✅) — `Story/AudioManager.cs`, `Story/AudioSettings.cs` → `_Project/Modules/Audio/Code/`. 네임스페이스 `LoveAlgo.Story` → `LoveAlgo.Modules.Audio`. 호출자 13개 `using` 갱신 (sed 일괄). Scenes·SO GUID 보존됨.
