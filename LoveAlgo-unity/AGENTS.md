# LoveAlgo — AI Rules

Unity 비주얼 노벨. 5히로인 × 30일. CSV 스크립트 엔진.

## MUST

- UniTask만 사용 (코루틴 금지)
- CancellationToken 항상 전달
- 싱글톤 접근은 `?.` 사용 — `GameManager.Instance?.Method()`
- DOTween은 `await tween.ToUniTask(cancellationToken: ct)` 패턴
- 한국어 주석 (`/// <summary>` 포함)
- 인스펙터 바인딩 최대 활용 (코드량 최소화)
- 코드 수정 후: `unity-cli editor refresh --compile` → `unity-cli console --type error`
- 여러 파일 수정 시 모든 수정 완료 후 refresh 한 번만

## MUST NOT

- 코루틴 (`StartCoroutine`, `yield return`) 사용
- GameState 세이브 포맷 변경 (호환성 파괴)
- 새 싱글톤 무분별 추가
- 하드코딩 (GameConstants / ScriptableObject 사용)
- ScriptRunner 직접 수정 (스토리 전체 영향 — 반드시 확인 후)
- `?.` 없는 싱글톤 접근

## 네임스페이스

| 네임스페이스 | 역할 |
|-------------|------|
| `LoveAlgo.Core` | 게임 흐름, Phase, 타임라인, 포인트 추적 |
| `LoveAlgo.Story` | CSV 실행, 대사, 배경, 캐릭터, 세이브 |
| `LoveAlgo.Schedule` | 스케줄 UI, 스탯 관리 |
| `LoveAlgo.UI` | UI 전환, 팝업 관리 |
| `LoveAlgo.MiniGame` | 미니게임 |
| `LoveAlgo.Shop` | 상점, 선물 |
| `LoveAlgo.Phone` | 메신저 |

## 패턴 (새 기능 추가 시)

- 새 팝업 → `ModalPopupBase` 상속 → `PopupManager.ShowModal<T>()`
- 새 미니게임 → `MiniGameBase` 상속 → CSV에서 `Flow,,MiniGame:이름:히로인,>`
- CSV 문법 → `docs/reference/csv-script-commands.md` 참조
- 스탯/호감도 수치 → `docs/reference/game-data.md` 참조

## 알려진 기술부채 (리팩토링 대상)

| 파일 | 문제 | 줄 수 |
|------|------|-------|
| `GameManager.cs` | God Object — Phase + Save + Day + Schedule + Audio 혼재 | ~900 |
| `ScriptRunner.cs` | 모든 Type 실행을 switch로 직접 처리, OCP 위반 | ~1200 |
| `SaveManager.cs` | 도메인 복원 + 스크린샷 + 텍스처 가공 혼재 | ~1400 |
| `PopupManager.cs` | 모달 관리 + Save/Load/Settings UI 로직 혼재 | ~600 |

분리 방향 → `docs/refactoring-roadmap.md` 참조

## 외부 라이브러리

UniTask, DOTween Pro, TextMesh Pro, Newtonsoft JSON, unity-cli-connector
