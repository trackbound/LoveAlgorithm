# LoveAlgo 아키텍처 문서

> 📖 이 문서는 AI 에이전트와 개발자가 프로젝트 구조를 빠르게 이해할 수 있도록 작성되었습니다.

---

## 📋 목차

1. [개요](#1-개요)
2. [시스템 계층도](#2-시스템-계층도)
3. [핵심 모듈](#3-핵심-모듈)
4. [데이터 흐름](#4-데이터-흐름)
5. [클래스 관계도](#5-클래스-관계도)
6. [게임 페이즈 흐름](#6-게임-페이즈-흐름)
7. [스토리 실행 파이프라인](#7-스토리-실행-파이프라인)
8. [폴더 구조](#8-폴더-구조)
9. [의존성 맵](#9-의존성-맵)

---

## 1. 개요

**LoveAlgo**는 대학 캠퍼스 배경의 연애 어드벤처 시뮬레이션 게임입니다.

### 기술 스택
| 항목 | 기술 |
|------|------|
| 엔진 | Unity |
| 비동기 | Cysharp UniTask |
| 애니메이션 | DOTween Pro |
| UI 텍스트 | TextMesh Pro |
| 직렬화 | Newtonsoft JSON.NET |

### 핵심 구성 요소
```
┌─────────────────────────────────────────────────────────────────┐
│                        LoveAlgo Game                            │
├─────────────────────────────────────────────────────────────────┤
│  [Core]         [Story]         [Schedule]      [UI]            │
│  게임 흐름       스토리 연출      스케줄 시스템   사용자 인터페이스 │
│  전역 상태       CSV 스크립트     스탯 관리       팝업 시스템       │
│  화면 효과       대사/선택지      미니게임        메인 UI 전환      │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. 시스템 계층도

```
┌─────────────────────────────────────────────────────────────────────┐
│                         APPLICATION LAYER                           │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                      GameManager                             │   │
│  │           (게임 전체 흐름 제어, Phase 관리)                    │   │
│  └─────────────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────────┤
│                         PRESENTATION LAYER                          │
│  ┌──────────────────┐  ┌──────────────────┐  ┌─────────────────┐   │
│  │    UIManager     │  │   PopupManager   │  │   StageManager  │   │
│  │  (메인 UI 전환)   │  │   (팝업 관리)    │  │  (연출 레이어)   │   │
│  └──────────────────┘  └──────────────────┘  └─────────────────┘   │
├─────────────────────────────────────────────────────────────────────┤
│                          DOMAIN LAYER                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────┐    │
│  │  ScriptRunner   │  │   GameState     │  │   ScheduleUI     │    │
│  │ (스토리 실행기)  │  │ (게임 상태 관리) │  │ (스케줄 선택)    │    │
│  └─────────────────┘  └─────────────────┘  └──────────────────┘    │
├─────────────────────────────────────────────────────────────────────┤
│                        INFRASTRUCTURE LAYER                         │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────┐    │
│  │  ScriptParser   │  │   SaveManager   │  │   AudioManager   │    │
│  │  (CSV 파싱)      │  │  (세이브/로드)   │  │   (오디오)       │    │
│  └─────────────────┘  └─────────────────┘  └──────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. 핵심 모듈

### 3.1 Core 모듈 (`LoveAlgo.Core`)

| 클래스 | 역할 | 싱글톤 |
|--------|------|--------|
| `GameManager` | 게임 전체 흐름 제어, Phase 전환 | ✅ |
| `StageManager` | 연출 레이어 접근 (배경/캐릭터) | ✅ |
| `ScreenFX` | 화면 효과 (Fade, Flash, Shake) | ✅ |
| `NameValidator` | 플레이어 이름 유효성 검증 | Static |

```
GameManager
├── CurrentPhase: GamePhase     # 현재 게임 상태
├── CurrentDay: int             # 현재 일차
├── RemainingActions: int       # 남은 행동 수
├── PlayerName: string          # 플레이어 이름
│
├── ChangePhase(GamePhase)      # Phase 전환
├── StartNewGame()              # 새 게임 시작
├── OnNameConfirmed(string)     # 이름 입력 완료
├── OnScheduleCompleted()       # 스케줄 완료
├── AutoSave() / Save(slot)     # 저장
└── LoadGame(slot)              # 로드
```

### 3.2 Story 모듈 (`LoveAlgo.Story`)

| 클래스 | 역할 | 싱글톤 |
|--------|------|--------|
| `ScriptRunner` | CSV 스크립트 실행기 | ✅ |
| `ScriptParser` | CSV → ScriptLine 파싱 | Static |
| `ScriptLine` | CSV 한 줄 데이터 구조 | - |
| `GameState` | 호감도/스탯/플래그 관리 | ✅ |
| `DialogueUI` | 대사창 UI (타이핑 효과) | - |
| `ChoiceUI` | 선택지 UI | - |
| `BackgroundLayer` | 배경 전환 (Fade/Cross) | - |
| `CharacterLayer` | 캐릭터 3슬롯(L/C/R) 관리 | - |
| `CharacterSlot` | 개별 캐릭터 슬롯 | - |
| `AudioManager` | BGM/SFX/Voice 재생 | ✅ |
| `SaveManager` | JSON 세이브/로드 | Static |

```
ScriptRunner (스토리 실행의 핵심)
├── lines: List<ScriptLine>         # 파싱된 스크립트
├── lineIndex: Dict<string,int>     # LineID → 인덱스
├── currentIndex: int               # 현재 실행 위치
│
├── StartScript(scriptName)         # Resources에서 로드 후 실행
├── Run()                           # 실행 시작
├── RunFrom(lineId)                 # 특정 위치부터 실행
├── OnClick()                       # 클릭 입력 처리
├── Rewind(textCount)               # N줄 되감기
│
├── ExecuteTextAsync()              # Text 라인 실행
├── ExecuteCharAsync()              # Char 라인 실행
├── ExecuteBGAsync()                # BG 라인 실행
├── ExecuteSoundAsync()             # Sound 라인 실행
├── ExecuteFXAsync()                # FX 라인 실행
├── ExecuteFlowAsync()              # Flow 라인 실행
└── ExecuteChoiceAsync()            # Choice/Option 실행
```

### 3.3 Schedule 모듈 (`LoveAlgo.Schedule`)

| 클래스 | 역할 |
|--------|------|
| `ScheduleUI` | 스케줄 선택 UI (3탭 × 3버튼) |
| `ScheduleSlot` | 개별 스케줄 버튼 |
| `ScheduleType` | 스케줄 종류 Enum (9종) |
| `ScheduleTable` | 스케줄 효과 데이터 테이블 |
| `StatGauge` | 스탯 게이지 UI |

### 3.4 UI 모듈 (`LoveAlgo.UI`)

| 클래스 | 역할 | 싱글톤 |
|--------|------|--------|
| `UIManager` | 메인 UI 전환 (Dialogue/Schedule/Title/Username) | ✅ |
| `PopupManager` | 팝업 관리 (Modal/Top 레이어) | ✅ |
| `ModalPopupBase` | 모달 팝업 베이스 클래스 | - |
| `TitleUI` | 타이틀 화면 | - |
| `UsernameUI` | 이름 입력 화면 | - |
| `SaveLoadPopup` | 세이브/로드 팝업 | - |
| `ConfirmPopup` | 확인/취소 팝업 | - |
| `AlertPopup` | 알림 팝업 | - |
| `ToastPopup` | 토스트 메시지 | - |
| `LogPopup` | 대사 로그 팝업 | - |
| `SettingsPopup` | 설정 팝업 | - |

### 3.5 MiniGame 모듈 (`LoveAlgo.MiniGame`)

| 클래스 | 역할 |
|--------|------|
| `MiniGameBase` | 미니게임 베이스 클래스 |
| `CherryBlossomGame` | 벚꽃 미니게임 |
| `JoggingGame` | 조깅 미니게임 |

---

## 4. 데이터 흐름

### 4.1 게임 상태 흐름

```
┌─────────────────────────────────────────────────────────────┐
│                      GameState (싱글톤)                      │
├─────────────────────────────────────────────────────────────┤
│  playerName: string                                         │
│  ─────────────────────────────────────                     │
│  STATS (5종):                                               │
│  ├─ strength (체력)                                         │
│  ├─ intelligence (지성)                                     │
│  ├─ sociability (사교성)                                    │
│  ├─ perseverance (끈기)                                     │
│  └─ fatigue (피로)                                          │
│  ─────────────────────────────────────                     │
│  lovePoints: Dict<캐릭터, 호감도>                            │
│  flags: Dict<플래그명, bool>                                 │
│  money: int                                                 │
├─────────────────────────────────────────────────────────────┤
│  GetStat(name) / AddStat(name, value)                       │
│  GetLove(char) / AddLove(char, value)                       │
│  GetFlag(name) / SetFlag(name, value)                       │
│  EvaluateCondition(condition) → bool                        │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
         ┌─────────────────────────────────┐
         │         SaveManager (Static)    │
         │  ─────────────────────────────  │
         │  Save(slot, phase, day, ...)    │
         │  Load(slot) → SaveData          │
         │  ApplyToGameState(SaveData)     │
         └─────────────────────────────────┘
                           │
                           ▼
              ┌────────────────────────┐
              │   JSON 파일 (Saves/)   │
              │   save_00.json (Auto)  │
              │   save_01~29.json      │
              └────────────────────────┘
```

### 4.2 스토리 스크립트 흐름

```
┌───────────────────┐
│  CSV 스크립트     │   Resources/Story/Prologue.csv
│  (TextAsset)      │
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│  ScriptParser     │   Parse(csv) → List<ScriptLine>
│                   │   BuildLineIndex() → Dict<LineID, index>
└─────────┬─────────┘
          │
          ▼
┌───────────────────┐
│  ScriptRunner     │   RunAsync() - 메인 실행 루프
│                   │   ├─ ExecuteLineAsync() - Type별 분기
│                   │   └─ HandleNextAsync()  - Next 처리
└─────────┬─────────┘
          │
          ├────────────────────────────────────────────┐
          │                                            │
          ▼                                            ▼
┌───────────────────┐                      ┌───────────────────┐
│  연출 레이어       │                      │  UI 레이어         │
│  ───────────────  │                      │  ───────────────  │
│  BackgroundLayer  │                      │  DialogueUI       │
│  CharacterLayer   │                      │  ChoiceUI         │
│  AudioManager     │                      │                   │
│  ScreenFX         │                      │                   │
└───────────────────┘                      └───────────────────┘
```

---

## 5. 클래스 관계도

### 5.1 싱글톤 의존 관계

```
                    ┌─────────────────────┐
                    │     GameManager     │
                    │     (게임 흐름)      │
                    └──────────┬──────────┘
                               │
           ┌───────────────────┼───────────────────┐
           │                   │                   │
           ▼                   ▼                   ▼
   ┌───────────────┐   ┌───────────────┐   ┌───────────────┐
   │  UIManager    │   │ ScriptRunner  │   │  SaveManager  │
   │  (UI 전환)    │   │ (스토리 실행)  │   │  (저장/로드)   │
   └───────┬───────┘   └───────┬───────┘   └───────────────┘
           │                   │
           │           ┌───────┴───────┐
           │           │               │
           ▼           ▼               ▼
   ┌───────────────┐   ┌───────────────┐
   │ PopupManager  │   │ StageManager  │
   │  (팝업)       │   │  (연출)       │
   └───────────────┘   └───────┬───────┘
                               │
                   ┌───────────┼───────────┐
                   │           │           │
                   ▼           ▼           ▼
           ┌─────────┐  ┌───────────┐  ┌─────────┐
           │Background│  │Character  │  │ScreenFX │
           │Layer    │  │Layer      │  │         │
           └─────────┘  └─────┬─────┘  └─────────┘
                              │
                    ┌─────────┼─────────┐
                    ▼         ▼         ▼
               ┌────────┐┌────────┐┌────────┐
               │Slot L  ││Slot C  ││Slot R  │
               └────────┘└────────┘└────────┘
```

### 5.2 UI 클래스 계층

```
MonoBehaviour
├── UIManager (싱글톤, 메인 UI 관리)
│   ├── DialogueUI
│   ├── ChoiceUI
│   ├── ScheduleUI
│   ├── TitleUI
│   └── UsernameUI
│
├── PopupManager (싱글톤, 팝업 관리)
│   ├── [Top Layer]
│   │   ├── ConfirmPopup
│   │   ├── AlertPopup
│   │   ├── ToastPopup
│   │   └── LogPopup
│   │
│   └── [Modal Layer] (Lazy 생성)
│       ├── SaveLoadPopup : ModalPopupBase
│       └── SettingsPopup : ModalPopupBase
│
└── ModalPopupBase (모달 팝업 베이스)
    ├── Show()
    ├── Hide()
    └── Close() → PopupManager.CloseModal()
```

### 5.3 Story 타입 시스템

```
ScriptLine (CSV 한 줄 데이터)
├── LineID: string        # 점프 앵커 (선택)
├── Type: LineType        # 라인 종류
├── Speaker: string       # 화자 (Text용)
├── Value: string         # Type별 데이터
├── NextType: NextType    # 진행 방식
└── DelaySeconds: float   # Delay 초

LineType (enum)
├── Text      → DialogueUI.ShowTextAsync()
├── Char      → CharacterLayer.ExecuteAsync()
├── BG        → BackgroundLayer.ExecuteAsync()
├── Sound     → AudioManager.ExecuteAsync()
├── FX        → ScreenFX.ExecuteAsync()
├── Flow      → Jump / End / Save / If
├── Choice    → 선택지 시작
└── Option    → 선택지 항목 → ChoiceUI

NextType (enum)
├── Immediate  # > : 즉시 다음
├── Click      # click : 입력 대기
├── Await      # await : 완료 대기
└── Delay      # 숫자 : N초 후 자동
```

---

## 6. 게임 페이즈 흐름

```
┌─────────────────────────────────────────────────────────────────────┐
│                          GamePhase Flow                              │
└─────────────────────────────────────────────────────────────────────┘

  ┌─────────┐   StartNewGame()   ┌──────────┐  OnNameConfirmed()  ┌──────────┐
  │  TITLE  │ ─────────────────► │ USERNAME │ ──────────────────► │ PROLOGUE │
  └────┬────┘                    └──────────┘                     └────┬─────┘
       │                                                               │
       │ ContinueGame()                               OnPrologueEnd()  │
       │ LoadGame(slot)                                                │
       │                                                               ▼
       │                                                        ┌──────────┐
       └───────────────────────────────────────────────────────►│ DAYLOOP  │◄──┐
                                                                └────┬─────┘   │
                                                                     │         │
                                                      OnSchedule     │         │
                                                      Completed()    │         │
                                                    (RemainingActions│         │
                                                         == 0)       │         │
                                                                     │         │
                                                                     ▼         │
                                                              ┌──────────┐     │
                                                              │  EndDay  │─────┘
                                                              │ (Day++)  │
                                                              └────┬─────┘
                                                                   │
                                                                   │ (조건 충족)
                                                                   ▼
                                                              ┌──────────┐
                                                              │  ENDING  │
                                                              └──────────┘
```

### Phase별 UI 표시

| Phase | 메인 UI | 설명 |
|-------|---------|------|
| `Title` | TitleUI | 타이틀 화면 (시작/이어하기/로드/설정/종료) |
| `Username` | UsernameUI | 플레이어 이름 입력 |
| `Prologue` | DialogueUI | 프롤로그 스토리 실행 |
| `DayLoop` | ScheduleUI | 스케줄 선택 (3행동/일) |
| `Ending` | - | 엔딩 화면 (TODO) |

---

## 7. 스토리 실행 파이프라인

### 7.1 CSV → 실행 과정

```
┌─────────────────────────────────────────────────────────────────────┐
│  1. LOAD                                                            │
│  ─────────                                                          │
│  ScriptRunner.StartScript("Prologue")                               │
│       │                                                             │
│       ├─► Resources.Load<TextAsset>("Story/Prologue")               │
│       └─► ScriptParser.Parse(csv) → List<ScriptLine>                │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  2. EXECUTE LOOP                                                    │
│  ────────────────                                                   │
│  while (currentIndex < lines.Count)                                 │
│  {                                                                  │
│      line = lines[currentIndex];                                    │
│                                                                     │
│      await ExecuteLineAsync(line);  ◄─── Type별 분기 실행            │
│      await HandleNextAsync(line);   ◄─── Next 처리 (click/delay)    │
│                                                                     │
│      currentIndex++;                                                │
│  }                                                                  │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  3. TYPE별 실행                                                      │
│  ──────────────                                                     │
│                                                                     │
│  Text ──► speaker 있으면 AutoEnterBySpeaker() (캐릭터 자동 등장)      │
│       ──► DialogueUI.ShowTextAsync(speaker, value)                  │
│                                                                     │
│  Char ──► CharacterLayer.ExecuteAsync(value)                        │
│           예: "C:Enter:Roa:Happy" → slotC.EnterAsync("Roa","Happy") │
│                                                                     │
│  BG   ──► Cross 아니면 CharacterLayer.ExitAllAsync() (캐릭터 퇴장)   │
│       ──► BackgroundLayer.ExecuteAsync(value)                       │
│           예: "School_Day:Fade:1.5"                                 │
│                                                                     │
│  Sound──► AudioManager.ExecuteAsync(value)                          │
│           예: "BGM:Morning", "SFX:Knock"                            │
│                                                                     │
│  FX   ──► FadeOut 시 DialogueUI.Hide()                              │
│       ──► ScreenFX.ExecuteAsync(value)                              │
│                                                                     │
│  Flow ──► Jump:LineID → currentIndex = lineIndex[LineID]            │
│       ──► End → return false (루프 종료)                             │
│       ──► If:조건:대상 → GameState.EvaluateCondition() → 조건부 점프  │
│                                                                     │
│  Choice─► Auto 모드 일시정지                                         │
│       ──► CollectOptions() → Option 라인들 수집                      │
│       ──► ChoiceUI.ShowAndWaitAsync()                               │
│       ──► 선택 결과로 점프                                           │
└─────────────────────────────────────────────────────────────────────┘
```

### 7.2 대사 타이핑 파이프라인

```
DialogueUI.ShowTextAsync(speaker, text)
    │
    ├─► SubstituteVariables()          # {{PlayerName}} → 실제 이름
    ├─► AddToLog(speaker, text)        # 대사 로그에 추가
    ├─► SetSpeaker(speaker)            # 화자 이름 표시
    ├─► ParseInlineTags(text)          # 인라인 태그 파싱
    │
    └─► 세그먼트 순회 (타이핑 루프)
        │
        ├─ SegmentType.Text      → 글자별 타이핑 + SFX
        ├─ SegmentType.Wait      → N초 대기 <wait=0.5/>
        ├─ SegmentType.SFX       → 효과음 재생 <sfx=Knock/>
        ├─ SegmentType.Emote     → 표정 변경 <emote=Happy/>
        ├─ SegmentType.SpeedStart→ 속도 변경 <speed=0.5>
        └─ SegmentType.SpeedEnd  → 속도 복원 </speed>
```

---

## 8. 폴더 구조

```
Assets/
├── Scripts/
│   ├── Core/                    # 핵심 시스템
│   │   ├── GameManager.cs       # 게임 전체 흐름 관리
│   │   ├── StageManager.cs      # 연출 레이어 매니저
│   │   ├── ScreenFX.cs          # 화면 효과
│   │   └── NameValidator.cs     # 이름 유효성 검증
│   │
│   ├── Story/                   # 스토리 시스템
│   │   ├── ScriptRunner.cs      # 스크립트 실행기
│   │   ├── ScriptParser.cs      # CSV 파서
│   │   ├── ScriptLine.cs        # 데이터 구조
│   │   ├── GameState.cs         # 게임 상태 (스탯/호감도/플래그)
│   │   ├── SaveManager.cs       # 세이브/로드
│   │   ├── DialogueUI.cs        # 대사창 UI
│   │   ├── ChoiceUI.cs          # 선택지 UI
│   │   ├── BackgroundLayer.cs   # 배경 레이어
│   │   ├── CharacterLayer.cs    # 캐릭터 레이어
│   │   ├── CharacterSlot.cs     # 캐릭터 슬롯
│   │   └── AudioManager.cs      # 오디오 매니저
│   │
│   ├── Schedule/                # 스케줄 시스템
│   │   ├── ScheduleUI.cs        # 스케줄 UI
│   │   ├── ScheduleSlot.cs      # 스케줄 버튼
│   │   ├── ScheduleType.cs      # Enum + 효과 데이터
│   │   └── StatGauge.cs         # 스탯 게이지
│   │
│   ├── UI/                      # UI 시스템
│   │   ├── UIManager.cs         # 메인 UI 관리
│   │   ├── PopupManager.cs      # 팝업 관리
│   │   ├── ModalPopupBase.cs    # 모달 베이스
│   │   ├── TitleUI.cs           # 타이틀 화면
│   │   ├── UsernameUI.cs        # 이름 입력
│   │   ├── SaveLoadPopup.cs     # 세이브/로드 팝업
│   │   ├── ConfirmPopup.cs      # 확인 팝업
│   │   ├── AlertPopup.cs        # 알림 팝업
│   │   ├── ToastPopup.cs        # 토스트 메시지
│   │   ├── LogPopup.cs          # 대사 로그
│   │   ├── SettingsPopup.cs     # 설정
│   │   └── SaveLoadSlot.cs      # 세이브 슬롯 아이템
│   │
│   ├── MiniGame/                # 미니게임
│   │   ├── MiniGameBase.cs      # 베이스 클래스
│   │   ├── CherryBlossomGame.cs # 벚꽃 게임
│   │   └── JoggingGame.cs       # 조깅 게임
│   │
│   └── Tester/                  # 테스트용
│       └── DebugRemoteUI.cs
│
├── Resources/
│   ├── Story/                   # CSV 스크립트
│   │   ├── Prologue.csv
│   │   └── Day01/...
│   ├── Backgrounds/             # 배경 이미지
│   ├── Characters/              # 캐릭터 이미지
│   │   ├── Roa/
│   │   │   ├── Default.png
│   │   │   ├── Happy.png
│   │   │   └── Sad.png
│   │   └── ...
│   └── Audio/
│       ├── BGM/
│       └── SFX/
│
└── Prefabs/
    ├── UI/
    └── Characters/
```

---

## 9. 의존성 맵

### 9.1 네임스페이스 의존성

```
┌───────────────────────────────────────────────────────────────────┐
│                        LoveAlgo.Core                              │
│  (GameManager, StageManager, ScreenFX, NameValidator)            │
│                             │                                     │
│                    uses ────┼──── uses                            │
│                             │                                     │
│          ┌──────────────────┴──────────────────┐                  │
│          │                                     │                  │
│          ▼                                     ▼                  │
│  ┌───────────────────┐               ┌───────────────────┐       │
│  │  LoveAlgo.Story   │               │   LoveAlgo.UI     │       │
│  │ (ScriptRunner,    │──── uses ────►│ (UIManager,       │       │
│  │  GameState, etc)  │               │  PopupManager)    │       │
│  └───────────────────┘               └───────────────────┘       │
│          │                                     │                  │
│          │                                     │                  │
│          └──────────────┬──────────────────────┘                  │
│                         │                                         │
│                         ▼                                         │
│              ┌───────────────────┐                                │
│              │ LoveAlgo.Schedule │                                │
│              │ (ScheduleUI, etc) │                                │
│              └───────────────────┘                                │
└───────────────────────────────────────────────────────────────────┘
```

### 9.2 외부 라이브러리 의존성

```
┌─────────────────────────────────────────────────────────────────┐
│                      External Dependencies                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Cysharp.UniTask ◄────────────────────────────────────────────  │
│  │                                                               │
│  ├─ ScriptRunner (async 스크립트 실행)                           │
│  ├─ DialogueUI (async 타이핑)                                    │
│  ├─ ChoiceUI (async 선택 대기)                                   │
│  ├─ BackgroundLayer (async 전환)                                 │
│  ├─ CharacterSlot (async 애니메이션)                             │
│  ├─ AudioManager (async 페이드)                                  │
│  ├─ ScreenFX (async 효과)                                        │
│  ├─ PopupManager (async 팝업)                                    │
│  └─ ScheduleUI (async Show/Hide)                                │
│                                                                  │
│  DG.Tweening (DOTween Pro) ◄─────────────────────────────────── │
│  │                                                               │
│  ├─ BackgroundLayer (CrossFade)                                  │
│  ├─ CharacterSlot (Enter/Exit 애니메이션)                        │
│  ├─ AudioManager (BGM 페이드)                                    │
│  ├─ ScreenFX (Fade 효과)                                         │
│  ├─ SaveLoadPopup (슬라이드 애니메이션)                           │
│  └─ ScheduleUI (페이드 애니메이션)                                │
│                                                                  │
│  TMPro (TextMesh Pro) ◄────────────────────────────────────────  │
│  │                                                               │
│  ├─ DialogueUI (대사 텍스트)                                     │
│  ├─ ChoiceUI (선택지 버튼)                                       │
│  └─ 모든 UI 텍스트                                               │
│                                                                  │
│  Newtonsoft.Json ◄─────────────────────────────────────────────  │
│  │                                                               │
│  └─ SaveManager (JSON 직렬화/역직렬화)                            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📝 Quick Reference

### 주요 싱글톤 접근

```csharp
// 게임 흐름
GameManager.Instance.ChangePhase(GamePhase.DayLoop);
GameManager.Instance.CurrentDay;
GameManager.Instance.RemainingActions;

// UI 전환
UIManager.Instance.ShowOnly(MainUIType.Dialogue);
UIManager.Instance.DialogueUI;

// 팝업
await PopupManager.Instance.ConfirmAsync("저장하시겠습니까?");
PopupManager.Instance.Toast("알림", "저장 완료!");

// 스토리 실행
await ScriptRunner.Instance.StartScript("Prologue");
ScriptRunner.Instance.OnClick();

// 게임 상태
GameState.Instance.AddLove("Roa", 5);
GameState.Instance.GetStat("Int");
GameState.Instance.EvaluateCondition("Love:Roa>=30");

// 연출
await StageManager.Instance.Background.ExecuteAsync("School_Day:Fade:1.5");
await StageManager.Instance.Character.ExecuteAsync("C:Enter:Roa:Happy");

// 오디오
AudioManager.Instance.PlayBGMAsync("Morning");
AudioManager.Instance.PlaySFX("Click");

// 화면 효과
await ScreenFX.Instance.FadeOutAsync(1f);
await ScreenFX.Instance.FadeInAsync(1f);
```

### CSV 스크립트 예시

```csv
LineID,Type,Speaker,Value,Next
# 배경 설정
,BG,,School_Day:Fade:1.5,await
,Sound,,BGM:Morning,>

# 캐릭터 등장
,Char,,C:Enter:Roa,await

# 대사
,Text,로아,{{PlayerName}}! 좋은 아침!,click
,Char,,C:Emote:Happy,>
,Text,로아,오늘 뭐 할래?,>

# 선택지
,Choice,,,click
,Option,,공부하자|Study|Stat:Int:1,
,Option,,놀러가자|Play|Love:Roa:2,
,Option,,자자...|Sleep|if:Fatigue>=50,

# 분기
Study,Text,로아,열심히 하자!,click
,Flow,,End,>

Play,Text,로아,신난다~!,click
,Flow,,End,>
```

---

*마지막 업데이트: 2026-01-23*
