# CSV 스크립트 문법 참조

> 스토리 CSV 파일의 구조와 문법 전체 레퍼런스

---

## CSV 컬럼 구조

```csv
LineID,Type,Speaker,Value,Next
```

| 컬럼 | 필수 | 설명 |
|------|------|------|
| `LineID` | ❌ | 점프 대상(앵커)에만 사용. 나머지는 빈 값 |
| `Type` | ✅ | 라인 종류 |
| `Speaker` | ❌ | Text 타입의 화자. 나머지 Type은 빈 값 |
| `Value` | ✅ | Type별 데이터 |
| `Next` | ✅ | 다음 라인으로의 진행 방식 |

---

## Next (진행 방식)

| 값 | 의미 | 사용 예 |
|----|------|---------|
| `>` | 즉시 다음 라인 (대기 없음) | 연출 연속 실행 |
| `click` | 플레이어 입력 대기 | 대사 끝 |
| `0.5` | N초 후 자동 진행 | 자동 대사/연출 타이밍 |
| `await` | 현재 액션 완료 대기 | 페이드/트윈 완료 후 진행 |

### 동시 연출 패턴 (권장)

여러 액션을 **동시에** 보이고 싶을 때는 마지막 행만 `await`/`click`/숫자로 두고
앞 행은 `>`로 연결한다. 그러면 앞 액션이 시작되자마자 다음 행이 발사되어 시각적으로 같이 진행된다.

```csv
# ❌ 부자연스러움 — 표정 → 배경 → 흔들림이 순차적으로 일어남
,Char,,C:Emote:Glare:Roa_PC_Negative,await
,BG,,VirtualSpace_Day:Fade:1.0,await
,FX,,CharShake:C:15:0.3,await
,Text,로아,어 지금 재미없는 잔소리 했다고 생각했지!,click

# ✅ 자연스러움 — 셋이 동시에 시작하고, 흔들림이 끝난 후 대사
,Char,,C:Emote:Glare:Roa_PC_Negative,>
,BG,,VirtualSpace_Day:Fade:1.0,>
,FX,,CharShake:C:15:0.3,await
,Text,로아,어 지금 재미없는 잔소리 했다고 생각했지!,click
```

> 참고: `C:Emote:표정:오버레이`처럼 한 행에 표정 + 오버레이를 함께 적으면
> 엔진이 내부적으로 두 동작을 자동으로 동시에 실행한다 (별도 행으로 나누지 않아도 된다).

---

## Type 레퍼런스

### Text (대사/나레이션)

| Speaker | Value | 설명 |
|---------|-------|------|
| `로아` | `안녕!` | 캐릭터 대사 |
| (빈 값) | `나레이션입니다.` | 나레이션 |

```csv
,Text,로아,안녕!,click
,Text,,햇살이 눈부셨다.,click
```

#### 인라인 태그

| 태그 | 설명 | 예시 |
|------|------|------|
| `<wait=0.5/>` | 0.5초 대기 | `잠깐...<wait=0.5/>뭐지?` |
| `<sfx=Knock/>` | 효과음 재생 | `<sfx=Knock/>누구세요?` |
| `<emote=Happy/>` | 표정 변경 | `<emote=Happy/>고마워!` |
| `<speed=0.5>...</speed>` | 타이핑 속도 | `<speed=0.3>천천히...</speed>` |
| `<size=1.5>...</size>` | 글자 크기 (TMP) | `<size=2>크게!</size>` |
| `<color=red>...</color>` | 글자 색상 (TMP) | `<color=#FF0000>빨강</color>` |

#### 변수 치환

`{{변수명}}` 형식으로 런타임 치환. 예: `{{PlayerName}}`

---

### Char (캐릭터 제어)

형식: `슬롯:액션:대상[:옵션]`

| 슬롯 | 위치 |
|------|------|
| `L` | 왼쪽 |
| `C` | 중앙 |
| `R` | 오른쪽 |

| 액션 | Value 예시 |
|------|------------|
| `Enter` | `C:Enter:Roa`, `L:Enter:SeoDaEun:Happy` |
| `Emote` | `C:Emote:Sad`, `R:Emote:Blush` |
| `Exit` | `C:Exit`, `L:Exit` |

```csv
,Char,,C:Enter:Roa,>
,Char,,C:Emote:Happy,>
,Char,,C:Exit,await
```

---

### BG (배경)

형식: `배경이름[:전환타입:시간]`

| 전환타입 | 설명 |
|----------|------|
| `Cut` | 즉시 교체 |
| `Fade` | 페이드 (기본) |
| `Cross` | 크로스페이드 |

```csv
,BG,,School_Day,>
,BG,,Cafe_Night:Fade:1.5,await
,BG,,Rooftop_Sunset:Cross:2.0,await
```

---

### CG (CG 이미지)

형식: `CG이름[:전환타입:시간]` 또는 `Exit[:시간]`

CG 표시 시 캐릭터 자동 퇴장 + 대사창 자동 숨김.
CG 종료(Exit) 시 대사창 자동 복원.

```csv
,CG,,CG/Roa_FirstMeet:Fade:1.0,click
,FX,,DialogueShow,>
,Text,로아,이 순간을 기억해줘.,click
,FX,,DialogueHide,>
,CG,,Exit:1.0,await
```

> CG 중 대사를 보려면 `FX:DialogueShow`를 먼저 실행

---

### Overlay (보조 배경)

형식: `이름:FadeIn[:시간[:투명도]]` 또는 `FadeOut[:시간]`

용도: 캐릭터별 테마 배경, 특수 분위기

```csv
,Overlay,,Roa_Theme:FadeIn:0.5,await
,Overlay,,FadeOut:0.5,await
```

- 투명도 기본값: 0.7 (70%)
- 리소스: `Resources/Overlays/이름` 우선, 없으면 `Resources/Backgrounds/이름`

---

### Sound (오디오)

형식: `카테고리:이름[:옵션]`

| 카테고리 | Value 예시 |
|----------|------------|
| `BGM` | `BGM:Morning`, `BGM:Stop`, `BGM:Stop:Fade:1.0` |
| `SFX` | `SFX:Knock`, `SFX:Heartbeat` |
| `Voice` | `Voice:Roa_01` |

---

### FX (시각 효과)

형식: `효과명[:인자1:인자2:...]`

| 효과 | Value 예시 |
|------|------------|
| `FadeOut` | `FadeOut:1.0` |
| `FadeIn` | `FadeIn:1.0` |
| `Flash` | `Flash` |
| `EyeOpen` | `EyeOpen:1.5` |
| `EyeClose` | `EyeClose:1.0` |
| `EyeCloseImmediate` | `EyeCloseImmediate` |
| `EyeBlink` | `EyeBlink:0.3:0.5:0.2` |
| `CamShake` | `CamShake:0.5:30`, `CamShake:Medium` |
| `CamZoom` | `CamZoom:1.3:0.5` (1.3배, 0.5초), `CamZoom:1.0:0.3` (복귀) |
| `CamPan` | `CamPan:100:0:0.5` (오른쪽 100px), `CamPan:0:0:0.3` (원점) |
| `CamReset` | `CamReset:0.4` (줌+팬 동시 원점 복귀) |
| `ColorTint` | `ColorTint:Sepia:0.3:0.5`, `ColorTint:Clear::0.3` |
| `DialogueHide` | `DialogueHide` |
| `DialogueShow` | `DialogueShow` |

#### FX 매크로 (복합 연출)

| 매크로 | 설명 | Value 예시 |
|--------|------|------------|
| `DayEnd` | 하루 종료 (페이드아웃→정리→자동저장) | `DayEnd`, `DayEnd:1.5` |
| `DayStart` | 하루 시작 (일차+1, 배경) | `DayStart:MyRoom_Day`, `DayStart:MyRoom_Day:3` |
| `Setup` | 즉시 장면 세팅 (검은 화면 뒤에서) | `Setup:BG=School_Day\|BGM=Morning` |

---

### Flow (흐름 제어)

| 명령 | Value 예시 |
|------|------------|
| `Jump` | `Jump:Study_Start` |
| `End` | `End` |
| `Save` | `Save` |
| `Day` | `Day:2` (CurrentDay 강제 설정 — 스케줄 UI 일차 표시용) |
| `Schedule` | `Schedule` (인라인 스케줄 — 1회 선택 후 스토리 복귀) |
| `Username` | `Username` (인라인 이름 입력 — 입력/확인 후 스토리 복귀, GameManager·GameState에 즉시 반영) |
| `If` | `If:Love:Roa>=30:Confession` |
| `MiniGame` | `MiniGame:CherryBlossom:Roa` |

#### 조건 문법 (`If:조건:점프대상`)

| 조건 패턴 | 예시 |
|-----------|------|
| `Love:캐릭터>=값` | `If:Love:Roa>=30:Confession` |
| `Stat:스탯>=값` | `If:Stat:Int>=20:SmartChoice` |
| `Flag:이름` | `If:Flag:Met_Roa:Reunion` |
| `!Flag:이름` | `If:!Flag:Confessed:FirstMeet` |

비교 연산자: `>=`, `<=`, `>`, `<`, `==`

#### 미니게임 (`MiniGame:게임이름:히로인ID`)

| 게임 | 설명 |
|------|------|
| `CherryBlossom` | 벚꽃 꽃잎 잡기 (30초) |
| `Jogging` | 하예은 조깅 속도 맞추기 (60초) |

점수 변환: `0→0, 10→+1, 20→+2, 30→+3` (히로인당 최대 5점)

---

### Choice / Option (선택지)

`Choice` — 선택지 시작 (Value 빈 값)
`Option` — 형식: `버튼텍스트|점프대상|효과1|효과2|...|조건`

#### Option 파싱 규칙

1. **1번째**: 버튼텍스트 (필수)
2. **2번째**: 점프대상 LineID (필수)
3. **3번째~**: 효과 또는 조건
   - `if:` 접두사 → 조건 (마지막에 위치)
   - 그 외 → 효과 (복수 가능)

#### 사용 가능한 효과

| 형식 | 설명 | 예시 |
|------|------|------|
| `Love:캐릭터:값` | 호감도 증감 | `Love:Roa:5` |
| `Stat:스탯:값` | 스탯 증감 | `Stat:Int:1` |
| `Flag:이름:값` | 플래그 설정 | `Flag:Met_Roa:true` |
| `Money:값` | 소지금 증감 | `Money:5000` |
| `SFX:이름` | 효과음 | `SFX:Heartbeat` |

스탯 종류: `Str`, `Int`, `Soc`, `Per`, `Fatigue`

#### 표시 조건

`if:` 접두사. 미충족 시 선택지 숨김.

| 형식 | 예시 |
|------|------|
| `if:Love:캐릭터>=값` | `if:Love:Roa>=30` |
| `if:Stat>=값` | `if:Int>=20` |
| `if:Flag:이름` | `if:Flag:Met_Roa` |
| `if:!Flag:이름` | `if:!Flag:Confessed` |

#### 종합 예시

```csv
,Text,로아,오늘 뭐 할까?,>
,Choice,,,click
,Option,,공부하자|Study_Start|Love:SeoDaEun:1,
,Option,,놀러가자|Play_Start|Love:LeeBom:1,
,Option,,자자...|Sleep_Start|Fatigue:-10|if:Fatigue>=50,
,Option,,고백한다|Confess|Love:Roa:10|SFX:Heartbeat|if:Love:Roa>=30,
```

---

## 주석

`#`으로 시작하는 줄은 주석.

```csv
# ═══ 1일차 아침 ═══
Morning_Start,BG,,MyRoom_Day,>
```

---

## 파일 구조

```
Resources/Story/
├── Prologue.csv
├── Day{N}_Morning.csv       # 아침 이벤트
├── Day{N}_Evening.csv       # 저녁 이벤트
├── Event{1~3}.csv           # 개인 이벤트 (Day 6,16,26)
├── Festival_Day{1~3}.csv    # 축제 (Day 10~12)
├── MT_Day{1~3}.csv          # MT (Day 20~22)
└── Ending_*.csv             # 엔딩 (Normal + 5히로인×Happy/Sad)
```

---

## 전체 예시

```csv
LineID,Type,Speaker,Value,Next
# ═══ 1일차 아침 - 등교 ═══

Morning_Start,BG,,MyRoom_Day:Fade:1.5,await
,Sound,,BGM:Morning,>
,Text,,눈을 떴다. 새 학기 첫날이다.,click

,Char,,C:Enter:Roa,await
,Text,로아,{{PlayerName}}! 좋은 아침!,click
,Char,,C:Emote:Happy,>
,Text,로아,오늘부터 같이 등교하자!,click

,Text,,어떻게 대답할까?,>
,Choice,,,click
,Option,,좋아! 같이 가자|Accept|Love:Roa:2,
,Option,,아직 졸려...|Decline|Love:Roa:-1,

Accept,Char,,C:Emote:Happy,>
,Sound,,SFX:Sparkle,>
,Text,로아,<emote=Happy/>정말? 신난다!,click
,Flow,,Jump:GoSchool,>

Decline,Char,,C:Emote:Sad,>
,Text,로아,그...그래...,click
,Flow,,Jump:GoSchool,>

GoSchool,FX,,FadeOut:1.0,await
,BG,,School_Gate:Cut,>
,FX,,FadeIn:1.0,await
,Text,,학교에 도착했다.,click
,Flow,,End,>
```
