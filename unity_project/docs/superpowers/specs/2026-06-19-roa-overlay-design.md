# 로아(Roa) 오버레이 자동 결합 — 설계서

- 작성일: 2026-06-19
- 위험도: 🟠 High (모듈 간 인터페이스/CSV 문법 + 세이브 스키마 가산 변경)
- 관련 ADR: ADR-007(EventBus·State SO 경유, UI 직접참조 금지)

---

## 1. 배경 / 목표

로아(`roa`)는 게임 내 가상 캐릭터로, 핸드폰/PC 안에서만 존재한다. 이를 화면에서
**캐릭터 중앙 슬롯의 단일 스프라이트 + 그와 한 쌍을 이루는 오버레이 이미지**로 시각화한다.

- 로아가 대화에 등장하면 `Overlay` 레이어에 디바이스(pc/모바일) × 감정 카테고리(기본/긍정/부정)에
  해당하는 오버레이가 **자동으로 함께** 뜬다. 에셋: `Assets/Resources/Overlay/{device}_{category}.png`
  (현재 존재: `pc_기본/긍정/부정`, `모바일_기본/긍정/부정` 6장).
- 로아 표정이 다른 감정 카테고리로 바뀌면 오버레이가 같은 디바이스에서 해당 카테고리로 **자연스럽게(페이드) 전환**.
- 로아가 퇴장/Clear 되면 오버레이도 **반드시 함께 사라진다**(씬 정리에서도 누락 금지).
- 디바이스(pc↔모바일)는 **로아 등장 시점에 CSV에서 지정**하고, 도중 **전환 명령**으로 오버레이만 바꿀 수 있다.

### 비목표 (YAGNI)
- 로아 외 캐릭터의 오버레이 결합.
- 감정 카테고리 4종 이상 확장.
- 수동 `Overlay` CSV 명령 / `storyOverlay` 미러 제거 (그대로 보존 — 충돌 없음, §7 참조).

---

## 2. 기존 구조 (확인된 사실)

| 요소 | 위치 | 비고 |
|---|---|---|
| 로아 식별자 | `ResourceAliasCatalogSO` | "로아" → id `roa` |
| 캐릭터 슬롯/표정 | `StageView` | `ShowCharacterCommand`(Enter/Emote/Exit/Clear) 구독, 슬롯→캐릭터 추적 |
| 인라인 표정 | `DialogueView` → `ShowSpeakerEmoteCommand` | 타이핑 중 뷰 레벨 발행(엔진 미개입) |
| Overlay 레이어 | `StageLayerView.overlayImage` | `Resources/Overlay/{name}` 로드 + 알파 페이드. **이미 "로아 전용"으로 설계됨** |
| Overlay 명령 | `ShowStageLayerCommand(StageLayerKind.Overlay, …)` | 현재는 수동 `Overlay` CSV 라인으로만 발행 |
| 표정 코드 | 숫자(`00`,`41`…), 별칭 카탈로그가 한글↔코드 매핑 | 엔진이 발행 **전** 해석 |
| CSV 5컬럼 | `LineID,Type,Speaker,Value,Next` | Char의 Value는 콜론 구분(`Enter:로아:기본`) |
| 세이브 미러 | `GameStateData.storyChars` / `storyOverlay` | 엔진이 발행 직전 "해석된 코드ID"로 기록(단일 작성자=엔진) |
| 복원 | `GameBootstrap.TryResumeStory` | 미러를 dur=0으로 재발행 |
| 정리 훅 | `NarrativeFinishedEvent` / `ResetNarrativeViewsCommand` | `StageView`/`StageLayerView`가 ClearAll |

---

## 3. 아키텍처

ADR-007 준수: **엔진(NarrativeController)** = 명령 발행 + 세이브 미러의 단일 작성자, **뷰** = 표시만.

### 3.1 신규 컴포넌트 `RoaOverlayController` (Game 씬 뷰)
`StageView`/`StageLayerView`의 형제 MonoBehaviour. 로아 오버레이의 **단일 두뇌**.

- **구독**
  - `ShowCharacterCommand` — Enter/Emote/Exit/Clear (명시적 표정 + 등장/퇴장)
  - `ShowSpeakerEmoteCommand` — 인라인 `<emote>` (타이핑 중 카테고리 전환)
  - `SetRoaDeviceCommand` (신규) — 디바이스 결정/전환
  - `NarrativeFinishedEvent` / `ResetNarrativeViewsCommand` — 런타임 상태 리셋
- **보유**
  - `RoaOverlaySO` 참조 (§4)
  - 런타임 상태(비영속): 현재 디바이스, 현재 카테고리, 로아 존재 여부
- **출력**: 기존 `ShowStageLayerCommand(StageLayerKind.Overlay, …)` 발행 → `StageLayerView`가 렌더(페이드 재사용).
- **State SO 읽기/쓰기 없음 → 순수 뷰.** 디바이스 상태는 항상 `SetRoaDeviceCommand`로 받는다.

> **왜 엔진-구동이 아니라 컨트롤러-구동인가:** 인라인 `<emote>`는 타이핑 중 뷰 레벨 이벤트라 엔진이
> 그 타이밍에 개입할 수 없다. 얼굴 스프라이트와 오버레이가 같은 순간 전환되려면 같은 뷰 레벨 이벤트를
> 받아야 한다. 따라서 컨트롤러(뷰)가 두뇌가 되고, 디바이스/세이브는 엔진이 명령·미러로만 다뤄 분리를 지킨다.

### 3.2 처리 규칙
컨트롤러는 들어오는 명령의 캐릭터/화자 id가 `RoaOverlaySO.roaCharId`와 일치할 때만 반응한다.

| 입력 | 처리 |
|---|---|
| `SetRoaDeviceCommand(d)` | 런타임 디바이스=d. 로아 존재 중이면 (디바이스 d, 현재 카테고리)로 오버레이 페이드 재렌더 |
| `ShowCharacterCommand` Enter (roa) | 카테고리=Resolve(emote). 오버레이 표시(디바이스+카테고리). 존재=true, 현재 카테고리 갱신 |
| `ShowCharacterCommand` Emote (roa) | 카테고리=Resolve(emote). 변경 시 페이드 전환 |
| `ShowSpeakerEmoteCommand` (roa) | 카테고리=Resolve(emote). 변경 시 페이드 전환 |
| `ShowCharacterCommand` Exit/Clear (roa) | 오버레이 종료(`IsClose`). 존재=false, 런타임 상태 클리어 |
| `NarrativeFinishedEvent` / `ResetNarrativeViewsCommand` | 런타임 상태 리셋(오버레이 이미지는 `StageLayerView`가 이미 ClearAll) |

전환은 항상 페이드(`StageLayerTuningSO.OverlayFadeDefault` 재사용). 등장/퇴장도 같은 페이드.

---

## 4. 신규 SO — `RoaOverlaySO`

```
[CreateAssetMenu] RoaOverlaySO : ScriptableObject
  string   roaCharId      = "roa"     // 이 캐릭터 등장 시에만 오버레이 결합
  string[] positiveEmotes               // 긍정 카테고리 표정 코드 (예: "41","42")
  string[] negativeEmotes               // 부정 카테고리 표정 코드
  // 위 둘에 없는 모든 표정 = 기본(Default) 폴백 (명시 나열 불필요)
  string   pcPrefix       = "pc"
  string   mobilePrefix   = "모바일"
  string   defaultSuffix  = "기본"
  string   positiveSuffix = "긍정"
  string   negativeSuffix = "부정"
  Device   defaultDevice  = Pc          // 등장 시 디바이스 토큰 생략·미설정일 때

  enum Category { Default, Positive, Negative }
  enum Device   { Pc, Mobile }

  Category ResolveCategory(string emoteCode)     // 순수: 코드 매칭, 미등록=Default
  string   OverlayName(Device device, Category cat) // "{prefix}_{suffix}" (예: "pc_긍정")
```

- 표정은 런타임에 **해석된 코드**(`00`/`41`…)로 흐른다(엔진이 ShowCharacterCommand/ShowSpeakerEmoteCommand
  발행 전 별칭→코드 해석). 따라서 SO도 **코드**로 매칭한다.
- 카테고리는 **긍정·부정만 나열**, 나머지는 자동 기본 — 신규 표정 추가 시 기본으로 안전하게 취급.
- `Resolve`/`OverlayName`은 UnityEngine 비의존 순수 메서드로 두어 EditMode 테스트 가능.
- 컨트롤러에 직렬화 바인딩, 미바인딩 시 `Resources.Load<RoaOverlaySO>` 폴백 경로(예: `Data/RoaOverlay`).

---

## 5. CSV 저작 표면

| 의도 | Type | Value | 결과 |
|---|---|---|---|
| 로아 등장 + 디바이스 | `Char` | `Enter:로아:기본:pc` | 중앙 스프라이트 + `pc_기본` 오버레이 |
| 표정 변경(명시) | `Char` | `Emote:웃음` 또는 `로아:웃음` | 얼굴 + 카테고리 맞춰 오버레이 전환 |
| 인라인 표정 | `Text` | 본문 `…<emote=긍정/>…` | 타이핑 닿는 순간 얼굴+오버레이 전환 |
| 디바이스 전환 | `RoaDevice` (신규) | `모바일` (또는 `pc`) | 현재 카테고리 유지, `모바일_(카테고리)`로 교체 |
| 퇴장 | `Char` | `Exit` / `Clear` | 스프라이트+오버레이 동시 종료 |

### 5.1 파서 변경
- `CharIntent`에 `string Device` 추가. `StageParser.ParseCharacter`가 **Enter**의 4번째 토큰을 디바이스로
  파싱(`Enter:캐릭터:표정:디바이스`). 현재 이 토큰은 무시되므로 추가만 하면 기존 분기와 충돌 없음.
  Exit/Clear/Emote/단축 문법은 디바이스 토큰 없음.
- 신규 `LineType.RoaDevice`. `ScriptParser`는 Type 컬럼을 `Enum.TryParse`로 처리하므로 enum 추가만으로
  파싱됨. 엔진에 `case LineType.RoaDevice` 한 줄 추가.

### 5.2 엔진(NarrativeController) 변경
- `PlayStageChar`: intent가 **roa Enter**이고 `Device`가 있으면(또는 미설정 시 기존/기본) → 디바이스 결정 →
  `storyRoaDevice` 기록 + `SetRoaDeviceCommand` 발행. 그 후 평소대로 `ShowCharacterCommand(Enter)` 발행.
- 신규 `PlayRoaDevice(line)`: Value(`pc`/`모바일`)를 디바이스로 파싱 → `storyRoaDevice` 기록 +
  `SetRoaDeviceCommand` 발행. (오버레이 자체는 컨트롤러가 처리.)
- roa Exit/Clear 시 `storyRoaDevice=""`로 초기화(잔여 방지).
- 디바이스 토큰 해석: 입력 문자열을 SO의 `pcPrefix`/`mobilePrefix`와 대소문자 무시 비교 →
  일치하면 해당 Device, 미인식이면 `defaultDevice`로 폴백(경고 로그). (별칭 카탈로그는 사용하지 않음 —
  디바이스는 코드 키워드 2종 고정.)

---

## 6. 세이브 / 복원 🔴

- `GameStateData`에 **`storyRoaDevice` (string, 기본 "")** 추가. 가산 변경 → 구세이브는 기본값으로 로드(무해).
- **단일 작성자=엔진**: 디바이스 결정 지점(Enter device 토큰 / `RoaDevice` 명령)에서만 기록.
- 오버레이 이름은 **별도 저장하지 않는다**. 복원 시:
  1. `GameBootstrap.TryResumeStory`가 `storyChars` 재발행 **앞에** `SetRoaDeviceCommand(storyRoaDevice)`를 1회 발행
     (단, `storyRoaDevice`가 비어있지 않을 때).
  2. 이어서 로아 `ShowCharacterCommand(Enter, roa, emote)` 재발행 → 컨트롤러가 (디바이스+표정)으로 오버레이 재구성.
- 인라인 `<emote>`로 인한 카테고리는 **비영속(전환적)** — 얼굴 스프라이트가 인라인 표정을 저장하지 않는 기존
  동작과 일관. 복원 시 마지막 명시 명령 상태로 얼굴·오버레이가 함께 돌아온다.
- `ClearStoryPosition`에서 `storyRoaDevice=""` 초기화.

---

## 7. 수동 Overlay 경로와의 관계

- 기존 수동 `Overlay` CSV 라인 + `RecordLayer`(→`storyOverlay`) 경로는 **변경/제거하지 않는다**.
- 로아 자동 오버레이는 그 경로를 거치지 않고 컨트롤러가 직접 `ShowStageLayerCommand(Overlay)`를 발행한다
  → `storyOverlay`는 갱신되지 않으므로 GameBootstrap의 `storyOverlay` 복원과 **이중 복원 충돌 없음**.
- 동일 `Overlay` 이미지를 수동 명령과 자동 결합이 동시에 다투는 경우는 last-write-wins(저작자 책임, 실사용
  상 Overlay = 로아 전용이라 발생 가능성 낮음).

---

## 8. 테스트

### EditMode (순수)
- `StageParser`: `Enter:로아:기본:pc` → device 토큰 파싱, 디바이스 없는 Enter/Exit/Emote/단축 회귀.
- `RoaOverlaySO.ResolveCategory`: 긍정/부정 코드 매칭, 미등록=Default.
- `RoaOverlaySO.OverlayName`: (Pc,Positive)=`pc_긍정`, (Mobile,Default)=`모바일_기본` 등.
- `RoaDevice` 라인 파싱(Type 인식, Value→Device).

### PlayMode
- 로아 Enter → `Overlay` 이미지 표시 + 올바른 스프라이트.
- `Char Emote` 카테고리 전환 → 오버레이 교체(페이드).
- 인라인 `<emote>` → 얼굴+오버레이 동시 전환.
- `RoaDevice` 전환 → 카테고리 유지, 디바이스만 교체.
- 로아 Exit/Clear → 오버레이 종료. `NarrativeFinishedEvent` → 정리.
- 세이브 라운드트립: 디바이스+표정 상태에서 저장 → 복원 시 동일 오버레이 재현.

---

## 9. 영향 파일 요약

| 구분 | 파일 | 변경 |
|---|---|---|
| 신규 | `Scripts/UI/RoaOverlayController.cs` | 컨트롤러(뷰) |
| 신규 | `Scripts/Narrative/RoaOverlaySO.cs` | 카테고리/디바이스 SO |
| 신규 | `Scripts/Core/Events/RoaOverlayEvents.cs` | `SetRoaDeviceCommand` |
| 수정 | `Core/Events/StageEvents.cs` | `CharIntent.Device` 추가 |
| 수정 | `Narrative/StageParser.cs` | Enter 디바이스 토큰 파싱 |
| 수정 | `Narrative/ScriptLine.cs` | `LineType.RoaDevice` 추가 |
| 수정 | `Narrative/NarrativeController.cs` | roa Enter 디바이스 처리 + `PlayRoaDevice` |
| 수정 | `Core/State/GameStateData.cs` | `storyRoaDevice` 추가 |
| 수정 | `Game/GameBootstrap.cs` | 복원 시 `SetRoaDeviceCommand` 선행 발행 |
| 신규 | `Resources/Data/RoaOverlay.asset` | SO 인스턴스 |
| 배선 | Game 씬 | `RoaOverlayController` 부착 + SO 바인딩 |
