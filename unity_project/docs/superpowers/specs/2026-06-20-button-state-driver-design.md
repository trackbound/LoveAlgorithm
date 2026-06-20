# 통합 버튼 상태 드라이버 (ButtonStateDriver) — 설계 스펙

- 날짜: 2026-06-20
- 위험도: 🟠 High (신규 UI 컴포넌트 + 프리팹 포맷 수정)
- 상태: 설계 승인 대기 → 구현 계획(writing-plans)

---

## 1. 배경 & 문제

현재 버튼 상태 비주얼이 **3개 메커니즘**으로 분산되어 있다.

| 메커니즘 | 정체 | 상태 소스 | pressed 틴트 | 비주얼 적용 | 토글 | 사운드 | 주 소비처 |
|---|---|---|---|---|---|---|---|
| `StyledButton` | `Button` 상속 | Selectable 상태머신 | 네이티브 ColorBlock | `Image.overrideSprite` 스왑 | `SetSelected` | 발행함 | Title 메뉴 6개, Modal Yes/No/Close, ChoiceSlot |
| `ButtonSpriteSwap` | Button 옆 MonoBehaviour | raw 포인터 이벤트 | 코드(C7C7C7) | `Image.sprite` 스왑 | `SetOn` | 안 냄 | SaveLoadView·SettingsView 화살표/토글, 모듈 일반 버튼 |
| `TitleHighlightSwitcher` | MonoBehaviour | raw 포인터 이벤트 | 없음 | 자식 `SetActive` (별도 Highlights 패널) | — | 안 냄 | 타이틀 하이라이트 패널 |

문제점:
- **3개를 다 알아야 함** — 인지 비용. 어떤 버튼이 어느 방식인지 규칙이 역할/역사적 구분에 의존.
- `StyledButton`은 `Selectable` 상속 탓에 "EventSystem 포커스(Selected)가 호버(Highlighted)를 가리는" 구조적 부채가 있어 `ResolveEffective`에 `pointerInside` 보정 로직을 따로 둔다.
- **사이즈 불일치 호버**(호버 아트가 기본과 크기가 다름)는 같은 Image 스프라이트 스왑으로는 정렬이 깨져, 결국 child-swap(`TitleHighlightSwitcher`)이 별도로 존재.
- **상태별 라벨 색 변경**(예: Modal Yes — 흰배경/검정글씨 → 핑크배경/흰글씨)이 흔한데, sprite-swap 방식과 라벨색 구동이 따로 논다.

## 2. 목표 & 비목표

**목표**
- 위 3개를 대체할 **단일 컴포넌트 `ButtonStateDriver`** 신설. 배경은 child-swap, 라벨은 공유 TMP 색 코드 구동, pressed는 활성 자식에 코드 틴트, UI 사운드까지 흡수.
- **Modal Yes/No 파일럿 이행**으로 시각·청각 패리티 증명.

**비목표 (후속 슬라이스)**
- Title 메뉴·Close·Settings·SaveLoad·모듈 일반 버튼의 이행.
- `StyledButton`/`ButtonSpriteSwap`/`TitleHighlightSwitcher` **코드 삭제** (다른 버튼이 아직 사용 — 이번엔 공존).
- 새 컴포넌트용 에디터 와이어툴 / 에셋 네이밍 규약 갱신.

## 3. 이행 전략

신규 컴포넌트 도입 + **점진 이행**(빅뱅 아님). 씬/프리팹 회귀 검증이 비싸므로 한 버튼군씩 옮긴다. 이번 스펙의 파일럿 = **Modal Yes/No** (사이즈 동일 → 가장 안전한 패리티 증명용).

## 4. 컴포넌트 구조 (계층 규약)

```
YesButton  (Button[targetGraphic=루트 투명 Image] + ButtonStateDriver + ChoiceSlot + LayoutElement)
├─ States
│   ├─ Normal   (Image: 흰 배경)   ← 기본 활성
│   └─ Hover    (Image: 핑크 배경)
└─ Label  (TMP, 항상 켜짐 — 상태 위에 떠 있음. 텍스트=ChoiceSlot.Bind, 색=ButtonStateDriver)
```

- **루트 Image = 투명 raycast 타겟**. 네이티브 Button의 클릭 수신용. 시각 배경은 전부 자식.
- **상태 자식 = 명시적 직렬화 참조** (`normalState` / `hoverState` / `onState` / `disabledState`, 타입 `GameObject`). 이름매칭 마법을 쓰지 않는다 — 자족 버튼엔 인스펙터 배선이 더 견고. 비운 슬롯은 `Normal`로 폴백.
- 드라이버는 `Button` 옆 MonoBehaviour. **Selectable 상속 안 함** → 포커스 가림 문제 구조적 부재.

## 5. 순수 결정층 (EditMode 단위테스트 대상, GameObject 불필요)

```csharp
enum State { Normal, Hover, On, Disabled }

// 우선순위: Disabled > On > Hover > Normal
static State ResolveActiveState(bool interactable, bool isOn, bool pointerInside);

// 눌림(interactable && pressed) 시 baseColor * pressedTint(C7C7C7), 아니면 baseColor
static Color ResolvePressedTint(bool interactable, bool pressed, Color baseColor, Color pressedTint);

// 라벨 색: 동일 우선순위로 TextColorBlock에서 선택
static Color ResolveTextColor(State state, in TextColorBlock c);

// 역할+호버/클릭 → SFX 이름 (StyledButton.ResolveSfx 이식: General/Choice/Silent)
static string ResolveSfx(UiSoundRole role, bool hover, UiSoundSO table);
```

- `pressedState`는 **자식이 아님** — 활성 자식 Image.color에 틴트를 곱해 표현(전용 pressed 아트 불필요).
- **`TextColorBlock`은 드라이버가 자체 정의** (`normal`/`hover`/`on`/`disabled` — `ButtonSpriteSwap`과 동형, 상태 4종과 1:1). `drive=false`면 라벨 색 미관여.
- **`UiSoundRole`은 `StyledButton`이 향후 삭제될 것을 대비해 중립 위치에 정의** — 드라이버 측 또는 공용 enum으로 두고, `StyledButton`이 그걸 참조하도록 정리(이번엔 신규만, StyledButton 변경 없음).

## 6. 얇은 어댑터 (MonoBehaviour)

- **입력**: 포인터 enter/exit(hover) · down/up(pressed, 좌클릭) · `SetOn(bool)` · `SetInteractable(bool)`.
- **`Apply()`**: `ResolveActiveState` 결과 자식만 `SetActive(true)`(나머지 false) → 활성 자식 Image.color에 pressed 틴트 → 라벨 색(`drive` 시) 적용.
- **사운드**: `OnPointerEnter`에서 hover음(interactable일 때만), `OnPointerClick`(interactable + 좌클릭)에서 click음 발행 — `EventBus.Publish(new PlaySfxCommand(...))`. 빈 이름이면 무음.
- **공개 API**: `SetOn` / `SetInteractable` / `IsOn` — `ButtonSpriteSwap`과 동일 시그니처로 둬서 SettingsView·SaveLoadView 후속 이행 시 호출부 무변경.
- **엣지**: `OnDisable`에서 `_pressed`/`_pointerInside` 리셋(비활성 중 잔류 방지). 활성 자식 참조가 null이면 안전 폴백(Normal, 없으면 무동작).
- 위치: `Assets/_Project/Scripts/UI/ButtonStateDriver.cs` (StyledButton·ButtonSpriteSwap과 동일 UI 어셈블리).

## 7. 파일럿 이행 — Modal Yes/No

`YesButton.prefab` / `NoButton.prefab` 변경:
1. 루트의 `StyledButton` 제거 → 네이티브 `Button` + `ButtonStateDriver` 부착.
2. 루트 `Image` = 투명 raycast 타겟으로 전환(`targetGraphic`).
3. 기존 흰 배경을 `States/Normal` 자식 Image로, 핑크 hover 스프라이트를 `States/Hover` 자식 Image로 분리.
4. `Label`(TMP) 유지. `ButtonStateDriver.label`에 바인딩, `textColors`(검정 normal → 흰 hover) 재현.
5. **`ChoiceSlot` 유지** — `ModalView`가 `Instantiate` 후 `ChoiceSlot.Bind(index, label, onSelected)`로 라벨 주입·onClick 배선하므로 필수. `ChoiceSlot.button` 참조를 **새 plain Button으로 재배선**(StyledButton fileID 소멸 대응), `ChoiceSlot.labelText`는 동일 Label 유지.
6. 사운드: 기존 General 역할 재현(`ButtonStateDriver`가 발행).

**합격 기준 = 패리티**: 마우스 호버 시 핑크 배경+흰 글씨, 떼면 흰 배경+검정 글씨, 눌림 시 살짝 어두워짐, 호버/클릭음 발생 — 이행 전 StyledButton 동작과 시각·청각 동일.

> 참고: 프리팹의 `ChoiceOptionSlot` EditorClassIdentifier는 `ChoiceSlot`의 옛 이름이 남은 stale 표기(실체는 guid `f46729da…` = `ChoiceSlot`). 기능 무해하며 교체 과정에서 자연 정리.

## 8. 테스트

- **EditMode** (`ButtonStateDriverTests`): §5 순수 함수 4종
  - `ResolveActiveState` 우선순위(Disabled>On>Hover>Normal) 및 조합.
  - `ResolvePressedTint` — interactable·pressed 조합별 곱/패스.
  - `ResolveTextColor` — 상태별 색 매핑.
  - `ResolveSfx` — General/Choice/Silent × hover/click, table null.
- **PlayMode** (`ButtonStateDriverPlayModeTests`): 실제 GameObject로
  - 포인터 enter/exit → Hover/Normal 자식 활성 + 라벨색 전환.
  - down/up → 활성 자식 color 틴트 적용/복원.
  - `SetOn(true)` → On 자식 활성(없으면 Normal 폴백).
  - `SetInteractable(false)` → Disabled 자식 활성(없으면 Normal 폴백), 호버 무반응·무음.

## 9. 위험 & 완화

- 🟠 프리팹 포맷 수동 수정 → Git diff 검토. Modal을 띄워 시각·청각 패리티 직접 확인.
- ChoiceSlot.button 재배선 누락 시 모달 버튼 클릭 무반응 → PlayMode `ModalViewPlayModeTests` 회귀 확인.
- 자식 참조 누락(폴백) 엣지를 EditMode/PlayMode 양쪽에서 검증.

## 10. 향후 (이 스펙 밖)

- Title 메뉴(StyledButton + TitleHighlightSwitcher 이중 → 단일 드라이버) 이행.
- Settings/SaveLoad 화살표·토글(ButtonSpriteSwap) 이행 — API 동형이라 호출부 무변경 기대.
- 3개 레거시 컴포넌트 삭제 + 에디터 와이어툴/에셋 네이밍 규약 갱신.
