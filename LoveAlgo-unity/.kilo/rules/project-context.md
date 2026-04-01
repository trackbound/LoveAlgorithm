# LoveAlgo 프로젝트 컨텍스트

대학 캠퍼스 연애 어드벤처 시뮬레이션. Unity URP.
5명의 히로인(하예은, 서다은, 이봄, 도희원, 로아) × 30일 루프.

## 필수 참조 문서

- **AGENTS.md** — MUST/MUST NOT 규칙, 패턴, 기술부채
- **docs/reference/csv-script-commands.md** — CSV 스크립트 DSL 전체 문법
- **docs/reference/game-data.md** — 히로인/스탯/호감도 수치
- **docs/refactoring-roadmap.md** — 코드 구조 개선 계획

## 핵심 싱글톤

`GameManager` · `ScriptRunner` · `GameState` · `UIManager` · `PopupManager` · `SaveManager` · `AudioManager` · `StageManager` · `ScreenFX`

## 게임 흐름

```
Title → Prologue → DayLoop(30일) → Ending
DayLoop: Morning → Schedule(3슬롯) → Evening → Night
```
