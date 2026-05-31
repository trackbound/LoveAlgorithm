# Headless Mode — 미니 ADR

날짜: 2026-05-22  
상태: 채택  
스코프: Phase B 자동화 테스트의 기반 (B2 PlayMode smoke의 사전 조건)

## 문제

자동화 테스트(B2 예정)가 "프롤로그 시작 → DayLoop 도달"이나 "CSV 한 편 끝까지 진행"을
검증하려면 스크립트가 UI 입력 없이도 끝까지 흘러야 한다. 현재 코드의 진입점들은 UI가
없으면 무한 대기에 빠지거나(`WaitForInlineScheduleAsync`, `MessageFlowCommand:wait`)
의도치 않은 분기로 빠진다(`UsernameFlowCommand`는 UI 없으면 그냥 return — 이름 미설정).

ChoiceLineExecutor에만 "UI 없으면 첫 선택지 자동" 분기가 있어 일관성도 없다.

## 결정

`LoveAlgo.Common.Headless` 정적 토글을 도입하고, **사용자 입력을 기다리는 모든 진입점**을
`Headless.IsEnabled` 분기로 일관 처리한다.

- 기본값 `false` — 일반 플레이는 100% 동일하게 동작.
- 테스트(향후 B2)가 setup에서 `Headless.Enable()`, teardown에서 `Headless.Disable()`.
- 도메인 리로드/PlayMode 진입 시 자동 false 복원 (N1 패턴).

## 진입점별 헤드리스 규약

| 진입점 | 일반 플레이 | 헤드리스 |
|---|---|---|
| `ScriptEngine.WaitForClickAsync` | 클릭 또는 Auto 딜레이 대기 | 즉시 통과 (자동 click) |
| `ChoiceLineExecutor` (UI 없음 또는 Headless) | ChoicePopup 표시 후 선택 대기 | 첫 선택지 자동 선택 + 점프 |
| `UsernameFlowCommand` | UsernameUI에서 이름 입력 대기 | 기본 이름("테스터")으로 즉시 설정 + 통과 |
| `ScheduleFlowCommand` | ScheduleUI에서 1회 선택 대기 | 인라인 스케줄 흐름 즉시 완료 (효과 미적용) |
| `MessageFlowCommand:wait` | 폰 열고 사용자 응답 대기 | 즉시 통과 (메시지 수신만 반영) |
| `MiniGameFlowCommand` | 미니게임 인스턴스화 + 결과 대기 | launch 자체 skip — 다음 라인으로 |
| `LockScreenFlowCommand` | 잠금화면 모드 진입 대기 | 이미 `ls == null` 분기로 통과 (변경 없음) |
| `LoadingSceneFlowCommand` | 시간 기반 fade | 변경 없음 (시간만 흐름 — 테스트는 짧음) |
| `Sound:BGM:*`, `BG`, `Char`, `CG`, `FX`, `Place`, `Overlay`, `SD` | UI/Stage 호출 | 변경 없음 (이미 null fallback이 있고, 부작용 미미) |
| `Flow:Save` | AutoSaveAsync | 변경 없음 (저장 자체는 헤드리스에서도 동작) |

### 애매한 케이스 처리

- **ScheduleFlowCommand**: 헤드리스에서 어떤 스케줄을 자동 선택할지가 문제. 효과를
  적용하면 게임 상태가 변하고 테스트의 다음 단계에 영향. **결정**: 효과 미적용 + 즉시
  완료. 테스트는 "스크립트가 끝까지 흘렀는가"만 확인하고, 스케줄 효과 검증은 별도 단위
  테스트가 책임.
- **UsernameFlowCommand**: 기본 이름을 비우면(`""`) 변수 치환이 빈 문자열이 돼 대사가
  어색해짐. **결정**: `"테스터"`로 자동 설정 — 디버그 흐름과 일관.
- **MiniGameFlowCommand**: 점수에 따라 분기가 달라지는 게임은 없으므로 skip이 안전.
  향후 점수 기반 분기가 생기면 ADR 갱신.
- **MessageFlowCommand**: `wait` 없는 형식은 그대로(즉시 통과). `wait`도 즉시 통과 —
  메신저에 메시지 수신은 이미 반영됨 (`MessengerManager.ReceiveMessage` 호출 후 분기).

## 인터페이스

```csharp
namespace LoveAlgo.Common
{
    public static class Headless
    {
        public static bool IsEnabled { get; private set; }
        public static void Enable()  => IsEnabled = true;
        public static void Disable() => IsEnabled = false;
    }
}
```

- 가장 단순한 정적 토글. 테스트가 setup/teardown에서 호출.
- 도메인 리로드 시 false로 복원 (`RuntimeInitializeOnLoadMethod`).
- 향후 ScriptableObject로 옵션(예: `AutoClickDelay`, `DefaultUsername`)을 외부화할 여지는
  남기되, 지금은 상수로 충분.

## 비채택 옵션

- **컨텍스트 객체 주입 (`HeadlessContext`)**: 깔끔하지만 호출 체인 깊이가 깊어 모든
  메서드 시그니처에 매개변수 추가 비용 큼. ADR 범위(B1)를 넘어섬.
- **인터페이스 분리 후 Mock 주입**: Phase C 인터페이스 분리 후에 더 자연스럽게 가능.
  지금은 정적 토글이 적절한 단순함.

## Phase 연결

- **B2**: 이 토글을 켜고 프롤로그 CSV를 끝까지 돌리는 smoke test 작성.
- **Phase C** (모듈 인터페이스 분리 후): `IHeadlessOptions` 같은 인터페이스로 외부화
  검토. 토글이 충분히 단순해서 그대로 둬도 무방.

## 회귀 안전

기본값 `IsEnabled = false`에서 모든 분기가 옛 동작과 동일해야 한다. 진입점마다
`if (Headless.IsEnabled) { ... } else { 기존 흐름 }` 형태로만 추가 — 기존 코드는
elseif 분기 안에 그대로.

회귀 검증은 B2 smoke test가 들어오기 전까지는 사용자의 PlayMode 진입 확인에 의존.
