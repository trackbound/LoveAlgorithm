# LoveAlgo — GitHub Copilot Instructions

모든 규칙은 **AGENTS.md** (프로젝트 루트)에 정의됨. 이 파일은 Copilot 전용 보조 설정.

## 참조 문서

- `AGENTS.md` — MUST/MUST NOT 규칙, 패턴, 기술부채
- `docs/reference/csv-script-commands.md` — CSV 스크립트 문법
- `docs/reference/game-data.md` — 히로인/스탯 수치
- `docs/refactoring-roadmap.md` — 코드 구조 개선 계획

## 🤖 AI 에이전트 작업 규칙

### 작업 전 필수 확인
1. **AGENTS.md 먼저 참조** - MUST/MUST NOT 규칙, 패턴, 기술부채
2. **기존 패턴 따르기** - 비슷한 기능이 이미 있으면 그 방식 복제
3. **네임스페이스 확인** - Core, Story, Schedule, UI, MiniGame 중 적절한 곳에 배치

### 코드 생성 규칙
```csharp
// ✅ 올바른 패턴
await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
await tween.ToUniTask(cancellationToken: ct);
GameState.Instance?.AddLove("Roa", 5);

// ❌ 금지 패턴
yield return new WaitForSeconds(1);  // 코루틴 금지
StartCoroutine(SomeMethod());        // 코루틴 금지
tween.OnComplete(() => { });         // 콜백 대신 await 사용
```

### 싱글톤 접근 패턴
```csharp
// 항상 null 체크 또는 ?. 사용
GameManager.Instance?.ChangePhase(GamePhase.DayLoop);
UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
ScriptRunner.Instance?.OnClick();
GameState.Instance?.AddStat("Int", 5);
AudioManager.Instance?.PlaySFX("Click");
StageManager.Instance?.Character.ExecuteAsync("C:Enter:Roa", ct);
ScreenFX.Instance?.FadeOutAsync(1f, ct);
PopupManager.Instance?.Toast("알림", "저장 완료!");
```

### 새 기능 추가 시 체크리스트
- [ ] 기존에 비슷한 기능 있는지 검색 (`grep_search` 활용)
- [ ] 적절한 네임스페이스에 배치
- [ ] 인스펙터 바인딩 활용 (코드 최소화)
- [ ] UniTask 반환, CancellationToken 지원
- [ ] 한국어 주석 작성

### 수정 작업 시 주의
- **ScriptRunner 수정 시**: 스토리 실행 전체에 영향, 신중히
- **GameState 수정 시**: 세이브/로드 호환성 확인
- **UI 수정 시**: UIManager/PopupManager 통해 접근

---

## 🔧 unity-cli (AI Agent ↔ Unity 연동)

> ⚠️ **Auto Refresh가 비활성화**되어 있습니다. 코드 수정 후 Unity에 자동 반영되지 않습니다.

- **코드 수정 후 반드시**: `unity-cli editor refresh --compile` → `unity-cli console --type error`
- **런타임 상태 검사**: `unity-cli exec`로 싱글톤 값, 씬 구조 등 확인
- **스크린샷**: `unity-cli screenshot --view game`
