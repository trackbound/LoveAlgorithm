# 🛠️ 개발 지침 (Dev Guide v1 — 코드 룰북)

> 이 문서는 감독과 Claude가 LoveAlgorithm 개발을 진행할 때 반드시 따르는 개발 및 협업 표준 룰북입니다.
> `_index.md`, `decisions.md`와 유기적으로 연동하여 아키텍처 표류와 과설계를 차단합니다.

---

## 0. 핵심 철학 — "출시를 목표로 하는 1인 개발"

모든 설계 및 구현 의사결정의 최우선 순위는 다음과 같습니다:
```
1. 출시 가능성 (6개월 이내 완성 및 출시가 가능한가?)
2. 재미 (비주얼 노벨 + 스케줄 시뮬레이션의 재미 요소가 살아있는가?)
3. 유지보수성 (3개월 후 감독이 코드를 보았을 때 직관적으로 파악되는가?)
4. 성능 및 확장성 (가비지 컬렉션 부하 최소화 및 데이터 확장)
```

### 감독이 베테랑 개발자라서 발생하는 특수 리스크 방지
이 프로젝트는 "비개발자 + AI" 조합과 정반대의 함정이 생깁니다:
1. **과설계 공모**: 감독도 AI도 "제대로 설계하고 싶어" 불필요한 추상화 계층을 쌓게 됩니다. 둘 다 만족하는 코드가 나오지만 게임 출시는 늦어집니다. (→ §1-4 과설계 게이트)
2. **검증 격차**: C# 베테랑인 감독이 코드를 "대충 훑어보고 머리로 통과"시키는 순간 결함이 누적됩니다. (→ §1-5 검증 규율)
3. **아키텍처 표류**: 개별 파일은 맞아 보이지만, 누적되면서 전체 모듈 참조 구조가 서서히 스파게티로 무너집니다. (→ §3-1 아키텍처 표준)

---

## 1. AI 협업 및 개발 규율

### 1-1. AI 답변 양식
불필요한 기초 해설이나 C# 문법 설명은 절대 생략합니다. 코드 제출 시 아래 항목을 명확히 제시합니다:
1. **작업 목적**: (1줄 요약)
2. **영향 범위**: (수정/생성 파일, 이벤트 구독자 및 State SO 파급 효과)
3. **구현 코드**: (전체 소스 또는 명확한 git diff)
4. **설계 근거**: (왜 이 패턴을 썼는지, 대안 대비 트레이드오프는 무엇인지)
5. **작동 검증 방법**: (동작 증거를 얻는 법)

### 1-2. 컨텍스트 관리 (추측 금지)
정보가 모호하거나 누락된 경우 임의로 코드를 추측하지 않습니다:
- 클래스/인터페이스의 구조는 `find_symbol`, `view_file` 등으로 직접 확인합니다.
- 외부 패키지 정보는 `Packages/manifest.json`을 분석하여 판단합니다.
- 비주얼/UI 리소스 형태를 추측해서 작업하지 않고, 목업이 필요한 경우 명확히 요청합니다.

### 1-3. 코드 컨벤션
- 표준 C# / Unity 6 스타일을 따릅니다 (PascalCase 타입/메서드, camelCase 필드).
- 네임스페이스: `LoveAlgo.{모듈명}` 필수 (예: `LoveAlgo.Modules.Stats`).
- XML 주석은 public API/인터페이스에만, 인라인 주석은 코드가 아닌 "왜 이렇게 짰는지"만 기술합니다.

### 1-4. 과설계 게이트 (Self-Audit)
모든 아키텍처 제안 전에 Claude 스스로 다음 항목을 검사합니다:
- [ ] 이 디자인 패턴이 지금 당장 꼭 필요한가, 아니면 "미래를 대비해서"인가?
- [ ] 이게 없으면 오늘 개발하는 기능이 깨지거나 불가능한가?
- [ ] 1인 솔로 개발의 인지 부하를 줄이는 데 기여하는가?
- [ ] 6개월 후 혼자 유지보수하기 쉬운 단순한 형태인가?
*3개 이상 만족하지 못할 경우, 무조건 가장 단순한 구조의 대안을 1순위로 제안합니다.*

### 1-5. 검증 규율 (결함 방지)
AI가 작성한 코드는 항상 미사용 코드, 하드코딩 수치, 중복/중첩 로직 등의 독자적인 결함 프로파일을 가집니다. 따라서 코드 제안 시 스스로 다음 **자가 의심 질문**을 동봉해야 합니다:
```markdown
*자가 검증 질문*
- 이 코드는 어떤 가정을 기반으로 작성되었는가?
- 이미 프로젝트 내에 동일한 연산을 처리하는 기존 메서드가 있는가?
- 하드코딩된 값(매직넘버/문자열)을 ScriptableObject나 CSV로 분리하였는가?
- 씬 전환, Null 참조, 빈 컬렉션 등 엣지 케이스에서 에러를 뿜지 않는가?
```

### 1-6. 세션 및 핸드오프(HANDOFF) 규율
대화가 길어질수록 컨텍스트 품질이 부패합니다. 하나의 의미 있는 마일스톤(작업)이 완료되면 즉시 `git commit`을 하고 세션을 닫습니다.
- 세션 전환 전: [HANDOFF.md](file:///c:/Users/chris/GitHub/LoveAlgorithm/unity_project/HANDOFF.md) 파일의 "다음 액션", "금지선", "진척 사항"을 갱신합니다.
- **형태 문서화 금지**: 프리팹 내부 하이어라키나 단순 클래스 필드 목록 등 코드를 보면 바로 알 수 있는 정보는 절대 마크다운 문서로 기록하지 않습니다 (코드 변경 시 문서가 거짓말이 되는 것을 방지). 오직 "왜"와 "규칙"만 문서로 남깁니다.

### 1-7. Git 커밋 규율
- 한 기능 = 한 커밋 (Atomic Commit)을 엄격히 준수합니다.
- 커밋 메시지 본문에 **"왜"** 변경했는지 이유를 포함합니다.
- 잘못된 AI 코드는 억지로 수정하려 하지 말고 `git restore` 또는 `git reset --hard`로 즉시 되돌리고 새로운 프롬프트로 재시도합니다.

---

## 2. 위험도 기반 리뷰 게이트

모든 변경 사항을 전수 검사하는 것은 시니어 개발자인 감독의 병목을 유발합니다. 위험도에 따라 리뷰 단계를 차등화합니다. Claude는 코드 제안 시 등급을 명시합니다.

| 등급 | 대상 | 리뷰 방식 |
|---|---|---|
| 🔴 Critical | 핵심 골격 (Services, EventBus, Save/Load 스키마) | 감독이 코드 전체 정독 및 최종 승인 |
| 🟠 High | 모듈 간 물리 인터페이스 변경, 세이브 데이터 포맷 수정 | 아키텍처 설계 합의 후 Git Diff 검토 |
| 🟡 Medium | 모듈 내부 세부 로직, 신규 기능 클래스 구현 | 동작 결과 로그/스크린샷 확인 후 Diff 신속 검토 |
| 🟢 Low | SO 에셋 추가, 단순 UI 바인딩/애니메이션 수치 튜닝 | 동작 테스트만 진행 후 즉시 머지 |

---

## 3. 코드 아키텍처 및 통신 표준

> ADR-007로 통신 패턴을 **EventBus + State SO 단일**로 전환했습니다. Service Locator(`Services`)·인터페이스 계약(`I*`)은 전면 폐기 — 신규 코드에서 부활 금지.

### 3-1. 핵심 패턴: EventBus(통지·명령) + State SO(동기 읽기)
피처 간 결합은 두 경로만 씁니다. 직접 호출·서비스 조회는 금지합니다.

```
[Feature A] <====== (독립) ======> [Feature B]
    │                                 │
    ├───> gameState.Day (State SO 직접 읽기) ─ (동기 GET)
    │                                 │
    └───> EventBus.Publish(Event) ───> 구독자 (비동기 통지·명령)
```

1. **동기 상태 조회 (State SO 직접 읽기)**:
   - 런타임 상태는 `GameStateSO` 등 State SO에 담고, 필요한 피처가 인스펙터/부팅으로 주입받아 직접 읽습니다.
     ```csharp
     int day = gameState.Day; // 동기 GET — 서비스 조회 불필요
     ```
   - 동기 *결과*가 꼭 필요한 소수(미니게임 점수, 인라인 Schedule/Username 등)는 **완료 핸들(UniTaskCompletionSource)을 실은 이벤트**로 처리합니다.
   - **순수층 + 얇은 어댑터**: 공식/결정 로직은 순수 static(`*Service`/`*Formula`, State SO 인자, EventBus 무관, EditMode 테스트). EventBus 연결은 얇은 MonoBehaviour 어댑터(`*Controller`/`*Manager`, OnEnable 구독→순수 호출→통지 발행).
2. **비동기 이벤트 전파 (`EventBus`)**:
   - 상태 변화, 연출 트리거 등 일방향 통지는 C# 구조체 이벤트를 정의해 발행합니다.
   - **이벤트 정의**:
     ```csharp
     public struct AffinityChangedEvent
     {
         public string HeroineId;
         public int NewValue;
     }
     ```
   - **발행**:
     ```csharp
     EventBus.Publish(new AffinityChangedEvent { HeroineId = "roa", NewValue = 85 });
     ```
   - **구독 및 해제** (구독 해제 누수 방지를 위해 `OnEnable`/`OnDisable` 짝을 맞춤):
     ```csharp
     private void OnEnable() => EventBus.Subscribe<AffinityChangedEvent>(OnAffinityChanged);
     private void OnDisable() => EventBus.Unsubscribe<AffinityChangedEvent>(OnAffinityChanged);
     ```

### 3-2. 매니저 4개 제한
글로벌 매니저는 오직 `GameManager`, `AudioManager`, `SaveManager`, `UIManager` 4개만 허용됩니다(초과 금지). 그 외 도메인 로직은 순수 static(`*Service`/`*Formula`) + 얇은 어댑터(`*Controller`)로 구현하고, 상태는 State SO에, 통신은 EventBus로 흘립니다. 이 매니저들은 부팅 컴포지션 루트(`GameBootstrap`)가 인스펙터로 State SO를 주입받는 일반 MonoBehaviour입니다(Singleton 강제 아님).

### 3-3. 어셈블리 정의 (asmdef) 구조
컴파일 분리 및 강제 의존성 방향 설정을 위해 피처별 `asmdef`를 활용합니다 (ADR-011).
- `LoveAlgo.Core` (의존성 0 — EventBus·Log·State SO·세이브 스키마 등 인프라)
- `LoveAlgo.Data` (→Core) · 피처 `LoveAlgo.{Affinity,Schedule,Game}` (→Core,Data) · `LoveAlgo.{Save,Audio,Shop}` (→Core) · `LoveAlgo.Narrative` (→Core,Affinity) · `LoveAlgo.UI` (→Core,TMP)
- 교차통신은 Core의 EventBus + State SO만 경유합니다. 서로 다른 피처 asmdef 간 직접 참조는 컴파일 에러로 자동 차단됩니다(인터페이스 계약 없음 — `IFace` 폐기, ADR-007).

### 3-4. 네이밍 컨벤션 (접미사 = 레이어·종류)
접미사 하나로 **(1) 어느 레이어인지 (2) MonoBehaviour인지**를 즉시 파악합니다. 모달/차단/자동소멸 같은 라이프사이클 의미는 이름에 담지 않습니다(그건 코드·이벤트가 표현). 역할을 지는 클래스에만 적용하며, 순수 유틸·데이터·enum(`GameConstants`·`DayLoop`·`CsvUtility`·`ScriptLine` 등)은 서술형 이름을 허용합니다. *(구 7종 UI 분류 표준 폐기 — 모달/팝업 인프라 의존이라 신 아키텍처에 부적합.)*

| 접미사 | 역할 | 종류 | 예 |
|---|---|---|---|
| **`*View`** | 화면 UI 전부 (대사·HUD·스케줄·상점·엔딩·선택지) — State를 읽고 Command를 발행하는 수동 표시 | MonoBehaviour | `DialogueView`, `HudView`, `ScheduleView`, `EndingView` |
| **`*Slot`** | 리스트 내 반복 항목 | MonoBehaviour | `ChoiceSlot`, `ScheduleSlot` |
| **`*Manager`** | 전역 골격 — GameManager·SaveManager·AudioManager·UIManager **4개로 고정**(초과 금지) | MonoBehaviour | (고정 4개) |
| **`*Controller`** | 피처 어댑터 — EventBus 구독 → 순수 호출 → 통지 발행 | MonoBehaviour | `ScheduleController`, `NarrativeController`, `FlowCommandController` |
| **`*Service` / `*Formula`** | 순수 결정·연산 / 순수 수식 (EventBus·Mono 무관, EditMode 테스트) | static | `ShopService`, `AffinityFormula` |
| **`*Parser` / `*Interpreter`** | 순수 파싱 / 해석 (문자열→구조·상태) | static | `ScriptParser`, `ChoiceEffectInterpreter` |
| **`*SO`** | ScriptableObject (상태·밸런스·데이터 정의) | ScriptableObject | `GameStateSO`, `GameBalanceSO` |
| **`*Command` / `*Event`** | EventBus 입력(의도·명령) / 통지(사실 발생) | struct | `ShowDialogueCommand` / `AffinityChangedEvent` |

- **UI는 전부 `*View`** — 화면 단위는 `*View`, 리스트 항목만 `*Slot`. 모달/차단 여부는 이름이 아니라 동작(완료-핸들 대기 등)으로 표현한다.
- **어댑터는 `*Controller`**(구 `*Router` 포함). `*Manager`는 4골격 전용 — 새 매니저 금지(과설계 게이트, HANDOFF 금지선5).
- **씬 배치 vs 프리팹**: 상시 노출 UI(`DialogueView` 등)는 씬 인스턴스 배치, 반복/가끔 노출 항목은 프리팹 동적 생성(`ChoiceSlot`·`ScheduleSlot`).

### 3-5. 씬 GameObject 하이어라키 및 Canvas 분리 정책
씬 하이어라키의 표준 구조는 다음과 같으며, Rebuild 연산 최적화를 위해 Canvas를 물리적으로 분리합니다.
```
Main/
  Main Camera
  EventSystem
  _Bootstrap/         진입점 그룹 — GameBootstrap(컴포지션 루트) + 4매니저 + 피처 컨트롤러(EventBus 구독). State SO 인스펙터 주입.
  _Stage/             Canvas (Camera mode) — ScreenFX, StageRig (인게임 연출)
  _Popup/             Canvas (Overlay mode) — Dimmer, Modal, Notification (팝업 인프라)
  _UI/                Canvas (Overlay mode) — 인게임 메인 UI 그룹 (DialogueView, ScheduleView 등)
```
> 구 표준의 `_Modules/`(글로벌 IService 등록 오브젝트)는 Service Locator 폐기(ADR-007)로 제거됨. 피처 컨트롤러는 EventBus 구독 MonoBehaviour라 `_Bootstrap`에 둡니다.
- **Canvas 분리 정책**: `_UI`(메인 UI)와 `_Popup`(팝업/알림) Canvas를 완전히 구분하여 팝업이 Rebuild될 때 메인 UI의 Canvas가 헛되이 Rebuild되는 성능 저하를 방지합니다.

### 3-6. 코드/자산 폴더 구조 (ADR-011)
코드(휘발)와 자산(보존)을 분리합니다. 구 `Modules/{Module}/{Code,Data,UI,Prefabs}` 자급자족 구조는 폐기되며, 소비처 이식이 끝나는 대로 `_Project/Modules`를 통째 삭제합니다.
- **코드** = `Assets/_Project/Scripts/{Feature}/` (피처별 asmdef). 범용·크로스도메인 UI(HUD·Popup 인프라)는 `Scripts/UI/`에, 특정 피처 전용 인게임 UI(`ScheduleView` 등 `*View`)는 그 피처 asmdef에 둔다 — 도메인+뷰 응집, UI asmdef가 모든 도메인을 참조하는 God화 방지. 이벤트 struct는 `Scripts/Core/Events/`.
- **프리팹** = `Assets/_Project/Prefabs/{Feature}/` (GUID 보존 이동).
- **SO `.asset` 인스턴스** = `Assets/Resources/Data/` (Resources.Load 경로 보존).
- **아트/오디오** = `Assets/_Project/Art`, `Audio` (타입별 중앙화, GUID는 폴더 무관이라 안전).

### 3-7. 모듈별 책임 경계 (명확한 R&R)
- **Narrative vs Stage**: Narrative는 스토리 CSV 명령어를 해석하여 이벤트를 발행하고, Stage는 이벤트를 받아 실제 스프라이트 출력 및 연출(트윈)을 수행합니다.
- **DayLoop vs Schedule vs Simulation**: DayLoop는 날짜 및 큰 페이즈(자유행동일/이벤트일)를 판별하고, Schedule은 자유행동 내부 낮/밤 스케줄을 처리하며, Simulation은 이 모드들을 호스팅하는 뷰 컨테이너입니다.
- **Stats vs Affinity**: Stats는 주인공의 1인 능력치를 다루고, Affinity는 히로인 5명과의 호감도 및 분기 게이트를 관리합니다.
- **Shop vs Inventory**: Shop은 상점 구매(장바구니, 소지금 차감)를 다루고, Inventory는 획득 품목 보관 및 인게임 세션 사용 버프를 다룹니다.


---

## 4. 데이터 설계 (정적/동적 분리)

### 4-1. SO의 최대 함정 방지 (Definition vs Instance 분리)
ScriptableObject(SO)를 사용할 때 가장 흔한 실수는 런타임에 변하는 상태값(예: 현재 호감도, 획득한 아이템 개수 등)을 SO 필드에 직접 쓰는 것입니다. 이는 에디터 상에선 영구 변경되어 빌드 시 버그가 되고 세이브 로드를 깨뜨립니다.

- **Definition (불변 SO 에셋)**: 기획 수치 및 에셋 정의. **런타임 시 오직 읽기 전용.**
  - 예: `ItemDataSO { id, displayName, price, effectValue }`
- **Instance (가변 일반 C# 클래스)**: 런타임에 변하는 동적 상태. 세이브 파일로 직렬화되는 대상.
  - 예: `ItemInstance { itemId, count, isEquipped }`

### 4-2. ID 네이밍 표준
ScriptableObject 에셋이나 CSV 내의 데이터 고유 ID는 아래 포맷을 따릅니다:
- 소문자 + 언더스코어 조합 사용.
- `item_001_pendant`, `todo_017_cleandesk`, `heroine_roa` 등.

---

## 5. 세이브 / 로드 및 직렬화

- **형식**: 디버깅과 마이그레이션이 용이한 JSON 형식을 채택합니다.
- **직렬화 라이브러리**: C# Dictionary 및 다형성 지원의 안정성을 고려하여 `Newtonsoft.Json`을 활용하거나, 이외의 라이브러리 채택 시 반드시 ADR을 기록하고 seam(접점)을 통해 격리하여 작성합니다.
- **저장 시점**: 세션 완료, 하루 종료(DayChanged), 중요 팝업 구매 완료 시 등 유저가 불합리함을 느끼지 않도록 핵심 트리거마다 자동 저장합니다.
