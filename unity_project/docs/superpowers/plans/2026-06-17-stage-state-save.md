# 스테이지 상태 세이브 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 스토리 중 세이브→로드 시 BG/BGM/Char에 더해 화면 색 보정(ColorTint)·눈꺼풀 닫힘(EyeMask)·SD/Overlay 레이어까지 동일 재현한다.

**Architecture:** 기존 BG/BGM/Char 미러 패턴을 그대로 확장 — 엔진(`NarrativeController`)이 명령 발행 직전 *해석된 최종값*을 `GameStateSO.Data`에 미러, `GameBootstrap.TryResumeStory`가 로드 직후 `dur=0`으로 즉시 재발행. CG(세이브 UI 구조적 차단)·Shake(순간값)는 비저장.

**Tech Stack:** Unity 6 / C# / EventBus(struct command) + ScriptableObject 상태 / NUnit (EditMode+PlayMode). 검증은 Unity MCP `run_tests`(에디터 연결) 또는 헤드리스 EditMode 배치.

위험도: **🔴 Critical (세이브 스키마 가산)**. 스펙: `docs/superpowers/specs/2026-06-17-stage-state-save-design.md`.

---

## File Structure

- `Assets/_Project/Scripts/Core/State/GameStateData.cs` — 스키마 4종 가산(스토리 위치 블록 바로 뒤).
- `Assets/_Project/Scripts/Narrative/NarrativeController.cs` — Record 헬퍼 3종 + 5개 발행 지점 배선 + ClearStoryPosition 확장.
- `Assets/_Project/Scripts/Game/GameBootstrap.cs` — TryResumeStory 재발행 4줄.
- `Assets/Tests/EditMode/StoryPositionSchemaTests.cs` — JSON 왕복 + 구세이브 기본값.
- `Assets/Tests/PlayMode/StoryPositionPlayModeTests.cs` — 미러 기록/클리어 + 복원 재발행.

명령 시그니처(확인됨):
- `ColorTintCommand(float r,g,b,alpha, float duration, bool clear, CompletionHandle h)` — 필드 `R/G/B/Alpha/Duration/Clear/Handle`.
- `EyeMaskCommand(EyeMaskAction action, float closeDur, openDur, holdDur, CompletionHandle h)` — `EyeMaskAction { Open, Close, CloseImmediate, Blink }`.
- `ShowStageLayerCommand(StageLayerKind kind, bool isClose, string name, LayerTransition transition, float duration, CompletionHandle h)` — `StageLayerKind { CG, SD, Overlay }`, `LayerTransition { Cut, Fade }`.

---

## Task 1: 스키마 가산 (GameStateData)

**Files:**
- Modify: `Assets/_Project/Scripts/Core/State/GameStateData.cs` (storyChars 블록 직후, 78행 부근)
- Test: `Assets/Tests/EditMode/StoryPositionSchemaTests.cs`

- [ ] **Step 1: 실패 테스트 작성** — `StoryPositionSchemaTests`에 메서드 추가, 기존 구세이브 테스트에 기본값 어서션 추가

`StoryFields_JsonRoundTrip` 다음에 새 메서드 추가:

```csharp
        [Test]
        public void StageStateFields_JsonRoundTrip()
        {
            var d = new GameStateData
            {
                storyTintR = 0.5f, storyTintG = 0.4f, storyTintB = 0.3f, storyTintA = 0.25f,
                storyEyeClosed = true,
                storySd = "sd_x",
                storyOverlay = "ov_y",
            };

            var back = JsonUtility.FromJson<GameStateData>(JsonUtility.ToJson(d));

            Assert.AreEqual(0.25f, back.storyTintA, 0.001f, "틴트 알파 왕복");
            Assert.AreEqual(0.5f, back.storyTintR, 0.001f);
            Assert.IsTrue(back.storyEyeClosed, "아이마스크 닫힘 왕복");
            Assert.AreEqual("sd_x", back.storySd);
            Assert.AreEqual("ov_y", back.storyOverlay);
        }
```

`OldSave_WithoutStoryFields_LoadsAsDefaults` 끝의 `Assert.AreEqual(0, d.storyChars.Count);` 다음에 추가:

```csharp
            Assert.AreEqual(0f, d.storyTintA, "부재 → 0 = 틴트 비활성");
            Assert.IsFalse(d.storyEyeClosed, "부재 → false = 눈 뜬 상태");
            Assert.AreEqual("", d.storySd);
            Assert.AreEqual("", d.storyOverlay);
```

- [ ] **Step 2: 컴파일 실패 확인**

Unity MCP `read_console`(또는 헤드리스 컴파일): `storyTintA` 등 미정의 멤버로 컴파일 에러 예상.

- [ ] **Step 3: 스키마 추가** — `GameStateData.cs` storyChars 선언(78행) 직후, `StoryCharRecord` 클래스 정의(80행) **앞**에 삽입

```csharp
        // ── 연출 지속 상태(스테이지 상태 세이브, 2026-06-17) ──
        // 로드 시 장면 시각 동일 재현: BG/Char에 더해 화면 색 보정/눈꺼풀 닫힘/SD·Overlay 레이어를 미러.
        // 발행 직전 해석된 최종값으로 기록 — 별칭/튜닝 변경 면역. 흔들림(순간값)·CG(CG 중 세이브 UI 접근
        // 구조적 차단)는 비저장. 가산적 확장이라 구버전 세이브는 기본값(0/false/빈)으로 로드 = 마이그레이션 무해.
        public float storyTintR;
        public float storyTintG;
        public float storyTintB;
        public float storyTintA; // > 0 이면 활성. Clear 발행값 = (0,0,0,0)
        public bool storyEyeClosed; // Close/CloseImmediate=true, Open=false (Blink는 순간이라 상태 불변)
        public string storySd = "";      // 현재 SD 레이어 이름(해석된 코드ID). 빈=없음
        public string storyOverlay = ""; // 현재 Overlay 레이어 이름(해석된 코드ID). 빈=없음
```

- [ ] **Step 4: EditMode 테스트 통과 확인**

Run: Unity MCP `run_tests`(EditMode, 필터 `StoryPositionSchemaTests`) 또는 헤드리스 `-runTests -testPlatform EditMode`.
Expected: `StageStateFields_JsonRoundTrip`·`OldSave_WithoutStoryFields_LoadsAsDefaults` PASS, 컴파일 0에러.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/Core/State/GameStateData.cs" "Assets/Tests/EditMode/StoryPositionSchemaTests.cs"
git commit -m "feat(save): 스테이지 상태 세이브 스키마 가산(틴트/아이마스크/SD/Overlay)

로드 시 장면 시각 재현을 위해 GameStateData에 연출 지속 상태 4종 추가.
가산적이라 구세이브는 기본값 로드(마이그레이션 무해). 미러/복원은 후속 태스크."
```

---

## Task 2: 저장 미러 (NarrativeController)

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/NarrativeController.cs`
  - Record 헬퍼 추가(`RecordChar` 직후, 289행 부근)
  - `ClearStoryPosition` 확장(247행 부근)
  - 5개 발행 지점 배선: `PlayColorTint`·`PlayEyeMask`·`PlayStageLayer`·`PlaySetup`(Overlay/Eye)·`PlaySceneFx`(Eye 3발행)
- Test: `Assets/Tests/PlayMode/StoryPositionPlayModeTests.cs`

- [ ] **Step 1: 실패 테스트 작성** — `StoryPositionPlayModeTests`에 미러 테스트 추가 (`Engine_RecordsAnchorAndStageSnapshot_ClearsOnFinish` 다음)

```csharp
        [UnityTest]
        public IEnumerator Engine_MirrorsStageState_TintEyeLayers_ClearsOnFinish()
        {
            var engine = CreateEngine();
            yield return null;

            // > = Immediate(WaitNext 비대기)라 핸들 미완료여도 통과 → 마지막 Text에서만 대기.
            const string csv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",FX,,ColorTint:Sepia,>\n" +   // index 0 — 틴트 미러
                ",FX,,EyeClose,>\n" +          // index 1 — 아이마스크 닫힘 미러
                ",SD,,sd_x,>\n" +              // index 2 — SD 레이어 미러
                ",Overlay,,ov_y,>\n" +         // index 3 — Overlay 레이어 미러
                ",Text,로아,anchor,click\n" +   // index 4 ← 대기 앵커
                ",Flow,,End,>\n";

            CompletionHandle dialogue = null;
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => dialogue = e.Handle));

            EventBus.Publish(new PlayScriptCommand(csv, "EvtX.csv"));
            yield return WaitUntilOrFrames(() => dialogue != null);
            Assert.IsNotNull(dialogue, "Text 앵커 도달");

            var d = _gs.Data;
            Assert.Greater(d.storyTintA, 0f, "틴트 미러(활성)");
            Assert.IsTrue(d.storyEyeClosed, "아이마스크 닫힘 미러");
            Assert.AreEqual("sd_x", d.storySd, "SD 레이어 미러");
            Assert.AreEqual("ov_y", d.storyOverlay, "Overlay 레이어 미러");

            dialogue.Complete();
            yield return WaitUntilOrFrames(() => !engine.IsRunning);
            Assert.IsFalse(engine.IsRunning, "스크립트 종료");
            Assert.AreEqual(0f, d.storyTintA, "정상 종료 → 틴트 클리어");
            Assert.IsFalse(d.storyEyeClosed, "아이마스크 클리어");
            Assert.AreEqual("", d.storySd, "SD 클리어");
            Assert.AreEqual("", d.storyOverlay, "Overlay 클리어");
        }
```

- [ ] **Step 2: 테스트 실패 확인**

Run: Unity MCP `run_tests`(PlayMode, 필터 `Engine_MirrorsStageState`).
Expected: FAIL — 미러가 없어 `storyTintA` 등이 기본값(0/false/빈)으로 어서션 실패.

- [ ] **Step 3a: Record 헬퍼 3종 추가** — `RecordChar` 메서드(289행 닫는 `}`) 직후 삽입

```csharp
        void RecordTint(float r, float g, float b, float a)
        {
            if (state == null) return;
            var d = state.Data;
            d.storyTintR = r; d.storyTintG = g; d.storyTintB = b; d.storyTintA = a;
        }

        void RecordEye(EyeMaskAction action)
        {
            if (state == null) return;
            if (action == EyeMaskAction.Blink) return; // 깜빡임은 순간 — 지속 상태 불변
            state.Data.storyEyeClosed =
                action == EyeMaskAction.Close || action == EyeMaskAction.CloseImmediate;
        }

        void RecordLayer(StageLayerKind kind, bool isClose, string name)
        {
            if (state == null) return;
            if (kind == StageLayerKind.CG) return; // CG 비저장(설계 §2)
            string value = isClose ? "" : (name ?? "");
            if (kind == StageLayerKind.SD) state.Data.storySd = value;
            else if (kind == StageLayerKind.Overlay) state.Data.storyOverlay = value;
        }
```

- [ ] **Step 3b: `ClearStoryPosition` 확장** — `d.storyChars.Clear();`(255행) 다음 줄에 삽입

```csharp
            d.storyTintR = d.storyTintG = d.storyTintB = d.storyTintA = 0f;
            d.storyEyeClosed = false;
            d.storySd = "";
            d.storyOverlay = "";
```

- [ ] **Step 3c: `PlayColorTint` 배선** — 두 발행 직전에 RecordTint 추가

`PlayColorTint`의 Clear 분기를:

```csharp
            if (intent.IsClear)
            {
                RecordTint(0f, 0f, 0f, 0f);
                EventBus.Publish(new ColorTintCommand(0f, 0f, 0f, 0f, dur, true, req));
            }
            else
            {
                float alpha = intent.Alpha >= 0f
                    ? intent.Alpha
                    : (colorTintTuning != null ? colorTintTuning.DefaultAlpha : 0.25f);
                Color c = colorTintTuning != null ? colorTintTuning.ColorFor(intent.Preset) : Color.gray;
                RecordTint(c.r, c.g, c.b, alpha);
                EventBus.Publish(new ColorTintCommand(c.r, c.g, c.b, alpha, dur, false, req));
            }
```

- [ ] **Step 3d: `PlayEyeMask` 배선** — 발행 직전에 RecordEye 추가

`PlayEyeMask`의 `var req = new CompletionHandle();` 다음 발행 줄을:

```csharp
            var req = new CompletionHandle();
            RecordEye(intent.Action);
            EventBus.Publish(new EyeMaskCommand(intent.Action, closeDur, openDur, holdDur, req));
```

- [ ] **Step 3e: `PlayStageLayer` 배선** — 이름을 한 번 해석해 미러+발행 공유

`PlayStageLayer`의 `float dur = ...` 다음 발행 블록을:

```csharp
            float dur = intent.Duration >= 0f ? intent.Duration : ResolveLayerFade(kind);
            string resolved = ResolveLayerName(kind, intent.Name);
            RecordLayer(kind, intent.IsClose, resolved);
            var req = new CompletionHandle();
            EventBus.Publish(new ShowStageLayerCommand(kind, intent.IsClose, resolved, intent.Transition, dur, req));
```

- [ ] **Step 3f: `PlaySetup` 배선** — Overlay·Eye 발행 직전에 미러 추가

`PlaySetup`의 Overlay 블록을:

```csharp
            if (s.Overlay != null)
            {
                RecordLayer(StageLayerKind.Overlay, false, s.Overlay);
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.Overlay, false, s.Overlay, LayerTransition.Cut, 0f, new CompletionHandle()));
            }
            if (s.Eye != null)
            {
                var action = string.Equals(s.Eye, "Open", StringComparison.OrdinalIgnoreCase)
                    ? EyeMaskAction.Open : EyeMaskAction.CloseImmediate;
                RecordEye(action);
                EventBus.Publish(new EyeMaskCommand(action, 0f, 0f, 0f, new CompletionHandle()));
            }
```

- [ ] **Step 3g: `PlaySceneFx` 배선** — EyeMask 3발행 직전에 RecordEye 추가

SceneEnd 발행:
```csharp
                var req = new CompletionHandle();
                RecordEye(EyeMaskAction.Close);
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.Close, dur, dur, 0f, req));
```

EyeClose(암전) 발행:
```csharp
            if (s.EyeClose)
            {
                RecordEye(EyeMaskAction.CloseImmediate);
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.CloseImmediate, 0f, 0f, 0f, new CompletionHandle()));
                yield return WaitNext(line, () => true);
                yield break;
            }
```

SceneStart 눈뜨기 발행:
```csharp
            var open = new CompletionHandle();
            RecordEye(EyeMaskAction.Open);
            EventBus.Publish(new EyeMaskCommand(EyeMaskAction.Open, SceneStartOpenDur, SceneStartOpenDur, 0f, open));
```

- [ ] **Step 4: PlayMode 테스트 통과 확인**

Run: Unity MCP `run_tests`(PlayMode, 필터 `Engine_MirrorsStageState`). 컴파일 0에러(`read_console`).
Expected: PASS — 미러 기록 + 종료 클리어.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/Narrative/NarrativeController.cs" "Assets/Tests/PlayMode/StoryPositionPlayModeTests.cs"
git commit -m "feat(save): 연출 지속 상태 미러(틴트/아이마스크/SD·Overlay)

NarrativeController가 발행 직전 해석값을 State에 미러 — PlayColorTint/PlayEyeMask/
PlayStageLayer + Setup·SceneFx 매크로 EyeMask·Overlay 발행 전수. CG는 미러 스킵.
정상 종료 시 ClearStoryPosition이 함께 비운다."
```

---

## Task 3: 복원 재발행 (GameBootstrap.TryResumeStory)

**Files:**
- Modify: `Assets/_Project/Scripts/Game/GameBootstrap.cs` (TryResumeStory, 86행 char foreach 직후)
- Test: `Assets/Tests/PlayMode/StoryPositionPlayModeTests.cs`

- [ ] **Step 1: 실패 테스트 작성** — `StoryPositionPlayModeTests`에 복원 테스트 추가 (`Bootstrap_ResumesPrologue_Directly` 다음)

```csharp
        [UnityTest]
        public IEnumerator Bootstrap_ResumesStageState_RepublishesTintEyeAndLayers()
        {
            DestroyResidents<GameManager>();
            var boot = CreateBootstrap(null);
            yield return null;

            var d = _gs.Data;
            d.storyScriptId = "prologue"; // 무대 재발행은 스크립트 분기 전에 일어남(분기는 무관)
            d.storyLineIndex = 0;
            d.storyTintR = 0.5f; d.storyTintG = 0.4f; d.storyTintB = 0.3f; d.storyTintA = 0.25f;
            d.storyEyeClosed = true;
            d.storySd = "sd_x";
            d.storyOverlay = "ov_y";

            ColorTintCommand? tint = null;
            EyeMaskCommand? eye = null;
            var layers = new List<ShowStageLayerCommand>();
            _subs.Add(EventBus.Subscribe<ColorTintCommand>(e => { tint = e; e.Handle?.Complete(); }));
            _subs.Add(EventBus.Subscribe<EyeMaskCommand>(e => { eye = e; e.Handle?.Complete(); }));
            _subs.Add(EventBus.Subscribe<ShowStageLayerCommand>(e => { layers.Add(e); e.Handle?.Complete(); }));

            boot.TryResumeStory();

            Assert.IsTrue(tint.HasValue, "틴트 재발행");
            Assert.AreEqual(0.25f, tint.Value.Alpha, 0.001f);
            Assert.IsFalse(tint.Value.Clear, "활성 틴트(클리어 아님)");
            Assert.IsTrue(eye.HasValue, "아이마스크 재발행");
            Assert.AreEqual(EyeMaskAction.CloseImmediate, eye.Value.Action, "닫힘 상태 = 즉시 감김 복원");
            Assert.AreEqual(2, layers.Count, "SD+Overlay 재발행");
            Assert.IsTrue(layers.Exists(l => l.Kind == StageLayerKind.SD && l.Name == "sd_x"), "SD 재발행");
            Assert.IsTrue(layers.Exists(l => l.Kind == StageLayerKind.Overlay && l.Name == "ov_y"), "Overlay 재발행");
            yield return null;
        }
```

- [ ] **Step 2: 테스트 실패 확인**

Run: Unity MCP `run_tests`(PlayMode, 필터 `Bootstrap_ResumesStageState`).
Expected: FAIL — 복원 코드 부재로 tint/eye/layers 미수신.

- [ ] **Step 3: TryResumeStory 재발행 추가** — char foreach 블록(86행 닫는 `}`) 직후, `if (id == "prologue")`(88행) **앞**에 삽입

```csharp
            if (d.storyTintA > 0f)
                EventBus.Publish(new ColorTintCommand(d.storyTintR, d.storyTintG, d.storyTintB, d.storyTintA, 0f, false, new CompletionHandle()));
            if (!string.IsNullOrEmpty(d.storySd))
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.SD, false, d.storySd, LayerTransition.Cut, 0f, new CompletionHandle()));
            if (!string.IsNullOrEmpty(d.storyOverlay))
                EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.Overlay, false, d.storyOverlay, LayerTransition.Cut, 0f, new CompletionHandle()));
            if (d.storyEyeClosed)
                EventBus.Publish(new EyeMaskCommand(EyeMaskAction.CloseImmediate, 0f, 0f, 0f, new CompletionHandle())); // 최상위 가림 → 마지막
```

- [ ] **Step 4: PlayMode 테스트 통과 확인 + 전체 회귀**

Run: Unity MCP `run_tests`(PlayMode 필터 `StoryPosition`) → 신규 포함 그린. 이어 EditMode+PlayMode 전체 회귀(`run_tests` 전체) — 컴파일 0, 기존 테스트 무회귀.
Expected: 신규 3개 PASS + 전체 그린.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/Game/GameBootstrap.cs" "Assets/Tests/PlayMode/StoryPositionPlayModeTests.cs"
git commit -m "feat(save): 스테이지 상태 복원 — 로드 시 틴트/아이마스크/SD·Overlay 즉시 재발행

TryResumeStory가 BG/Char 재현 직후 저장된 연출 지속 상태를 dur=0으로 재발행.
아이마스크는 최상위 가림이라 마지막. 스테이지 상태 세이브 슬라이스 완료."
```

---

## 마무리 (실행 후 별도)

- **HANDOFF.md 갱신**: 현재상태/다음액션에 스테이지 상태 세이브 완료 기록(틴트/아이마스크/SD·Overlay 저장·복원, CG·Shake 비저장 한계 명시). 감독 승인 후 커밋.
- **STORY_CSV_GUIDE.md**: "CG 진행 중 `Flow,,Save` 비권장(로드 시 CG 누락, 대사는 정확)" 한 줄 추가(스펙 §8). 🟢 — 선택.
- **감독 Play 검증**: 틴트/암전 모놀로그/SD·Overlay 떠 있는 장면에서 세이브→타이틀→이어하기 시 화면 동일 복원.

---

## Self-Review 결과

- **Spec coverage**: §4 스키마→T1, §5 저장(5발행지점)→T2(3c~3g), §6 복원→T3, §7 테스트→T1·T2·T3 전부 매핑. CG 제외(§2)→RecordLayer/RecordEye 가드. ✅
- **Placeholder scan**: TBD/TODO 없음, 모든 코드 블록 완전. ✅
- **Type consistency**: `RecordTint/RecordEye/RecordLayer` 명칭 T2 정의·T2 호출 일치. 명령 필드명(`Alpha/Clear/Action/Kind/Name`) 이벤트 정의와 일치. `EyeMaskAction`/`StageLayerKind`/`LayerTransition` enum 값 확인됨. ✅
