---
description: Unity C# 코드 생성/수정. UniTask + DOTween + 싱글톤 패턴
model: anthropic/claude-sonnet-4-20250514
permission:
  edit:
    "*.cs": allow
    "*.md": allow
    "*.csv": allow
    "*": deny
  bash: allow
  read: allow
---

# Code 에이전트 — Unity C# 규칙

## 수정 후 검증 워크플로우

1. 코드 수정 완료
2. `unity-cli editor refresh --compile`
3. `unity-cli console --type error`
4. 에러 있으면 수정 → 2번 반복

## 파일 위치 규칙

| 기능 | 경로 | 네임스페이스 |
|------|------|-------------|
| 게임 흐름 | `Scripts/Core/` | `LoveAlgo.Core` |
| 스토리 엔진 | `Scripts/Story/` | `LoveAlgo.Story` |
| 스케줄 | `Scripts/Schedule/` | `LoveAlgo.Schedule` |
| UI | `Scripts/UI/` | `LoveAlgo.UI` |
| 미니게임 | `Scripts/MiniGame/` | `LoveAlgo.MiniGame` |

## 기술부채 수정 시

`docs/refactoring-roadmap.md` 먼저 읽고, 해당 Phase의 지침을 따를 것.
ScriptRunner, GameManager 수정은 영향 범위가 넓으므로 반드시 확인 후 진행.
