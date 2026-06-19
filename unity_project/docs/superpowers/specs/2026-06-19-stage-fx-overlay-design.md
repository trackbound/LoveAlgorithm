# 스테이지 투명 오버레이 FX (`StageFx`) — 설계 (2026-06-19)

> 위험도: 🟠 High (모듈 간 명령 규약 신설 — Narrative 파서·Core 이벤트·UI 뷰 추가). PlayFx 캐스케이드 통합 diff 검토 + 엣지 자가검증 동봉.
> 최우선 제약: **기존 기능 전부 이전처럼 작동** — 특히 풀스크린 `Video` 명령/`VideoView`는 손대지 않는다.

## 1. 배경 / 목표

스테이지에 캐릭터(ROA)가 배치된 상태에서, 캐릭터 **위·대사창 아래** 레이어로 투명 영상 효과를 얹는다.
참고 자료(`Assets/서류정리/`):
- `서류정리 예시 영상.mp4` — 최종 조립(효과 + 그 위에 실제 ROA가 앉아 표정 변화).
- `서류정리효과.mov` — 효과만 분리한 알파 소스. 좌하단 "Document" 폴더에 서류가 쌓임 → 우측 브라우저 창 2개 슬라이드인 → ✨반짝임 → 페이드아웃 (10.5초).

핵심: 효과는 **투명 영상**으로 재생하고, 그 위 ROA의 **표정만 기존 `Char ... Emote` 명령(코드/스크립트)으로 제어**한다. 효과가 도는 동안 표정이 교체될 수 있어야 하므로 효과는 **논블로킹**이다.

### 1.1 발견된 기술적 제약 (결정 근거)
- 소스 `서류정리효과.mov`는 `qtrle`(QuickTime Animation RLE) + `argb` 코덱이다. **Unity `VideoPlayer`가 재생 못 한다**(H.264/HEVC/VP8/VP9만 지원).
- 이미 들어있던 `Resources/Animation/2_file.mov`도 동일 qtrle → 재생 불가(준비 타임아웃으로 스킵될 뿐). **본 작업에서 제거.**
- 기존 `VideoView`는 풀스크린·불투명·order 32000·블로킹·탭스킵 → 오버레이 용도에 구조적으로 부적합.

## 2. 채택 방향 (감독 승인)

**투명 영상(플랫폼별 인코딩) + 신규 논블로킹 오버레이 명령.** 명령 이름 `StageFx`.

선택 근거: 아트 1:1 재현(감독이 영상 방식 선택). 트리거는 스토리 스크립트 명령(기존 FX들과 동일한 통합).

> 대안 기각:
> - **네이티브 재현**(스프라이트+트윈+파티클) — 크로스플랫폼·경량이나 아트와 100% 동일하지 않아 기각.
> - **PNG 시퀀스 플립북** — 아트 거의 동일·크로스플랫폼이나 프레임수×1080p 메모리 부담으로 기각.

## 3. 아키텍처 (기존 FX 패턴 그대로 — ADR-007)

발행자(Narrative) → 구조체 이벤트 → 구독자(UI 뷰), Service Locator·UI 직접참조 없음. EyeMask/Shake/ColorTint/Video 와 동형.

| 계층 | 신규 항목 | 역할 |
|---|---|---|
| 파서(순수) | `StageFxOverlayParser` (`LoveAlgo.Story`) | `StageFx:<이름>[:Loop]` Value를 `StageFxIntent`로 분해. EventBus·UnityEngine 비의존(EditMode 테스트). 비-`StageFx` head는 `IsValid=false`로 위임 |
| 명령(Core, `LoveAlgo.Events`) | `PlayStageFxCommand` | `readonly struct { string Name; bool Loop; CompletionHandle Handle }` |
| 뷰(UI, `LoveAlgo.UI`) | `StageFxOverlayView` | `PlayStageFxCommand` 구독 → `Resources/Animation/{이름}` 투명 클립을 알파 블렌딩 RawImage로 재생. 자가구성(전용 Canvas) |
| 엔진 | `NarrativeController.PlayFx` 캐스케이드 1줄 | `StageFxOverlayParser.Parse` → 유효 시 명령 발행 후 **핸들 즉시 완료**(논블로킹) |

### 3.1 파서 문법 (`StageFxOverlayParser`)
`VideoParser`와 동형:
```
StageFx:<파일명>[:Loop]
```
- `parts[0]`가 `StageFx`(case-insensitive)가 아니면 `IsValid=false`.
- `parts[1]` = 파일명(필수, 없으면 invalid).
- 이후 토큰 `Loop`(순서무관·케이스무시) → `Loop=true`. 기본 `Loop=false`(1회 재생 후 자동 종료).
- `Skippable` 개념 없음(논블로킹·탭통과라 스킵 불필요).

```csharp
public struct StageFxIntent { public bool IsValid; public string Name; public bool Loop; }
```

### 3.2 엔진 통합 (`NarrativeController.PlayFx`)
`Video` 캐스케이드 항목 **바로 위**에 추가(둘 다 `Resources/Animation` 로드라 인접 배치):
```csharp
var stageFx = StageFxOverlayParser.Parse(line.Value);
if (stageFx.IsValid)
{
    var h = new CompletionHandle();
    EventBus.Publish(new PlayStageFxCommand(stageFx.Name, stageFx.Loop, h));
    // 논블로킹: 즉시 완료시켜 다음 줄(표정 등)로 진행. 오버레이는 자체적으로 끝까지 재생 후 자동 숨김.
    h.Complete();
    yield return WaitNext(line, () => true);
    yield break;
}
```
> 순서 주의: `StageFx`는 `Video`보다 먼저 검사해야 한다(`VideoParser`는 head가 `Video`일 때만 유효하므로 충돌 없음. 안전상 인접·상단 배치).

## 4. 뷰 / 레이어 / 렌더링 (`StageFxOverlayView`)

- **자가구성**: `Awake`에서 전용 `Canvas`(ScreenSpaceOverlay) + `CanvasScaler` + 풀스크린 `RawImage` + `VideoPlayer` + `RenderTexture`를 코드로 구성(`VideoView` 패턴). 씬엔 GO 하나만.
- **레이어**: sortingOrder = **캐릭터(_Stage 캔버스) 위 + 대사 캔버스 아래.** 구현 시 씬의 _Stage/대사 캔버스 실제 order를 확인해 그 사이 정수로 확정(예: _Stage=N, Dialogue=M → N<order<M). `GraphicRaycaster` 불필요.
- **탭 통과**: `RawImage.raycastTarget = false` → 대사 진행 탭이 효과에 막히지 않음(스킵 아님).
- **투명 합성**: `RenderTexture`는 `ARGB32`, `VideoPlayer.renderMode = RenderTexture`, `RawImage`가 알파 블렌딩으로 합성. (Editor/Android에서 VP8 알파 webm의 알파가 보존되는지 구현 중 1차 검증.)
- **재생 안정성(VideoView 검증 패턴 재사용)**: `Prepare()`→`isPrepared` 대기 후 `Play()`(검은 프레임 방지), `waitForFirstFrame`, `loopPointReached`로 종료 감지, `PrepareTimeout` 가드.
- **오디오**: 게임 오디오 **뮤트하지 않는다**(컷씬이 아니라 부가 효과). 효과 자체 사운드가 있으면 Direct 출력(소스 webm은 무음).
- **종료/정리**: 비-Loop는 `loopPointReached`에서 자동 숨김. Loop는 `NarrativeFinishedEvent`/`ResetNarrativeViewsCommand` 구독으로 정리. `OnDestroy`에서 `RenderTexture.Release`.
- **방어**: 클립 없음 → `Log.Warn` 후 즉시 종료(hang 0). 명령은 이미 핸들 완료 상태라 엔진 블로킹 없음.

## 5. 영상 자산 (플랫폼별 인코딩)

| 플랫폼 | 포맷 | 경로 | 상태 |
|---|---|---|---|
| Editor / Android / Standalone | VP8 알파 webm (`yuva420p`) | `Resources/Animation/서류정리.webm` | ✅ 인코딩 검증 완료(252프레임 24fps, ~2.7MB) |
| iOS 실기기 | HEVC + 알파 (`hvc1`) `.mov` | `Resources/Animation/서류정리_ios.mov`(가칭) | ⚠️ **후속(Mac 필요)** |

- iOS HEVC+알파는 Apple `hevc_videotoolbox`(macOS 전용)에서만 인코딩 가능 — 현재 Windows ffmpeg(`hevc_mf`/`libx265`)는 알파 미지원. **Mac 후속 작업**으로 분리.
- 런타임 클립 선택: `#if UNITY_IOS` 분기로 iOS 자산명, 그 외 webm. iOS 자산이 없는 동안 iOS 빌드에서는 **효과만 스킵**(클립 없음 → 경고 후 즉시 종료, hang 없음).
- 변환 커맨드(webm, 재현용):
  ```
  ffmpeg -i 서류정리효과.mov -c:v libvpx -pix_fmt yuva420p -auto-alt-ref 0 -b:v 2M -an 서류정리.webm
  ```
- 기존 재생 불가 `Resources/Animation/2_file.mov`(qtrle) 제거.

## 6. 사용 예 (스토리 CSV)

```
StageFx:서류정리        # 오버레이 시작(논블로킹, 즉시 다음 줄)
Char:C:로아:집중
Wait:3
Char:C:로아:기쁨
Wait:3
Char:C:로아:활짝
```
효과 10.5초 동안 표정이 시퀀싱된다. 효과는 끝나면 자동으로 사라진다.

## 7. 테스트 / 검증

- **EditMode(순수 파서)**: `StageFxOverlayParser` — `StageFx:서류정리`(기본), `:Loop`, 빈/비-StageFx head(위임), 대소문자, 잉여 콜론.
- **에디터 동작**: 스토리에 `StageFx:서류정리` 줄 추가 → ① 캐릭터 위·대사 아래로 효과가 투명하게 뜨는지 ② 효과 중 `Char Emote`로 표정이 바뀌는지 ③ 끝나면 자동 사라지는지 ④ 탭이 대사로 통과되는지 ⑤ 클립 없을 때 스킵·hang 없음.
- **회귀**: 기존 `Video:` 풀스크린 재생 정상.

## 8. 범위 밖 (후속)

- iOS HEVC+알파 자산 인코딩(Mac) 및 `#if UNITY_IOS` 클립 선택 실배선.
- 효과 위치/스케일 튜닝 SO(현재 풀스크린 고정).
- 효과 자체 사운드.
