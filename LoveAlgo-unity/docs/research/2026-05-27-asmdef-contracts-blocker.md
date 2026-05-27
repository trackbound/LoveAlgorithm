# ADR: Contracts asmdef 블로커 — C4 진행 제약

작성: 2026-05-27 (Phase C3-5/6 + C4-1 라운드)

## 결론

`LoveAlgo.Common.asmdef`는 성공적으로 분리(자동 검증 161/161 통과).
`LoveAlgo.Contracts.asmdef`는 **현재 불가** — 양방향 의존이 cycle을 만듦.

## 왜 막혔나

Contracts 파일들이 인터페이스 시그니처 / 도큐먼트 cref에 **모듈의 concrete 타입**을 사용한다:

| Contracts 파일 | 외부 모듈 의존 (concrete) |
|---|---|
| IAffinity | `LoveAlgo.Modules.Affinity` — `AffinityInfo`, `PointCategory`, `EndingType` |
| IDayLoop | `LoveAlgo.Modules.DayLoop` — `EventPhase` |
| INarrative | `LoveAlgo.Story` — `ScriptLine`, `DialogueLogEntry` / `LoveAlgo.UI` — `DialogueUI`, `ChoicePopup`, `DialogueShowButton` |
| IStage | `LoveAlgo.Core`, `LoveAlgo.Stage`, `LoveAlgo.Story` |
| ISave | `LoveAlgo.Core`, `LoveAlgo.Story.SaveSystem` |
| IPhone/IShop/ISchedule/ISimulation/ITitle/ITutorial 등 | 각 모듈의 MonoBehaviour UI 타입 |

만약 `LoveAlgo.Contracts.asmdef`를 만들면:
- 그 asmdef가 위 모듈들의 asmdef들을 reference해야 함
- 하지만 그 모듈들은 자기 인터페이스 (IAffinity 등) implement → Contracts asmdef를 reference해야 함
- **→ asmdef 양방향 cycle** (Unity asmdef는 cycle 금지)

## 빠진 사이드 효과

`asmdef ↛ Assembly-CSharp` (asmdef'd 어셈블리는 Assembly-CSharp 참조 불가)이라는
Unity 규칙 때문에, **Contracts asmdef 전에는 어떤 leaf 모듈도 asmdef 불가**:

- MiniGame이 asmdef되려면 `LoveAlgo.Contracts` (SFXClipRequestedEvent 등) 참조 필요
- Contracts가 Assembly-CSharp에 있으면 asmdef'd MiniGame이 그걸 못 봄
- 결과: C4-2 (leaf 모듈 분리)는 Contracts 풀려야 가능

## 풀려면

Contracts를 진짜 leaf로 만들기 — 외부 모듈 concrete 의존 제거. 두 가지 옵션:

### Option A: 데이터/enum을 Contracts로 이동

- `AffinityInfo`, `PointCategory`, `EndingType` → `LoveAlgo.Contracts` 네임스페이스로 이동
- `EventPhase` → 동일
- `ScriptLine`, `LineType`, `NextType`, `DialogueLogEntry`, `BGTransition` → 동일
- Modules의 implementation은 그대로, just 데이터 정의 위치만 이동
- 대규모 import 변경 필요 (수십~수백 파일)

### Option B: UI MonoBehaviour 노출 추상화

INarrative.DialogueUI 같은 게터의 반환 타입을:
- `MonoBehaviour` (base 타입 통일)
- 또는 `IDialogueUI` 인터페이스 신설 (Contracts 안에 정의)

UI에 메서드 호출하던 호출자(Core, Settings 등)가 인터페이스 메서드 호출로 전환.
바깥에서 보면 인터페이스만 보이고 concrete MonoBehaviour는 모듈 내부에만.

대량 작업이고, 인터페이스 표면 결정이 필요 (각 UI의 어떤 메서드를 노출할지).

### 권장

Option A를 먼저 (작은 enum/data 5~7개 이동), Option B는 그 후. 두 단계 모두 자체 라운드 가치.

## 도미노 풀린 정도 (현재)

| 상태 | 항목 | 비고 |
|---|---|---|
| ✅ asmdef 됨 | LoveAlgo.Common | autoReferenced=true |
| 🚫 막힘 | LoveAlgo.Contracts | 위 cycle |
| 🚫 막힘 | 모든 모듈 (MiniGame/Audio/Stage/Save/…) | Contracts 의존 |
| ✅ 도미노 의존 분석 끝남 | C3-1~6 (38 사이트 마이그레이션) | cross-module concrete 호출 0 (Audio 모듈 내부 + DI bridge 1건 제외) |

## 다음 라운드

**C4-Phase A**: 데이터/enum 타입을 Contracts로 이동. 수십 파일의 using 변경.
EditMode 161 테스트가 안전망. 회귀 시 즉시 revert.

**C4-Phase B**: 위 완료 후 Contracts asmdef 시도. 통과하면 leaf 모듈 점진 분리.
