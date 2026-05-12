# MODULE_STRUCTURE.md

> VN + 미연시 정석 모듈 구조. AI 바이브 코딩에 최적화.
> **신규는 무조건 이 구조로. 기존은 작업 시 점진 이주.**

---

## 핵심 철학

1. **한 모듈 = 한 책임 = 한 폴더**
2. **모듈 간 직접 참조 금지** — `EventBus` (이벤트) + `Services` (인터페이스 조회)만
3. **데이터/로직/UI 분리** — 모듈 안에서 `Data/` `UI/` 하위 폴더로
4. **모듈 내부는 자유, 외부와는 계약** — 인터페이스/이벤트만 노출

---

## 폴더 구조

```
Assets/
├── Scripts/
│   ├── Core/                  글로벌 인프라 (모듈 아님, 공용 데이터)
│   │   ├── GameState.cs           플레이어 데이터 (POCO)
│   │   ├── GameConstants.cs       매직넘버 일원화
│   │   ├── ResourcePaths.cs       리소스 경로 상수
│   │   ├── PrefsKeys.cs           PlayerPrefs 키
│   │   └── Bootstrap.cs           앱 진입점, 모듈 등록
│   │
│   ├── Common/                공용 인프라 (싱글톤 아닌 정적 서비스)
│   │   ├── EventBus.cs            강타입 이벤트 버스
│   │   ├── Services.cs            서비스 로케이터 (모듈 인터페이스 조회)
│   │   ├── SingletonMonoBehaviour.cs
│   │   └── ...
│   │
│   ├── Modules/               게임 기능 (각각 자급자족)
│   │   ├── Narrative/             VN 엔진 — "무엇을 말할지"
│   │   ├── Stage/                 시각 연출 — "어떻게 보여줄지"
│   │   ├── DayLoop/               개강~엔딩 흐름
│   │   ├── Schedule/              자유행동 (낮/밤)
│   │   ├── Stats/                 플레이어 스탯
│   │   ├── Affinity/              히로인 호감도 + 엔딩 분기 ⭐신규
│   │   ├── Shop/                  구매
│   │   ├── Inventory/             보유/사용
│   │   ├── Phone/                 메신저
│   │   ├── MiniGame/              미니게임
│   │   ├── Gacha/                 퍼즐 가챠 ⭐신규
│   │   ├── LockScreen/            PC잠금 연출 ⭐신규
│   │   ├── Save/                  세이브/로드
│   │   ├── Audio/                 BGM/SFX/Voice
│   │   └── Title/                 타이틀/이름설정
│   │
│   ├── Debug/
│   └── Editor/
│
├── Data/                      모듈별 SO (기획자 인스펙터 수정 영역)
│   ├── Narrative/
│   ├── Schedule/
│   ├── Affinity/
│   ├── Shop/
│   ├── Inventory/
│   ├── Gacha/
│   └── LockScreen/
│
├── Prefabs/Modules/           모듈별 프리팹
│   ├── Narrative/
│   ├── Stage/
│   ├── Shop/
│   └── ...
│
└── Resources/                 런타임 동적 로드 (CSV, 캐릭터, 배경)
    ├── Story/                     CSV 스크립트
    ├── Characters/
    ├── Backgrounds/
    └── ...
```

---

## 모듈 폴더 표준

```
Modules/{Name}/
├── {Name}Module.cs        진입점. 등록·초기화.
├── *Controller.cs         핵심 로직 (1~3개)
├── Events/                이벤트 struct 정의
│   └── *Event.cs
├── Data/                  SO 정의
│   └── *SO.cs
├── UI/                    UI 스크립트 + 프리팹 참조
│   └── *UI.cs
└── README.md (선택)       모듈 외부 계약 설명
```

**규칙:**
- `{Name}Module.cs`는 MonoBehaviour 1개. 씬 하이어라키 `_Modules/{Name}Module` 위치
- 모듈 비활성화 = 루트 GO `SetActive(false)` 한 줄
- 다른 모듈 직접 참조 금지. `EventBus.Publish(...)` 또는 `Services.Get<I...>()` 만 사용
- 모듈 내부에서만 쓰는 클래스는 `internal` 권장

---

## 책임 경계 (혼동 주의)

### Narrative vs Stage
- **Narrative**: 스크립트 라인을 읽고 해석. "캐릭터 X 등장 with 표정 Y" 이벤트 발행.
- **Stage**: 그 이벤트 받아 실제 이미지 띄우고 위치/연출 처리.
- 핸들러는 `Services.Get<IStage>().ShowCharacter(...)` 호출.

### DayLoop vs Schedule
- **DayLoop**: "오늘이 1차 이벤트일이냐 자유행동일이냐" — 큰 페이즈.
- **Schedule**: 자유행동일 안에서 "낮에 뭘 할 것이냐" — 단일 행동 선택.

### Stats vs Affinity
- **Stats**: 플레이어 본인 능력치 (체력/지성/사교성/끈기/피로) — 1인분.
- **Affinity**: 히로인 5명 각자의 호감도 게이지 — 5인분, 임계치 비교.

### Shop vs Inventory
- **Shop**: "사는 행위" — 장바구니, 구매 확인, 잔액.
- **Inventory**: "가진 것 + 쓰는 행위" — 보유 목록, 세션 효과, 중복 50% 페널티.

### Save
- 각 모듈은 `ISaveProvider` 구현해 자기 데이터를 직렬화 가능 형태로 제공.
- Save 모듈이 모아서 JSON 직렬화. 역방향도 동일.
- 모듈은 다른 모듈 데이터를 모름.

---

## 하이어라키 표준

```
[Scene Root]
├── _Bootstrap                BootstrapEntry (앱 진입)
│
├── _Core
│   ├── GameStateHolder       (GameState wrapper)
│   └── PersistentData
│
├── _Modules
│   ├── NarrativeModule
│   │   └── ScriptRunner
│   ├── StageModule
│   │   ├── BG
│   │   ├── Characters
│   │   ├── ScreenFX
│   │   └── Overlay
│   ├── DayLoopModule
│   ├── ScheduleModule
│   ├── StatsModule
│   ├── AffinityModule
│   ├── ShopModule
│   ├── InventoryModule
│   ├── PhoneModule
│   ├── MiniGameModule
│   ├── GachaModule
│   ├── LockScreenModule
│   ├── SaveModule
│   ├── AudioModule
│   └── TitleModule
│
├── _UI
│   ├── Layer_HUD             상시 노출 (스탯창, 설정버튼)
│   ├── Layer_Modal           모달 (클릭 차단)
│   ├── Layer_Popup           Top 알림/토스트
│   └── Layer_Overlay         씬 전환, 페이드
│
└── _Persistent               씬 전환 시 유지 (DontDestroyOnLoad)
```

---

## 통신 패턴

### 1. 이벤트 (느슨한 통지)
```csharp
// 발행 (Schedule 모듈)
EventBus.Publish(new ScheduleSelectedEvent {
    Type = ScheduleType.PartTime_Store,
    Slot = TimeSlot.Day
});

// 구독 (Stats 모듈, Inventory 모듈, ... 자유)
EventBus.Subscribe<ScheduleSelectedEvent>(OnScheduleSelected);
```

### 2. 인터페이스 조회 (요청-응답)
```csharp
// Stage 모듈이 등록
Services.Register<IStage>(this);

// Narrative 모듈이 사용
Services.Get<IStage>().ShowCharacter("Roa", "Default", "C");
```

**규칙:**
- 일방향 통지 → 이벤트
- 동기 결과 필요 → 인터페이스 조회
- 다른 모듈 클래스 직접 `using` / 직접 참조 → **금지**

---

## 점진 이주 트리거

**상태 범례:**
- ⬜ 미착수
- 🔄 래퍼만 생성 (원본 위치 유지, IService 등록·이벤트 발행 진입점 마련)
- 🟦 부분 이주 (래퍼 + 일부 호출자 IService 경유로 전환)
- ✅ 완전 이주 (원본 코드 `Modules/{Name}/`로 이전 완료)
- ⛔ 스킵 (모듈화 가치 낮음 — 필요 시 재평가)

| 모듈 | 현재 위치 | 목표 위치 | 상태 | 비고 / 트리거 |
|------|---------|---------|------|---------|
| **Common (인프라)** | `Common/` | `Common/` | ✅ | `EventBus`, `Services`, `Bootstrap` |
| **Affinity** | `_Project/Modules/Affinity/Code/` | (완료) | ✅ | 파일 이동 + 네임스페이스 `LoveAlgo.Modules.Affinity` + 호출자 5개 갱신 완료 |
| **Stats** | `_Project/Modules/Stats/Code/` | (완료) | ✅ | 파일 이동. GameState는 Core 유지 (중앙 상태). 게임플레이 호출자 IStats 경유. Save/Debug는 GameState 직접 |
| **DayLoop** | `_Project/Modules/DayLoop/Code/` | (완료) | ✅ | 파일 이동. DayLoopController는 Core 유지. IDayLoop 쿼리/이벤트 모듈화 |
| **Audio** | `_Project/Modules/Audio/Code/` | (완료) | ✅ | 파일 이동 + 네임스페이스 `LoveAlgo.Modules.Audio` + 호출자 13 갱신 완료 |
| Narrative | `Story/` (대부분) | `Modules/Narrative/` | ⬜ | 분리 트리거: Narrative 본격 수정 시 |
| **Stage** | `_Project/Modules/Stage/Code/` | (완료) | ✅ | 파일 이동 + `IStage` + `StageModule`. 네임스페이스 유지 (LoveAlgo.Core / LoveAlgo.Story) |
| **Save** | `_Project/Modules/Save/Code/` | (완료) | ✅ | 파일 이동 + `ISave` + `SaveModule`. 원본 static 유지, 바인딩은 기능 작업 시 |
| **Schedule** | `_Project/Modules/Schedule/Code/` | (완료) | ✅ | 파일 이동 + `ISchedule` + `ScheduleModule`. ScheduleUI 내부 로직 분리는 E섹션 참조 |
| **Shop** | `_Project/Modules/Shop/Code/` | (완료) | ✅ | 파일 이동 + `IShop` + `ShopModule`. Inventory 분리는 기능 작업 시 |
| **Phone** | `_Project/Modules/Phone/Code/` | (완료) | ✅ | 파일 이동 + `IPhone` + `PhoneModule` |
| **MiniGame** | `_Project/Modules/MiniGame/Code/` | (완료) | ✅ | 파일 이동 + `IMiniGame` + `MiniGameModule` |
| Title | `UI/TitleUI.cs` | (모듈 없음) | ⛔ | 단일 UI 컴포넌트, cross-module 호출 표면 없음 — 필요 시 GameFlow 모듈 흡수 |
| **Affinity (신규 기능)** | — | `Modules/Affinity/` | 🔄 | 위 Affinity 항목 참조 |
| **LockScreen** | 없음 | `Modules/LockScreen/` | ⬜ | A5 (PC잠금 연출 작업 시) |
| **Gacha** | 없음 | `Modules/Gacha/` | ⬜ | A4 (퍼즐 가챠 작업 시) |

**금지:** "정리만을 위한" 일괄 이동. 반드시 기능 작업과 묶어서 이주.

**래퍼 → 완전 이주 기준:**
- 래퍼(🔄/🟦)는 새 코드가 `IService` 경유로 호출하도록 유도하는 단계.
- 완전 이주(✅)는 원본 클래스 파일이 `Modules/{Name}/`로 이동 + 네임스페이스 변경 + 다른 모듈 참조가 인터페이스로만 남은 상태.
- 일반적으로 해당 모듈 본격 개편 작업과 함께 진행.
