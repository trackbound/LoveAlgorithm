# Handoff — 씬 바인딩 작업 안내

코드 작업 완료, **씬에서 Inspector 바인딩 필요한 항목들**.

> 일반 모듈 바인딩(Popup/Save/Settings/Narrative 등)은 완료. 이 문서는 **LockScreen** 본구현 후속 작업만 남김.

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
