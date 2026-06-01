# 재작업용 기능 인벤토리 (Feature Inventory for Rewrite)

> 목적: 아트/프리팹은 유지하고 **코드베이스를 처음부터 새로 작성**하기 위해, 기존 코드가 가진 모든 기능·규칙·수치를 빠짐없이 기록한 문서.
> 작성: 2026-06-01. 기존 코드(266개 .cs) 전수 분석 결과.
> 원칙: **여기 적힌 게임 규칙/수치는 재작업 시 그대로 재현해야 함** (특히 §4 호감도 공식).

---

## 0. 한눈에 보기 — 모듈 맵

| 영역 | 모듈 | 핵심 책임 |
|---|---|---|
| 인프라 | Core, Common | 부팅, 서비스 로케이터, 이벤트버스, 게임상태, 페이즈/플로우 |
| 내러티브 | Narrative, Stage | CSV 스토리 엔진, 명령 해석, 무대 연출(캐릭터/배경/CG/FX) |
| 시뮬레이션 | DayLoop, Schedule, Simulation, Shop | 30일 루프, 자유행동, 상점/아이템 |
| 관계 | Affinity, Stats | 호감도 계산·엔딩 분기, 플레이어 스탯 |
| 기능 화면 | LockScreen, MiniGame, Phone, Title, Tutorial, Settings, Audio | 개별 기능 화면/시스템 |
| UI 프레임워크 | UI (Popup/Notification/Components) | 팝업·토스트·공용 컴포넌트 |

통신 패턴: **Services.Get\<IFoo\>()** (동기 요청) + **EventBus.Publish/Subscribe** (비동기 통지).

---

## 1. Core / 부팅 / 플로우

### 진입 & 라이프사이클
- **Bootstrap** — 도메인 리로드/종료 시 EventBus·Services 초기화 (정적 훅).
- **EntryRouter** — 첫 실행이면 LockScreen, 아니면 Title로 라우팅. (PlayerPrefs 플래그 기준) 디버그: `forceFirstSetup`, `skipLockScreen`.
- **GameManager** (싱글톤) — Flow/DayLoop/Session 컨트롤러 + 상태(CurrentPhase, CurrentDay, RemainingActions, PlayerName) 보유. DOTween 초기화, buildGUID 불일치 시 stale 세이브 정리. 데모 모드 지원.
- **SceneBootstrapper** — 씬별 서비스 초기화.

### 페이즈 & 타임라인
- **GamePhase** enum: `Title, Username, Prologue, DayLoop, Ending` (+ `Transitioning = -1` 가드).
- **GameFlowController** — 페이즈 전환 오케스트레이션 (EnterTitle/Username/Prologue/DayLoop/Ending). 페이드/로딩스크린/자동저장 트리거. 재진입 가드 `_isTransitioning`.
- **GameTimeline** — 30일 고정 스케줄 테이블 (GameBalanceSO 또는 하드코딩 폴백).
  - `StoryArc`: Opening, FreeTime1~6, Event1~3, Festival, MT, Confession
  - `DayType`: Free, PersonalEvent, GroupEvent, Confession
  - 구조: Day1~2 Opening / Day6·16·26 개인이벤트(+3·+6·+9) / Day10~12 축제(+4) / Day20~22 MT(+5) / Day30 고백→엔딩 / 나머지 자유.
- **DayLoopController** — 스케줄 선택→스탯 적용→행동 소모→하루 종료. 인라인 스케줄(CSV `Schedule,await`) 지원. 엔딩 판정은 AffinityCalculator에 위임.
- **DayEventTable** — (day, timing) → 이벤트 목록. `DayTiming`: Morning(스케줄 전)/Evening(행동 소진 후). 조건부 글로벌 이벤트. 발동 기록(firedEvents)으로 중복 방지·세이브.
- **GameFlowJumper** — 디버그/에디터용 통합 점프 (스크립트/LineId/메모리 CSV).
- **SessionController** — 새 게임/이어하기/로드/자동저장 조정. 로드 시 2-pass 스테이지 정리 + 스크립트 위치 복원.

### GameState (런타임 상태)
- 필드: playerName, strength, intelligence, sociability, perseverance, fatigue, money.
- 딕셔너리: lovePoints(charId→pts), flags(name→bool), choiceHistory(점프 타깃 목록).
- API: GetStat/AddStat/SetStat (0~MaxStat 클램프), GetLove/AddLove/SetLove, GetFlag/SetFlag, AddMoney(≥0)/SetMoney.
- **조건 미니언어** `EvaluateCondition(string)`:
  - `Flag:Name`, `!Flag:Name`
  - `Love:CharId>=N`, `Stat:Id>=N`, `StatId>N` (직접)
  - `EndingCount>=N` (PlayerPrefs 머신 전역 카운터)
  - 연산자: `>=, <=, ==, >, <`. 레거시 `Love_Xxx`/`Stat_Xxx` 정규화.
- `EndingCount` PlayerPrefs 전역 카운터 (IncrementEndingCount).

### Common 유틸
| 파일 | 역할 |
|---|---|
| SingletonMonoBehaviour\<T\> | 단일 인스턴스 베이스 |
| Services | 서비스 로케이터: Register/Get(throw)/TryGet/Has/Clear, 도메인 리로드 가드 |
| EventBus | struct 이벤트 강타입 pub/sub, Subscribe→IDisposable, SubscribeOnDestroy 자동 해제 |
| ListenerBag | UI 리스너 일괄 바인딩/해제 |
| Log | 빌드별 조건부 로거 (Info/Warn 릴리즈 제외) |
| MoneyFormat | ₩ 한국 원화 포맷 |
| NameValidator | 이름 검증 (한글 2~6 / 영문 2~12, 금칙어) |
| HangulToDubeolsik | 한글→두벌식 영문 매핑 |
| Headless | 테스트 자동화 토글 (입력 대기 우회) |

---

## 2. Narrative — CSV 스토리 엔진 (Ink 대체 검토 핵심)

### CSV 포맷
파일: `Assets/StreamingAssets/Story/*.csv`. 컬럼 5개:
```
LineID, Type, Speaker, Value, Next
```
- **LineID**: 점프 앵커 (옵션).
- **Type**: `Text, Char, BG, CG, SD, Overlay, Sound, FX, Flow, Choice, Option, Place`.
- **Speaker**: 화자명 / 빈칸=나레이션 / `{{Player}}` 치환.
- **Value**: Type별 명령 데이터.
- **Next**: `click`(클릭 대기) / `await`(완료 대기) / `>`(즉시) / `1.5`(초 딜레이).
- 규칙: `#` 주석, 헤더행 스킵, `\n` 이스케이프, CSV 더블쿼트 이스케이프, 멀티라인 지원.

### 명령 카탈로그 (Type별 핸들러)
- **Text** — 대사/나레이션. 빈 화자=나레이션(자동 dim), 인라인 `<emote=name/>`, `{{Player}}` 치환, 글자별 타이핑.
- **Char** — 캐릭터 레이어(슬롯 L/C/R). 액션: `Enter / Exit / Emote / EnterUp / ExitDown / Clear / Move`. 슬롯 별칭 L/C/R, Center 등. 형식 `slot:action:charId:expr[:overlay]`.
- **BG** — 배경 전환. `bgName:transition[:duration]`, transition: `Cut / Fade / Cross(기본)`. 동일 BG 스킵, CG 중엔 뒤에서 교체.
- **CG** — 전체화면 컷신(대사 숨김). `imageName:action[:duration]`, Exit/Close/Hide로 종료. 진입 시 오토모드 정지·캐릭터 숨김.
- **SD** — SD/치비 컷신(부분, 캐릭터 위).
- **Overlay** — 가상 배경 오버레이(무드/테마). FadeIn/FadeOut.
- **Sound** — `BGM:name` / `SFX:name`, Voice는 Text 화자에서 자동.
- **FX** — 화면/연출/매크로:
  - 화면: `FadeOut/FadeIn/Flash/CamShake/CamZoom/CamPan/CamReset`
  - 아이마스크: `EyeOpen/EyeClose/EyeCloseImmediate/EyeBlink`
  - 색: `ColorTint:preset[:alpha[:dur]]`
  - 흔들기: `StageShake/DialogueShake/CharShake`
  - 캐릭터: `CharJump/CharDim/CharGlitch`
  - 미디어: `Video:file[:Loop|Skippable|NoSkip]`, `LoadingScene[:time]`
  - 매크로: `Setup:BG=..|BGM=..|Char=..|Overlay=..|Eye=..`, `DayStart/DayEnd/NextDay/SceneStart/SceneEnd`, `Wait:sec`, `DialogueHide/Show`
- **Flow** — 분기·상태·모듈 연동:
  - 제어: `Jump:lineId`, `If:condition:lineId`, `Mark:label`(연출 재현용 마커)
  - 호감도: `Affinity:EventChoice:heroineId:eventTag:basePoints` (Event3 자동 +2), `Affinity:Point:heroineId:category:amount`
  - UI/상태: `LockScreen:mode[:Time=HH:mm][:FadeOut]`, `Username`, `Schedule`, `Message:charId:text[:wait]`, `MiniGame:game:heroineId`, `Day:N`
- **Choice / Option** — 분기. Choice는 부모 마커, 이어지는 Option들을 수집. Option 형식 `displayText|jumpTarget[:condition]`, 조건 미충족 옵션 숨김. 선택 시 LineID로 점프, choiceHistory 기록. Headless 시 첫 옵션 자동.
- **Place** — 장소/이벤트 배너(자동 소멸).

### 엔진 아키텍처
- **ScriptParser** — CSV→`List<ScriptLine>`, 검증·이스케이프·기본 페이드.
- **ScriptRunner** (싱글톤) — 스크립트 플레이어. LoadScript/StartScript/StartScriptFrom, OnClick, 오토모드, Rewind, JumpToIndex.
- **ScriptEngine** — async(UniTask) 실행 루프: 핸들러 조회→실행→Next 처리→인덱스 증가.
- **LineHandlerRegistry** — Type→ILineExecutor 매핑. `IResettableExecutor`로 점프 시 상태 리셋.
- **ExecutionDependencies** — 핸들러용 서비스(stage/audio/dialogueUI) 주입, null-safe.
- **CommandAliases** — 대소문자 무시 별칭 정규화.
- **StageStateSynthesizer** — 점프 시 가장 가까운 Mark부터 BG/Char/CG 재생하여 무대 상태 복원 (중간 로드 대응).

---

## 3. Stage — 무대 연출

- **StageModule** (IStage) — 레이어 매니저 + DB.
- 레이어: **BackgroundLayer**(2장 크로스페이드), **CharacterLayer**(3슬롯 L/C/R) + **CharacterSlot**, **CGLayer**, **SDCutsceneLayer**, **VirtualBGOverlay**, **EyeMask**(검은 바), **MonologueDim**, **VideoLayer**.
- **CharacterStageDatabase** (SO) — 캐릭터별 스케일/오프셋/피벗.
- **ScreenFX** (싱글톤) — 페이드/플래시/카메라 셰이크·줌·팬/틴트/캐릭터 트레머·점프·딤·글리치. 전부 DOTween.

### Narrative UI
- **DialogueUI** — 화자명+대사. 글자별 타이핑(속도 0~1), 구두점 후 자동 멈춤, `<emote=.../>` 콜백, ▼ 인디케이터, 오토모드, 버튼(타이틀/세이브/로드/설정/오토/로그/숨기기).
- **ChoicePopup** — 옵션 버튼 생성·조건 필터·선택→점프. Headless 폴백.
- **LogPopup** — 대사 백로그(채팅형), 동일 화자 그룹핑, 초상화, 증분 빌드.

---

## 4. Affinity — 호감도 시스템 ⚠️ 공식 그대로 재현

파일: `Modules/Affinity/Code/AffinityCalculator.cs`, `HeroinePointTracker.cs`, `EndingType.cs`.

### 총점 공식
```
총점 = 기본점수 + 스탯보너스 + 특수보너스(로아만)
기본점수 = Event + Dialogue + Gift + MiniGame
```
카테고리 상한: Event 27 (1차+3·축제+4·2차+6·MT+5·3차+9, 3차 재선택 +2 / 로아 제외) · Dialogue 15 (15회×+1) · Gift 8 (2·3차) · MiniGame 5.

### 스탯 보너스 (로아 제외)
- 히로인 선호 스탯이 4스탯(Str/Int/Soc/Per) 중 단독 1등 → **+3**
- 공동 1등 → **+1**
- 2등 이하 → 0

### 로아 피로 보너스 (스탯 보너스 대신)
- 피로 70~79 → **+3** / 80~89 → **+6** / 90~100 → **+10**

### 호감도 티어
0~9 모르는 사이 / 10~19 아는 사이 / 20~29 친구 / 30~39 가까운 친구 / 40+ 연인 후보.

### 엔딩 임계치 (GameConstants 폴백)
| 히로인 | 임계치 | 선호 스탯 |
|---|---|---|
| Roa (로아) | 46 | Fatigue(피로) |
| HaYeEun (하예은) | 32 | Str |
| SeoDaEun (서다은) | 35 | Int |
| LeeBom (이봄) | 39 | Soc |
| DoHeewon (도희원) | 43 | Per |

### 엔딩 판정 (DetermineEndingHeroine)
1. **로아 히든** (우선): 피로 ≥70 AND 총점 ≥46 → Roa
2. **일반**: 이벤트 선택 ≥1회 + 총점 ≥임계치 중 (총점-임계치) 마진 최대 히로인
3. 해당 없음 → 노멀(고백 없음)
- Happy/Sad: 총점 ≥임계치 → Happy, 미만 → Sad.
- **EndingType** (10종): 4히로인×2분기 + RoaMeriBad + NoConfession + None.

히로인 ID: `Roa, HaYeEun, SeoDaEun, LeeBom, DoHeewon`.

---

## 5. Stats / DayLoop / Schedule / Simulation / Shop

### Stats
- 스탯 5종: `Str, Int, Soc, Per`(전투 스탯) + `Fatigue`(특수). 범위 0~100 (MaxStat 클램프).
- **StatsModule** (IStats) — GameState 래퍼, StatChangedEvent 발행.

### DayLoop / EventPhase
- **EventPhase** enum 13단계: Opening→Event1→AfterEvent1→Festival→AfterFestival→Event2→AfterEvent2→MT→AfterMT→Event3→AfterEvent3→Confession→Ending.
- 이벤트일(IsEventDay): Event1/Festival/Event2/MT/Event3/Confession → 자유행동 없음.
- 자유행동일(IsFreeActionDay): 2행동/일(낮·밤).
- MaxDay 30, ActionsPerDay 2.

### Schedule — 자유행동 (3카테고리 × 3 = 9)
| 카테고리 | 액션 | 돈 | Str | Int | Soc | Per | 피로 | 제한 |
|---|---|---|---|---|---|---|---|---|
| 알바 | Store | +20,000 | | | | +1 | +5 | |
| | Loading | +50,000 | | | | +2 | +15 | 1일 1회 |
| | Invest | ±50~100% | | | | | | 최소 30,000 |
| 운동 | A | | +3 | | | | | |
| | B | | +2 | | | +1 | +5 | |
| | C | | +1 | | | +1 | +3 | |
| 공부 | D | | | +3 | | | +5 | |
| | E | | | +2 | | | +3 | |
| | F | | | +1 | +1 | | +2 | |

### Simulation
- **SimulationMode**: None / Schedule(메인) / Shop(서브).
- **SimulationModule** — QuickMenu 호스팅, 서브모드 등록·라우팅(EnterSimulation/OpenSubMode), OnEntered/OnExited/OnSubModeChanged 이벤트.

### Shop — 상점/아이템 (43개 하드코딩 폴백)
- **Gift** 15개(히로인당 3) — Event2/Event3에서 호감도 +. 예: gift_monitor(120k)→Roa +3/+5.
- **Consumable** 5개 — 즉시 피로 회복. **같은 날 동일 태그 2회째 50% 페널티** (`Max(1, base/2)`).
- **SessionBuff** 25개 — 다음 자유행동 1회 스탯 +, 복합 효과 가능(예: buff_blanket Per+1/피로-2). 동일 50% 페널티.
- 해금(ItemAvailability): Always / AfterEvent2Start(Day16+) / AfterEvent3Start(Day26+) / AfterConfession(Day30+).
- 구매 플로우: Buy/BuyBatch → BuyBatchAndApply (Consumable·SessionBuff 즉시 적용, Gift는 인벤토리).

---

## 6. 기능 화면 모듈

### LockScreen — PC 잠금 인트로
- 모드: **FirstSetup**(비번 미설정, 평문 입력) / **Normal**(검증·마스킹, 3회 오류 시 열쇠 아이콘) / **Reset**(기존 비번 확인 X) / **GameStart**(연출용, LOGIN만).
- **PasswordHasher** — 1~7자, SHA256+salt(16B Base64).
- 기능: 투두 33개 중 랜덤 3개, 로아 메시지 4개 순차+SFX, 힌트 6종, 시계(고정/실시간/`Time=` 오버라이드).
- CSV: `LockScreen:{FirstSetup|Normal|Reset|Auto|GameStart}[:Time=HH:mm][:FadeOut]`.
- SO: LockScreenContentSO(메시지/힌트/시계/SFX), ToDoListSO, ToDoItemSO.

### MiniGame
- **MiniGameBase** — Intro→Start→Gameplay→Result, 타이머, 결과 콜백.
- **CherryBlossomGame** (로아) — 떨어지는 꽃잎 클릭(+1), 꽃(5%, +3, 5배속), 30초, 10초마다 ×1.5 가속. 점수→호감도: ≥30→+3 / ≥20→+2 / ≥10→+1 (상한 5).
- **JoggingGame** (하예은) — 스페이스바 리듬 레이스. 속도게이지(decay -0.3/s, +0.08/press), 속도 6단계(0~120px/s), 예은 14브레이크포인트 타임라인+대사. 결승 ±0.5s→30 / ±1.5s→20 / ±4s→10 / 실패 0. 속도≥5 불꽃 이펙트.
- CSV: `MiniGame:{game}:{heroineId}` → 점수 반환(내부에서 호감도 변환).

### Phone — 인게임 메신저
- **MessengerSystem** — 친구 레지스트리, 채팅방, 메시지 히스토리, 미읽음 추적, 일자/이벤트 자동 메시지.
- 데이터: ChatMessage(발신/텍스트/일자/타임스탬프), ChatRoom(ownerId/메시지/미읽음), FriendProfile, MessengerSaveData.
- 친구 5히로인 하드코딩(c01~c05). 미읽음 배지, MarkAsRead, 상태메시지, 저장/복원.
- API(IPhone): OpenChat/ShowPhoneUI/Close/SetNotificationVisible.

### Audio
- **AudioManager** (싱글톤) — 오디오 소스 5개(BGM/SFX/Voice/UI/UITyping).
- BGM 크로스페이드, SFX 캐싱, Voice(캐릭터별 볼륨), UI 사운드 자동 바인딩.
- 볼륨: Master/BGM/SFX/CharacterVoice (0~1 → AudioMixer dB). 캐릭터 진입 SFX, 자동 캐릭터 BGM 전환(옵션).
- API(IAudio): PlayBGM/StopBGM/PlaySFX/PlayVoice/StopVoice/Set*Volume.

### Title
- **TitlePanel** — New Game / Continue / Settings. **UsernameUI** — 이름 입력. **ExtraPopup** — CG 갤러리.

### Tutorial
- **TutorialOverlay** — 스포트라이트 하이라이트 + 핫스팟 주석, 자동/수동 종료.

### Settings
- PlayerPrefs: 볼륨(Master/BGM/SFX/캐릭터 음성), 대사속도/오토속도, 해상도 인덱스/전체화면.
- TakeSnapshot/RevertToSnapshot (취소 시 비파괴 복원). GameConstants.Resolutions[].
- API(ISettings): 볼륨/대사/해상도 프로퍼티 + Save/Load/ResetToDefaults/ShowSettingsUI.

### UI 프레임워크
- **PopupBase** — 모달/다이얼로그 베이스. 애니(FloatUp/Fade/SlideRight), 레이어(Modal/Dialog/Notification). 제네릭 `PopupBase<TResult>`(UniTask).
- **PopupSystem** (싱글톤) — Type별 레지스트리, PreWarm, 레이어 루트 자동생성, 오픈 스택+ESC, ConfirmAsync/AlertAsync/Toast/ToastSequence, CloseAll(Immediate).
- **ConfirmPopup** — 메인+서브텍스트 4 + 확인/취소, bool 반환.
- **ToastNotification** / **PlaceNotification** (CSV Place 연동).
- 공용 컴포넌트: TextBubble, ButtonEX, HoverButton, TabGroup, ScrollbarEX, ToggleButton, TextMarquee, PetalParticleUI.

---

## 7. Save / Load

- **SaveData** (JSON, Version=1): Phase, CurrentDay, RemainingActions / 스크립트 위치(ScriptName·LineId·LineIndex) / PlayerName·Money / 스탯 5종 / LovePoints·Flags / SaveTime·ChapterName / 스테이지 상태(BG·BGM·Characters·CG·SD·Overlay·MonologueDim·FadeBlack·EyeClosed) / FiredEvents·PointTracker / ShopSaveData·MessengerSaveData / ChoiceHistory / UsedLoadingToday.
- **SaveManager** — 슬롯 30개(0=자동, 1~29 유저), 스크린샷 시스템, 직렬화/슬롯/썸네일 서브시스템 위임.
- 저장 트리거: 프롤로그 후 자동, 하루 종료(DayChanged), 수동 슬롯. buildGUID 불일치 시 전체 삭제.

---

## 8. Ink 도입 평가

### 현 엔진이 잘하는 것
- 대사/분기/조건/플래그/스탯 연동 + 무대·오디오·FX 명령이 **CSV 한 곳에 통합**.
- 비개발자도 읽기 쉬운 명시적 명령 문법, 별칭/대소문자 무시.
- 점프 시 무대 상태 자동 복원(StageStateSynthesizer), 선택 히스토리/백로그.

### Ink가 대체 가능한 부분 vs 글루가 필요한 부분
| 기능 | 현재 | Ink |
|---|---|---|
| 대사·나레이션 | O | 네이티브 |
| 선택지·분기 | O | 네이티브 (조건 필터는 변수로) |
| 변수·플래그·조건 | O | 네이티브 (Ink 변수) |
| 스탯/호감도 시스템 | O | external function 글루 필요 |
| 캐릭터/배경/CG/FX | O | **Ink 태그(`#`) + 글루 레이어 필수** |
| 오디오(BGM/SFX) | O | **태그 글루** |
| 모듈 연동(LockScreen/Message/MiniGame/Schedule) | O | **external function 글루** |
| 무대 상태 복원 | O (Synthesizer) | Ink는 변수상태 저장 가능, 연출 복원은 별도 구현 |

### 권고 (요약)
- **Ink는 도입 가능**하되, 핵심 가치는 "분기/변수/세이브 상태"에 있음.
- 무대·오디오·FX·모듈 호출은 결국 **Ink 태그 + 디스패치 글루 레이어**로 다시 만들어야 함 → 현재 CSV의 `Flow:`/`FX:` 명령을 Ink 태그 문법으로 옮기는 작업.
- 트레이드오프: Ink는 복잡한 분기/변수 관리가 강력하지만, 현재 CSV의 "한 줄=한 연출" 명시성은 일부 잃음(태그가 대사에 섞임).
- **결정 필요**: (A) Ink 채택 + 글루 레이어 신규 / (B) 현 CSV 엔진 개선 유지 / (C) CSV 포맷 정리 + 엔진만 재작성.

---

## 9. 재작업 시 반드시 보존할 것 (체크리스트)
- [ ] §4 호감도 공식·임계치·로아 피로 보너스·엔딩 판정 로직 (수치 그대로)
- [ ] §1 30일 타임라인 구조 + 이벤트 점수(1차+3·축제+4·2차+6·MT+5·3차+9)
- [ ] §5 자유행동 9종 수치 + 상점 43아이템 + 중복 50% 페널티 + 해금 게이트
- [ ] §2 CSV 명령 전체 카탈로그 (또는 Ink 태그로 1:1 매핑)
- [ ] §7 세이브 스키마 (마이그레이션 위해 버전 관리)
- [ ] 아트/프리팹 GUID 보존 (씬·SO 참조 유지)
