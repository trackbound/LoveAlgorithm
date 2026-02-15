# LoveAlgo 스크립트 스키마 v2

> 스토리 CSV 파일의 구조와 문법 정의

## 개요

스토리는 **단일 CSV 파일**에서 대사, 연출, 선택지를 모두 관리합니다.
기존의 LineTable + ChoiceTable 분리 구조를 통합하여 작성 및 관리가 용이합니다.

---

## CSV 컬럼 구조

```csv
LineID,Type,Speaker,Value,Next
```

| 컬럼 | 필수 | 설명 |
|------|------|------|
| `LineID` | ❌ | 점프 대상(앵커)에만 사용. 나머지는 빈 값 |
| `Type` | ✅ | 라인 종류 (Text, Char, BG, Sound, FX, Flow, Choice, Option) |
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

#### 인라인 태그 (XML 스타일)

대사 텍스트 안에서 사용. TMP 리치 텍스트와 통합.

| 태그 | 설명 | 예시 |
|------|------|------|
| `<wait=0.5/>` | 0.5초 대기 | `잠깐...<wait=0.5/>뭐지?` |
| `<sfx=Knock/>` | 효과음 재생 | `<sfx=Knock/>누구세요?` |
| `<emote=Happy/>` | 표정 변경 | `<emote=Happy/>고마워!` |
| `<speed=0.5>...</speed>` | 타이핑 속도 | `<speed=0.3>천천히...</speed>` |
| `<size=1.5>...</size>` | 글자 크기 (TMP) | `<size=2>크게!</size>` |
| `<color=red>...</color>` | 글자 색상 (TMP) | `<color=#FF0000>빨강</color>` |

#### 변수 치환

```csv
,Text,로아,{{PlayerName}}! 반가워!,click
```

`{{변수명}}` 형식으로 런타임에 치환.

---

### Char (캐릭터 제어)

형식: `슬롯:액션:대상[:옵션]`

| 슬롯 | 의미 |
|------|------|
| `L` | 왼쪽 |
| `C` | 중앙 |
| `R` | 오른쪽 |

| 액션 | 설명 | Value 예시 |
|------|------|------------|
| `Enter` | 등장 | `C:Enter:Roa`, `L:Enter:Daeun:Happy` |
| `Emote` | 표정 변경 | `C:Emote:Sad`, `R:Emote:Blush` |
| `Exit` | 퇴장 | `C:Exit`, `L:Exit` |

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

**CG 표시 시 자동 처리:**
- 캐릭터 자동 퇴장
- 대사창 자동 숨김

**CG 종료(Exit) 시:**
- 대사창 자동 표시

| 명령 | 설명 | Value 예시 |
|------|------|------------|
| `CG이름` | CG 페이드인 표시 | `CG/Roa_FirstMeet:Fade:1.0` |
| `Exit` | CG 페이드아웃 숨김 | `Exit`, `Exit:1.5` |

```csv
# CG 표시 (캐릭터 퇴장, 대사창 숨김)
,CG,,CG/Roa_FirstMeet:Fade:1.0,click

# 클릭 시 대사 진행 (대사창은 숨겨진 상태)
,FX,,DialogueShow,>
,Text,로아,이 순간을 기억해줘.,click
,FX,,DialogueHide,>

# CG 종료 (대사창 복원)
,CG,,Exit:1.0,await
```

> 💡 **Tip**: CG 표시 중 대사를 보여주려면 `FX:DialogueShow`를 먼저 실행하세요.

---

### Overlay (보조 배경)

형식: `이름:FadeIn[:시간[:투명도]]` 또는 `FadeOut[:시간]`

**용도:**
- 캐릭터별 테마 배경 (로아와 대화 시 로아 테마)
- 특수 분위기 오버레이 (꿈, 회상 등)

| 명령 | 설명 | Value 예시 |
|------|------|------------|
| `이름:FadeIn` | 오버레이 표시 | `Roa_Theme:FadeIn`, `Roa_Theme:FadeIn:0.5:0.7` |
| `FadeOut` | 오버레이 숨김 | `FadeOut`, `FadeOut:0.5` |

```csv
# 로아 대화 시작 - 테마 배경 페이드인
,Overlay,,Roa_Theme:FadeIn:0.5,await
,Char,,C:Enter:Roa,await
,Text,로아,안녕!,click

# 대화 끝 - 테마 배경 페이드아웃
,Char,,C:Exit,await
,Overlay,,FadeOut:0.5,await
```

**투명도 파라미터:**
- 기본값: 0.7 (70%)
- 0.0 = 완전 투명, 1.0 = 완전 불투명
- 예: `Roa_Theme:FadeIn:0.5:0.5` → 0.5초에 50% 투명도

**리소스 경로:**
- `Resources/Overlays/이름` 우선 로드
- 없으면 `Resources/Backgrounds/이름` 에서 로드

---

### Sound (오디오)

형식: `카테고리:이름[:옵션]`

| 카테고리 | 설명 | Value 예시 |
|----------|------|------------|
| `BGM` | 배경음악 | `BGM:Morning`, `BGM:Stop`, `BGM:Stop:Fade:1.0` |
| `SFX` | 효과음 | `SFX:Knock`, `SFX:Heartbeat` |
| `Voice` | 보이스 | `Voice:Roa_01` |

```csv
,Sound,,BGM:Morning,>
,Sound,,SFX:DoorOpen,>
,Sound,,BGM:Stop,await
```

---

### FX (시각 효과)

형식: `효과명[:인자1:인자2:...]`

| 효과 | 설명 | Value 예시 |
|------|------|------------|
| `FadeOut` | 화면 어둡게 (대사창도 숨김) | `FadeOut:1.0` |
| `FadeIn` | 화면 복귀 | `FadeIn:1.0` |
| `Flash` | 화면 번쩍임 | `Flash` |
| `EyeOpen` | 눈 뜨는 효과 (위아래 검은 바 열림) | `EyeOpen:1.5` |
| `EyeClose` | 눈 감는 효과 (위아래 검은 바 닫힘) | `EyeClose:1.0` |
| `EyeCloseImmediate` | 즉시 눈 감기 (애니메이션 없이) | `EyeCloseImmediate` |
| `EyeBlink` | 눈 깜빡임 (닫힘→대기→열림) | `EyeBlink:0.3:0.5:0.2` |
| `CamShake` | 카메라 흔들림 | `CamShake:0.5:30`, `CamShake:Medium` |
| `DialogueHide` | 대사창 숨김 | `DialogueHide` |
| `DialogueShow` | 대사창 표시 | `DialogueShow` |

#### FX 매크로 (복합 연출)

| 매크로 | 설명 | Value 예시 |
|--------|------|------------|
| `DayEnd` | 하루 종료 연출 (페이드아웃→무대 정리→자동저장) | `DayEnd`, `DayEnd:1.5` |
| `DayStart` | 하루 시작 연출 (일차+1, 배경 설정) | `DayStart:MyRoom_Day`, `DayStart:MyRoom_Day:3` |
| `Setup` | 즉시 장면 세팅 (검은 화면 뒤에서) | `Setup:BG=School_Day\|BGM=Morning` |

```csv
,FX,,FadeOut:1.5,await
,FX,,EyeOpen:1.5,await
,FX,,CamShake:0.3:20,>
,FX,,EyeBlink:0.3:0.5,await
,FX,,DayEnd,await
,FX,,DayStart:MyRoom_Day,>
,FX,,Setup:BG=School_Day|BGM=Morning,>
```

---

### Flow (흐름 제어)

| 명령 | 설명 | Value 예시 |
|------|------|------------|
| `Jump` | 라인 이동 | `Jump:Study_Start` |
| `End` | 챕터/스토리 종료 | `End` |
| `Save` | 자동 저장 | `Save` |
| `If` | 조건 분기 | `If:Love:Roa>=30:Confession` |
| `MiniGame` | 미니게임 실행 | `MiniGame:CherryBlossom:Roa` |

```csv
,Flow,,Jump:Morning_End,>
,Flow,,If:Flag:Met_Roa:Roa_Route,>
,Flow,,MiniGame:CherryBlossom:Roa,>
,Flow,,End,>
```

#### 조건 문법

`If:조건:점프대상`

| 조건 패턴 | 의미 | 예시 |
|-----------|------|------|
| `Love:캐릭터>=값` | 호감도 | `If:Love:Roa>=30:Confession` |
| `Stat:스탯>=값` | 스탯 | `If:Stat:Int>=20:SmartChoice` |
| `Flag:이름` | 플래그 true | `If:Flag:Met_Roa:Reunion` |
| `!Flag:이름` | 플래그 false | `If:!Flag:Confessed:FirstMeet` |

#### 미니게임 문법

`MiniGame:게임이름:히로인ID`

| 게임이름 | 설명 |
|---------|------|
| `CherryBlossom` | 벚꽃 꽃잎 잡기 (30초) |
| `Jogging` | 하예은과 조깅 속도 맞추기 (60초) |

점수 → 포인트 변환: `0→0, 10→+1, 20→+2, 30→+3` (히로인당 최대 5점)

---

### Choice / Option (선택지)

#### Choice
선택지 시작을 알림. Value는 빈 값.

#### Option
선택지 항목. 형식: `버튼텍스트|점프대상|효과1|효과2|...|조건`

| 파트 | 필수 | 설명 |
|------|------|------|
| 버튼텍스트 | ✅ | 선택지에 표시될 텍스트 |
| 점프대상 | ✅ | 선택 시 이동할 LineID |
| 효과 | ❌ | 선택 시 적용 (복수 가능) |
| 조건 | ❌ | 표시 조건 (`if:` 접두사) |

```csv
,Text,로아,오늘 뭐 할까?,>
,Choice,,,click
,Option,,공부하자|Study_Start|Love:Daeun:1,
,Option,,놀러가자|Play_Start|Love:Bom:1,
,Option,,자자...|Sleep_Start|Fatigue:-10|if:Fatigue>=50,
```

#### Option 파싱 규칙

`|` 구분자로 분리 후:
1. **1번째**: 버튼텍스트 (필수)
2. **2번째**: 점프대상 LineID (필수)
3. **3번째~**: 효과 또는 조건
   - `if:` 접두사 → 조건 (마지막에 위치)
   - 그 외 → 효과 (복수 가능)

#### 사용 가능한 효과 (Effect)

| 효과 | 형식 | 설명 | 예시 |
|------|------|------|------|
| 호감도 | `Love:캐릭터:값` | 캐릭터 호감도 증감 | `Love:Roa:5`, `Love:Daeun:-3` |
| 스탯 | `Stat:스탯:값` | 플레이어 스탯 증감 | `Stat:Int:1`, `Stat:Fatigue:-10` |
| 플래그 | `Flag:이름:값` | 플래그 설정 | `Flag:Met_Roa:true`, `Flag:Confessed:true` |
| 돈 | `Money:값` | 소지금 증감 | `Money:5000`, `Money:-1000` |
| 효과음 | `SFX:이름` | 즉시 효과음 재생 | `SFX:Heartbeat`, `SFX:Coin` |

**스탯 종류**: `Str`(체력), `Int`(지성), `Soc`(사교성), `Per`(끈기), `Fatigue`(피로)

#### 표시 조건 (Condition)

`if:` 접두사로 시작. 조건 미충족 시 선택지 숨김.

| 조건 | 형식 | 설명 | 예시 |
|------|------|------|------|
| 호감도 | `if:Love:캐릭터>=값` | 호감도 비교 | `if:Love:Roa>=30` |
| 스탯 | `if:Stat>=값` | 스탯 비교 | `if:Int>=20`, `if:Fatigue>=50` |
| 플래그 | `if:Flag:이름` | 플래그 true | `if:Flag:Met_Roa` |
| 플래그 부정 | `if:!Flag:이름` | 플래그 false | `if:!Flag:Confessed` |

**비교 연산자**: `>=`, `<=`, `>`, `<`, `==`

#### Option 예시

```csv
# 기본 (효과 없음)
,Option,,그냥 가자|GoSchool,

# 효과 1개
,Option,,공부하자|Study|Stat:Int:1,

# 효과 여러 개
,Option,,열심히 공부!|Study|Stat:Int:2|Stat:Fatigue:5|Love:Daeun:1,

# 조건만 (효과 없음)
,Option,,자자...|Sleep|if:Fatigue>=50,

# 효과 + 조건
,Option,,고백한다|Confess|Love:Roa:10|SFX:Heartbeat|if:Love:Roa>=30,
```

---

## 주석

`#`으로 시작하는 줄은 주석으로 처리됩니다.

```csv
# ═══ 1일차 아침 ═══
Morning_Start,BG,,MyRoom_Day,>
```

---

## 파일 구조

```
Story/
├── Day01/
│   ├── D01_Morning.csv
│   ├── D01_Afternoon.csv
│   └── D01_Night.csv
├── Day02/
│   └── ...
├── Routes/
│   ├── Roa_Route.csv
│   └── Daeun_Route.csv
└── Common/
    └── Endings.csv
```

---

## 전체 예시

```csv
LineID,Type,Speaker,Value,Next
# ═══ 1일차 아침 - 등교 ═══

# 장면 설정
Morning_Start,BG,,MyRoom_Day:Fade:1.5,await
,Sound,,BGM:Morning,>

# 나레이션
,Text,,눈을 떴다. 새 학기 첫날이다.,click

# 로아 등장
,Char,,C:Enter:Roa,await
,Text,로아,{{PlayerName}}! 좋은 아침!,click
,Char,,C:Emote:Happy,>
,Text,로아,오늘부터 같이 등교하자!,click

# 선택지
,Text,,어떻게 대답할까?,>
,Choice,,,click
,Option,,좋아! 같이 가자|Accept|Love:Roa:2,
,Option,,아직 졸려...|Decline|Love:Roa:-1,

# 수락 루트
Accept,Char,,C:Emote:Happy,>
,Sound,,SFX:Sparkle,>
,Text,로아,<emote=Happy/>정말? 신난다!,click
,Flow,,Jump:GoSchool,>

# 거절 루트
Decline,Char,,C:Emote:Sad,>
,Text,로아,그...그래...,click
,Flow,,Jump:GoSchool,>

# 합류
GoSchool,FX,,FadeOut:1.0,await
,BG,,School_Gate:Cut,>
,FX,,FadeIn:1.0,await
,Text,,학교에 도착했다.,click
,Flow,,End,>
```
