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
| `SaveManager.cs` | 도메인 복원 + 스크린샷 + 텍스처 가공 혼재 | ~1400 |

> ScriptRunner → StoryEngine 리팩토링 완료, PopupManager 리팩토링 완료

분리 방향 → `docs/refactoring-roadmap.md` 참조

## 에셋 레지스트리 (Asset Registry)

에셋 정보는 `asset-registry/` 디렉토리에 자동 생성됨.

| 파일 | 내용 |
|------|------|
| `asset-registry.json` | **요약 인덱스** — 먼저 읽고 필요한 상세 파일 참조 |
| `characters.json` | 캐릭터 스프라이트, 표현, SO, BGM |
| `audio.json` | BGM 목록, SFX 패턴/수량 |
| `stage.json` | 배경(장소별), CG, SD |
| `ui.json` | UI 아트 카테고리, 프리팹 구조 |
| `data.json` | ScriptableObject, 설정, 아이템 |
| `scripts.json` | 네임스페이스별 클래스 목록 |
| `story.json` | 스토리 CSV 파일 |

```bash
python asset-registry/generate.py   # Assets/ 스캔 → 카테고리별 JSON 재생성
```

- 명명 규칙 → `asset-registry/naming-conventions.md` 참조
- `Assets/` 변경 커밋 시 pre-commit hook이 자동 재생성

## 외부 라이브러리

UniTask, DOTween Pro, TextMesh Pro, Newtonsoft JSON, unity-cli-connector
