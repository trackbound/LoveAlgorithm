# 네이밍 컨벤션

> AI 위임 및 사람 협업 시 *"이 클래스가 무엇인지/어떻게 표시되는지"*를 이름만으로 파악 가능하도록 통일.

---

## UI 분류 매트릭스

| Suffix | 정의 | 차단성 (게임 진행) | 자동 소멸 | 베이스 클래스 |
|--------|------|------------------|---------|--------------|
| **`*Popup`** | 모달 다이얼로그 | ✅ 진행 차단 + 배경 dim | ❌ 명시적 닫기 | `PopupBase` (필수, Layer=Modal) |
| **`*Panel`** | **게임 진행 외부** (진입 흐름·메뉴·게이트) | △ 진입 차단 | ❌ 명시적 종료 | `MonoBehaviour` |
| **`*Notification`** | 자동 소멸 알림 | ❌ 없음 | ✅ 타이머 | `MonoBehaviour` |
| **`*Tooltip`** | 호버 시 표시, 떠나면 사라짐 | ❌ | ✅ 호버 종료 | `MonoBehaviour` |
| **`*Overlay`** | 반투명 위 가이드 | △ 가이드 동안 | ❌/✅ | `MonoBehaviour` |
| **`*UI`** | **게임 진행 중** 모드/인라인 상태 | △ 모드 의존 | ❌ | `MonoBehaviour` |
| **`*Widget`** | 다른 UI의 구성 부속 | — | — | `MonoBehaviour` |
| **`*Slot`** / **`*Entry`** | 리스트 항목 | — | — | `MonoBehaviour` |
| **`*Manager`** | 시스템 싱글톤 | — | — | `SingletonMonoBehaviour` |

### 카테고리 결정 흐름

1. **모달인가?** (다른 UI 차단 + dim) → **`*Popup`**
2. **게임 진행 외부 화면인가?** (타이틀, 잠금화면 등 진입 흐름) → **`*Panel`**
3. **잠깐 떴다 사라지는 알림인가?** → **`*Notification`**
4. **호버 시 표시되는 보조 정보인가?** → **`*Tooltip`**
5. **반투명 가이드/튜토리얼인가?** → **`*Overlay`**
6. **게임 진행 중 활성되는 모드/인라인 상태인가?** (상점/스케줄/이름입력 등) → **`*UI`**
7. **다른 UI 안의 구성 부속인가?** → **`*Widget`**
7. **리스트의 한 항목인가?** → **`*Slot`** (선택형) / **`*Entry`** (표시형)

---

## 추가 규칙

- **`Popup` suffix**: 모달(차단 + dim) 동작. **`PopupBase` 상속 필수** (Layer/useDimmer/Stack 통보 표준 흐름).
- **결과 반환 팝업** (`ConfirmPopup` 등): `PopupBase<TResult>` 상속. `AwaitResult()` + `Complete(result)` 사용.
- **Notification/Tooltip** 등 자동소멸·호버 UI도 `PopupBase`(Layer=Top, useDimmer=false) 상속 권장 — 새 팝업 추가 비용 최소화.
- **`*UI` suffix는 모드 단위 컨테이너에만** (예: `ScheduleUI`, `ShopUI`). 위젯·슬롯·엔트리에는 사용 금지.
- 약어 `EX`(확장)는 컴포넌트 전용 (`ButtonEX`, `ScrollbarEX`).
- 모듈 진입점은 **`{Name}Module`** (예: `AudioModule`, `SaveModule`).
- 인터페이스는 **`I{Name}`** (`IAudio`, `ISave`, `ISettings`).
- ScriptableObject 데이터는 **`*SO`** (`ToDoItemSO`, `LockScreenContentSO`).

---

## 카테고리별 예시 (정상 분류)

### `*Popup` (모달, ModalPopupBase 상속)
- `SaveLoadPopup` — 세이브/로드 슬롯 모달
- `SettingsPopup` — 설정 모달
- `ExtraPopup` — Extra 메뉴 모달
- `AlertPopup`, `ConfirmPopup` — 범용 알림/확인
- `PhonePopup` — 메신저 (모달, 풀스크린 외관)

### `*Panel` (게임 진행 외부 — 진입 흐름/메뉴/게이트)
- `TitlePanel` — 타이틀 화면 (게임 시작 전 메뉴)
- `LockScreenPanel` — PC잠금 (게임 진입 게이트, 해제 전 시작 X)

### `*UI` (게임 진행 중 모드/인라인 상태)
- `ScheduleUI` — 자유행동일 스케줄 화면
- `ShopUI` — 상점 모드 화면
- `QuickMenu` — Schedule/Shop 컨텍스트 메뉴
- `UsernameUI` — 스토리 중 이름 입력 인라인 (대사 흐름 한 단계)
- `DialogueUI` — 대사창 (스토리 진행 중 상시)

### `*Notification` (자동 소멸)
- `ToastNotification` — 토스트 메시지
- `PlaceNotification` — 장소 변경 알림 (좌상단 잠깐 표출)

### `*Overlay`
- `TutorialOverlay` — 튜토리얼 가이드

### `*Widget`
- `ClockWidget`, `ToDoWidget`, `RoaMessageWidget`, `PasswordInputWidget`
- `StatGaugeWidget`, `ChatBubbleWidget` (검토 후)

### `*Slot` / `*Entry`
- `SaveLoadSlot`, `PhoneChatSlot`, `PhoneFriendSlot`, `ShopCartSlot`, `ShopSaleSlot`, `ScheduleSlot`
- `LogEntry`, `LogHeader`, `LogCharacterEntry`, `LogExtraEntry`, `LogUserEntry`, `LogNarrationEntry`

### Components (재사용 컨트롤)
- `ButtonEX`, `HoverButton`, `ToggleButton`, `TabGroup`, `TextMarquee`, `ScrollbarEX`, `PetalParticleUI`

---

## 모듈 폴더 구조

각 모듈은 `Assets/_Project/Modules/{ModuleName}/`에 위치하며 다음 하위 폴더 사용:

```
{ModuleName}/
  Code/             코어 로직, I{Name}, {Name}Module
    Events/         EventBus용 struct
  Data/             ScriptableObject 정의
  UI/               이 모듈 전용 UI 클래스
  Prefabs/          이 모듈 전용 프리팹 (클래스명과 동일)
    Sub/            sub-prefab 그룹 (예: Bubbles/)
  Art/              모듈 전용 아트 리소스 (선택)
```

**프리팹 응집 규칙:**
- 모듈 전용 프리팹은 반드시 `{Module}/Prefabs/`에 위치 (코드와 1:1 응집)
- 프리팹 파일명 = 메인 클래스명 (공백·축약 금지)
- `SaveLoadPopup.prefab`, `ScheduleUI.prefab`, `StageRig.prefab` 식
- 공용 프리팹은 `_Project/UI/{Category}/Prefabs/`

공용 UI (모듈 종속 없음) 는 `Assets/_Project/UI/{Core,Components,Popups,Notifications,Contextual,HUD,Panels}/`.
실제 사용 카테고리만 폴더 생성 (현재: Core, Components, Popups, Notifications, Contextual).

---

## 씬 GameObject 네이밍

씬 하이어라키도 코드 클래스명과 동일하게 (1:1 매핑):

```
Main/
  _Bootstrap/        부트스트랩, GameManager, SessionController
  _Modules/          모든 IService 등록 GameObject (1개 1행)
    AudioModule
    SaveModule
    StageModule
    SettingsModule
    NarrativeModule
    LockScreenModule
    ...
  _Stage/            BG/Char/CG 렌더링 (StageRig 등)
  _UI/
    HUD/             상시 표시 (없으면 비어둠)
    Notifications/   PlaceNotification, ToastNotification
    Contextual/      ScheduleUI, ShopUI, QuickMenu
    Modals/          PopupManager 산하 (실 모달)
    Panels/          LockScreenPanel, TitlePanel (게임 상태 단위)
  _Debug/
```

---

## 위반 시 처리

새 코드 작성 또는 기존 코드 변경 시 이 컨벤션 위반이 발견되면:
1. 가능하면 즉시 rename (작은 변경)
2. 큰 변경(상속 변경 등)은 `docs/MODULE_STRUCTURE.md`에 메모 후 별도 작업
