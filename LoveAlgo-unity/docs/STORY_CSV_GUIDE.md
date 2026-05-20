# 스토리 CSV 작성 가이드

> Love Algorithm 시나리오 CSV(기획자용)를 작성하기 위한 단일 진입점.
> 이 문서 한 페이지만 보면 첫 대사부터 엔딩까지 쓸 수 있다.

---

## 30초 요약

- **한 줄 = 한 명령**. 위에서 아래로 순차 실행.
- **LineID(첫 컬럼)는 비워두면** 변환기가 자동 부여(`pro_001`, `pro_002`…).
- **이름은 한글 그대로 써라**. 변환기가 ID로 바꿔준다(`로아` → `c01`, `자취방 책상` → `bg_10_06`).
- **Notes(마지막 컬럼)**는 기획 메모. 엔진 CSV에서는 자동 제거되니 자유롭게 적어도 된다.
- 다 쓰면 Unity 메뉴 **`Tools > LoveAlgo > Story > Convert 기획 CSV`** → Report에 빨간 줄(Missing) 없으면 OK.

---

## 컬럼 6개

| 컬럼 | 설명 |
|---|---|
| `LineID` | 라인 식별자. 빈 칸으로 두면 자동. Jump 대상이거나 회차별 분기 라벨이 필요할 때만 직접 입력(예: `roa_intro`). |
| `Type` | 명령 종류 (아래 치트시트 참조). |
| `Speaker` | 대사 화자. `Text`일 때만 사용. 나머지 Type은 공란. |
| `Value` | 명령의 본문/인자. Type마다 형식이 다름. |
| `Next` | 다음 라인으로 가는 방식. 비우면 변환기가 Type별 기본값으로 채움. |
| `Notes` | 기획 메모. 엔진은 무시. |

---

## Type 치트시트

`Value` 예시는 **기획자가 쓰는 한글 표기**다. 변환기가 자동으로 엔진 ID로 바꾼다.

| Type | Speaker | Value 예시 | Next 기본 | 의미 |
|---|---|---|---|---|
| **Text** | 캐릭터명 또는 공란(나레이션) | `<emote=밝게웃음/>안녕!` | `click` | 대사·지문. `<emote=한글/>` 태그로 표정 변경. |
| **Char** | (공란) | `C:Enter:로아:밝게웃음:Mob` | `click` | 캐릭터 등장. `C:Exit`로 퇴장. `C:Emote:표정:로아` 로 표정만. |
| **BG** | (공란) | `자취방 책상` 또는 `자취방 책상:Cross` | `await` | 배경 전환. 전환 효과 미지정 시 `Cut`. (`:Cross`, `:Fade:1.0` 가능) |
| **CG** | (공란) | `로아 첫만남` / `로아 첫만남:Fade:2.0` / `Close` | `await` | 풀화면 CG. `Close`로 닫기. |
| **SD** | (공란) | `다은 첫만남` / `Close` | `await` | SD 일러스트. |
| **Sound** | (공란) | `BGM:로아` / `SFX:123` / `BGM:Stop` | `>` | BGM/SFX. **`Fade:N` 같은 파라미터는 자동 제거된다** (페이드 연출이 필요하면 직접 FX로). |
| **FX** | (공란) | `Wait:0.5` / `CamShake` / `SceneEnd` | `await` | 연출 효과. |
| **Flow** | (공란) | `Jump:roa_intro` / `Save` / `End` / `Schedule` / `Day:1` / `Username` / `LockScreen:GameStart` / `Affinity:로아:+1` | `>` | 흐름 제어. |
| **Choice / Option** | (공란) | 선택지 분기 | 별도 | 선택지(Choice)와 각 보기(Option). |

**Next 값**:
- `click` = 화면 클릭으로 진행 (대사 다음 줄)
- `await` = 이 명령이 끝날 때까지 기다림 (페이드/연출)
- `>` = 즉시 다음 라인 (BGM 깔고 바로 다음 줄로)

비워두면 위 표의 기본값을 변환기가 자동으로 넣어준다.

---

## LockScreen (PC잠금 연출)

기획서 §진입 정보 + §비밀번호 시스템.

### 서브명령
| Flow 명령 | 의미 |
|---|---|
| `Flow:LockScreen:GameStart` | **CSV에 박지 말 것** — 게임 설치 후 최초 1회 진입은 `EntryRouter`가 자동 처리. CSV는 그 다음 흐름만 책임짐 |
| `Flow:LockScreen:FirstSetup` | 비번 첫 설정 — 평문 입력 (이름 입력 직후) |
| `Flow:LockScreen:Normal` | 일반 잠금/로그인 — 마스킹, 비번 검증 |
| `Flow:LockScreen:Reset` | 비번 재설정 — 새 비번 입력 흐름 |
| `Flow:LockScreen:Auto` | 비번 유무에 따라 Normal / FirstSetup 자동 판별 |

### 옵션 (콜론으로 자유 조합)
- `Time=HH:mm` — 시계 1회 오버라이드 (예: `Time=23:58`)
- `FadeOut` — outro에 페이드아웃까지 자동 포함 → 다음 라인이 자연스럽게 시작
- `NoFadeOut` — 페이드아웃 생략 (기본값과 동일)

### 동작
- Panel 표시 → 사용자가 로그인 완료 → `OnFlowComplete` → 스토리 자동 복귀.
- await로 호출하면 잠금 동안 스크립트 정지.

### 예시
```
,Flow,,LockScreen:GameStart:FadeOut,await         ← 게임 첫 진입
,Flow,,LockScreen:FirstSetup:FadeOut,await         ← 로아가 "비번 설정해줘" 직후
,Flow,,LockScreen:Normal:Time=07:30,await          ← 다음날 아침 재로그인 연출
,Flow,,LockScreen:Auto:FadeOut:Time=23:58,await    ← 시간 명시 자동 분기
```

---

## 자주 쓰는 패턴

### 1. 캐릭터 첫 등장 + 대사

```
,Char,,C:Enter:로아:밝게웃음:Mob,,
,Text,로아,<emote=밝게웃음/>안녕!,,
,Text,로아,<emote=눈웃음/>오늘 날씨 좋네.,,
,Char,,C:Exit,,
```

### 2. 배경 전환 + BGM 깔기

```
,BG,,자취방 책상:Cross,,
,Sound,,BGM:로아,,
,Text,,자취방 책상에 앉았다.,,
```

### 3. CG 띄우기 → 닫기

```
,CG,,로아 첫만남:Fade:2.0,,
,Text,,로아의 첫인상은 강렬했다.,,
,CG,,Close,,
```

### 4. 클릭 진행 vs `await`/`>` 구분

```
,Text,로아,한 줄 대사. (클릭으로 다음 진행),click,
,Sound,,BGM:Stop,>,     ← BGM 끄고 즉시 다음 줄
,FX,,Wait:1.0,await,    ← 1초 대기 후 다음 줄
,Text,,…,,
```

### 5. 회차별 엔딩 분기 (함정 주의)

Notes 메모로 "2회차용", "3회차용"이라고 적어도 **엔진 CSV에서는 사라진다**. 회차 분기가 필요하면 **LineID 라벨 + `Flow:Jump:Value:ReplayCount`** 같은 명시적 분기를 써야 한다. 분기 메타가 필요할 때는 단순 Notes로 끝내지 말고 개발자에게 알려라.

---

## 한글 이름 일람

> 출처: `Assets/Resources/Data/EmoteMap.asset`, `CharacterMetaDatabase.asset`, `Assets/_Project/Modules/Narrative/Editor/Mappings/{Bg,Cg,Sd,Sound}Map.asset`. 매핑이 추가/수정되면 이 표도 갱신할 것.

### 캐릭터 (CharacterMetaDatabase)

| 한글 이름 | ID |
|---|---|
| 로아 | c01 |
| 서다은 | c02 |
| 하예은 | c03 |
| 도희원 | c04 |
| 이봄 | c05 |

### 표정 (EmoteMap)

| 한글 | ID | 한글 | ID | 한글 | ID |
|---|---|---|---|---|---|
| 기본 | _00 | 찌릿 | _21 | 울먹 | _31 |
| 눈웃음 | _11 | 쌔쥠 | _22 | 눈물 주르륵 | _32 |
| 밝게웃음 | _12 | 머쓱 | _23 | 와아앙 울기 | _33 |
| 활짝 | _13 | 어질어질 | _24 | 부끄러워 | _34 |
| 행복 | _14 | | | 피곤/졸려 | _35 |
| 깜짝 | _41 | 반짝빠짝 | _42 | 궁금 | _43 |
| 윙크 | _44 | 자신만만 | _45 | | |

작성 형식: `<emote=밝게웃음/>` — **슬래시(`/`)와 닫는 꺾쇠(`>`)** 필수.

### 배경 (BgMap)

| 카테고리 | 한글 이름 | ID |
|---|---|---|
| 메인 | 검은 화면 | bg_00_00 |
| 메인 | 편의점 앞 밤 | bg_00_01 |
| 자취방 | 자취방 전경 낮 / 밤 / 밤 불켜기 | bg_10_01 / 02 / 03 |
| 자취방 | 자취방 침대위 아침 / 밤 | bg_10_04 / 05 |
| 자취방 | 자취방 책상 | bg_10_06 |
| 공대 | 공대 앞 낮 / 밤 | bg_20_01 / 02 |
| 공대 | 공대 강의실복도 / 학생복지실 / 강의실 낮 / 강의실 낮 벚꽃 | bg_20_03 / 04 / 05 / 06 |
| 캠퍼스 | 캠퍼스거리1 맑음 / 캠퍼스거리2 맑음 | bg_30_01 / 02 |
| 학생회관 | 학생회관_앞_낮 / _밤 / _행정실 / _복도 / _게시판 | bg_40_01 ~ 05 |
| 학생회관 | 학생회관_동방_낮_나무 / _벚꽃 | bg_40_06 / 07 |
| 편의점 | 편의점 앞 낮 / 편의점 앞 밤 | bg_60_01 / bg_60_02 |

(이외에 미등록 한글명을 쓰면 변환 시 **Missing BG**로 떨어진다. 그 경우 `BgMap.asset`에 항목을 추가하거나 개발자에게 알려라.)

### CG (CgMap)

| 한글 이름 | ID |
|---|---|
| 로아 첫만남 | cg_c01_01 |
| 예은 입부신청서 작성 | cg_c03_01 |
| 예은 입부신청서 작성 | cg_02_char_yeeun_01 |

### SD (SdMap)

| 한글 이름 | ID |
|---|---|
| 다은 첫만남 | sd_c02_01 |
| 희원 첫만남 | sd_c04_01 |
| 봄 첫만남 | sd_c05_01 |

### Sound (SoundMap)

현재 SoundMap은 비어 있다 → **`BGM:`/`SFX:` 뒤에 적은 키가 그대로 엔진 ID로 전달**된다. 사용 가능 키는 `Assets/Resources/` 하위 오디오 파일명 또는 별도 정의된 키 그대로(`BGM:로아`, `BGM:Daily1`, `BGM:Stop`, `SFX:123` 등).

---

## 하지 말 것 (실수 모음)

| ❌ 안 됨 | 왜 / 대신 |
|---|---|
| `BGM:로아:Fade:4.0` | Fade 파라미터는 자동 제거 → 페이드 연출 사라짐. 필요하면 `FX,,Wait:N,await` 조합으로. |
| `<emote=활짝웃음>` (슬래시 없음) | 변환기가 인식 못 함. `<emote=활짝웃음/>` 형태로. |
| `<emote=활짝>` 같이 표 외 한글 | EmoteMap에 없는 단어는 Missing Emote로 떨어짐. 표의 정확한 이름 사용. |
| 회차 분기를 Notes로만 표시 | Notes는 엔진 CSV에서 사라짐. 분기는 LineID 라벨 + Flow:Jump로. |
| 빈 행으로 시각 구분 | 변환 시 제거됨. 시각적 그룹이 필요하면 Notes 컬럼 활용. |
| 매핑 안 된 한글 BG/CG/SD | 조용히 통과되지 않음 — Missing 보고로 뜸. SO에 추가하거나 개발자에게 요청. |
| 멀티라인 셀 안에 따옴표 묶어서 `"..."` 사용 | 가능하긴 하지만 `\n` 직접 입력으로 통일 권장 (한 줄 = 한 라인). |

---

## 변환 실행

1. 원본 작성: `Assets/_Project/Modules/Narrative/Art/Story/프롤로그(기획).csv` (기본 경로) 또는 다른 챕터 CSV.
2. Unity 메뉴 **`Tools > LoveAlgo > Story > Convert 기획 CSV`** 클릭.
3. Report 영역 확인:
   - **Missing Emote/BG/CG/SD/Character** 0건이면 OK.
   - 각 항목은 `[행 N] 종류: 토큰 — "원문 발췌…"` 형식으로 표시 → 해당 행으로 가서 수정.
4. 다시 Convert → 깨끗하면 완료.

자동 백업: 변환 전 기존 엔진 CSV가 `*.bak.csv`로 보존된다.

---

## 문서 갱신 책임

- 매핑 SO에 항목을 추가했으면 이 문서의 "한글 이름 일람"도 함께 갱신.
- 코드 측 Type/Next 기본값이 바뀌면 "Type 치트시트" 표도 동기화.
- 변환 규칙(예: 한글 자동 매핑 정책)이 바뀌면 "30초 요약"부터 다시 검토.
