# 스테이지 상태 세이브 — 설계 (스펙)

> 작성 2026-06-17 · 위험도 **🔴 Critical (세이브 스키마 가산)** — 감독 정독+승인, 컴파일·테스트 동작 증거 필수.
> 선행 슬라이스 = `481b6fb` 스토리 위치 세이브(BG/BGM/Char 미러). 이 문서는 그 위에 연출 지속 상태를 가산한다.

---

## 1. 목적

스토리 중 세이브 → 로드 시 **그 장면을 시각적으로 동일하게 재현**한다. 직전 슬라이스가 BG/BGM/슬롯
캐릭터까지 미러했으나 **틴트·아이마스크·스테이지 레이어(SD/Overlay)** 지속 상태는 비저장이라, 로드 시 화면
보정이 풀리거나(틴트) 가려져야 할 화면이 노출(아이마스크 닫힘)되거나 떠 있어야 할 레이어가 사라진다.
이번 슬라이스가 그 간극을 메운다.

**성공 기준**: 스토리 진행 중 임의 시점에 세이브 → 타이틀 → 이어하기 했을 때, BG/BGM/캐릭터에 더해
화면 색 보정·눈꺼풀 닫힘·SD/Overlay 레이어가 저장 순간과 동일하게 복원된다.

## 2. 범위

| 상태 | 저장? | 근거 |
|---|---|---|
| ColorTint (색 보정) | ✅ | 화면에 지속적으로 걸린 상태. 로드 시 유지돼야 분위기 일치. |
| EyeMask 닫힘 | ✅ | 암전 모놀로그 등 눈 감은 채 진행하는 장면 — 미저장 시 로드하면 가려져야 할 화면 노출. |
| StageLayer SD / Overlay | ✅ | 지속 이미지 레이어(SD 캐릭터·캐릭터 테마 Overlay). 미저장 시 로드 후 사라짐. |
| CG 레이어 | ❌ | CG 진입 시 뷰가 `SetCgModeCommand` 발행 → 대사창+인포바 숨김 → **수동 세이브 구조적 차단**. 저장해도 복원 트리거 없음(YAGNI). |
| Shake (흔들림) | ❌ | 순간 임팩트값 — 상태가 아님. |
| 인라인 `<emote>` 표정 | ❌ | 기존과 동일(컨트롤러 발행분만 미러). |

## 3. 접근

기존 BG/BGM/Char 미러 패턴을 그대로 확장한다. 엔진(NarrativeController)이 명령 발행 직전에 *해석된
최종값*을 State SO에 미러 → SaveManager가 언제 직렬화해도 일관(별도 캡처 시점 없음). 복원은
GameBootstrap이 로드 직후 즉시(`dur=0`) 재발행.

**기각한 대안**:
- 뷰가 자기 상태를 직접 직렬화(SaveState/LoadState) → 뷰가 State SO에 의존, ADR-007(뷰=표시만) 위반.
- 모든 FX 이벤트를 구독하는 별도 스냅샷 컴포넌트 → 미러 책임이 이미 NarrativeController에 있는데 분산, 과설계.

## 4. 스키마 가산 (`GameStateData`)

전부 가산적 확장 — 구버전 세이브 로드 시 기본값(off/빈)으로 채워져 **마이그레이션 무해**.

```csharp
// 연출 지속 상태(스테이지 상태 세이브, 2026-06-17). 연출 순간값(흔들림)·CG는 비저장(설계 §2).
public float storyTintR, storyTintG, storyTintB, storyTintA; // storyTintA>0 이면 활성. Clear 발행값=(0,0,0,0)
public bool   storyEyeClosed;     // Close/CloseImmediate=true, Open=false (Blink는 순간이라 상태 불변)
public string storySd = "";       // 현재 SD 레이어 이름(해석된 코드ID). 빈=없음
public string storyOverlay = "";  // 현재 Overlay 레이어 이름(해석된 코드ID). 빈=없음
```

틴트는 BG와 동일하게 *해석된 최종 RGB+알파*를 저장 → ColorTintTuningSO 프리셋이 바뀌어도 복원이
흔들리지 않는다(별칭/튜닝 변경 면역).

## 5. 저장 (NarrativeController — 발행 직전 미러)

기존 `RecordBg`/`RecordBgm`/`RecordChar` 형제로 추가한다.

- `RecordTint(r,g,b,a)` ← `PlayColorTint`. Clear 분기는 `(0,0,0,0)`.
- `RecordEye(EyeMaskAction)` ← EyeMask를 발행하는 **세 지점 전부**:
  - `PlayEyeMask` (FX 라인)
  - `PlaySceneFx` (SceneStart/SceneEnd 매크로)
  - `PlaySetup` (Setup 매크로의 `Eye=Close/Open`)
  - 매핑: `Close`/`CloseImmediate`→`true`, `Open`→`false`, `Blink`→무시(상태 불변).
- `RecordLayer(StageLayerKind, isClose, name)` ← SD/Overlay 발행 지점:
  - `PlayStageLayer` (CG/SD/Overlay 공용 — **CG kind는 미러 스킵**)
  - `PlaySetup` (Setup 매크로의 `Overlay=`)
  - `isClose`면 해당 슬롯 이름을 빈 문자열로.
- `ClearStoryPosition`에 4필드(틴트/eye/sd/overlay) 초기화 추가 — 정상 종료 시 클리어.

## 6. 복원 (`GameBootstrap.TryResumeStory`)

기존 BG/BGM/Char 재발행 **직후**, 전부 `dur=0` 즉시 발행:

```
if (d.storyTintA > 0)      ColorTintCommand(R,G,B,A, 0f, false, handle)
if (storySd != "")         ShowStageLayerCommand(SD,      false, storySd,      Cut, 0f, handle)
if (storyOverlay != "")    ShowStageLayerCommand(Overlay, false, storyOverlay, Cut, 0f, handle)
if (storyEyeClosed)        EyeMaskCommand(CloseImmediate, 0,0,0, handle)   // 최상위 가림 → 마지막에
```

발행 순서: BG → 캐릭터(기존) → 레이어 SD/Overlay → 틴트 → 아이마스크. 각자 다른 캔버스/레이어라
시각 z-순서는 발행 순서와 무관하나, 아이마스크는 최상위 가림이라 의미상 마지막에 둔다.

## 7. 테스트 (작동 증거)

- **EditMode** `StoryPositionSchemaTests` 확장:
  - 새 4필드 JSON 직렬화 왕복.
  - 구세이브(부재 필드) 로드 시 기본값(tint a=0·eye open·sd/overlay 빈).
- **PlayMode** `StoryPositionPlayModeTests` 확장:
  - tint/eye/SD/overlay 발행 → 미러 기록 확인.
  - Clear(틴트)·Open(eye)·Close(레이어)가 미러 해제(기본값 환원) 확인.
  - `TryResumeStory`가 저장된 상태를 즉시 재발행(명령 수신) 확인.

## 8. 알려진 한계 (불변, 설계 의도)

- **CG 비저장**: 수동 세이브는 CG 중 인포바 숨김으로 자연 차단. 유일 엣지 = 작가가 CG 시퀀스
  중간에 `Flow,,Save`를 거는 비정상 패턴 → 로드 시 CG만 누락(대사는 정확, fail-soft). 작가
  가이드(STORY_CSV_GUIDE)에 "CG 진행 중 Save 비권장" 한 줄 추가 권고.
- **Shake 비저장**: 순간 임팩트.
- **인라인 `<emote>` 드리프트**: 컨트롤러 발행 표정만 미러(RecordChar) — 타이핑 중 인라인 표정 교체는 미러 안 됨(기존 한계 그대로).

## 9. 마이그레이션 / 위험

- 가산적 스키마 → 구세이브 무해(기본값 로드).
- 런타임 상태 영구화 금지(금지선 #6) 준수 — SO 에셋에 저장 안 함, GameStateData 직렬화만.
- 호감도 공식·수치 무관(금지선 #2).
