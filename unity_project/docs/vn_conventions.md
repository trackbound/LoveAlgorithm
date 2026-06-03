# 🎴 VN 컨벤션 — 재작성 정본 레퍼런스 (베낄 대상)

> **슬라이스 착수 시 구 코드 대신 이 문서를 본다.** ADR-012(재설계≠전사)의 *실행 장치*.
> 구 코드는 "동결 수치(REWRITE_TUNING_VALUES)·CSV 문법(STORY_COMMANDS)" 확인용으로만 연다 — 구조·네이밍·분해는 여기서 가져온다.

---

## 0. 왜 이 문서가 있나

구 코드를 먼저 읽으면 설계가 **구 분류·네이밍에 닻을 내린다**(예: 단일 `ScreenFX` 클래스 → 신 코드 전역 "Fx" 어휘). 한 줄 지침(ADR-012)만으론 안 바뀌었으므로, **베낄 대상 자체를 구 코드 → 이 문서로 교체**한다.

---

## 1. 착수 순서 (요구사항 우선 — 구 코드 나중)

1. `STORY_COMMANDS.md`·`REWRITE_FEATURE_INVENTORY.md`에서 **"무엇을 하는가"(요구사항)** 먼저 파악.
2. 아래 §3 렌더타깃 축으로 **클래스 책임을 설계**.
3. **그 다음에만** 구 코드를 연다 — 동결 수치·CSV 문법 확인용.

🚩 **레드플래그**: *"이 클래스/필드가 STORY_COMMANDS 요구사항 때문인가, 구 코드가 있어서인가?"* — 후자면 멈춘다. 커밋 메시지 "이식(port)" 표현 금지(ADR-012).

---

## 2. VN 표준 ↔ 본 프로젝트 (우리는 이미 표준을 따른다)

용어만 다를 뿐 구조는 업계 VN 엔진(Naninovel·Ren'Py)과 동일. **표준을 새로 만드는 게 아니라 표준 용어로 정리**하는 것.

| VN 표준 개념 | 업계 예 (Naninovel/Ren'Py) | 본 프로젝트 |
|---|---|---|
| Script (스크립트) | `.nani` / `.rpy` | CSV (STORY_COMMANDS 문법) |
| Command (명령) | `@command` / statement | `LineType` + `Value`(콜론 구분) |
| Actor (배우=캐릭터) | character actor | Char 슬롯 L/C/R + `StageView` |
| Background / Layer | `@back` / layers | BG·CG·SD·Overlay + `StageView`/`StageLayerView` |
| Transition (전환) | command params | `Cut`/`Fade`/`CrossFade` 토큰 |
| Effect (연출) | spawn / fx | §3 렌더타깃별 |
| State (상태) | custom vars | `GameStateSO` (동기 읽기, ADR-007) |
| Signal / Event | signals | `EventBus` struct (통지·명령) |

---

## 3. 연출 분류축 = 렌더타깃 ("Fx 버킷" 금지)

구 `ScreenFX`는 모든 연출을 **한 클래스 + "Fx" 버킷**에 욱여넣었다. 신 코드는 **무엇에 작용하는가(렌더타깃)**로 명명·배치한다.
*(분해 패턴 자체 — Parser→Command→View→TuningSO 얇은 어댑터 — 는 ADR-007이라 유지. 바꾸는 건 **명명·축·배치**다.)*

| 렌더타깃 | 작용 대상 | 효과(STORY_COMMANDS) | C# 명명 | 씬 위치 |
|---|---|---|---|---|
| **화면 오버레이** | 화면 전체 위에 덮기 | FadeOut·FadeIn·Flash·ColorTint·EyeMask | `ScreenFade`·`ColorTint`·`EyeMask` | `_ScreenOverlay` 캔버스(최상위) |
| **무대 변형** | 배경+캐릭터 전체 트랜스폼 | CamZoom·CamPan·CamReset·StageShake | `Camera`·`StageShake` | `_Stage/Content` 트랜스폼 |
| **액터 효과** | 개별 캐릭터 슬롯 | CharShake·CharJump·CharDim·CharGlitch | `Char*` | 캐릭터 슬롯(L/C/R) |
| **무대 레이어** | 이미지 레이어 표시/전환 | BG·Char·CG·SD·Overlay | `Stage`·`StageLayer` | `_Stage` |

⚠️ **작가 어휘 ≠ 구현 축**: CSV는 작가 편의로 "Shake"를 한 패밀리로 부른다(CamShake·StageShake·CharShake). 하지만 C# 렌더 계층은 **렌더타깃이 갈리면 갈라야** 한다 — CamShake/StageShake=무대 변형, CharShake=액터 효과. 명령 이름의 접두사("Cam"/"Char")가 아니라 *어디에 그리는가*로 묶는다.

---

## 4. CSV 문법 vs C# 구현 (경계 — 헷갈리면 안 됨)

- **CSV 작가-대면 토큰 = 동결 문법(ADR-009). 유지.**
  `LineType.FX`, `FX:CamShake:...`, 별칭(`Shake`→`CamShake`), `NormalizeFX`/`IsKnownFX`, `FXCommandSignatures`, `SFX`(사운드). 작가가 이미 쓰는 문법 — 바꾸면 스토리 CSV가 깨진다.
- **C# 내부 타입/GO/네임스페이스 = 깔끔하게.**
  "FX"는 *스크립트 명령어*지 C# 클래스 접미사가 아니다. `CameraFxView`✗→`CameraView`○ · `_ScreenFx`✗→`_ScreenOverlay`○ · `LoveAlgo.Story.StoryEngine`✗→`LoveAlgo.Narrative`○(asmdef명 정합).

---

## 5. 네이밍 규칙 (dev_guide §3-4 재확인 + 구 어휘 금지)

- **접미사 = 레이어/종류** (정본 표 = `dev_guide.md` §3-4): `*View`(화면 UI) · `*Slot`(리스트 항목) · `*Controller`(어댑터) · `*Service`/`*Formula`(순수 결정·수식) · `*Parser`/`*Interpreter`(순수 파싱·해석) · `*SO` · `*Command`/`*Event`(struct).
- **네임스페이스 = asmdef명 정합**: `LoveAlgo.{Core,Data,Affinity,Narrative,UI,…}`.
- 🚫 **금지 어휘**: C# 타입의 `Fx` · 구 클래스명 그대로(`ScreenFX`·`ScriptRunner`·`ScriptEngine`·`OptionData`) · 구 ns(`StoryEngine`·`LoveAlgo.Modules.*`).

---

## 6. 안티패턴 (진단에서 도출 — 반복 금지)

1. ❌ "Fx" 잡동사니 버킷 네이밍 → ✅ §3 렌더타깃/도메인어.
2. ❌ 거대 디스패치(switch/if-캐스케이드)를 한 컨트롤러에 누적 — 구 `ScreenFX`·`ScriptRunner` 환생 → ✅ `dev_guide` §3-7 R&R: **`NarrativeController`는 해석→발행만, 렌더는 View**. 새 연출은 *새 명령 + 전용 View 구독*으로 추가하고 `PlayFx` 캐스케이드를 키우지 않는다.
3. ❌ 구 클래스/필드 1:1 이식(`OptionData`/`ScriptRunner` 베끼기) → ✅ STORY_COMMANDS 요구사항에서 책임 도출.
4. ❌ 구 네임스페이스·GO명 답습(`StoryEngine`·`_ScreenFx`) → ✅ asmdef·렌더타깃 명명.

---

## 7. 모범 예시 = ScreenFade 슬라이스 (화면 오버레이)

새 연출의 **정본 템플릿**. 분해는 이걸 따른다(형태=코드 직접 읽기, §1-6 — 여기엔 책임·근거만):

- `ScreenFadeParser` (순수, Narrative) — CSV `Value` → 인텐트. EditMode 테스트.
- `ShowScreenFadeCommand` (struct, Core/Events) — 완료 핸들(`CompletionHandle`) 실은 *의도*.
- `ScreenFadeView` (UI) — 명령 구독 → `_ScreenOverlay` 알파 lerp → 핸들 완료.
- `ScreenFadeTuningSO` (동결값, Resources/Data) — 페이드/플래시 기본 지속(ADR-012).
- `NarrativeController` — Parse→동결값 해석→Publish→Next 대기. **렌더 안 함**.

**근거**: 화면 전체 오버레이라 최상위 `_ScreenOverlay` 캔버스. Parser 순수→EditMode, View 라이프사이클→PlayMode. 구 `ScreenFX`(싱글톤+거대 switch+DOTween, 카메라·캐릭터까지 한 클래스)와 **정반대** — 단일 책임·테스트 가능·EventBus 디커플.

---

*정본 인덱스 = `docs/_index.md`. 결정 근거 = `decisions.md`(ADR-007/009/012). 룰북 = `dev_guide.md`. 요구사항 = `STORY_COMMANDS.md`·`REWRITE_FEATURE_INVENTORY.md`.*
