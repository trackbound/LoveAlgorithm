# CLAUDE.md — Love Algorithm 작업 원칙

> 최상위 문서. Claude Code가 이 프로젝트에서 일할 때 항상 따르는 규칙.

## 게임 정체성
**비주얼노벨 + 스케줄 시뮬레이션 + 이벤트 분기**
- VN: CSV 스크립트 엔진 (대사/이벤트/연출)
- 시뮬: 자유행동(낮·밤 2회) → 스탯 성장
- 이벤트: 1차 → 축제 → 2차 → MT → 3차 → 고백 → 엔딩

---

## 1. 태도

- **간결하게**. 잘 작동하면 설명 생략 가능. 오류·중요 결정만 언급.
- **확신 없으면 묻는다**. 추측해서 코드 만들지 말 것.
- **목업이 필요하면 반드시 요청**. UI/아트 리소스 형태를 추측해서 작업하지 않는다.

## 2. 전략 — 모듈 독립성

기능 추가 요청 = "그 기능만 만들고 기존 코드 건드리지 않는다."

| 규칙 | 의미 |
|------|------|
| 한 기능 = 한 폴더 | `Scripts/{Feature}/` 안에서 자급자족 |
| 외부 통신은 이벤트/인터페이스 | 직접 참조 금지. `GameManager` 같은 God Object 의존 X |
| 데이터는 SO 또는 CSV | 코드에 하드코딩 금지 — **기획자가 인스펙터에서 수정 가능해야 함** |
| 프리팹은 자족적 | 컴포넌트가 프리팹 내부에서 완결. 씬에 외부 의존 최소화 |

세부 폴더/하이어라키 구조, 책임 경계, 통신 패턴: [`docs/NAMING.md`](docs/NAMING.md) 참조.

## 2.5 Obsolete API 금지

Unity/TMP/패키지 API 중 `[Obsolete]` 마크된 것은 **절대 새로 쓰지 말 것**. 본 프로젝트 Unity 버전(6+) 기준 빈출 교체표:

| 옛 (사용 금지) | 신규 (이걸로) |
|---|---|
| `Object.FindObjectOfType<T>()` | `Object.FindAnyObjectByType<T>()` (또는 `FindFirstObjectByType<T>()`) |
| `Object.FindObjectsOfType<T>()` | `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` |
| `TMP_Text.enableWordWrapping = true/false` | `TMP_Text.textWrappingMode = TextWrappingModes.Normal/NoWrap` |
| `WWW` / `UnityWebRequest.Get(...).Send()` | `UnityWebRequest.SendWebRequest()` (UniTask 권장) |
| `Resources.LoadAsync` 같은 deprecated coroutine 패턴 | `Addressables` 또는 동기 `Resources.Load` |
| `OnLevelWasLoaded` | `SceneManager.sceneLoaded += ...` |

규칙:
- 새 코드 작성 시 IDE가 strikethrough 경고 → **다른 API 검색 후 채택**.
- 기존 코드에서 obsolete 경고가 보고되면 **그 PR에서 같이 고침** (별도 작업 안 미룸).
- 잘 모르는 API는 Unity 문서에서 "obsolete" 키워드로 그 클래스 페이지 검색.

## 2.6 로깅 규칙 — `LoveAlgo.Common.Log` 우선 사용

`Debug.Log`/`Debug.LogWarning`은 릴리즈 빌드에서도 호출 자체(문자열 보간 + 콘솔 I/O)가
실행되므로 매 라인/매 프레임 호출되는 경로에서 비용이 누적된다. **새 코드는
`LoveAlgo.Common.Log` 헬퍼를 디폴트로 사용**.

| 상황 | 사용할 것 |
|---|---|
| 정보성 (개발 디버그용, 릴리즈 빌드에서 사라져도 OK) | `Log.Info(...)` |
| 경고 (개발 중 주의 환기, 릴리즈 빌드에서 사라져도 OK) | `Log.Warn(...)` |
| 에러 (사용자/QA에게도 보여야 함) | `Log.Error(...)` 또는 `Debug.LogError(...)` |
| 예외 | `Log.Exception(e)` |

`Log.Info/Warn`은 `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`로
릴리즈 빌드에서 호출이 컴파일러에 의해 제거된다 — 문자열 보간 비용 0.

기존 `Debug.Log` 84여 개는 점진 마이그레이션. 새 핫팟(Update/코루틴/매 라인 처리)을
작성하면 무조건 `Log.Info`. 사용자에게 보고되는 진짜 에러만 `Debug.LogError` 또는
`Log.Error` 유지.

## 2.7 UI 접근 규칙 — `Services.TryGet<I*>()` 직접 사용

`UIManager.Instance.DialogueUI` 같은 wrapper 프로퍼티는 **신규 코드 사용 금지**.
Wrapper는 결국 `Services.TryGet<INarrative>()?.DialogueUI`로 위임될 뿐이고,
UIManager가 거의 모든 모듈 인터페이스를 끌어안아 모듈 분리 시 강한 결합 지점이 됨.

```csharp
// ❌ 옛 패턴
var ui = UIManager.Instance?.DialogueUI;

// ✅ 새 패턴
var ui = LoveAlgo.Common.Services.TryGet<INarrative>()?.DialogueUI;
```

기존 호출처는 그대로 동작(소프트 deprecate). Phase C 인터페이스 분리 후 정식
`[Obsolete]` 표시 + 전수 마이그레이션 예정.

## 3. 토큰 효율

- **수정 범위 = 그 기능 폴더만**. 의도치 않은 다른 파일 수정 금지.
- **요약·재진술 생략**. 코드 변경 후 길게 정리하지 말 것.
- **mcp-unity 사용 금지** (토큰 낭비). 컴파일 검증·콘솔 로그·씬 조작은 사용자가 Unity 에디터에서 직접 수행. Claude는 코드 변경 후 "Unity에서 컴파일 확인 부탁" 정도만 알린다.

### Serena MCP 우선 사용

코드 탐색·수정 시 Read/Grep 대신 Serena 도구를 우선 사용한다. 토큰 비용이 크게 낮고 결과가 정확함.

| 작업 | 우선 사용 | 비고 |
|------|-----------|------|
| 심볼(클래스/메서드) 찾기 | `find_symbol` | Grep+Read 조합보다 압도적으로 저렴 |
| 참조 찾기 (refactor) | `find_referencing_symbols` | 안전한 이름 변경·시그니처 변경에 필수 |
| 파일 구조 파악 | `get_symbols_overview` | Read로 전체 읽지 말 것 |
| 메서드 단위 수정 | `replace_symbol_body` | Edit으로 큰 파일 들춰보지 말 것 |
| 심볼 앞/뒤 삽입 | `insert_after_symbol` / `insert_before_symbol` | 새 메서드·필드 추가 |
| 의미 검색 | `search_for_pattern` | 정규식 기반, Grep 대체 |

**Serena 비적합 상황** — 이때만 Read/Grep 사용:
- CSV / Markdown / JSON / YAML 같은 비-코드 파일
- 100줄 미만의 짧은 파일 (Read가 더 빠름)
- 파일 전체 흐름 이해가 필요한 경우 (예: 새 파일 처음 보기)

## 4. 기획 친화성 (Inspector / SO)

신규 기능에 숫자/문자열이 들어가면:
1. 먼저 **ScriptableObject 또는 CSV**로 빼낸다.
2. 코드는 SO 참조만. 매직넘버 금지.
3. SO 위치: `Assets/Data/{Feature}/`
4. 기본값(폴백) 코드는 SO 없을 때만 사용.

예시: `ScheduleDataSO`, `ResourcePaths`, `GameConstants`.

## 5. 아트 / 프리팹

작업 전 확인:
- 캐릭터: `Resources/Characters/Char_{Name}_{Emotion}.png` (5명 × 8표정)
- 배경: `Resources/Backgrounds/BG_*.png`
- 아이템 아이콘: `Art/GUI/{번호}_{분류}_{이름}.png`
- 로아 PC잠금: `Art/UI/Stage/Roa_PC_*.png`

**리소스 부족 시**: `docs/ASSET_REQUESTS.md`에 요청 추가하고 사용자에게 알린다.

## 6. 작업 진행

- 현재 작업 리스트: [`docs/WORK_PLAN.md`](docs/WORK_PLAN.md)
- 리소스 요청: [`docs/ASSET_REQUESTS.md`](docs/ASSET_REQUESTS.md)
- 스토리 CSV 작성 규칙: [`docs/STORY_CSV_GUIDE.md`](docs/STORY_CSV_GUIDE.md)
- 작업 완료 / 새 작업 발견 시 두 문서 갱신.

## 7. 커밋 / 변경

- 한 기능 = 한 커밋. 여러 기능을 하나로 합치지 않는다.
- 사용자가 명시적으로 요청할 때만 커밋. 자동 커밋 금지.

## 8. 워크플로우 (큰 작업)

코드 100줄 이상 영향 / 여러 파일 / 모듈 신설 같은 큰 작업은 다음 흐름 따른다:

1. **리서치 산출물 보관** — Explore agent 결과를 채팅에 휘발시키지 않고 `docs/research/YYYY-MM-DD-{topic}.md`로 저장. 다음 세션·후속 작업에서 재활용.
2. **Plan 모드 활용** — 큰 작업 전 plan 모드로 `.claude/plans/*.md` 작성. 사용자가 에디터에서 plan 파일을 열어 인라인 메모로 정정 가능. *"메모 반영하고 plan 업데이트"* 명령 받으면 절대 코드 작성하지 말 것 — plan 업데이트만.
3. **잘못된 방향이면 revert** — 작은 수정 누적보다 *"되돌리고 범위 좁혀 재시도"*가 더 좋은 결과. 사용자 요청 시 git revert/reset부터.
4. **기술/라이브러리 선택은 사용자** — Claude는 옵션 제시 + 추천만. *"A안 vs B안"* 형식.
5. **컨텍스트 외부화** — 긴 세션에서 정확도 저하 방지. plan/research/docs로 외부화하여 다음 세션이 새 컨텍스트로 시작해도 정확.
