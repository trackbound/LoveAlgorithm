# Story CSV Commands Reference

자동 생성 파일 — `Tools/Story/Generate FX Reference` 메뉴로 갱신.
기본값 출처: `Assets/Resources/Data/FXDefaultsConfig.asset` (로드 성공)

## 컬럼

`LineID, Type, Speaker, Value, Next`

- **LineID**: 점프 anchor (선택). 비워두면 순차 실행.
- **Type**: `Text` / `Char` / `BG` / `CG` / `SD` / `Overlay` / `Sound` / `FX` / `Flow` / `Choice` / `Option` / `Place`
- **Speaker**: 화자(캐릭터 ID 또는 displayName). 비워두면 나레이션.
- **Value**: 명령별 페이로드 (콜론 구분).
- **Next**: `>` (즉시) / `click` (클릭 대기) / `await` (액션 완료까지) / 숫자 (초 단위 대기)

## FX 명령

| 명령 | 시그니처 | 기본값 | 별칭 |
|---|---|---|---|
| `FadeOut` | `FadeOut:[duration]` | duration=0.9s | — |
| `FadeIn` | `FadeIn:[duration]` | duration=0.9s | — |
| `Flash` | `Flash:[duration]` | duration=0.14s | — |
| `CamShake` | `CamShake:[duration[:strength]]` | duration=0.3s, strength=Medium(25) | Shake |
| `StageShake` | `StageShake:[duration[:strength]]` | duration=0.3s, strength=Medium | — |
| `DialogueShake` | `DialogueShake:[duration[:strength]]` | duration=0.3s, strength=Medium | — |
| `CamZoom` | `CamZoom:[zoomLevel[:duration]]` | zoom=1.0, duration=0.5s | Zoom |
| `CamPan` | `CamPan:x:y[:duration]` | x=0, y=0, duration=0.5s | Pan |
| `CamReset` | `CamReset:[duration]` | duration=0.4s | Reset |
| `ColorTint` | `ColorTint:preset[:alpha[:duration]]` | preset, alpha=0.25, duration=0.5s | Tint |
| `EyeOpen` | `EyeOpen:[duration]` | duration=0.8s | Open |
| `EyeClose` | `EyeClose:[duration]` | duration=0.8s | Close |
| `EyeCloseImmediate` | `EyeCloseImmediate:(인자 없음)` | — | — |
| `EyeBlink` | `EyeBlink:[close[:open[:hold]]]` | close=0.1s, open=0.15s, hold=0.05s | Blink |
| `CharShake` | `CharShake:[slot[:strength[:duration]]]` | slot, strength=18, duration=0.3s | — |
| `CharJump` | `CharJump:[slot[:height[:duration]]]` | slot, height=35, duration=0.3s | — |
| `CharDim` | `CharDim:[slot[:alpha[:duration]]]` | slot, alpha=0.4, duration=0.3s | — |
| `CharGlitch` | `CharGlitch:[slot[:strength[:duration]]]` | slot, strength=1, duration=0.6s | — |

> Shake 강도는 숫자(`30`) 또는 프리셋(`Weak`/`Medium`/`Strong`) 모두 가능. 케이스 무시.

## 영상 (Video)

전체 화면 컷씬 영상 재생 — 대화창·HUD 위(최상위)에 떠서 인트로·컷씬 연출용.
리소스: `Resources/Animation/{파일명}` (VideoClip으로 임포트). *(수기 유지 — FX 튜닝 SO가 없어 자동생성 표에 안 들어감.)*

### 시그니처

```
FX,,Video:파일명[:Loop][:NoSkip|:Skippable],await
```

| 토큰 | 동작 | 비고 |
|---|---|---|
| (없음) | 1회 재생 후 종료까지 대기 | 기본 스킵 가능 |
| `Loop` | 무한 반복 — **비블로킹**(즉시 다음 라인 진행, 영상은 배경 유지) | 종료/도구정리 시 정리 |
| `NoSkip` | 클릭 스킵 불가 (1회성 인트로용) | 클릭은 흡수만 |
| `Skippable` | 클릭 스킵 허용 (명시 — 기본값) | — |

- 플래그는 **순서 무관·케이스 무시**. `Next`는 보통 `await`(영상 끝까지 대기) — `Loop`는 await여도 즉시 통과.

### 동작

- **입력 차단**: 영상이 화면 전체를 덮어 아래 UI 클릭이 막힌다. 내러티브 진행도 정지(await 대기).
- **오디오 격리**: 컷씬(non-Loop) 동안 게임 BGM/SFX 자동 뮤트, 영상 자체 사운드는 재생. 종료 시 복원.
- **스킵**: 기본 클릭 스킵 가능(시작 0.3s는 실수 스킵 방지로 무시). `NoSkip`이면 클릭을 흡수만 하고 스킵 안 됨.
- **안전**: 파일이 없으면 경고 로그 후 즉시 통과 — 흐름이 막히지 않는다.

### 예시

```
FX,,Setup:BG=빈 화면,>
FX,,Video:roa_CG01_intro:NoSkip,await         # 인트로 컷씬 (스킵 불가, 끝까지 재생)
Text,로아,기다리고 있었어!,click
```

> 영상은 `Resources/Animation/`에 두고 **코드 파일명을 직접 기입**(별칭 카탈로그 미적용). H.264는 baseline 프로파일 권장 — 아니면 임포트 설정에서 **Transcode**를 켠다(재생 타임스탬프 경고 방지).

## 매크로

| 매크로 | 인자 | 기본값 |
|---|---|---|
| `DayStart` | `[bgPath][:Wake\|Cut\|Reveal][:actionCount]` | mode=Wake |
| `DayEnd` | `[fadeDuration][:Wake\|Cut]` | fadeOut=0.8s, fadeIn=0.3s, mode=Wake |
| `NextDay` | `[Wake\|Cut][:bgPath][:actionCount]` | DayEnd + DayStart 일괄 |
| `SceneStart` | `[bgPath[:EyeClose]]` | eyeOpen=0.6s, pauseAfter=0.4s |
| `SceneEnd` | `[fadeDuration]` | 0.5s |
| `LoadingScene` (alias `Loading`) | `[displayTime]` | 2.0s |
| `Setup` | `BG=...\|BGM=...\|Char=...[:slot]\|Overlay=...` | 즉시(Cut) 전환 |
| `Wait` | `[seconds]` | 1.0s |
| `DialogueHide` | — | — |
| `DialogueShow` | — | — |

## 씬 전환 패턴

> **핵심 차이**:
> - **Wake**: 검은 화면에서 EyeMask로만 가린 상태 → **대사창은 보임** (눈 감고 모놀로그 가능)
> - **Cut**: ScreenFX가 풀 암전 → **대사 불가**, 다음 씬은 페이드인으로 reveal

### Pattern A — Wake (잠들기 → 다음날 아침)
```
FX,,DayEnd,await                              # = DayEnd:Wake (기본)
Text,,(잠이 들었다...),click
FX,,DayStart:BG_Room_Morning,await            # 눈 감은 상태 유지
Text,로아,(어... 벌써 아침인가),click          # 눈 감은 채 모놀로그
FX,,Open:0.8,await                            # 눈 뜨기
Char,,Enter:로아:Default,await
```

### Pattern B — Cut (다른 장소로 즉시 전환)
```
FX,,DayEnd:Cut,await
FX,,DayStart:BG_Cafe_Day:Cut,await            # 풀 암전 → BG 페이드인
Char,,Enter:로아:Default,await
Text,로아,왔어?,click
```

### Pattern C — NextDay sugar (한 줄)
```
FX,,NextDay:Wake:BG_Room_Morning,await        # Pattern A 한 줄
FX,,NextDay:Cut:BG_Cafe_Day,await             # Pattern B 한 줄
```

### Pattern D — Loading scene 거쳐서 전환
```
FX,,DayEnd:Cut,await
FX,,Loading:2.0,await                         # = Flow,,LoadingScene:2.0
FX,,DayStart:BG_Cafe_Day:Cut,await
```

## PC 잠금 화면 (LockScreen)

기획서 §진입 정보: 게임 첫 시작 시 타이틀 대신 잠금 화면이 5초 페이드인으로 등장.
그 후 스토리 진행 중에도 비번 입력 연출이 필요할 때 호출.

### Flow 명령 시그니처

```
Flow,,LockScreen:<mode>[:Time=HH:mm][:FadeOut|NoFadeOut],await
```

| 모드 | 설명 |
|---|---|
| `FirstSetup` | 비번 첫 설정 (이름 입력 직후) — 평문 입력, 마스킹 없음 |
| `Normal` | 평상 잠금화면 — 저장된 비번 검증, * 마스킹 |
| `Reset` | 재설정 — 기존 비번 확인 없이 새 비번 입력 (FirstSetup 흐름) |
| `Auto` | 비번 설정 여부 자동 판별 — 있으면 Normal, 없으면 FirstSetup |
| `GameStart` | 게임 첫 시작 sugar — 5초 페이드인 강제 + Auto |

**옵션 토큰** (순서 자유, 케이스 무시):
- `Time=HH:mm` — 시계 1회 오버라이드
- `FadeOut` — Outro에 페이드아웃(black→0)까지 포함 → 완료 시 화면 노출됨
- `NoFadeOut` — 페이드아웃 생략 (기본 — 검은 화면으로 끝남, 다음 라인이 FadeIn 처리)

### 사용 패턴

#### A — 게임 첫 시작 (EntryRouter가 자동 호출, 보통 CSV 불필요)
```
# 거의 사용 안 함 — EntryRouter가 GameStart를 자동 실행
Flow,,LockScreen:GameStart:FadeOut,await
```

#### B — 이름 입력 직후 비번 설정 (튜토리얼)
```
Text,로아,이름이 뭐야?,click
Flow,,Username,await                          # 이름 입력
Text,로아,이제 비밀번호도 설정하자.,click
Flow,,LockScreen:FirstSetup,await             # 비번 첫 설정 — 다음 라인이 검은 상태에서 시작
FX,,FadeIn:3,await                             # 검은 화면 페이드아웃
Text,로아,비밀번호 잘 기억해.,click
```

#### C — 스토리 중 재로그인 (다른 날 시작 등)
```
Flow,,LockScreen:Normal:Time=07:30:FadeOut,await
Text,로아,일어났어?,click
```

#### D — 비번 자동 판별 (이미 설정됐는지 모를 때)
```
Flow,,LockScreen:Auto:Time=23:58,await        # IsPasswordSet ? Normal : FirstSetup
```

### 분기 흐름 요약

```
게임 실행
├─ 비번 없음 (첫 실행) ──→ EntryRouter → LockScreen:GameStart (5s 페이드인) → FirstSetup → 비번 저장 → Title
└─ 비번 있음 (재실행)   ──→ EntryRouter → Title (LockScreen 우회)

스토리 진행 중
├─ 이름 입력 직후      ──→ Flow,,LockScreen:FirstSetup,await
├─ 하루 시작 연출      ──→ Flow,,LockScreen:Normal:Time=07:30:FadeOut,await
└─ 비번 분실 흐름       ──→ Flow,,LockScreen:Reset,await
```

## 메신저 (Messenger)

스토리에서 메신저 시퀀스를 도착시킨다(별칭 `Message`). 시퀀스 내용은 별도 메신저 CSV
(`StreamingAssets/Messenger/`)에 작성하고, 시퀀스 id ↔ CSV 매핑은 `MessengerScriptCatalog.asset`에 등록한다.

```
Flow,,Messenger:<시퀀스id>,>            # 도착만(폰 버튼 진동·배지) — 스토리는 즉시 계속
Flow,,Messenger:<시퀀스id>:Wait,await   # 확인 필수 — 유저가 메신저를 열어 끝까지 읽을 때까지 정지
```

- `Wait` 없음 = 백그라운드 도착. 유저가 나중에 폰 버튼/빠른 메뉴로 열람.
- `Wait` = 기획서 "확인 필수인 메시지" — 진동 후 읽힘까지 스크립트 대기(선택지 응답 포함).
- 카탈로그 미등록 id는 경고 후 스킵(대기도 즉시 해제) — 진행이 막히지 않는다.
- 일자 자동 도착(예: 이벤트 전날 약속 메시지)은 CSV가 아니라 카탈로그의 `deliverDay`로 지정.
- 메신저 CSV 선택지의 `Love:{히로인}:{n}` 효과는 **호감도 정본 id**(`Roa`/`HaYeEun`/`SeoDaEun`/`LeeBom`/`DoHeewon`,
  스토리 `Affinity:` 명령과 동일)를 쓴다 — 발신자(Speaker)의 `c0X`(친구/에셋 id)와 다른 id 공간이니 주의.

## Char 액션

```
Char,,[slot:]Enter:캐릭터[:표정[:오버레이]]    # 페이드 등장
Char,,[slot:]EnterUp:캐릭터[:표정[:오버레이]]  # 아래→위 슬라이드 등장
Char,,[slot:]Emote:표정                       # 표정만 변경
Char,,[slot:]Exit                              # 페이드 퇴장
Char,,[slot:]ExitDown                          # 아래로 슬라이드 퇴장
Char,,[slot:]Clear                             # 즉시 숨김
```

- **slot 생략 시 자동으로 `C` (중앙)** — 신규 단축 문법.
- slot: `L` / `C` / `R` (또는 `Left`/`Center`/`Right`).

## BG 전환

| 토큰 | 동작 | 별칭 |
|---|---|---|
| `Cut` | 즉시 교체 | — |
| `Fade` | 페이드아웃→교체→페이드인 (기본 0.5s) | — |
| `CrossFade` | 크로스페이드 | `Cross` |

## 작성 팁

1. **명령은 케이스 무시** — `FadeOut`, `fadeout`, `FADEOUT` 모두 동일.
2. **별칭 활용** — `FX,,Shake,await` = `FX,,CamShake,await`.
3. **Char 슬롯 생략** — `Char,,Enter:로아:Default,await` (슬롯 C 자동).
4. **기본값 생략** — `FX,,FadeOut,await`만 써도 SO 기본값 적용.
5. **검증** — `Tools/Story/Validate All Story CSV` 실행 시 오타·인자 갯수 오류 콘솔 출력.

