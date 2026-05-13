# Plan — LockScreen Module 본구현

> 기반: `uploads/PC잠금 연출 기획서.pdf` (기획 갱햄 / 디자인 엿냥 / 개발 재환)
> 작업범위: `Assets/_Project/Modules/LockScreen/` (스캐폴드 → 기획서 매칭 본구현)
>
> **사용자 검토 안내**: 이 파일을 에디터에서 열어 각 항목 옆 `<!-- memo: ... -->` 형태로 정정 메모 가능. "메모 반영하고 plan 업데이트" 명령 시 코드 X, plan만 갱신.

---

## 0. 현황 (Where we are)

LockScreen 폴더에 **스캐폴드는 거의 완성**, 다만 **기획서와 핵심 갭이 큼**.

```
Modules/LockScreen/
├── Code/
│   ├── ILockScreen.cs           ✅ 인터페이스 ok (수정 필요: 비번 형식)
│   ├── LockScreenController.cs  ✅ 모드 분기·이벤트 발행 ok
│   ├── LockScreenMode.cs        ✅
│   ├── LockScreenModule.cs      ✅
│   ├── PasswordHasher.cs        ⚠️ IsValidPin4 → IsValidPassword(1~7자)
│   ├── PrefsKeys.cs             ✅
│   └── Events/                  ✅ 4종 이벤트
├── Data/
│   ├── LockScreenContentSO.cs   ⚠️ roaMessages만 있음. 안내 문구·시각 추가 필요
│   ├── ToDoItemSO.cs            ✅
│   └── ToDoListSO.cs            ✅
├── UI/
│   ├── LockScreenPanel.cs       ⚠️ 모드 분기는 ok. 메시지 시퀀스·페이드/슬라이드 연출 X
│   ├── PasswordInputWidget.cs   🚨 4자리 키패드 — 자유 7자 InputField로 전면 교체
│   ├── ClockWidget.cs           ⚠️ 실시간만. 고정 모드 추가
│   ├── ToDoWidget.cs            ✅
│   ├── ToDoEntry.cs             ✅
│   └── RoaMessageWidget.cs      ⚠️ 단일 표시만. 4개 순차+위로올라옴 애니 필요
└── Prefabs/                     ⬜ 비어있음
```

**아트 자산 현황** (`Art/UI/Stage/`)
- ✅ Roa_PC_Default/Positive/Negative.png (얼굴 표정 톤; 잠금화면용은 아닐 수 있음)
- ⬜ 비번창/LOGIN/눈/열쇠 아이콘 → 디자이너 작업 중. 기획서 첨부 이미지에 LOGIN, WARNING, 음악 위젯, 투두 위젯, 시계 배경 보임

---

## 1. 갭 분석 (Spec vs Code)

| # | 기획서 요구 | 현재 코드 | 처리 |
|---|------------|-----------|------|
| 1 | 비번 **최대 7자, 문자 제한 없음** | 0~9 4자리 PIN 강제 | PasswordHasher 검증 변경 + Widget 교체 |
| 2 | **첫 설정 시 평문, 그 후엔 \*로 표기** | Reveal 토글에만 의존 | 첫 설정 시 Password→Standard 자동 전환 |
| 3 | 시계 **고정 시간** (인게임 시간과 별개, 스크립트별 표기) | 실시간 OS 시각 | 모드 추가 (Fixed / Real) + SO·인스펙터 |
| 4 | 로아 메시지 **4개 순차 출력**, 5초 후 시작, 1개당 효과음, 위로 올라옴, 기존은 뒤로 + 투명화 | 인덱스 1개 정적 표시 | RoaMessageWidget 시퀀스 코루틴 (DOTween) |
| 5 | 마지막 메시지 +3초 후 **클릭 가능 → 좌측 위젯 슬라이드아웃 + 메시지 슬라이드다운 + dim → LOGIN/입력창 활성화** | 없음 | LockScreenPanel `IntroSequence` 신규 |
| 6 | LOGIN 클릭 → **3초 페이드인 / 3초 페이드아웃 → 스크립트 1행 시작** | 없음 | OutroSequence + ScreenFX/Stage 페이드 활용 |
| 7 | 게임 첫 시작은 **검은 → 5초 페이드 → 잠금화면**, 이후엔 타이틀 → 로그인 | Bootstrap/Title에 통합 X | Bootstrap에서 IsPasswordSet=false 분기 |
| 8 | 입력 오류 시 **빠른 진동 애니** | 없음 | DOTween Shake on field |
| 9 | 3회 오류 시 **안내 텍스트 + 우측 하단 열쇠 아이콘** | 열쇠만, 안내 텍스트 없음 | header에 hint 문구 set |
| 10 | 열쇠 클릭 → **"새로운 비밀번호 설정을 진행하시겠습니까?" 팝업 → 예: FirstSetup 동일** | LockScreenMode.Reset이 "기존 확인 후 새로" 흐름이라 어긋남 | ConfirmPopup 사용 + Reset 흐름 단순화 |
| 11 | **눈 아이콘 — 감은눈 기본**, 클릭 시 뜬눈 + 평문 노출 | Toggle 컴포넌트 사용 (현재 OK) | 기본값 false 확인 |
| 12 | 안내 문구 **5종**: 첫설정 / 설정완료 / 평상 입력 / 3회 오류 / (재설정 진입은 첫설정과 동일) | header 4종 (이름/문구 어긋남) | SO에 통일 |
| 13 | CSV에서 잠금화면 호출 (스크립트 시트 타이밍에 따라) | FlowCommand 없음 | `LockScreenFlowCommand` 추가 |
| 14 | 투두 33개 + 4개 메시지 콘텐츠 채움 | SO만 존재, 빈 데이터 | Asset 생성 + 데이터 채움 |

---

## 2. 결정 사항 (사용자 확인 필요)

각 항목에 `<!-- memo: ... -->` 또는 ❌/✅ 표시로 의견 주세요. 메모 없으면 권장안 그대로 진행합니다.

### D1. 비번 입력 UI 패러다임
**✅ 결정 (2026-05-13)**: TMP_InputField + 눈 토글 + 진동 애니. 가상 키패드 제거.
이유: PC 빌드 → OS 키보드 직접 입력. 한글 IME 지원.

### D2. 비번 최소 길이
**✅ 결정 (2026-05-13)**: 1자 이상 7자 이하. 빈 비번 차단.

### D3. 시계 시간 처리
**✅ 결정 (2026-05-13)**: ContentSO.fixedClockTime이 비어있으면 실시간, 채워져 있으면 고정. CSV Time= 인자로 1회 오버라이드.

### D4. 진입 분기 위치
**✅ 결정 (2026-05-13)**: SceneBootstrapper에서 IsPasswordSet 체크. false → 5초 페이드 + LockScreenPanel.OpenFirstSetup() (Title 우회). true → 기존 Title 흐름.

### D5. CSV 명령 명세
**✅ 결정 (2026-05-13)**: `LockScreen:OpenFirstSetup` / `LockScreen:OpenNormal[:Time=HH:mm]`. UsernameFlowCommand 패턴으로 ExecuteAsync.

### D6. 첫 시작 5초 페이드 처리
**✅ 결정 (2026-05-13)**: LockScreenPanel 자체 CanvasGroup 사용 (모듈 독립성).

### D7. 로그인 후 페이드 (3초인/3초아웃)
**✅ 결정 (2026-05-13)**: LockScreenPanel 자체 검은 오버레이 CanvasGroup으로 처리.

### D8. 메시지 효과음
**✅ 결정 (2026-05-13)**: 임시로 기존 `dialoguenext.mp3` 재활용 (`IAudio.PlaySFX`). SerializeField AudioClip 노출 — 도착 시 교체. TODO 주석 + Debug.Log 표기.

### D9. 모듈 통합
**✅ 결정 (2026-05-13)**: PlayerPrefs (PC당 1개).

### D10. 비번 분실 팝업
**✅ 결정 (2026-05-13)**: 기존 ConfirmPopup 재사용.

---

## 3. 구현 단계 (Step-by-step)

### Step 1 — 데이터/검증 갱신 (코드, 토큰 적음)
- `PasswordHasher.IsValidPin4` → `IsValidPassword(string, int min=1, int max=7)`
- `LockScreenContentSO` 확장:
  - `string fixedClockTime` (선택, "HH:mm" 또는 빈 문자열)
  - `string hintFirstSetup` 등 안내 문구 5종
  - `string roaMessage_0..3` (이미 array 있음 유지)
- `ILockScreen` 시그니처 정리:
  - `bool SetPassword(string pwd)` (메서드명 유지, 검증만 변경)
  - `bool VerifyPassword(string pwd)`
  - `string GetHint(LockScreenHint kind)` 추가
  - `string GetClockTime()` 추가 (fixed 또는 실시간 반환)

### Step 2 — PasswordInputWidget 전면 교체
- 키패드 SerializeField 제거
- TMP_InputField 1개 + 눈 토글(감은눈 기본) + 진동용 RectTransform + 열쇠 아이콘 GO
- `OnPwdEntered` event (string)
- `Confirm()` (LOGIN 버튼 클릭 또는 Enter 키) → 길이 검증 후 발행
- `PlayShake()` (오류 시 호출)
- `SetMaskMode(bool maskByDefault)` — 첫 설정 모드면 false로 호출

### Step 3 — RoaMessageWidget 시퀀스
- 단일 메시지 → 4메시지 순차 출력
- `IEnumerator PlaySequence(IList<string> messages, float intervalSec)`
- 메시지 풀 4슬롯 prefab, DOTween Y이동 + alpha
- 메시지 1개 출력마다 callback (효과음 트리거)
- `Hide()` (아래로 슬라이드)

### Step 4 — ClockWidget 모드 추가
- `Mode { Real, Fixed }` SerializeField
- Fixed 모드: ContentSO 또는 외부 `SetFixedTime("23:58")` 사용

### Step 5 — LockScreenPanel 본 흐름 (가장 큰 변경)
시퀀스 (코루틴 또는 UniTask):

```
OpenFirstSetup():
  1. CanvasGroup.alpha = 0; SetActive(true)
  2. ToDoWidget.Populate, ClockWidget.Refresh
  3. await Fade(0→1, 5초)              # 첫 시작만. 일반 진입은 0초.
  4. await Wait(5초)                    # 메시지 출력 시작 전
  5. await RoaMessageWidget.PlaySequence(4개, 인터벌 1초, 매번 SFX)
  6. await Wait(3초)                    # 마지막 +3초
  7. SetActive(InputCatcher) — 클릭 대기
  8. on click:
     - 좌측 위젯 슬라이드아웃 (Warning, Music, ToDo)
     - RoaMessageWidget.Hide() (아래로)
     - dim 0→0.6 fade in
     - PasswordInput + LOGIN 활성화 (Mask=false, header=hintFirstSetup)
  9. Confirm 시 SetPassword → header를 hintComplete로 변경 → Outro

OpenNormal():
  동일하나 페이드는 0초. Mask=true. header=hintNormal.
  실패 시 PlayShake(). FailCount>=3이면 header=hintForgot, 열쇠 아이콘 활성화.
  열쇠 클릭 → ConfirmPopup → 예 → CurrentMode = FirstSetup으로 전환 + ApplyUI

OutroSequence():
  await Fade(LockScreen 1→0, 3초)
  await Fade(BlackOverlay 0→1, 3초)    # → caller에 제어 반환 직전
  (caller가 다음 화면 띄움)
  await Fade(BlackOverlay 1→0, 3초)
```

`LockScreenPanel`이 너무 비대해질 수 있어 — 시퀀스 부분을 별도 `LockScreenSequencer` 클래스로 분리할지 검토. **권장: 일단 Panel에 통합. 200줄 넘으면 분리.**

### Step 6 — Reset 흐름 단순화
- 기획서: 재설정은 "기존 확인 X, 바로 새 비번 설정"
- `OpenForReset()` → 내부적으로 `FirstSetup`과 동일한 입력 단계로
- 기존 `Step.EnterCurrent` 분기 제거 또는 사용 안 함

### Step 7 — CSV Flow 명령
`Modules/Narrative/Code/Engine/Flow/LockScreenFlowCommand.cs` 신규

### Step 8 — Bootstrap 진입 분기
- `SceneBootstrapper` 또는 첫 화면 진입점에서 IsPasswordSet 체크
- 분기 후 5초 페이드 + 첫 잠금화면 (Title 우회) 또는 평소 Title 흐름

### Step 9 — 콘텐츠 채움
- `Assets/Data/LockScreen/`에 SO Asset 생성 (메뉴 → LoveAlgo/LockScreen/...):
  - `LockScreenContent.asset` — 메시지 4개 + 안내 5종 + 고정시각
  - `ToDoList.asset` — 33개 ToDoItem 등록
  - `ToDoItem_*.asset` × 33 — 기획서 17페이지 목록 그대로
- 메시지 4개 (기획서에서 보이는 2개 외 2개는 사용자 작성 필요):
  - "어디야?"
  - "왜 안 와…"
  - (임시) "지금 들어와줘."
  - (임시) "기다리고 있을게."
  - **TODO**: 정식 메시지 받으면 SO에서 교체. 코드 변경 X.

### Step 10 — UI 프리팹 + 씬 통합 (사용자 작업)
docs/HANDOFF_NOTES.md에 가이드 추가.

---

## 4. 신규/변경 파일 요약

**변경**:
- `Code/PasswordHasher.cs` — 검증 메서드명·로직
- `Code/ILockScreen.cs` — GetHint, GetClockTime 추가
- `Code/LockScreenController.cs` — Reset 단순화 + 새 인터페이스 메서드
- `Data/LockScreenContentSO.cs` — 필드 확장
- `UI/LockScreenPanel.cs` — 시퀀스/페이드/슬라이드/Reset 팝업
- `UI/PasswordInputWidget.cs` — InputField 전환
- `UI/RoaMessageWidget.cs` — 4메시지 시퀀스
- `UI/ClockWidget.cs` — Fixed 모드

**신규**:
- `Code/LockScreenHint.cs` — enum
- `Modules/Narrative/Code/Engine/Flow/LockScreenFlowCommand.cs`
- (선택) `UI/LockScreenSequencer.cs`

**Asset (Unity 에디터)**:
- `Assets/Data/LockScreen/LockScreenContent.asset`
- `Assets/Data/LockScreen/ToDoList.asset`
- `Assets/Data/LockScreen/Items/ToDo_*.asset` × 33
- `Assets/_Project/Modules/LockScreen/Prefabs/LockScreenPanel.prefab`

---

## 5. 검증 (CLAUDE.md §3 — Unity 검증은 사용자가)

코드 작업 후 사용자에게:
- [ ] Unity 컴파일 에러 0
- [ ] 씬 바인딩 (HANDOFF_NOTES 따라)
- [ ] 첫 시작: 검은 → 5초 페이드 → 잠금화면 → 4메시지 → 클릭 → 입력 → 로그인 → 페이드인/아웃
- [ ] 일반: 비번 검증 + 3회 오류 → 열쇠 → 팝업 → 재설정
- [ ] 시계 고정 텍스트 표시
- [ ] CSV에서 `LockScreen:OpenNormal` 호출 시 동작

---

## 6. 자산 요청 갱신

**수령 완료로 이동** (기획서 첨부 보고 확인):
- WARNING 위젯 / 음악 위젯 / 투두 위젯 / 시계 배경(창문/달)

**남은 요청** (디자이너 작업):
- [높음] 비밀번호 입력창 디자인
- [높음] LOGIN 버튼 (활성/비활성 2종)
- [높음] 눈 아이콘 (뜬눈/감은눈)
- [높음] 열쇠 아이콘
- [중간] 메시지 도착 효과음 — 060_Message.mp3 재활용 가능?

---

## 7. 작업 순서 의견

코드 의존도/위험 순:
1. PasswordHasher + ILockScreen + LockScreenContentSO (작은 데이터/검증, 안전)
2. PasswordInputWidget 재작성
3. ClockWidget 모드 추가
4. LockScreenController Reset 단순화
5. RoaMessageWidget 시퀀스 (DOTween)
6. LockScreenPanel 통합 시퀀스 (가장 큼)
7. LockScreenFlowCommand (외부 모듈 — Narrative 영향)
8. Bootstrap 진입 분기 (외부 — Bootstrap 영향)
9. 콘텐츠 SO 생성 (Unity 에디터, 사용자 도와야 함)
10. HANDOFF_NOTES 갱신 + ASSET_REQUESTS 갱신

**Code/ 안에서만 끝나는 1~6**까지 한 번에 하고, 7~8은 외부 모듈 영향 있으니 분리 PR 검토.

---

## 8. 현재 plan 상태

- **✅ 진행 승인 (2026-05-13)**: D1~D10 모두 결정, 미결정 사운드/텍스트는 임시값 + TODO 주석으로 표기
- 작업 중. 진행 상황은 docs/WORK_PLAN.md 참고.
