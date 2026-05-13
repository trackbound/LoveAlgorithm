# 히로인명 전역 일괄 변경 — 2026-05-13

> 5명 중 4명 풀네임(성+이름) 표기로 변경. Roa만 그대로.

## 매핑

| 기존 표기 | 신규 표기 | 비고 |
|-----------|-----------|------|
| `Daeun` / `daeun` | `SeoDaEun` / `seodaeun` | 서다은 |
| `Yeun`, `Yeeun` / `yeun`, `yeeun` | `HaYeEun` / `hayeeun` | 하예은 — 코드에 두 표기 혼재 → 통일 |
| `Bom` / `bom` | `LeeBom` / `leebom` | 이봄 |
| `Heewon` / `heewon` | `DoHeewon` / `doheewon` | 도희원 |
| `Roa` | `Roa` | 변경 없음 |

## 적용 규칙

- 단어 경계 = 영문자(`A-Za-z`)만 단어 문자로 간주. 언더스코어/숫자/구두점 양옆에서는 토큰 매치 허용.
  - 정규식: `(?<![A-Za-z])token(?![A-Za-z])`
  - 따라서 `Char_Daeun_Default`, `SD_Daeun_01`, `thank_daeun`, `BGM:Daeun`, `"Bom"` 등 모두 매치.
- `Yeeun` → `Yeun` 순서로 적용해 `Yeun`이 `Yeeun`의 부분 매치를 일으키지 않도록 보호.
- 대소문자 보존: Pascal → Pascal, 소문자 → 소문자.

## 처리 범위

| 항목 | 건수 |
|------|------|
| 콘텐츠 치환 파일 | **59** (1차 49 + 2차 10) |
| 파일명 rename | **16** (SD 3, CG 1, Log 포트레이트 4, Loading 8) |
| `.meta` 페어 동기 | 모두 동시 rename (Unity GUID 보존) |
| 잔류 | **0** (메인 작업 영역) |

### 카테고리별 콘텐츠 변경 (히트 수 상위)

| 파일 | 히트 |
|------|------|
| `Assets/Resources/Data/characters_emotes.json` | 36 |
| `docs/migrations/2026-05-13-asset-rename.md` | 29+37 |
| `Assets/Resources/Story/Prologue.csv` | 16+25 |
| `Assets/_Project/Modules/Shop/Code/ItemDatabase.cs` | 12 |
| `Assets/Resources/Data/ItemCatalog.asset` | 12 |
| `Assets/Resources/Story/Event1.csv` | 12+8 |
| `Assets/_Project/Modules/Settings/Code/SettingsPopup.cs` | 8 |
| `Assets/_Project/Modules/Phone/Code/MessengerManager.cs` | 11 |
| `Assets/_Project/Core/LoadingScreen.cs` | 8 |
| 외 다수 (Affinity, Audio, Narrative, Debug, Core, GameConstants, Prefab 등) | |

### 파일명 Rename 상세

#### Resources/SD
- `sd_01_char_daeun_01.png` → `sd_01_char_seodaeun_01.png`
- `sd_02_char_heewon_01.png` → `sd_02_char_doheewon_01.png`
- `sd_03_char_bom_01.png` → `sd_03_char_leebom_01.png`

#### Resources/CG
- `cg_02_char_yeeun_01.png` → `cg_02_char_hayeeun_01.png`

#### Art/UI/Popup/Log
- `log_portrait_bom.png` → `log_portrait_leebom.png`
- `log_portrait_daeun.png` → `log_portrait_seodaeun.png`
- `log_portrait_heewon.png` → `log_portrait_doheewon.png`
- `log_portrait_yeun.png` → `log_portrait_hayeeun.png`

#### Resources/UI/Loading
- `Load_Bom_01.png`, `Load_Bom_02.png` → `Load_LeeBom_*.png`
- `Load_Daeun_01.png`, `Load_Daeun_02.png` → `Load_SeoDaEun_*.png`
- `Load_Heewon_01.png`, `Load_Heewon_02.png` → `Load_DoHeewon_*.png`
- `Load_Yeun_01.png`, `Load_Yeun_02.png` → `Load_HaYeEun_*.png`

## 규약 문서 갱신

- `docs/ASSET_NAMING.md` 캐릭터 ID 매핑 영문 컬럼 갱신 (Daeun→SeoDaEun, Yeeun→HaYeEun, Heewon→DoHeewon, Bom→LeeBom)
- `docs/ASSET_NAMING.md` SD/CG 소분류(char) 키 목록 갱신: `roa, seodaeun, hayeeun, doheewon, leebom`
- `docs/ASSET_REQUESTS.md` 진행 중 요청 항목의 캐릭터명 갱신

## 예외 / 미처리

- `Assets/Scenes/Main/LightingData.asset` — 바이너리(UTF-8/CP949 디코드 실패). 캐릭터명 미포함 영역이라 영향 없음.
- `Assets/.claude/worktrees/stoic-fermi-e72784/` — 개발자 작업 분기. 1차 패스에서만 일부 콘텐츠 치환됨. 추후 메인 머지 또는 분기 정리 시 별도 통합.

## 후속 작업

- [ ] Unity 에디터에서 컴파일 확인 — `Resources.Load`/`Char_*` 옛 식별자가 메서드 시그니처에 남아 있지 않은지 빌드로 검증
- [ ] CSV(`Prologue.csv`, `Event1.csv`)의 SD/CG 파일명 참조 — 옛 형식(`SD_Daeun_01`, `CG_Yeun_01`)이 이제 `SD_SeoDaEun_01`, `CG_HaYeEun_01`로 치환되었지만, 실제 파일명은 신규 규약(`sd_01_char_seodaeun_01.png`, `cg_02_char_hayeeun_01.png`)임. CSV는 옛 alias 기반 path 변환을 사용하는지 확인 후, 필요 시 신규 규약 직접 참조로 재정비.
- [ ] Excel(`Character_Emotion_List.xlsx`, `SD_List.xlsx`, `CG_List.xlsx`)의 캐릭터 영문 키 컬럼 갱신
