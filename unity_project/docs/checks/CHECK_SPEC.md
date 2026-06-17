# CHECK_SPEC — 자동 점검·준비 루프 (정본)

> 무인 스케줄로 도는 "점검·준비 루프"의 정본 겸 프롬프트 소스.
> 에이전틱 루프 중 **읽기전용·리포트만** 형태 — 게이트(CLAUDE.md 위험도 리뷰·자동커밋 금지·추측 금지)와 충돌 없이 굴리는 버전.

## 목적
적절한 시각(기본 매일 04:00, 에디터 닫힌 새벽)에 자동으로:
1. **EditMode 테스트가 여전히 green인가** (헤드리스 배치)
2. **금지선/obsolete가 새로 들어왔나** (정적 grep)
3. **무엇이 드리프트됐나** (git: 브랜치·미커밋·최근 커밋)

를 점검하고, 다음 세션의 Claude가 바로 읽을 **리포트 + 제안 다음 액션**을 남긴다.
**코드 수정·커밋·푸시 없음.** 판단은 하되 실행(게이트 통과)은 감독 몫.

## 점검 항목 ("green" 정의)
| 항목 | 통과 기준 | 방법 |
|---|---|---|
| EditMode 테스트 | failed = 0 | 헤드리스 `-runTests -testPlatform EditMode` |
| Obsolete API | 신규 0 | grep `FindObjectOfType`·`FindObjectsOfType`·`enableWordWrapping` |
| 금지선(Service Locator) | 0 | grep `Services.(TryGet\|Get\|Register)` |
| 매니저 골격 | 4개(Game/Audio/Save/UI) 유지 | `class *Manager` 열거 |
| Git 드리프트 | (정보) | 브랜치·`status --porcelain`·`log` |

> PlayMode·`I*` 인터페이스 부활·과설계 등 **판단이 필요한 항목**은 grep이 아니라 claude 요약 단계에서 다룬다. 헤드리스 PlayMode는 불안정 → 무인 제외(데스크에서 MCP `run_tests`로).

## 구성
- `tools/auto-check/run-check.ps1` — 러너(결정적: 테스트+grep+git → raw, 이어서 claude 요약).
- `tools/auto-check/check-prompt.md` — claude -p 프롬프트(점검 지침 정본).
- `tools/auto-check/register-task.ps1` — Windows 작업 스케줄러 등록/해제.
- 출력: `docs/checks/YYYY-MM-DD-check.md`(상세) + `docs/checks/_latest.md`(최신). HANDOFF 최상단이 `_latest.md`를 가리킴.

## 비용·안전 브레이크
- claude는 **읽기전용**(`--allowedTools Read Grep Glob` + 읽기 git만). 리포트 파일은 **스크립트가** 쓴다 → claude는 파일시스템·커밋 불가.
- **`--max-budget-usd 0.50`** 하드 상한. 모델 = `haiku`(기본 — 정기 점검엔 충분, `-Model sonnet`으로 격상 가능). claude는 HANDOFF **상단 요약부만** 읽음(비용 절약).
- 하루 1회. 헤드리스 테스트 타임아웃 기본 20분.
- **끄기**: `tools/auto-check/DISABLED` 파일 생성, 또는 `Disable-ScheduledTask -TaskName LoveAlgo-AutoCheck`.

## 전제
- 스케줄 시각에 **에디터 닫혀 있어야** 헤드리스 테스트가 돈다(열려 있으면 자동 건너뜀+표기).
- 작업은 **현재 사용자**로 실행 → 로그인 상태 필요(잠금은 무방). claude 인증이 사용자 프로필에 있어야 함.
- 임시 산출물(`last-run/` 의 log/xml)은 `.gitignore`로 커밋 제외.
