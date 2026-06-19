# 비밀번호 입력 커스텀 시스템 S3 (오류/분실/재설정) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Normal 비밀번호를 3회 이상 틀리면 우하단 열쇠 아이콘 + 분실 안내가 등장하고, 열쇠를 누르면 재설정 확인 모달(예/아니오)이 뜬다. 예→Reset(재설정) 재진입, 아니오→모달만 닫고 잠금화면 유지.

**Architecture:** `KeyResetButton`(UI)이 클릭 시 기존 `ShowModalCommand`(예/아니오)를 발행하고, 예 선택 콜백에서 신규 `RequestPasswordResetCommand`를 발행. `LockScreenController`(로직)와 `LockScreenView`(표시)가 각각 이를 구독 — 컨트롤러는 `_mode=Reset`·오류횟수 0(핸들 유지), View는 Reset UI 재구성(평문·"입력 완료"·설정 가이드)+열쇠 숨김. 열쇠 노출은 View가 `PasswordVerifyFailedEvent.ErrorCount >= lostThreshold(3)`에서 트리거. 재설정 확인 모달은 기존 `ModalView`/`ShowModalCommand`/`ModalRequest`(Yes/No) 재사용 — 신규 모달 인프라 없음.

**Tech Stack:** Unity 6000.5.0f1, C#, EventBus, uGUI+TMP, Unity Test Framework, Unity MCP.

## Global Constraints

- S1/S2 계획의 Global Constraints + "테스트 배치 개정"을 승계(로직=EditMode 헤드리스, 코루틴/구독반응=PlayMode, run_tests 정규화 전체 이름, EditMode는 OnEnable/구독 미발화 → 직접 호출/수동 배선).
- 교차통신 EventBus + State SO만(ADR-007). 모달은 기존 `ShowModalCommand`/`ModalRequest`(Yes/No) 재사용.
- 매직넘버 금지: 열쇠 임계치(`lostThreshold=3`)·모달 문구/라벨은 인스펙터 직렬화.
- 커밋 Atomic + "왜" + `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. 각 태스크 자기 파일만 `git add`(repo 무관 변경 다수 — `git add -A` 금지).
- 기존 테스트 보존: LockScreenPlayModeTests 5 + S1(EM4/PM2) + S2(EM4/PM2) 그린 유지.

---

## 파일 구조 (생성/수정 맵)

| 파일 | 동작 | 책임 |
|---|---|---|
| `Assets/_Project/Scripts/Core/Events/LockScreenEvents.cs` | 수정 | `RequestPasswordResetCommand` 추가 |
| `Assets/_Project/Scripts/UI/KeyResetButton.cs` | 생성 | 열쇠 버튼: 표시/숨김 + 클릭→재설정 확인 모달→예→Reset 요청 |
| `Assets/_Project/Scripts/Narrative/LockScreenController.cs` | 수정 | `RequestPasswordResetCommand`→`_mode=Reset`·오류0(핸들 유지) |
| `Assets/_Project/Scripts/UI/LockScreenView.cs` | 수정 | ≥3 분실 가이드+열쇠 노출 / Reset 요청→Reset UI 재구성·열쇠 숨김 |
| `Assets/_Project/Prefabs/LockScreen.prefab` | 재구성 | 우하단 열쇠 버튼 배치+배선(감독이 위치 튜닝) |
| `Assets/Tests/EditMode/LockScreenIntroEditModeTests.cs` | 수정 | KeyResetButton EditMode 1~2건 |
| `Assets/Tests/EditMode/LockScreenControllerEditModeTests.cs` | 수정 | Reset 요청→Reset 저장 1건 |
| `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` | 수정 | View ≥3 노출 / Reset 재구성 PlayMode 2건 |

---

## Task 1: RequestPasswordResetCommand + KeyResetButton

**Files:**
- Modify: `Assets/_Project/Scripts/Core/Events/LockScreenEvents.cs`
- Create: `Assets/_Project/Scripts/UI/KeyResetButton.cs`
- Test: `Assets/Tests/EditMode/LockScreenIntroEditModeTests.cs` (추가)

**Interfaces:**
- Produces:
  - `readonly struct RequestPasswordResetCommand {}` (namespace `LoveAlgo.Events`)
  - `KeyResetButton`: `void SetVisible(bool)`(root GO 활성 토글), `void RequestReset()`(예/아니오 `ShowModalCommand` 발행, 예=index0→`RequestPasswordResetCommand`). 직렬화 `Root`(GameObject)/`Button` + public 접근자.

- [ ] **Step 1: 이벤트 추가**

`LockScreenEvents.cs` — `PasswordAcceptedEvent` 다음(네임스페이스 닫기 전)에:

```csharp
    /// <summary>비밀번호 재설정 요청(분실 모달 '예') — KeyResetButton→Controller(_mode=Reset)·View(Reset UI 재구성). 핸들은 유지(현 잠금 세션 그대로).</summary>
    public readonly struct RequestPasswordResetCommand { }
```

- [ ] **Step 2: 실패 테스트 작성** — EditMode 파일에 추가

`Assets/Tests/EditMode/LockScreenIntroEditModeTests.cs` 클래스 내부에 추가(상단 using에 `System.Collections.Generic` 필요 시 추가):

```csharp
        [Test]
        public void KeyResetButton_SetVisible_Toggles_Root()
        {
            var go = new GameObject("KeyReset");
            var rootVis = new GameObject("KeyVisual");
            rootVis.transform.SetParent(go.transform);
            var kr = go.AddComponent<KeyResetButton>();
            kr.Root = rootVis;

            kr.SetVisible(false);
            Assert.IsFalse(rootVis.activeSelf, "숨김");
            kr.SetVisible(true);
            Assert.IsTrue(rootVis.activeSelf, "노출");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void KeyResetButton_Yes_Publishes_ResetCommand()
        {
            var go = new GameObject("KeyReset");
            var kr = go.AddComponent<KeyResetButton>();

            LoveAlgo.Events.ShowModalCommand captured = default;
            bool gotModal = false;
            var sub1 = EventBus.Subscribe<LoveAlgo.Events.ShowModalCommand>(e => { captured = e; gotModal = true; });
            int resetCount = 0;
            var sub2 = EventBus.Subscribe<LoveAlgo.Events.RequestPasswordResetCommand>(_ => resetCount++);

            kr.RequestReset();
            Assert.IsTrue(gotModal, "재설정 확인 모달 발행");
            Assert.AreEqual(2, captured.Buttons.Count, "예/아니오 2버튼");

            captured.Handle.Select(1); // 아니오 → 재설정 안 함
            Assert.AreEqual(0, resetCount, "아니오는 재설정 발행 안 함");

            // 새 모달에서 예 선택
            gotModal = false;
            kr.RequestReset();
            captured.Handle.Select(0); // 예 → 재설정
            Assert.AreEqual(1, resetCount, "예 → RequestPasswordResetCommand 1회");

            sub1.Dispose(); sub2.Dispose();
            Object.DestroyImmediate(go);
        }
```

- [ ] **Step 3: 실패 확인**

Run: `run_tests` mode=EditMode test_names=["LoveAlgo.Tests.EditMode.LockScreenIntroEditModeTests.KeyResetButton_Yes_Publishes_ResetCommand"] include_details=true → 컴파일 에러(`KeyResetButton` 없음).

- [ ] **Step 4: KeyResetButton 구현**

`Assets/_Project/Scripts/UI/KeyResetButton.cs`:

```csharp
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowModalCommand, ModalButton, ModalButtonKind, ModalRequest, RequestPasswordResetCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 비밀번호 분실 시 우하단 열쇠 버튼(*View: LockScreen). 3회+ 오류 시 View가 <see cref="SetVisible"/>(true).
    /// 클릭 시 재설정 확인 모달(예/아니오)을 기존 <see cref="ShowModalCommand"/>로 발행하고, 예 선택 콜백에서
    /// <see cref="RequestPasswordResetCommand"/>를 발행한다(→Controller/View Reset 재진입). ADR-007 표시+명령.
    /// 표시/숨김은 <see cref="root"/>(미바인딩 시 자신) 활성 토글. 문구/라벨은 인스펙터 직렬화.
    /// </summary>
    public class KeyResetButton : MonoBehaviour
    {
        [Tooltip("표시/숨김 대상(열쇠 비주얼 루트). 미바인딩 시 자기 GameObject.")]
        [SerializeField] GameObject root;
        [SerializeField] Button button;
        [SerializeField] string modalTitle = "";
        [TextArea][SerializeField] string modalMessage = "새로운 비밀번호 설정을 진행하시겠습니까?";
        [SerializeField] string yesLabel = "예";
        [SerializeField] string noLabel = "아니오";

        public GameObject Root { get => root; set => root = value; }
        public Button Button { get => button; set => button = value; }

        void OnEnable() { if (button != null) button.onClick.AddListener(RequestReset); }
        void OnDisable() { if (button != null) button.onClick.RemoveListener(RequestReset); }

        /// <summary>열쇠 표시/숨김(root 활성 토글).</summary>
        public void SetVisible(bool visible)
        {
            var target = root != null ? root : gameObject;
            target.SetActive(visible);
        }

        /// <summary>재설정 확인 모달 발행 — 예(0)=재설정 요청, 아니오(1)=닫기만.</summary>
        public void RequestReset()
        {
            var buttons = new List<ModalButton>
            {
                new ModalButton(yesLabel, ModalButtonKind.Yes),
                new ModalButton(noLabel, ModalButtonKind.No),
            };
            var req = new ModalRequest(idx => { if (idx == 0) EventBus.Publish(new RequestPasswordResetCommand()); });
            EventBus.Publish(new ShowModalCommand(modalTitle, modalMessage, buttons, req));
        }
    }
}
```

- [ ] **Step 5: 통과 확인**

Run: `refresh_unity`(force,wait) → `read_console` types=["error"] 0건 → `run_tests` mode=EditMode test_names=["LoveAlgo.Tests.EditMode.LockScreenIntroEditModeTests.KeyResetButton_SetVisible_Toggles_Root","LoveAlgo.Tests.EditMode.LockScreenIntroEditModeTests.KeyResetButton_Yes_Publishes_ResetCommand"] include_details=true → total=2 passed=2.

- [ ] **Step 6: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Events/LockScreenEvents.cs Assets/_Project/Scripts/UI/KeyResetButton.cs Assets/_Project/Scripts/UI/KeyResetButton.cs.meta Assets/Tests/EditMode/LockScreenIntroEditModeTests.cs
git commit -m "feat(lockscreen): KeyResetButton + RequestPasswordResetCommand

Why: 분실 시 우하단 열쇠 버튼 클릭→재설정 확인 모달(예/아니오, 기존 ShowModalCommand
재사용), 예 선택 시 RequestPasswordResetCommand 발행(스펙 §오류/분실). 표시/숨김은
root 토글, 문구 인스펙터 직렬화(ADR-012).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: 컨트롤러 재설정 요청 처리

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/LockScreenController.cs`
- Test: `Assets/Tests/EditMode/LockScreenControllerEditModeTests.cs` (추가)

**Interfaces:**
- Produces: `LockScreenController.OnResetRequested()` (public — 테스트 직접 호출). `RequestPasswordResetCommand` 구독 시 `_mode=Reset`·`_errorCount=0`(핸들 유지). 이후 `OnSubmit`은 Reset 경로(저장+release).

- [ ] **Step 1: 실패 테스트 작성** — 컨트롤러 EditMode 파일에 추가

```csharp
        [Test]
        public void ResetRequest_Switches_To_Reset_And_Saves_New_Password()
        {
            _gs.Password = "1234";
            var handle = new CompletionHandle();
            _ctrl.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, handle));
            _ctrl.OnSubmit(new SubmitPasswordCommand("9999")); // 불일치 — 유지
            Assert.IsFalse(handle.IsComplete, "불일치 후 유지");

            _ctrl.OnResetRequested(); // 재설정 진입(핸들 유지, 모드 Reset)

            _ctrl.OnSubmit(new SubmitPasswordCommand("5555")); // Reset 저장
            Assert.AreEqual("5555", _gs.Password, "Reset → 새 비번 저장");
            Assert.IsTrue(handle.IsComplete, "Reset 저장 후 핸들 완료(진행)");
        }
```

- [ ] **Step 2: 실패 확인**

Run: `run_tests` mode=EditMode test_names=["LoveAlgo.Tests.EditMode.LockScreenControllerEditModeTests.ResetRequest_Switches_To_Reset_And_Saves_New_Password"] include_details=true → 컴파일 에러(`OnResetRequested` 없음).

- [ ] **Step 3: 컨트롤러 구현**

`LockScreenController.cs`:

(a) 구독 필드에 `_resetReqSub` 추가 — 기존 `IDisposable _showSub, _submitSub, _finishSub, _resetSub;`를 교체:

```csharp
        IDisposable _showSub, _submitSub, _finishSub, _resetSub, _resetReqSub;
```

(b) `OnEnable`에 구독 1줄 추가(기존 `_resetSub = ...` 다음):

```csharp
            _resetReqSub = EventBus.Subscribe<RequestPasswordResetCommand>(_ => OnResetRequested());
```

(c) `OnDisable` 교체:

```csharp
        void OnDisable()
        {
            _showSub?.Dispose(); _submitSub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose(); _resetReqSub?.Dispose();
            _showSub = _submitSub = _finishSub = _resetSub = _resetReqSub = null;
        }
```

(d) `OnShow` 다음에 메서드 추가:

```csharp
        /// <summary>재설정 요청 — 현 잠금 세션을 Reset 모드로 전환(핸들 유지, 오류 0). 이후 Submit은 저장 경로. 직접 호출도 가능(테스트).</summary>
        public void OnResetRequested()
        {
            if (_pending == null) return; // 활성 잠금 없음 — 무시.
            _mode = LockMode.Reset;
            _errorCount = 0;
        }
```

- [ ] **Step 4: 통과 확인**

Run: `refresh_unity`(force,wait) → `read_console` 0건 → `run_tests` mode=EditMode test_names=["LoveAlgo.Tests.EditMode.LockScreenControllerEditModeTests.ResetRequest_Switches_To_Reset_And_Saves_New_Password"] include_details=true → total=1 passed=1. 또한 S2 컨트롤러 3건도 재실행해 그린 확인.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Narrative/LockScreenController.cs Assets/Tests/EditMode/LockScreenControllerEditModeTests.cs
git commit -m "feat(lockscreen): 컨트롤러 재설정 요청 → Reset 전환

Why: RequestPasswordResetCommand 수신 시 현 잠금 세션을 Reset 모드로 전환하고 오류
횟수를 비운다(핸들 유지 — 새 비번 저장 후 진행). 분실 재설정 플로우 로직(스펙 §오류/분실).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: View — ≥3 분실 노출 + Reset 재구성

**Files:**
- Modify: `Assets/_Project/Scripts/UI/LockScreenView.cs`
- Test: `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (추가)

**Interfaces:**
- Consumes: `KeyResetButton.SetVisible(bool)`, `LockScreenGuideText.LockGuideState.Lost`, `RequestPasswordResetCommand`.
- Produces (View 동작):
  - 직렬화 `keyButton`(KeyResetButton)·`lostThreshold=3` + 접근자.
  - `OnVerifyFailed`가 `ErrorCount >= lostThreshold`면 가이드 `Lost` + `keyButton.SetVisible(true)`.
  - `RequestPasswordResetCommand` 구독 → `_mode=Reset`·`ConfigureForMode(Reset)`·`keyButton.SetVisible(false)`·입력 초기화/재포커스.
  - `OnShow` 시작 시 `keyButton.SetVisible(false)`(세션 리셋). 기존 API 불변·null-safe.

- [ ] **Step 1: View 구현**

`LockScreenView.cs`:

(a) 직렬화 필드 추가(기존 `loginButton` 필드 다음):

```csharp
        [Tooltip("분실 시 우하단 열쇠 버튼(3회+ 오류 노출).")]
        [SerializeField] KeyResetButton keyButton;
        [Tooltip("분실 안내·열쇠 노출 임계 오류 횟수.")]
        [SerializeField] int lostThreshold = 3;

        public KeyResetButton KeyButton { get => keyButton; set => keyButton = value; }
        public int LostThreshold { get => lostThreshold; set => lostThreshold = value; }
```

(b) 구독 필드에 `_resetReqSub` 추가 — 기존 `IDisposable _sub, _finishSub, _resetSub, _failSub, _acceptSub;` 교체:

```csharp
        IDisposable _sub, _finishSub, _resetSub, _failSub, _acceptSub, _resetReqSub;
```

(c) `OnEnable` 구독부에 1줄 추가(기존 `_acceptSub = ...` 다음):

```csharp
            _resetReqSub = EventBus.Subscribe<RequestPasswordResetCommand>(_ => OnResetRequested());
```

(d) `OnDisable` 교체:

```csharp
        void OnDisable()
        {
            _sub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _failSub?.Dispose(); _acceptSub?.Dispose(); _resetReqSub?.Dispose();
            _sub = _finishSub = _resetSub = _failSub = _acceptSub = _resetReqSub = null;
            if (input != null) input.onSubmit.RemoveListener(OnInputSubmit);
        }
```

(e) `OnShow`의 `ConfigureForMode(_mode);` **직전**에 열쇠 숨김 추가:

```csharp
            if (keyButton != null) keyButton.SetVisible(false); // 새 세션 — 열쇠 숨김
            ConfigureForMode(_mode);
```

(f) `OnVerifyFailed` 끝에 ≥임계 분실 처리 추가:

```csharp
        void OnVerifyFailed(PasswordVerifyFailedEvent e)
        {
            if (overlay == null || !overlay.activeSelf) return;
            if (passwordField != null) passwordField.Shake();
            if (input != null) { input.text = ""; input.ActivateInputField(); }
            else if (passwordField != null) passwordField.ResetField();

            if (e.ErrorCount >= lostThreshold)
            {
                if (guide != null) guide.SetState(LockScreenGuideText.LockGuideState.Lost);
                if (keyButton != null) keyButton.SetVisible(true);
            }
        }

        /// <summary>재설정 요청 — Reset 모드로 UI 재구성(평문·설정 가이드·"입력 완료"), 열쇠 숨김, 입력 초기화.</summary>
        void OnResetRequested()
        {
            if (overlay == null || !overlay.activeSelf) return;
            _mode = LockMode.Reset;
            ConfigureForMode(_mode);
            if (keyButton != null) keyButton.SetVisible(false);
            if (input != null) { input.text = ""; input.ActivateInputField(); }
            else if (passwordField != null) passwordField.ResetField();
        }
```

- [ ] **Step 2: 실패 테스트 작성** — PlayMode 파일에 추가(OnEnable 구독 발화)

`Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` 클래스 내부에 추가:

```csharp
        [UnityTest]
        public IEnumerator View_ThreeFails_Reveals_Key_And_Lost_Guide()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(viewGo.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var guide = viewGo.AddComponent<LockScreenGuideText>();
            guide.Label = label; guide.LostText = "분실";
            var keyVis = new GameObject("KeyVisual");
            keyVis.transform.SetParent(viewGo.transform);
            var kr = viewGo.AddComponent<KeyResetButton>();
            kr.Root = keyVis;
            view.Overlay = overlay; view.Guide = guide; view.KeyButton = kr; view.LostThreshold = 3;
            viewGo.SetActive(true);
            yield return null; // OnEnable 구독

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            Assert.IsFalse(keyVis.activeSelf, "초기엔 열쇠 숨김");

            EventBus.Publish(new PasswordVerifyFailedEvent(1));
            Assert.IsFalse(keyVis.activeSelf, "1회는 미노출");
            EventBus.Publish(new PasswordVerifyFailedEvent(3));
            Assert.IsTrue(keyVis.activeSelf, "3회+ → 열쇠 노출");
            Assert.AreEqual("분실", label.text, "3회+ → 분실 가이드");

            Object.DestroyImmediate(viewGo);
        }

        [UnityTest]
        public IEnumerator View_ResetRequest_Reconfigures_To_Setup_And_Hides_Key()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(viewGo.transform);
            var input = inputGo.AddComponent<TMP_InputField>();
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(viewGo.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var guide = viewGo.AddComponent<LockScreenGuideText>();
            guide.Label = label; guide.SetupText = "설정"; guide.NormalText = "입력"; guide.LostText = "분실";
            var keyVis = new GameObject("KeyVisual");
            keyVis.transform.SetParent(viewGo.transform);
            var kr = viewGo.AddComponent<KeyResetButton>();
            kr.Root = keyVis;
            view.Overlay = overlay; view.Input = input; view.Guide = guide; view.KeyButton = kr; view.LostThreshold = 3;
            viewGo.SetActive(true);
            yield return null;

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            EventBus.Publish(new PasswordVerifyFailedEvent(3)); // 열쇠+분실
            Assert.IsTrue(keyVis.activeSelf);

            EventBus.Publish(new RequestPasswordResetCommand());
            Assert.IsFalse(keyVis.activeSelf, "재설정 → 열쇠 숨김");
            Assert.AreEqual("설정", label.text, "재설정 → 설정 가이드(Reset=FirstSetup UI)");

            Object.DestroyImmediate(viewGo);
        }
```

- [ ] **Step 3: 실행(컴파일 + EditMode 회귀 + PlayMode)**

Run 순서:
1. `refresh_unity`(force,wait) → `read_console` types=["error"] 0건.
2. PlayMode 신규 2건: `run_tests` mode=PlayMode test_names=["LoveAlgo.Tests.PlayMode.LockScreenIntroPlayModeTests.View_ThreeFails_Reveals_Key_And_Lost_Guide","LoveAlgo.Tests.PlayMode.LockScreenIntroPlayModeTests.View_ResetRequest_Reconfigures_To_Setup_And_Hides_Key"] init_timeout=120000 include_details=true → poll(wait_timeout=90) → total=2 passed=2. (포커스 교착 시 1회 재시도 후 컴파일 0에러 게이트로 남김.)
3. 회귀: `run_tests` mode=PlayMode test_names=["LoveAlgo.Tests.PlayMode.LockScreenPlayModeTests"] → 5/5. EditMode 전체 LockScreen 관련도 재실행(그린 유지).

- [ ] **Step 4: 커밋**

```bash
git add Assets/_Project/Scripts/UI/LockScreenView.cs Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs
git commit -m "feat(lockscreen): View 3회+ 분실 노출 + 재설정 재구성

Why: 오류 누적이 임계(기본 3) 이상이면 분실 가이드 + 우하단 열쇠 노출, 재설정 요청 시
Reset 모드로 UI 재구성(평문·설정 가이드)하고 열쇠를 숨긴다. 새 Show마다 열쇠 리셋
(스펙 §오류/분실).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: 프리팹 — 열쇠 버튼 배치 + 배선 (감독 시각 튜닝 전제)

> **성격:** Unity MCP `execute_code`로 구조+배선만(코드 아님). 위치는 placeholder, 감독이 우하단으로 튜닝 + 플레이 시각 검증. password_key.png 사용(이미 Sprite).

- [ ] **Step 1: 열쇠 버튼 GO 생성 + 배선**

`LockScreen.prefab`의 `LockOverlay > InputGroup`(또는 LockOverlay 직속) 아래에 `KeyVisual`(Image=password_key + Button) 생성, 우하단 앵커(anchor (1,0), pivot (1,0), pos 약 (-40,40), 64×64), **기본 비활성**(SetActive(false)). `LockScreen` 루트에 `KeyResetButton` 컴포넌트 추가 — `Root`=KeyVisual, `Button`=KeyVisual의 Button. `LockScreenView.KeyButton`=그 KeyResetButton, `LostThreshold`=3 배선.

- [ ] **Step 2: 검증**

저장 후 read-back execute_code로 `view.KeyButton != null`, `kr.Root != null`, `kr.Button != null`, KeyVisual 기본 비활성 확인. `read_console` 0 에러.

- [ ] **Step 3: 커밋**

```bash
git add Assets/_Project/Prefabs/LockScreen.prefab
git commit -m "feat(lockscreen): 프리팹 우하단 열쇠 버튼 배치+배선

Why: 분실 재설정 진입점. 기본 숨김, KeyResetButton/View 배선. 위치는 placeholder —
감독 시각 튜닝/플레이 검증 예정.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**1. 스펙 커버리지(S3):**
- 3회+ 우하단 열쇠 + 분실 가이드 → Task 3 `OnVerifyFailed` ≥lostThreshold. ✓
- 열쇠 클릭 → 예/아니오 모달(기존 ShowModalCommand) → Task 1 `KeyResetButton.RequestReset`. ✓
- 예→재설정(Reset 재진입) → Task 1(이벤트)+Task 2(컨트롤러)+Task 3(View 재구성). ✓
- 아니오→모달만 닫고 잠금 유지 → Task 1(예만 발행, 아니오 no-op) + 잠금 유지(핸들 무변). ✓
- Reset=FirstSetup UI(평문·"입력 완료"·설정 가이드) → View `ConfigureForMode(Reset)`(S1 매핑). ✓
- 프리팹 열쇠 배치 → Task 4(감독 튜닝). ✓

**2. 플레이스홀더 스캔:** 모든 코드 단계 실제 코드. 신규 수치는 인스펙터(lostThreshold/모달 문구). ✓

**3. 타입 일관성:** `RequestPasswordResetCommand`(T1) ↔ Controller(T2)/View(T3) 구독 일치. `KeyResetButton.SetVisible/Root/Button`(T1) ↔ View/프리팹/테스트 일치. `LockGuideState.Lost`(S1) ↔ T3 사용 일치. `ModalButton/ModalButtonKind/ModalRequest/ShowModalCommand`(기존 LoveAlgo.Events) 일치. `OnResetRequested`(T2 public) ↔ 테스트 직접 호출 일치. ✓
