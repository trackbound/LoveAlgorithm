# 캐릭터 폴더 구조 전환 + 표정-만-명시 문법 — 설계 (2026-06-19)

> 위험도: 🟠 High (파서·세이브 기록 포맷·모듈 간 해석 규약 변경). diff 검토 + 엣지 자가검증 동봉.
> 최우선 제약: **기존 기능 전부 이전처럼 작동** (인라인 `<emote=>`, `C:Enter:…`/`C:Emote:…`, 세이브 복원, 로그 초상, 히로인 배치).

## 1. 배경 / 목표

`Resources/Characters`를 평면 나열(`c01_00.png`)에서 **캐릭터별 폴더 + 한글 표정 파일**로 재편한다.
에셋 이동은 이미 완료됨:

```
Characters/Roa/기본.png, 깜짝.png, 눈웃음.png, 밝게웃음.png, 울먹.png, 찌릿.png, 활짝.png, 궁금.png, 머쓱.png
Characters/Daeun/…  Yeeun/(+부끄)  Heewon/  Bom/
```

구 평면 파일(`c01_00.png` 등)은 git에서 삭제됨 → **현재 코드는 `Characters/{char}_{emote}`로 로드하므로 모든 캐릭터 스프라이트가 안 뜨는 상태**. 본 작업이 이를 복구하고, 작가 문법도 단순화한다.

목표 2가지:
- **A.** 새 폴더 구조에 맞게 리소스 해석/로딩 복구.
- **B.** 스토리 CSV에서 "표정만 명시"로 화자 캐릭터 표정 변경(Char 행 + 인라인 모두).

## 2. 채택 방향 (감독 승인: 안 1)

**런타임 식별자를 폴더/파일명으로 통일하고, 구 코드는 별칭으로 보존.**

- 캐릭터 id: `c01..c05` → `Roa, Daeun, Yeeun, Heewon, Bom` (폴더명과 동일).
- 표정 id: 코드 `00,11,…` → 한글 파일명 `기본, 눈웃음, …`.
- 구 코드(`c01`, `00`)는 **별칭으로 잔존** → 기존 세이브의 `c01`/`00` 기록이 그대로 해석·복원됨(세이브 안 깨짐).
- 결과 경로: `Characters/{char}/{emote}` = `Characters/Roa/기본` 로 직결. 명령 추가 필드 0.
- `_slotChar`·화자매칭·세이브·로그·배치가 한 네임스페이스(`Roa`)로 일관.

> 대안(안 2: c01 끝까지 유지 + 폴더 매핑층)은 표정이 코드/파일명 2값으로 갈리고 플러밍이 늘어 기각.

## 3. 변경 상세

### 3.1 ResourceAliasCatalog.asset (데이터)

**characters** — `id`를 폴더명으로, 구 코드를 별칭에 추가:

| 신 id | aliases |
|---|---|
| Roa | 로아, Roa, c01 |
| Daeun | 서다은, SeoDaEun, c02 |
| Yeeun | 하예은, HaYeEun, c03 |
| Heewon | 도희원, DoHeewon, c04 |
| Bom | 이봄, LeeBom, c05 |

**emotes** — `id`를 한글 파일명으로, 구 코드를 별칭에 추가. 예:

| 신 id | aliases |
|---|---|
| 기본 | Default, 00 |
| 눈웃음 | 11 |
| 밝게웃음 | BrightSmile, 12 |
| 활짝 | 활짝웃음, 13 |
| 찌릿 | 21 |
| 깜짝 | 41 |
| 울먹 | 31 |
| 궁금 | 43 |
| 부끄 | 34 |
| 머쓱 | 23 |
| …(파일 없는 나머지 코드: id를 한글명으로, 코드 별칭 유지. 미사용·미존재라 로드 시에만 경고) |

- `defaultEmote`: `00` → `기본`.
- 미등록 토큰은 기존대로 passthrough → 파일명 직기입도 동작.

### 3.2 StageView.cs

- `LoadCharSprite`: 키 합성 `{character}_{emote}` → **`{character}/{emote}`** (StageView.cs:418).
- 표정 생략 시 "캐릭터 단독 파일"(`Characters/c01`) 분기 제거 — 단독 파일 없음. emote 비면 경고/스킵(컨트롤러가 Enter엔 항상 기본표정 보충).
- 그 외 코루틴/페이드/슬롯 로직 무변경.

### 3.3 CharacterStageCatalog.asset (데이터)

- `characterId` 키 `c01..c05` → `Roa..Bom` (5건). 발행되는 Character가 폴더명이므로 키 일치 유지 → 히로인별 배치(스케일/오프셋) 그대로 작동.

### 3.4 DialogueLogView 초상 맵 (데이터, 프리팹/씬 직렬화)

- `portraits` 리스트의 `speakerId` 키 `c01..c05` → `Roa..Bom`. SpeakerId가 폴더명이 되므로 키 갱신해야 초상 유지. 스프라이트 참조는 불변.

### 3.5 NarrativeController.cs — 해석/추적 (Part 2 코어)

- `ResolveCharEmote`: 별칭 해석 로직 자체는 불변(카탈로그가 폴더/파일명을 반환하게 됨). Enter 시 표정 생략 → `DefaultEmote`(=`기본`) 보충 유지.
- **마지막 화자 추적**: `PlayText`에서 화자의 캐릭터 id(`ResolveSpeakerId`, 미등록이면 null)를 `_lastSpeakerId`에 기록.
- **표정-by-식별자**: 대상 캐릭터 id(명시 `Roa` 또는 `_lastSpeakerId`)로 `state.storyChars`에서 현재 슬롯을 찾아 `ShowCharacterCommand(slot, Emote, id, emoteFile)` 발행. 무대에 없으면 경고(인라인 emote와 동일 정책). Char 행 Next(await 등) 완료 의미 보존.

### 3.6 StageParser.cs — Char Value 단축 문법 (Part 2)

기존 문법 전부 유지(`[slot:]Enter:캐릭터[:표정]`, `[slot:]Emote:표정`, `Exit`, `Clear`; 뒤 잉여 토큰 `:Mob`/`:PC` 무시). **추가**: 액션 키워드가 없을 때의 분기 —

- `캐릭터:표정` (예 `Roa:웃음`) → 액션 Emote, character=캐릭터, emote=표정. 컨트롤러가 캐릭터→슬롯 해석.
- `표정` (단일 토큰, 예 `웃음`) → 액션 Emote, character=∅, **직전 화자** 대상.
- `slot:표정` (예 `C:웃음`) → 명시 슬롯 Emote(기존 슬롯 타깃과 동일).

라우팅 구분(직전화자 / 식별자 / 명시슬롯)을 위해 `CharIntent`에 슬롯 명시 여부(또는 타깃 모드)를 표현. 무효 입력은 기존대로 IsValid=false.

### 3.7 InlineTagParser.cs — 인라인 단축 (Part 2)

- 기존 `<emote=표정/>`, `<wait:sec>` 유지.
- **추가**: `=`도 `:`도 없는 단일 토큰 태그 `<웃음>` → emote(=`웃음`)로 처리. 화자→슬롯 해석은 기존 `ShowSpeakerEmoteCommand` 경로 재사용.
- 알 수 없는 표정 토큰은 컨트롤러/StageView에서 경고+스킵(텍스트 표시는 불변). TMP 리치텍스트(`<b>` 등)는 본 프로젝트 대사에서 미사용(현재도 미지원 태그는 제거됨) — 단일토큰=emote 정책의 부작용 허용.

### 3.8 Prologue.csv — 콘텐츠 정합 (감독 결정: CSV 토큰을 파일명으로 교체)

- `활짝웃음` → `활짝` 치환(파일은 `활짝`). 그 외 토큰은 파일과 일치 확인됨(`머쓱` 포함, Roa 폴더에 추가됨).
- 기존 `<emote=…/>` 표기는 그대로 둠(back-compat). 신규 `<웃음>`는 선택적.

## 4. 영향 / 회귀 방지 체크리스트

| 기능 | 보존 방법 |
|---|---|
| 인라인 `<emote=표정/>` | 파서 분기 유지, 표정 별칭이 파일명으로 해석 |
| `C:Enter:로아:기본:Mob` | 파서 기존 분기 유지, 잉여 토큰 무시 유지 |
| `C:Emote:찌릿:PC` | 동일 |
| Setup 매크로 캐릭터 | `ResolveCharEmote` 경유 동일 |
| 구 세이브 복원(c01/00) | 코드가 별칭으로 잔존 → 해석 성공 |
| 신 세이브 복원(Roa/기본) | 정식 id로 해석 |
| 로그 초상 | `portraits` 키 갱신 |
| 히로인 배치 | `CharacterStageCatalog` 키 갱신 |

## 5. 테스트

- EditMode: `ResourceAliasCatalogTests`(id/별칭 갱신, 구코드 별칭 해석), `CharacterStageCatalogTests`(키 갱신), `StageParserTests`(단축 문법 3종 + 기존 회귀), `InlineTagParser`(단일토큰 emote) 신규.
- PlayMode: `AliasResolutionPlayModeTests`(폴더/파일명 해석), `StageViewPlayModeTests`(경로 `{char}/{emote}` 로드), 직전화자·식별자 표정 변경 시나리오.
- 수동: Prologue 1회 통주행 — 로아 표정 전환·초상·배치 육안 확인.

## 6. 범위 밖 (YAGNI)

- 파일 없는 표정 신규 아트(코드 보존, 사용 시 경고).
- 슬롯 내 표정 크로스페이드(현행 즉시 교체 유지).
- 메신저/가챠 등 Characters 외 시스템.
