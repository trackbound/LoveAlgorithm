# 게임 페이즈 흐름

## Phase 전이도

```
┌─────────┐  StartNewGame()  ┌──────────┐  OnNameConfirmed()  ┌──────────┐
│  TITLE  │ ───────────────► │ USERNAME │ ──────────────────► │ PROLOGUE │
└────┬────┘                  └──────────┘                     └────┬─────┘
     │                                                             │
     │ ContinueGame()                             OnPrologueEnd()  │
     │ LoadGame(slot)                                              │
     │                                                             ▼
     │                                                      ┌──────────┐
     └─────────────────────────────────────────────────────►│ DAYLOOP  │◄──┐
                                                            └────┬─────┘   │
                                                                 │         │
                                                  OnSchedule     │         │
                                                  Completed()    │         │
                                                (Remaining=0)    │         │
                                                                 ▼         │
                                                          ┌──────────┐     │
                                                          │  EndDay  │─────┘
                                                          │ (Day++)  │
                                                          └────┬─────┘
                                                               │ (Day>30)
                                                               ▼
                                                          ┌──────────┐
                                                          │  ENDING  │
                                                          └──────────┘
```

## Phase별 메인 UI

| Phase | UI | 설명 |
|-------|-------|------|
| `Title` | TitleUI | 시작/이어하기/로드/설정/종료 |
| `Username` | UsernameUI | 플레이어 이름 입력 |
| `Prologue` | DialogueUI | 프롤로그 스토리 실행 |
| `DayLoop` | ScheduleUI | 스케줄 선택 (2행동/일, 30일) |
| `Ending` | DialogueUI | 엔딩 스크립트 → 타이틀 복귀 |

## 30일 타임라인 개요

| 일차 | 타입 | 아크 |
|------|------|------|
| 1~5 | 자유 + 이벤트 | Opening, FreeTime1 |
| 6 | 개인 이벤트 | Event1 |
| 7~9 | 자유 | FreeTime2 |
| 10~12 | 축제 | Festival (3일 연속) |
| 13~15 | 자유 | FreeTime3 |
| 16 | 개인 이벤트 | Event2 |
| 17~19 | 자유 | FreeTime4 |
| 20~22 | MT | MT (3일 연속) |
| 23~25 | 자유 | FreeTime5 |
| 26 | 개인 이벤트 | Event3 |
| 27~29 | 자유 | FreeTime6 |
| 30 | 고백 | Confession |

## 엔딩 판정

1. `HeroinePointTracker.GetTotalPoint()` 로 각 히로인 포인트 집계
2. `AffinityCalculator` 로 스탯 보너스 반영 (선호 스탯 1위: +3, 공동 1위: +1)
3. 최고 포인트 히로인 결정 → 해당 히로인 엔딩
4. `IsHappyEnding(heroineId)` — 포인트 임계값 이상이면 Happy, 아니면 Sad
5. 어떤 히로인도 임계값 미달이면 Normal 엔딩
