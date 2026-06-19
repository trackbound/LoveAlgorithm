# 첫 실행 ROA 메시지 인트로 (자동 연출) — 설계

- **작성일**: 2026-06-19
- **범위**: `01_message` 단일 화면 인트로 (시리즈 1편). 후속 편(02, 03…)은 별도 스펙.
- **위험도**: 🟠 High — 모듈 간 인터페이스 추가(`MessageStack` 완료 신호), asmdef 참조 변경, 씬 핸드오프.
- **참고 자료**: `Assets/첫실행/01_message.mp4` (목업 타깃), 슬라이스 에셋 `_bg/header/message_box/warn/audio/todo/todo checkbox.png`.

---

## 1. 목적 & 연출 의도

플레이어가 게임을 설치하고 **완전 첫 구동**하는 순간, 히로인 AI **ROA**가 메신저로 말을 거는 도입부를 보여준다. 자정 직전(23:58)의 밤, "기다리다 지친" ROA가 연달아 메시지를 보내는 짧은(≈10초) 자동 연출. 끝나면 곧장 프롤로그로 넘어간다.

**무입력 자동 연출** — 클릭/탭 없이 애니메이션처럼 자동 진행:

```
게임 첫 실행
  → 오버레이 콘텐츠 페이드인
  → 좌측 warn 위젯만 idle 흔들림 (audio·todo·시계는 정적)
  → 메시지가 아래→위 슬라이드인하며 스택 누적
  → 블랙 크로스페이드 → 프롤로그 시작
```

---

## 2. 확정 결정 사항 (브레인스토밍 합의)

| 항목 | 결정 |
|---|---|
| 범위 | `01_message` 1화면만. 끝에서 기존 새게임→프롤로그로 핸드오프 |
| 진행 | 무입력 자동(탭/클릭 없음). 기존 탭→넘김 폐기 |
| 메시지 저장 | `MessageSequenceSO` 데이터 자산(인스펙터에 줄 입력 — "가볍게 인스펙터 튜닝" 의도 충족) |
| 메시지 문구 | placeholder 4줄, 인스펙터에서 교체 가능: `아니야?` / `왜 안 와...` / `보고 싶어!` / `앗, 온, 온 거 같은 기분이 들어` |
| 좌측 HUD | `warn.png` 흔들림 / `audio.png`·`todo.png` 정적 |
| 상단 시계 | `23:58` 고정 TMP 텍스트 |
| 버블 구성 | 고정 헤더(`header.png` "MODE: AWAITING YOU ♥") 1개 + 각 메시지는 `message_box` 버블. 카드 `Message from ROA` 라벨 유지 |
| 텍스트 등장 | 슬라이드인만(타이핑 없음). 통째 즉시 표시 |
| 사운드 | 메시지 도착 SFX 후크만 마련(`AudioClip` 필드, 음원은 추후 감독이 추가, null=무음) |
| 타이밍 | 인스펙터 노출 + 영상 유사 기본값 |
| 완료 감지 | `MessageStackController`에 plain C# `event Action Completed` 추가(안정 변형) |
| 크로스페이드 | 씬 로드 가로질러 생존하는 블랙 브리지(DontDestroyOnLoad) |
| asmdef | 기존 `LoveAlgo.UI`에 신규 코드 추가 + UI asmdef에 `LoveAlgo.MessageStack` 참조 1개 추가(1안) |

---

## 3. 아키텍처

```
[App 시작]
 FirstLaunchBootstrap (기존) ── flag 미표시 + Title 씬(buildIndex 0) ──► FirstLaunchOverlay Instantiate + MarkSeen
        │
        ▼
 FirstLaunchDirector (신규, 오케스트레이터)
   ① 오버레이 콘텐츠 CanvasGroup 페이드인
   ② WarnWidgetShake 시작 (audio·todo·시계 정적)
   ③ MessageStackController.Play()            ← 기존 부품 재사용
   ④ Completed 이벤트 대기 + 짧은 hold
   ⑤ FirstLaunchTransitionBridge(블랙) 생성
        │
        ▼
 FirstLaunchTransitionBridge (신규, DontDestroyOnLoad)
   블랙 페이드인 → StartNewGameCommand 발행 → 씬 로드 생존 → 새 씬 부팅 후 블랙 페이드아웃 → 자기 파괴
        │
        ▼
 SceneFlowController(기존) → Game 씬 → GameBootstrap(NewGame) → 프롤로그(Setup:BG=블랙 → Video:1_intro)
```

핸드오프 근거: `Prologue.csv`는 `Setup:BG=블랙` → `Video:1_intro:NoSkip`으로 시작하므로 블랙에서 출발한다. 브리지의 블랙 페이드아웃이 프롤로그 블랙으로 자연 연결된다. 또 `SceneManager.LoadScene`은 동기 로드라 교체 순간 1~2프레임 끊김이 있을 수 있는데, 생존 블랙 브리지가 그 틈을 덮어 컷/번쩍임을 제거한다.

---

## 4. 컴포넌트 & 경계

### 4.1 신규/수정 코드

| 컴포넌트 | 종류 | 책임 | 의존 |
|---|---|---|---|
| `FirstLaunchDirector` | 신규 MonoBehaviour | 연출 타임라인 소유: 페이드인 → 흔들기 → 스택 재생 → 완료 대기 → Bridge 생성. 모든 타이밍 인스펙터 노출 | `MessageStackController`(Play/Completed), `WarnWidgetShake` |
| `FirstLaunchTransitionBridge` | 신규 MonoBehaviour | DontDestroyOnLoad. 블랙 Image(자체 Canvas, 고sortOrder) 페이드인 → `StartNewGameCommand` 발행 → postLoadHold 후 페이드아웃 → 파괴. 코루틴 lerp | `LoveAlgo.Common`(EventBus), `LoveAlgo.Events`(StartNewGameCommand) |
| `WarnWidgetShake` | 신규 MonoBehaviour | warn 위젯 idle 흔들림(사인 기반 anchoredPosition/회전 흔들림 코루틴, DOTween 미사용=기존 관례). 진폭/주기 인스펙터 | — |
| `MessageStackController` | 기존 + 최소수정 | `public event Action Completed;` 추가 — `PlayRoutine` 종료 시 1회 발화. EventBus 미도입(자급자족 유지). 기존 동작 불변 | — |
| `FirstLaunchOverlayView` | **폐기** | 탭→넘김 제거. 흐름은 Director가 소유 | — |

### 4.2 신규 데이터/에셋

| 에셋 | 종류 | 내용 |
|---|---|---|
| `FirstLaunchMessages.asset` | `MessageSequenceSO` | senderName=`ROA`, startDelay≈0.5, 4줄 placeholder(+per-line delay≈1.2) |
| `FirstLaunchOverlay.prefab` | 재구성 | 아래 §5 하이어라키 |
| `_bg/header/warn/audio/todo.png` | Sprite 임포트 | UI Image 바인딩용 Sprite(2D/UI)로 임포트 설정 확인 |

### 4.3 재사용(무변)

- `MessageStackController` 의 슬라이드/스택/maxVisible 로직 (Completed 이벤트만 추가)
- `MessageCardView`, `MessageCard.prefab` — 카드 1장(헤더/본문). 아트 톤만 필요 시 스킨 조정
- `FirstLaunchBootstrap`, `FirstLaunchFlag`, `FirstLaunchMenu` — 첫구동 판정/스폰/디버그 리셋

---

## 5. 프리팹 하이어라키

```
FirstLaunchOverlay (Canvas sortOrder=200, CanvasScaler 1920×1080, GraphicRaycaster)
├─ CanvasGroup                  ← Director 페이드인 대상
├─ Background  (Image: _bg.png, 풀스크린, raycastTarget=off)
├─ HUD
│  ├─ Clock   (TMP "23:58", 상단 중앙)
│  ├─ Warn    (Image: warn.png) + WarnWidgetShake
│  ├─ Audio   (Image: audio.png, 정적)
│  └─ Todo    (Image: todo.png, 정적)
├─ Messages
│  ├─ Header        (Image: header.png — 고정 1개)
│  └─ StackContainer(RectTransform)
│        + MessageStackController(cardPrefab=MessageCard, sequence=FirstLaunchMessages, playOnStart=false)
└─ FirstLaunchDirector
```

프리팹 조립은 Unity MCP로 직접 구성(YAML 수작업 회피). `FirstLaunchTransitionBridge`는 프리팹에 두지 않고 Director가 런타임 생성(DontDestroyOnLoad 단독 객체) — 또는 별도 경량 프리팹을 Director가 Instantiate.

---

## 6. 연출 타임라인 & 타이밍

| 단계 | 기본값(초) | 파라미터 위치 |
|---|---|---|
| 콘텐츠 페이드인 | 0.6 | Director |
| (warn 흔들림) | 상시 | WarnWidgetShake (진폭/주기) |
| 첫 버블 전 대기 | 0.5 | SO startDelay |
| 버블 간격 | 1.2 | SO per-line delay |
| 버블 상승/시프트 | 0.35 / 0.3 | MessageStackController(기존) |
| 마지막 버블 후 hold | 1.5 | Director |
| 블랙 페이드인 | 0.8 | Bridge |
| postLoadHold(씬 부팅 대기) | 0.2 | Bridge |
| 블랙 페이드아웃 | 0.8 | Bridge |

---

## 7. 데이터 흐름 & 엣지 케이스

- **플래그**: 기존대로 스폰 직후 `MarkSeen()`. 연출 중 강제종료해도 다음 구동은 타이틀. (변경 없음)
- **프리팹 부재**: 기존 fail-open 유지(미마킹 + 스킵 → 타이틀).
- **무입력 보장**: Background/카드 `raycastTarget=off`로 탭이 흐름에 개입 불가.
- **SFX 후크**: Director(또는 카드 스폰 경로)에 `AudioClip` 직렬화 필드 + `AudioSource`. 버블 등장마다 재생, null이면 무음. 음원만 추후 바인딩.
- **씬 가드**: 기존 `FirstLaunchBootstrap`의 buildIndex==0 가드 유지(에디터에서 게임 씬 직접 Play 시 개입 방지).
- **Bridge 생존**: DontDestroyOnLoad. 발행→씬 로드→페이드아웃→파괴까지 Director 파괴(씬 언로드)와 무관하게 자기완결.

---

## 8. 테스트 (🟠 증거)

- **EditMode**
  - `FirstLaunchMessages` SO: sender=`ROA`, 줄 수/문구 검증.
  - 기존 `FirstLaunchFlagTests` 유지.
- **PlayMode**
  - 기존 `MessageStackPlayModeTests` 그대로 그린(부품 무변, Completed 추가는 비파괴).
  - 신규: Director가 `Completed` 후 `StartNewGameCommand`를 정확히 1회 발행하는지(EventBus 구독 카운트).
  - 신규: Bridge가 발행 후 자기 파괴하는지.
- **에디터 수동**: `Tools/Debug/Reset First Launch` → Play → 페이드인·warn 흔들림·메시지 스택·블랙→프롤로그 핸드오프 육안 확인.

---

## 9. 비범위 (YAGNI)

- 후속 인트로 편(02, 03…) — 별도 스펙.
- 타이핑 효과, 실제 시각 시계, WARNING/TODO 항목 동적 갱신 — 제외.
- 실제 SFX/BGM 음원 제작 — 후크만 제공.
- 스킵 버튼/입력 — 무입력 연출이므로 제외.
