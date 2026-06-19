# 비밀번호 입력 커스텀 시스템 S2 (Normal 로그인 + 검증) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Normal(평상 잠금) 모드에서 저장된 비밀번호와 입력을 대조해 일치 시 로그인 진행(핸들 완료), 불일치 시 입력칸 진동 + 오류 횟수 통지하고 잠금화면을 유지(재입력)한다. 기본 마스킹은 S1에서 이미 모드별로 적용됨.

**Architecture:** `LockScreenController`(로직/저장, ADR-007 완료-핸들)가 Normal 분기에서 `GameStateSO.Password`와 대조. 일치→`PasswordAcceptedEvent` 발행 + 핸들 완료, 불일치→`PasswordVerifyFailedEvent(누적횟수)` 발행 + 핸들 유지. `LockScreenView`는 Normal에서 **제출 즉시 닫지 않고**(재입력 위해) 결과 이벤트를 기다린다 — Accepted→닫기, Failed→`PasswordInputField.Shake()` + 입력 초기화·재포커스. 오류 횟수는 세션 런타임(컨트롤러), 세이브 비저장.

**Tech Stack:** Unity 6000.5.0f1, C#, EventBus(`LoveAlgo.Common`), Unity Test Framework(EditMode/PlayMode, NUnit), Unity MCP(`run_tests`/`read_console`/`refresh_unity`).

## Global Constraints

- S1 계획(`2026-06-19-password-input-system-s1.md`)의 Global Constraints + "⚠️ 테스트 배치 개정"을 그대로 승계한다(로직 테스트=EditMode 헤드리스, 코루틴=PlayMode, run_tests는 정규화 전체 이름).
- **EditMode 헤드리스 사실**: 런타임 생성 객체의 `OnEnable`/EventBus 구독이 자동 발화하지 않는다 → 컨트롤러 테스트는 `OnShow`/`OnSubmit`을 **직접 호출**하고, 결과 이벤트는 테스트가 `EventBus.Subscribe`로 관찰한다. View의 이벤트 반응(구독 필요)은 PlayMode에서 검증(현재 PlayMode 실행 가능).
- 교차통신은 EventBus + State SO만(ADR-007). UI는 표시+명령, 의미/저장은 Controller.
- 매직넘버 금지: 신규 수치 없음(진동은 S1 PasswordInputField 인스펙터값 재사용).
- 커밋: 한 기능=한 커밋(Atomic), 본문에 "왜". 끝에 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. 각 태스크는 자기 파일만 `git add`(repo에 무관한 미커밋 변경 다수 — `git add -A` 금지).
- **기존 테스트 보존**: `LockScreenPlayModeTests` 5종(FirstSetup 경로)은 무변·그린 유지. S1 EditMode 4 + PlayMode 2도 유지.

## 설계 정제 (설계 §2 보강)

- 신규 이벤트 **2종**: `PasswordVerifyFailedEvent { int ErrorCount }`(설계 §2), **`PasswordAcceptedEvent {}`**(추가 — Normal 일치 시 View 닫기 신호). FirstSetup/Reset은 S1대로 View가 Confirm에서 자체 닫기(Accepted 불필요).
- Normal Confirm은 닫기를 지연. FirstSetup/Reset Confirm은 S1 동작(즉시 닫기) 유지.
- 오류 횟수 ≥3의 열쇠/분실 가이드는 **S3** 범위. S2는 `PasswordVerifyFailedEvent`에 횟수만 실어 보내고 View는 진동만(가이드 무변).

---

## 파일 구조 (생성/수정 맵)

| 파일 | 동작 | 책임 |
|---|---|---|
| `Assets/_Project/Scripts/Core/Events/LockScreenEvents.cs` | 수정 | `PasswordVerifyFailedEvent`/`PasswordAcceptedEvent` 추가 |
| `Assets/_Project/Scripts/Narrative/LockScreenController.cs` | 수정 | Normal 검증 분기 + 오류 횟수 + 이벤트 발행 |
| `Assets/_Project/Scripts/UI/LockScreenView.cs` | 수정 | Normal 지연 닫기 + Failed(진동)·Accepted(닫기) 구독 |
| `Assets/Tests/EditMode/LockScreenControllerEditModeTests.cs` | 생성 | 컨트롤러 Normal 검증/횟수 EditMode |
| `Assets/Tests/EditMode/LockScreenIntroEditModeTests.cs` | 수정 | View Normal 지연 닫기 EditMode 1건 추가 |
| `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` | 수정 | View Failed→진동 / Accepted→닫기 PlayMode 2건 추가 |

---

## Task 1: 이벤트 추가 + 컨트롤러 Normal 검증

**Files:**
- Modify: `Assets/_Project/Scripts/Core/Events/LockScreenEvents.cs`
- Modify: `Assets/_Project/Scripts/Narrative/LockScreenController.cs`
- Test: `Assets/Tests/EditMode/LockScreenControllerEditModeTests.cs` (생성)

**Interfaces:**
- Produces:
  - `readonly struct PasswordVerifyFailedEvent { int ErrorCount; ctor(int) }` (namespace `LoveAlgo.Events`)
  - `readonly struct PasswordAcceptedEvent { }` (namespace `LoveAlgo.Events`)
  - `LockScreenController`: Normal 일치 시 `PasswordAcceptedEvent` 발행 + 핸들 완료, 불일치 시 `PasswordVerifyFailedEvent(누적)` 발행 + 핸들 유지. `OnShow`가 오류 횟수 0 리셋. 기존 `OnShow`/`OnSubmit` 시그니처 불변.

- [ ] **Step 1: 이벤트 추가**

`Assets/_Project/Scripts/Core/Events/LockScreenEvents.cs` — `SubmitPasswordCommand` 구조체 **다음**(파일 끝 `}` 직전)에 추가:

```csharp
    /// <summary>비밀번호 검증 실패(Normal 불일치) 통지 — Controller→View(진동 + 누적 오류 횟수). ErrorCount는 1부터.</summary>
    public readonly struct PasswordVerifyFailedEvent
    {
        public readonly int ErrorCount;
        public PasswordVerifyFailedEvent(int errorCount) { ErrorCount = errorCount; }
    }

    /// <summary>비밀번호 수락(Normal 일치) 통지 — Controller→View(잠금화면 닫기). 저장/핸들 완료는 Controller가 함께 수행한다.</summary>
    public readonly struct PasswordAcceptedEvent { }
```

- [ ] **Step 2: 실패 테스트 작성** — 신규 EditMode 파일

`Assets/Tests/EditMode/LockScreenControllerEditModeTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Common;                  // EventBus
using LoveAlgo.Core;                    // GameStateSO
using LoveAlgo.Events;                  // ShowLockScreenCommand, SubmitPasswordCommand, LockMode, CompletionHandle, PasswordVerifyFailedEvent, PasswordAcceptedEvent
using LoveAlgo.Story.StoryEngine.Flow;  // LockScreenController

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// LockScreenController Normal 검증(EditMode). 헤드리스라 OnShow/OnSubmit을 직접 호출하고
    /// 결과 이벤트는 테스트가 구독해 관찰한다. 일치→Accepted+핸들완료, 불일치→Failed(누적)+핸들유지.
    /// </summary>
    public class LockScreenControllerEditModeTests
    {
        GameStateSO _gs;
        LockScreenController _ctrl;
        GameObject _go;
        readonly List<IDisposable> _subs = new();

        [SetUp]
        public void SetUp()
        {
            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();
            _go = new GameObject("LockScreenController");
            _ctrl = _go.AddComponent<LockScreenController>();
            _ctrl.State = _gs;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        [Test]
        public void Normal_Match_Publishes_Accepted_And_Completes_Handle()
        {
            _gs.Password = "1234";
            bool accepted = false;
            _subs.Add(EventBus.Subscribe<PasswordAcceptedEvent>(_ => accepted = true));
            var handle = new CompletionHandle();
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, handle));

            _ctrl.OnSubmit(new SubmitPasswordCommand("1234"));

            Assert.IsTrue(accepted, "일치 → PasswordAcceptedEvent 발행");
            Assert.IsTrue(handle.IsComplete, "일치 → 핸들 완료(로그인 진행)");
        }

        [Test]
        public void Normal_Mismatch_Publishes_Failed_And_Keeps_Handle()
        {
            _gs.Password = "1234";
            int failCount = 0;
            int lastErr = 0;
            _subs.Add(EventBus.Subscribe<PasswordVerifyFailedEvent>(e => { failCount++; lastErr = e.ErrorCount; }));
            var handle = new CompletionHandle();
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, handle));

            _ctrl.OnSubmit(new SubmitPasswordCommand("9999"));
            Assert.AreEqual(1, failCount, "불일치 1회 → Failed 1회");
            Assert.AreEqual(1, lastErr, "누적 오류 = 1");
            Assert.IsFalse(handle.IsComplete, "불일치 → 핸들 유지(재입력)");

            _ctrl.OnSubmit(new SubmitPasswordCommand("8888"));
            Assert.AreEqual(2, failCount, "불일치 2회 → Failed 2회");
            Assert.AreEqual(2, lastErr, "누적 오류 = 2");
            Assert.IsFalse(handle.IsComplete);
        }

        [Test]
        public void Normal_ErrorCount_Resets_On_New_Show()
        {
            _gs.Password = "1234";
            int lastErr = 0;
            _subs.Add(EventBus.Subscribe<PasswordVerifyFailedEvent>(e => lastErr = e.ErrorCount));
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            _ctrl.OnSubmit(new SubmitPasswordCommand("x"));
            _ctrl.OnSubmit(new SubmitPasswordCommand("y"));
            Assert.AreEqual(2, lastErr, "리셋 전 누적 2");

            // 새 잠금화면(Show) → 오류 횟수 리셋
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            _ctrl.OnSubmit(new SubmitPasswordCommand("z"));
            Assert.AreEqual(1, lastErr, "새 Show 후 누적 1로 리셋");
        }
    }
}
```

- [ ] **Step 3: 실패 확인**

Run: `run_tests` mode=EditMode test_names=["LoveAlgo.Tests.EditMode.LockScreenControllerEditModeTests.Normal_Match_Publishes_Accepted_And_Completes_Handle"] include_details=true
Expected: 컴파일 에러(`PasswordAcceptedEvent` 없음) 또는 FAIL. `read_console`로 확인.

- [ ] **Step 4: 컨트롤러 구현**

`Assets/_Project/Scripts/Narrative/LockScreenController.cs`:

(a) 필드 `LockMode _mode;` 아래(또는 `_pending` 근처)에 추가:

```csharp
        int _errorCount; // Normal 누적 입력 실패(세션 런타임, 세이브 비저장). 새 Show 시 0.
```

(b) `OnShow` 본문 끝에 리셋 추가:

```csharp
        public void OnShow(ShowLockScreenCommand e)
        {
            _pending?.Complete(); // 이전 미완료 핸들(비정상)이 엔진을 막지 않도록 먼저 정리.
            _pending = e.Handle;
            _mode = e.Mode;
            _errorCount = 0; // 새 잠금화면 세션 — 오류 횟수 리셋.
        }
```

(c) `OnSubmit` 본문을 아래로 교체:

```csharp
        public void OnSubmit(SubmitPasswordCommand e)
        {
            if (_pending == null) return; // 활성 잠금화면 없음 — 무시.

            switch (_mode)
            {
                case LockMode.Normal:
                    if (state != null && state.Password == e.Password)
                    {
                        EventBus.Publish(new PasswordAcceptedEvent()); // View 닫기 신호.
                        ReleasePending(); // 로그인 성공 → 엔진 진행.
                    }
                    else
                    {
                        _errorCount++;
                        EventBus.Publish(new PasswordVerifyFailedEvent(_errorCount)); // 진동 + 횟수.
                        // 핸들 유지 — 잠금화면 유지(재입력).
                    }
                    break;

                case LockMode.FirstSetup:
                case LockMode.Reset:
                    if (state == null)
                        Debug.LogError("[LockScreenController] state(GameStateSO) 미바인딩 — 비번 저장 불가.");
                    else
                    {
                        state.Password = e.Password; // 평문 저장(해싱은 후속).
                        Log.Info($"[LockScreenController] {_mode} 비번 설정 완료(len={e.Password?.Length ?? 0}).");
                    }
                    ReleasePending();
                    break;

                default: // Auto/GameStart 등 — 핸들만 풀어 진행(후속 구현).
                    ReleasePending();
                    break;
            }
        }
```

> 클래스 상단 `using LoveAlgo.Events;`는 이미 있음(ShowLockScreenCommand 등). `PasswordAcceptedEvent`/`PasswordVerifyFailedEvent`도 같은 네임스페이스라 추가 using 불필요.

- [ ] **Step 5: 테스트 통과 확인**

Run: `refresh_unity`(compile=request, scope=all, mode=force, wait_for_ready=true) → `read_console` types=["error"] 0건 → `run_tests` mode=EditMode test_names=["LoveAlgo.Tests.EditMode.LockScreenControllerEditModeTests.Normal_Match_Publishes_Accepted_And_Completes_Handle","LoveAlgo.Tests.EditMode.LockScreenControllerEditModeTests.Normal_Mismatch_Publishes_Failed_And_Keeps_Handle","LoveAlgo.Tests.EditMode.LockScreenControllerEditModeTests.Normal_ErrorCount_Resets_On_New_Show"] include_details=true
Expected: total=3 passed=3.

- [ ] **Step 6: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Events/LockScreenEvents.cs Assets/_Project/Scripts/Narrative/LockScreenController.cs Assets/Tests/EditMode/LockScreenControllerEditModeTests.cs Assets/Tests/EditMode/LockScreenControllerEditModeTests.cs.meta
git commit -m "feat(lockscreen): Normal 비번 검증 — 일치 수락/불일치 진동·횟수

Why: Normal 모드에서 저장 비번과 대조해 일치 시 PasswordAcceptedEvent+핸들 완료,
불일치 시 PasswordVerifyFailedEvent(누적 횟수)+핸들 유지(재입력). 오류 횟수는 세션
런타임(새 Show 시 리셋). FirstSetup/Reset은 저장 후 진행(스펙 §3,§5 S2).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: View — Normal 지연 닫기 + Failed(진동)·Accepted(닫기)

**Files:**
- Modify: `Assets/_Project/Scripts/UI/LockScreenView.cs`
- Test: `Assets/Tests/EditMode/LockScreenIntroEditModeTests.cs` (1건 추가), `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (2건 추가)

**Interfaces:**
- Consumes: `PasswordVerifyFailedEvent`, `PasswordAcceptedEvent`, `PasswordInputField.Shake()`/`ResetField()`.
- Produces (LockScreenView 동작 변경):
  - OnEnable이 `PasswordVerifyFailedEvent`(→진동·초기화·재포커스), `PasswordAcceptedEvent`(→Hide) 구독.
  - `Confirm`이 Normal에서는 닫지 않음(결과 대기). FirstSetup/Reset은 S1대로 즉시 닫기.
  - 기존 public API/시그니처 불변. 미바인딩 필드 null-safe.

- [ ] **Step 1: 실패 테스트 작성 (EditMode — Normal 지연 닫기)**

`Assets/Tests/EditMode/LockScreenIntroEditModeTests.cs` 클래스 내부에 추가(헤드리스라 `view.OnShow`/`view.Confirm` 직접 호출):

```csharp
        [Test]
        public void View_Normal_Confirm_Defers_Close()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var vInputGo = new GameObject("VInput");
            vInputGo.transform.SetParent(viewGo.transform);
            var vInput = vInputGo.AddComponent<TMP_InputField>();
            view.Overlay = overlay;
            view.Input = vInput;
            viewGo.SetActive(true);

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            Assert.IsTrue(overlay.activeSelf, "Normal Show → 오버레이 활성");

            vInput.text = "1234";
            view.Confirm(); // Normal: 제출하되 닫지 않음(검증 대기)
            Assert.IsTrue(overlay.activeSelf, "Normal 제출 후에도 유지(재입력/검증 대기)");

            Object.DestroyImmediate(viewGo);
        }
```

- [ ] **Step 2: 실패 확인**

Run: `run_tests` mode=EditMode test_names=["LoveAlgo.Tests.EditMode.LockScreenIntroEditModeTests.View_Normal_Confirm_Defers_Close"] include_details=true
Expected: FAIL(현재 Confirm이 Normal에서도 Hide → overlay 비활성). `read_console` 0 에러.

- [ ] **Step 3: View 구현**

`Assets/_Project/Scripts/UI/LockScreenView.cs`:

(a) 구독 필드 추가 — 기존 `IDisposable _sub, _finishSub, _resetSub;`를 교체:

```csharp
        IDisposable _sub, _finishSub, _resetSub, _failSub, _acceptSub;
```

(b) `OnEnable`의 구독부에 2줄 추가(기존 `_resetSub = ...` 다음):

```csharp
            _failSub   = EventBus.Subscribe<PasswordVerifyFailedEvent>(OnVerifyFailed);
            _acceptSub = EventBus.Subscribe<PasswordAcceptedEvent>(_ => Hide());
```

(c) `OnDisable`의 dispose부 교체:

```csharp
        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _failSub?.Dispose(); _acceptSub?.Dispose();
            _sub = _finishSub = _resetSub = _failSub = _acceptSub = null;
            if (input != null) input.onSubmit.RemoveListener(OnInputSubmit);
        }
```

(d) `Confirm`의 마지막 `Hide();`를 Normal 분기로 교체:

```csharp
            EventBus.Publish(new SubmitPasswordCommand(pwd)); // 저장/검증은 Controller(ADR-007).
            // Normal은 검증 결과를 기다린다(불일치 재입력). 닫기는 PasswordAcceptedEvent 수신 시.
            if (_mode != LockMode.Normal) Hide();
        }
```

(e) `OnInputSubmit` 근처(아무 메서드 사이)에 핸들러 추가:

```csharp
        /// <summary>검증 실패 — 입력칸 진동 + 입력 초기화·재포커스(가이드는 S3에서 ≥3 분실 처리).</summary>
        void OnVerifyFailed(PasswordVerifyFailedEvent e)
        {
            if (overlay == null || !overlay.activeSelf) return;
            if (passwordField != null) passwordField.Shake();
            if (input != null) { input.text = ""; input.ActivateInputField(); }
            else if (passwordField != null) passwordField.ResetField();
        }
```

> `using LoveAlgo.Events;`는 이미 있음 — 신규 이벤트는 같은 네임스페이스.

- [ ] **Step 4: EditMode 통과 + 회귀 확인**

Run: `refresh_unity`(force, wait) → `read_console` 0 에러 → `run_tests` mode=EditMode test_names=["LoveAlgo.Tests.EditMode.LockScreenIntroEditModeTests.View_Normal_Confirm_Defers_Close"] include_details=true → total=1 passed=1.
또한 기존 4 EditMode(LockScreenIntroEditModeTests) + 3 컨트롤러 EditMode가 그대로 통과하는지 해당 이름들로 재실행해 확인.

- [ ] **Step 5: 실패 테스트 작성 (PlayMode — Failed→진동 / Accepted→닫기)**

`Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` 클래스 내부에 추가(PlayMode라 OnEnable 구독 발화):

```csharp
        [UnityTest]
        public IEnumerator View_Accepted_Event_Hides_Overlay()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            view.Overlay = overlay;
            viewGo.SetActive(true);
            yield return null; // OnEnable 구독

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            Assert.IsTrue(overlay.activeSelf, "Normal Show → 활성");

            EventBus.Publish(new PasswordAcceptedEvent());
            Assert.IsFalse(overlay.activeSelf, "Accepted → 닫힘");

            Object.DestroyImmediate(viewGo);
        }

        [UnityTest]
        public IEnumerator View_Failed_Event_Shakes_And_Clears_Input()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(viewGo.transform);
            var input = inputGo.AddComponent<TMP_InputField>();
            var pf = viewGo.AddComponent<PasswordInputField>();
            pf.Input = input;
            pf.ShakeDuration = 0.1f;
            view.Overlay = overlay;
            view.Input = input;
            view.PasswordField = pf;
            viewGo.SetActive(true);
            yield return null; // OnEnable 구독

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            input.text = "9999";
            var rt = (RectTransform)input.transform;
            Vector2 basePos = rt.anchoredPosition;

            EventBus.Publish(new PasswordVerifyFailedEvent(1));
            Assert.AreEqual("", input.text, "실패 → 입력 초기화");

            // 진동 중 한 프레임은 기준 위치에서 벗어난다
            yield return null;
            bool moved = (rt.anchoredPosition - basePos).sqrMagnitude > 0.0001f;
            // 진동 종료까지 대기 후 복원 확인
            float t = 0f;
            while (t < 0.3f) { t += Time.deltaTime; yield return null; }
            Assert.That(rt.anchoredPosition.x, Is.EqualTo(basePos.x).Within(0.01f), "진동 후 복원");
            Assert.IsTrue(moved, "실패 → 진동 발생");

            Object.DestroyImmediate(viewGo);
        }
```

> 상단 `using`에 `LoveAlgo.Common`(EventBus)·`LoveAlgo.Events`가 필요(없으면 추가). `UnityEngine`/`TMPro`/`UnityEngine.TestTools`/`System.Collections`는 기존 보유.

- [ ] **Step 6: PlayMode 실행(현재 가능)**

Run: `run_tests` mode=PlayMode test_names=["LoveAlgo.Tests.PlayMode.LockScreenIntroPlayModeTests.View_Accepted_Event_Hides_Overlay","LoveAlgo.Tests.PlayMode.LockScreenIntroPlayModeTests.View_Failed_Event_Shakes_And_Clears_Input"] init_timeout=120000 include_details=true → poll get_test_job(wait_timeout=90).
Expected: total=2 passed=2. (PlayMode가 포커스 교착으로 멈추면, 멈춘 잡 해제 후 1회 재시도; 그래도 막히면 컴파일 0에러를 게이트로 두고 감독 수동 실행에 남긴다.)

- [ ] **Step 7: 기존 회귀 확인**

Run: `run_tests` mode=PlayMode test_names=["LoveAlgo.Tests.PlayMode.LockScreenPlayModeTests"] → 5/5 PASS(FirstSetup 경로 무변). 막히면 컴파일 0에러로 대체 + 감독 수동.

- [ ] **Step 8: 커밋**

```bash
git add Assets/_Project/Scripts/UI/LockScreenView.cs Assets/Tests/EditMode/LockScreenIntroEditModeTests.cs Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs
git commit -m "feat(lockscreen): View Normal 지연 닫기 + 검증 결과 반응

Why: Normal 제출은 즉시 닫지 않고 검증을 대기 — PasswordAcceptedEvent 시 닫고,
PasswordVerifyFailedEvent 시 입력칸 진동 + 초기화·재포커스(재입력). FirstSetup/Reset은
S1대로 즉시 닫기. 기존 PlayMode 5종 무변(스펙 §3 S2).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**1. 스펙 커버리지(S2):**
- Normal 저장 비번 대조 → Task 1 컨트롤러 switch. ✓
- 마스킹 기본(감은눈) → S1 `ConfigureForMode(Normal)`에서 이미 적용(S2 무변). ✓
- 일치→완료 / 불일치→`PasswordVerifyFailedEvent`+횟수 → Task 1. ✓
- 오류 시 진동 + 잠금 유지(재입력) → Task 2 `OnVerifyFailed` + Normal 지연 닫기. ✓
- 가이드 "비밀번호를 입력해주세요." → S1 `ConfigureForMode(Normal)`이 `Normal` 상태로 설정(S2 무변). ✓
- ≥3 열쇠/분실 = S3(비범위, 명시).

**2. 플레이스홀더 스캔:** 모든 코드 단계에 실제 코드 포함. 신규 수치 없음(진동은 S1 인스펙터값). ✓

**3. 타입 일관성:** `PasswordVerifyFailedEvent(int)`/`PasswordAcceptedEvent()`(Task1) ↔ Task2 구독/발행 일치. `PasswordInputField.Shake()/ResetField()`/`ShakeDuration`(S1) ↔ Task2 테스트/View 사용 일치. `LockMode.Normal/FirstSetup/Reset`(기존 enum) 일치. 컨트롤러 `OnShow/OnSubmit` public(기존) ↔ EditMode 직접 호출 일치. ✓
