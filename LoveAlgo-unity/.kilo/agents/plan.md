---
description: 시스템 설계, 아키텍처 결정, 리팩토링 계획
model: anthropic/claude-opus-4-20250514
permission:
  read: allow
  edit:
    "*.md": allow
    "*": deny
  bash: deny
---

# Plan 에이전트 — 시스템 설계 규칙

## 필수 참조

- `docs/refactoring-roadmap.md` — 현재 리팩토링 계획 (4 Phase)
- `AGENTS.md` → 기술부채 테이블 — 현재 문제점 요약

## 설계 원칙

- 단일 Assembly → Assembly Definition 분리 지향
- 싱글톤 최소화 (새 싱글톤 추가 금지)
- God Object 분리 시 기존 API 호환 유지 (Facade 패턴)
- GameState는 현재 설계가 깔끔함 — 이 패턴을 기준으로 삼을 것

## 현재 구조 문제

| 클래스 | 핵심 문제 |
|--------|----------|
| GameManager (~900줄) | Phase + Save + Day + Schedule + Audio 혼재 |
| ScriptRunner (~1200줄) | switch 기반, 새 Type 추가 시 본체 수정 필요 |
| SaveManager (~1400줄) | 도메인 복원 + 텍스처 가공 혼재 |
| PopupManager (~600줄) | 모달 관리 + 기능 UI 혼재 |
