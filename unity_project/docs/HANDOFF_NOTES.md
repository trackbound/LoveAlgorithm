# Handoff — 씬 바인딩 작업 안내

코드 작업 완료, **씬에서 Inspector 바인딩 필요한 항목들**.

> 일반 모듈 바인딩(Popup/Save/Settings/Narrative 등)은 완료. 아래 **스테이지(_Stage)** 신규 배선 + **LockScreen**(구 아키텍처) 후속이 남음.

---

## M3 슬라이스2 — 스테이지 `_Stage` 캔버스 배선 (2026-06-02)

스테이지 BG+Char 코드+테스트 완성(커밋 b967218). 아래는 `Game.unity` 인스펙터 배선 — 코드는 손댈 것 없음.
렌더링 구조 결정 = **별도 `_Stage` 캔버스(UI Image), `_UI`보다 낮은 sortingOrder**(감독 결정, ADR-004 rebuild 격리).

### 1. 씬 계층 추가 (`Game.unity` 루트에 `_Stage`)

```
_Stage  (Canvas: Screen Space-Overlay, Sort Order = -10  ← _UI(0)보다 낮게 / CanvasScaler는 _UI와 동일 복사)
├── Background
│   ├── Back   (Image + CanvasGroup alpha=0)   ← 크로스페이드 임시, 형제 위쪽(먼저=뒤에 그려짐)
│   └── Front  (Image + CanvasGroup alpha=1)   ← 현재 배경, 형제 아래쪽(나중=앞에 그려짐)
└── Characters
    ├── SlotL  (Image + CanvasGroup alpha=0)
    ├── SlotC  (Image + CanvasGroup alpha=0)
    └── SlotR  (Image + CanvasGroup alpha=0)
StageView 컴포넌트 = `_Stage` 루트(또는 빈 자식 GO)에 부착.
```
- BG Front/Back Image: RectTransform 전체화면 stretch(anchors 0,0~1,1, offset 0), color 흰색, **Raycast Target off**, sprite 비움.
- 슬롯 Image: **Raycast Target off**, 위치/크기/앵커는 캐릭터에 맞춰 Play로 튜닝(레이아웃은 감독 영역). 각 GO에 CanvasGroup 부착.
- GraphicRaycaster는 `_Stage`에서 제거 권장(스테이지는 클릭 안 받음 — 대사 클릭은 `_UI`/DialogueView 전체화면 Image가 받고 sortingOrder가 더 높아 위에 있음).

### 2. StageView 인스펙터 바인딩

- **Bg Front** = Background/Front의 Image, **Bg Front Group** = 그 CanvasGroup
- **Bg Back** = Background/Back의 Image, **Bg Back Group** = 그 CanvasGroup
- **Slot L / C / R** = 각 SlotBinding → `image`=슬롯 Image, `group`=슬롯 CanvasGroup (중첩 ref, 드래그)
- **Bg Root** = `BG`, **Char Root** = `Characters` (기본값 유지)

### 3. NarrativeController 연결

`_Bootstrap/NarrativeController` 인스펙터 → **Stage Tuning** = `Resources/Data/StageTuning.asset` 드래그.

### 4. 데모 CSV (NarrativeDevTrigger)

`_UI/Simulation/DevNarrativeButton`의 NarrativeDevTrigger → **Inline Demo Csv** 필드에 붙여넣기(실 파일 키: BG `bg_10_01`, 캐릭터 `c01_00`/`c01_11`):

```
LineID,Type,Speaker,Value,Next
,BG,,bg_10_01:Cut,await
,Char,,Enter:c01:00,await
,Text,,데모 내러티브 시작.,click
,Text,로아,안녕! 잘 지냈어?,click
,Char,,Emote:11,>
,Text,로아,오늘 기분 어때?,click
,Choice,,,>
,Option,,반갑게 인사한다|opt_a|Stat:Soc:2,>
,Option,,그냥 지나친다|opt_b|Money:100,>
opt_a,Text,로아,역시 너밖에 없어!,click
,Char,,Exit,await
,Flow,,End,>
opt_b,Text,,어색하게 지나쳤다.,click
,Char,,Clear,>
```
- 첫 BG는 `Cut`(검정 백드롭 없으니 Fade는 투명 깜빡임) — Cross/Fade 데모는 두 번째 BG 추가 후. 슬라이스 밖(CG/SD/Overlay/FX/Sound)은 스킵 로그만.

### 5. 검증 (Play) + 저장 주의

- DevNarrativeButton 클릭 → BG 즉시 표시 → c01 등장(페이드) → 대사 → 표정 11 교체 → 선택지 → (opt_a)퇴장 후 종료 / (opt_b)즉시 클리어. 종료 시 `_Stage` 비워짐.
- ⚠️ **저장 전 부팅 active 확인**(HANDOFF 씬 구조 경고): `_UI/Narrative`=inactive · EndingRoot=inactive · Simulation=active · **`_Stage`=active(빈 상태)**.

---

## LockScreen 모듈 본구현 (2026-05-13)

기획서 `PC잠금 연출 기획서.pdf` 본구현 완료. **씬/Asset 작업 필요**.

### 1. 씬 GameObject 추가

**`_Modules/LockScreenModule`** 빈 GameObject:
- 컴포넌트: `LockScreenController` + `LockScreenModule` (LockScreenModule이 Controller 자동 RequireComponent)
- 인스펙터:
  - Controller: `ToDoList`, `Content` SO 드래그 (아래 §3 Asset)
  - Module: `Panel Scene Instance` 또는 `Panel Prefab` (아래 §2)

**`_Bootstrap/EntryRouter`** 빈 GameObject:
- 컴포넌트: `EntryRouter`
- 디버그: `Force First Setup` / `Skip Lock Screen` 둘 다 false 확인 (빌드 전)

### 2. LockScreenPanel.prefab 만들기

`_Project/Modules/LockScreen/Prefabs/LockScreenPanel.prefab` 신규:

```
LockScreenPanel (CanvasGroup, LockScreenPanel 컴포넌트)
├── Background (창문/달 배경)
├── Clock         → ClockWidget (Mode=Auto)
├── LeftWidgets   → 인스펙터에서 leftWidgets 리스트에 등록 (slide-out 그룹)
│   ├── WarningWidget   (PATIENCE LIMIT REACHED — WarningShakeWidget 부착)
│   ├── MusicWidget     (음악 위젯)
│   └── ToDoWidget      → ToDoWidget (entrySlots: ToDoEntry × 3)
├── RoaMessages   → RoaMessageWidget (slots: MessageSlot × 4)
│   └── 각 슬롯: CanvasGroup + RectTransform + TMP_Text
├── InputCatcher  → 전체화면 투명 Button (메시지 후 클릭 대기)
├── LoginStage    (초기 inactive)
│   ├── DimBackground (CanvasGroup, alpha=0)
│   ├── HeaderText (TMP_Text — 안내 문구)
│   ├── PasswordInput → PasswordInputWidget
│   │   ├── InputField (TMP_InputField, characterLimit=7)
│   │   ├── RevealToggle (눈 아이콘 토글)
│   │   ├── KeyIcon (GameObject, 우측 하단)
│   │   ├── KeyButton (열쇠 클릭)
│   │   └── ConfirmButton (LOGIN 버튼)
│   └── ShakeTarget (PasswordInput과 같거나 부모 RectTransform)
└── BlackOverlay (CanvasGroup, alpha=0 — Outro fade-in용)
```

**Sound 할당 (임시)**:
- LockScreenPanel.sfxSource: AudioSource 컴포넌트
- LockScreenPanel.messageSfx: `Resources/dialoguenext.mp3` 임시 드래그 (정식 SFX 도착 시 교체)

### 3. Asset 생성

**메뉴**: `LoveAlgo > LockScreen > Generate Default Assets`

자동 생성:
- `Resources/Data/LockScreen/LockScreenContent.asset` (메시지 4개·안내문구 5종·시계 23:58)
- `Resources/Data/LockScreen/ToDoList.asset`
- `Resources/Data/LockScreen/ToDo/ToDo_*.asset` × 33 (기획서 17p 그대로)

정식 메시지·효과음 도착 시 인스펙터에서 LockScreenContent.asset 교체.

### 4. 검증 시나리오

```
A. 첫 시작 (PlayerPrefs 비번 미설정)
   → 검은 → 5초 페이드 → 잠금화면 → 5초 후 메시지 4개 → 마지막 +3초 → 클릭
   → 위젯 슬라이드아웃 + dim → 입력창 활성화 (평문 모드) → 비번 입력 → LOGIN
   → "비밀번호 설정 완료!" → 1초 → 패널 페이드아웃 + 검은 페이드인 → 타이틀

B. 일반 진입 (비번 설정 후)
   → 타이틀 (잠금 우회)
   → 스토리에서 CSV `,Flow,,LockScreen:OpenNormal,>` 호출 시 잠금화면
   → 마스킹 입력 → 검증 → 통과 시 OutroSequence

C. 3회 오류
   → 각 오류마다 진동 + 입력 클리어
   → 3회 시 안내 "비밀번호를 잊으셨다면..." + 우측 하단 열쇠 노출
   → 열쇠 클릭 → ConfirmPopup ("새로운 비밀번호 설정을 진행하시겠습니까?")
   → 예 → FirstSetup 흐름과 동일

D. CSV 시계 오버라이드
   → `,Flow,,LockScreen:OpenNormal:Time=07:30,>` → 시계 07:30 표시
```

### 5. 디버그용 PlayerPrefs 초기화

비번 리셋 테스트는 Edit → Clear All PlayerPrefs (또는 디버그 메뉴에 `LockScreenController.ClearPassword()` 호출 버튼).
또는 EntryRouter 인스펙터의 `Force First Setup` 일시 체크.
