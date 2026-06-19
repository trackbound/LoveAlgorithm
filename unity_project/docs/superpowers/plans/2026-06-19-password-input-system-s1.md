# 비밀번호 입력 커스텀 시스템 S1 (진입 연출 + FirstSetup 해피패스) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 잠금화면 진입 시 영상(`03_login.mp4`)대로 위젯이 슬라이드아웃 → 딤 페이드 → 비밀번호 입력 UI가 등장하고, FirstSetup(첫 설정) 모드에서 평문 7자 입력 + 눈 토글 + "입력 완료" 버튼으로 비번을 저장하는 해피패스를 구현한다.

**Architecture:** 기존 `LockScreenView`(Show 수신·overlay 동기 활성·Confirm→Submit)와 `LockScreenController`(FirstSetup 저장)를 **가산 확장**한다. 진입 연출은 선택적 `LockScreenIntroDirector`에 위임(미바인딩 시 기존 즉시 경로 폴백 → 기존 테스트 보존). 입력칸 마스킹/눈토글/7자/진동은 `PasswordInputField`, 안내 텍스트는 `LockScreenGuideText`, 확정 버튼은 `LoginButton`으로 책임 분리. 연출은 코루틴 lerp(DOTween 미사용=기존 관례).

**Tech Stack:** Unity 6000.5.0f1, C#, uGUI + TextMeshPro, 코루틴, Unity Test Framework(EditMode/PlayMode, NUnit), EventBus(`LoveAlgo.Common`), Unity MCP(`run_tests`/`read_console`/`manage_*`).

## Global Constraints

- Unity 6000.5.0f1. Obsolete API 신규 사용 금지(`FindObjectOfType`→`FindAnyObjectByType`, `enableWordWrapping`→`textWrappingMode` 등).
- 로깅: 디버그는 `LoveAlgo.Common.Log.Info/Warn`(릴리즈 제거), 사용자 보고 에러만 `Log.Error`/`Debug.LogError`.
- 교차통신은 EventBus + State SO만. `Services`/`UIManager.Instance.*` wrapper/`I*` 서비스 조회 금지(ADR-007). UI는 표시+명령 발행만.
- 신규 UI 코드는 기존 `LoveAlgo.UI` 어셈블리(`Assets/_Project/Scripts/UI/`)에 둔다. 로직(컨트롤러)은 `LoveAlgo.Narrative`.
- 매직넘버 금지(ADR-012): 모든 연출 수치/타이밍/문구는 인스펙터 `[SerializeField]`로 노출.
- 커밋: 한 기능=한 커밋(Atomic), 본문에 "왜(Why)" 명시. 메시지는 한국어. 끝에 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- 테스트: Unity MCP `run_tests`(mode=EditMode/PlayMode, testFilter) 또는 에디터 Test Runner. 스크립트 변경 후 `read_console`로 컴파일 0 에러 확인 뒤 진행.
- **기존 테스트 보존**: `LockScreenPlayModeTests`(View 동기 overlay 활성/빈 입력 무시/통합 hang0)는 신규 컴포넌트 미바인딩 폴백으로 그대로 그린 유지.
- 경로 구분자는 슬래시(/). Unity 경로는 `Assets/` 기준.
- 커밋 범위에 무관한 기존 미커밋 변경(폰트/씬/프리팹 등)이 섞여 있으니, **각 태스크는 자신이 생성/수정한 파일만 `git add`** 한다.

## ⚠️ 테스트 배치 개정 (2026-06-19, 이 항목이 각 태스크의 테스트 파일 지정을 **override**)

이 환경은 **PlayMode 테스트가 에디터 포커스 교착**으로 MCP 자동 실행이 막힌다(감독이 터미널로 대화 → 에디터 unfocus → PlayMode 진입 데드락). 따라서:

- **코루틴이 없는 로직 테스트는 EditMode로 배치** — 포커스 없이 MCP가 헤드리스로 실행 가능.
  - 파일: `Assets/Tests/EditMode/LockScreenIntroEditModeTests.cs`, 클래스 `LockScreenIntroEditModeTests`, 네임스페이스 `LoveAlgo.Tests.EditMode`.
  - 해당: GuideText(완료), PasswordInputField **마스킹/7자**(Task 2), LoginButton(Task 3), View 모드 구성(Task 5).
  - asmdef `Assets/Tests/EditMode/LoveAlgo.Tests.EditMode.asmdef`에 `Unity.TextMeshPro`,`UnityEngine.UI` 참조 추가됨(완료).
  - 순수 로직은 `[Test]`(IEnumerator/`yield` 불필요)로 작성. `OnEnable`은 활성 GameObject에 `AddComponent` 시 동기 호출되므로 EditMode에서도 발화.
- **진짜 런타임(StartCoroutine/Time.deltaTime)이 필요한 테스트만 PlayMode로 남긴다.**
  - 파일: `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs`, 클래스 `LockScreenIntroPlayModeTests`.
  - 해당: PasswordInputField **Shake**(Task 2), IntroDirector **타임라인**(Task 4).
  - **MCP 헤드리스 실행 불가** — 작성만 하고, 감독이 Unity Test Runner에서 직접 실행하거나(포커스 유지) 슬라이스 말미에 일괄 수동 검증한다. 서브에이전트는 이 두 PlayMode 테스트의 PASS를 기다리지 말 것(작성+컴파일 0에러까지가 게이트).
- **run_tests 필터는 정규화 전체 이름**을 쓴다(짧은 메서드명은 0개 매칭). 예:
  `test_names=["LoveAlgo.Tests.EditMode.LockScreenIntroEditModeTests.<MethodName>"]`, `mode=EditMode`.
- 기존 `LockScreenPlayModeTests`(회귀 가드)도 PlayMode라 MCP 헤드리스로는 못 돌린다 → **컴파일 0에러로 비파괴 확인** + 감독 수동 실행 대상에 포함.

---

## 파일 구조 (생성/수정 맵)

| 파일 | 동작 | 책임 |
|---|---|---|
| `Assets/_Project/Scripts/UI/LockScreenGuideText.cs` | 생성 | 입력칸 위 상태별 안내 텍스트(인스펙터 문구) |
| `Assets/_Project/Scripts/UI/PasswordInputField.cs` | 생성 | TMP_InputField 래핑: 7자 제한, 마스킹/눈토글, 오류 진동 |
| `Assets/_Project/Scripts/UI/LoginButton.cs` | 생성 | 모드별 라벨 + active/deact 스프라이트 토글 + 클릭→Confirm |
| `Assets/_Project/Scripts/UI/LockScreenIntroDirector.cs` | 생성 | 진입 연출(위젯 슬라이드아웃→딤→입력 reveal→콜백) |
| `Assets/_Project/Scripts/UI/LockScreenView.cs` | 수정 | 모드별 위젯 구성 + 진입 연출 위임 + "설정 완료!" 전환 |
| `Assets/_Project/Prefabs/LockScreen.prefab` | 재구성 | 위젯 PNG 배치 + 신규 컴포넌트 배선 |
| `Assets/Art/UI/Screen/LockScreen/*.png` | import 확인 | Sprite(2D/UI) 임포트 확인(btn_login_*, password_view/hidden/key 등) |
| `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` | 생성 | 신규 컴포넌트 4종 행동 검증 |
| `Assets/Tests/PlayMode/LockScreenPlayModeTests.cs` | (무변) | 기존 그린 유지 확인 |

**컴포넌트 협력 요약(데이터 흐름):**
`ShowLockScreenCommand(mode)` → `LockScreenView.OnShow`(overlay 동기 on + `ConfigureForMode` + intro 위임) → `LockScreenIntroDirector.Play(onInputReady)` → `onInputReady`에서 입력 포커스 → 사용자 입력 → 엔터 or `LoginButton`→`LockScreenView.Confirm` → `SubmitPasswordCommand` → `LockScreenController` 저장.

---

## Task 1: LockScreenGuideText (상태별 안내 텍스트)

**Files:**
- Create: `Assets/_Project/Scripts/UI/LockScreenGuideText.cs`
- Test: `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (신규, 본 태스크 분량만)

**Interfaces:**
- Produces:
  - `public enum LockGuideState { Setup, SetupComplete, Normal, Lost }`
  - `public void SetState(LockGuideState state)` — `label.text`를 상태별 직렬화 문구로 교체. `label` null이면 no-op.

- [ ] **Step 1: 실패 테스트 작성** — 신규 테스트 파일 생성

`Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 비밀번호 입력 커스텀 시스템 S1 신규 컴포넌트(GuideText/PasswordInputField/LoginButton/IntroDirector)
    /// 단위 행동 검증. 프리팹 배선은 에디터 검증으로 별도.
    /// </summary>
    public class LockScreenIntroPlayModeTests
    {
        [UnityTest]
        public IEnumerator GuideText_SetState_Swaps_Label_Text()
        {
            var go = new GameObject("Guide");
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var guide = go.AddComponent<LockScreenGuideText>();
            guide.Label = label;
            guide.SetupText = "설정문구";
            guide.SetupCompleteText = "완료문구";
            guide.NormalText = "입력문구";
            guide.LostText = "분실문구";
            yield return null;

            guide.SetState(LockScreenGuideText.LockGuideState.Setup);
            Assert.AreEqual("설정문구", label.text);
            guide.SetState(LockScreenGuideText.LockGuideState.SetupComplete);
            Assert.AreEqual("완료문구", label.text);
            guide.SetState(LockScreenGuideText.LockGuideState.Normal);
            Assert.AreEqual("입력문구", label.text);
            guide.SetState(LockScreenGuideText.LockGuideState.Lost);
            Assert.AreEqual("분실문구", label.text);

            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인**

Run: Unity MCP `run_tests` mode=PlayMode testFilter=`LockScreenIntroPlayModeTests`
Expected: 컴파일 에러 — `LockScreenGuideText` 형식 없음. (`read_console`로 확인)

- [ ] **Step 3: 최소 구현**

`Assets/_Project/Scripts/UI/LockScreenGuideText.cs`:

```csharp
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 잠금화면 입력칸 위 안내 텍스트(*View: LockScreen). 상태별 문구를 인스펙터에 직렬화하고
    /// <see cref="SetState"/>로 교체한다(ADR-012: 문구 = 데이터). label 미바인딩 시 no-op(안전).
    /// </summary>
    public class LockScreenGuideText : MonoBehaviour
    {
        public enum LockGuideState { Setup, SetupComplete, Normal, Lost }

        [SerializeField] TMP_Text label;
        [TextArea][SerializeField] string setupText = "앞으로 사용할 비밀번호를 입력해주세요.\n최대 7자까지 입력 가능합니다.";
        [SerializeField] string setupCompleteText = "비밀번호 설정 완료!";
        [SerializeField] string normalText = "비밀번호를 입력해주세요.";
        [TextArea][SerializeField] string lostText = "비밀번호를 잊으셨다면 우측 하단 열쇠 모양 버튼을 눌러주세요.";

        public TMP_Text Label { get => label; set => label = value; }
        public string SetupText { get => setupText; set => setupText = value; }
        public string SetupCompleteText { get => setupCompleteText; set => setupCompleteText = value; }
        public string NormalText { get => normalText; set => normalText = value; }
        public string LostText { get => lostText; set => lostText = value; }

        public void SetState(LockGuideState state)
        {
            if (label == null) return;
            label.text = state switch
            {
                LockGuideState.Setup => setupText,
                LockGuideState.SetupComplete => setupCompleteText,
                LockGuideState.Normal => normalText,
                LockGuideState.Lost => lostText,
                _ => label.text
            };
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `run_tests` mode=PlayMode testFilter=`GuideText_SetState_Swaps_Label_Text`
Expected: PASS. `read_console` 컴파일 0 에러.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/UI/LockScreenGuideText.cs Assets/_Project/Scripts/UI/LockScreenGuideText.cs.meta Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs.meta
git commit -m "feat(lockscreen): 상태별 안내 텍스트 LockScreenGuideText

Why: 비밀번호 설정/입력/설정완료/분실 안내 문구를 입력칸 위에 상태로 노출
(스펙 §3 상태 머신). 문구는 인스펙터 직렬화로 감독 튜닝(ADR-012).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: PasswordInputField (7자 제한 · 마스킹/눈토글 · 진동)

**Files:**
- Create: `Assets/_Project/Scripts/UI/PasswordInputField.cs`
- Test: `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (추가)

**Interfaces:**
- Consumes: `TMP_InputField`, `UnityEngine.UI.Image`/`Button`, `Sprite`.
- Produces:
  - `public TMP_InputField Input { get; set; }`
  - `public bool Masked { get; }` — true=★ 마스킹.
  - `public void SetMasked(bool masked)` — `input.contentType`(Standard/Password) 교체 + 라벨 갱신 + 눈 스프라이트 동기.
  - `public void ToggleEye()` — `SetMasked(!Masked)`.
  - `public void ResetField()` — `input.text = ""`.
  - `public void Shake()` — 입력 RectTransform 빠른 진동 1버스트(코루틴), 종료 시 기준 위치 복원.

- [ ] **Step 1: 실패 테스트 작성** — 위 테스트 파일에 메서드 추가

`Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (클래스 내부에 추가):

```csharp
        [UnityTest]
        public IEnumerator PasswordField_Sets_Limit_And_Toggles_Masking()
        {
            var go = new GameObject("PwField");
            go.SetActive(false);
            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(go.transform);
            var input = inputGo.AddComponent<TMP_InputField>();
            var iconGo = new GameObject("Eye");
            iconGo.transform.SetParent(go.transform);
            var icon = iconGo.AddComponent<UnityEngine.UI.Image>();
            var field = go.AddComponent<PasswordInputField>();
            field.Input = input;
            field.EyeIcon = icon;
            var closed = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var open = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            field.EyeClosedSprite = closed;
            field.EyeOpenSprite = open;
            field.MaxLength = 7;
            go.SetActive(true);
            yield return null; // OnEnable → characterLimit 적용

            Assert.AreEqual(7, input.characterLimit, "7자 제한 적용");

            field.SetMasked(true);
            Assert.IsTrue(field.Masked);
            Assert.AreEqual(TMP_InputField.ContentType.Password, input.contentType, "마스킹 시 Password");
            Assert.AreSame(closed, icon.sprite, "마스킹=감은눈");

            field.ToggleEye();
            Assert.IsFalse(field.Masked);
            Assert.AreEqual(TMP_InputField.ContentType.Standard, input.contentType, "노출 시 Standard");
            Assert.AreSame(open, icon.sprite, "노출=뜬눈");

            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator PasswordField_Shake_Restores_Base_Position()
        {
            var go = new GameObject("PwField2");
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(go.transform);
            var input = inputGo.AddComponent<TMP_InputField>();
            var rt = (RectTransform)inputGo.transform;
            rt.anchoredPosition = new Vector2(100f, 50f);
            var field = go.AddComponent<PasswordInputField>();
            field.Input = input;
            field.ShakeDuration = 0.1f;
            yield return null;

            field.Shake();
            float t = 0f;
            while (t < 0.3f) { t += Time.deltaTime; yield return null; }

            Assert.That(rt.anchoredPosition.x, Is.EqualTo(100f).Within(0.01f), "진동 후 X 복원");
            Assert.That(rt.anchoredPosition.y, Is.EqualTo(50f).Within(0.01f), "진동 후 Y 복원");

            Object.DestroyImmediate(go);
        }
```

- [ ] **Step 2: 실패 확인**

Run: `run_tests` mode=PlayMode testFilter=`PasswordField`
Expected: 컴파일 에러 — `PasswordInputField` 없음.

- [ ] **Step 3: 최소 구현**

`Assets/_Project/Scripts/UI/PasswordInputField.cs`:

```csharp
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 잠금화면 비밀번호 입력칸(*View: LockScreen). TMP_InputField를 래핑해 7자 제한,
    /// 모드별 마스킹 기본값(★/평문)과 눈 토글, 오류 시 빠른 진동을 담당한다(스펙 §3).
    /// 마스킹은 <see cref="TMP_InputField.contentType"/> Standard↔Password 전환 + 라벨 강제 갱신.
    /// 진동은 코루틴 1버스트(WarnWidgetShake 관례), 종료 시 기준 위치 복원. 수치는 인스펙터 노출.
    /// </summary>
    public class PasswordInputField : MonoBehaviour
    {
        [SerializeField] TMP_InputField input;
        [Tooltip("눈 아이콘 Image(감은눈/뜬눈 스프라이트 교체).")]
        [SerializeField] Image eyeIcon;
        [Tooltip("눈 토글 버튼. 클릭 시 마스킹 반전. 미바인딩 시 토글 직접 호출만 가능.")]
        [SerializeField] Button eyeButton;
        [SerializeField] Sprite eyeClosedSprite; // 감은눈 = 마스킹(★)
        [SerializeField] Sprite eyeOpenSprite;   // 뜬눈 = 평문 노출
        [SerializeField] int maxLength = 7;

        [Header("Shake")]
        [SerializeField] float shakeAmplitude = 12f;
        [SerializeField] float shakeFrequency = 60f;
        [SerializeField] float shakeDuration = 0.25f;

        public TMP_InputField Input { get => input; set => input = value; }
        public Image EyeIcon { get => eyeIcon; set => eyeIcon = value; }
        public Button EyeButton { get => eyeButton; set => eyeButton = value; }
        public Sprite EyeClosedSprite { get => eyeClosedSprite; set => eyeClosedSprite = value; }
        public Sprite EyeOpenSprite { get => eyeOpenSprite; set => eyeOpenSprite = value; }
        public int MaxLength { get => maxLength; set => maxLength = value; }
        public float ShakeDuration { get => shakeDuration; set => shakeDuration = value; }
        public bool Masked { get; private set; }

        Coroutine _shakeCo;

        void OnEnable()
        {
            if (input != null) input.characterLimit = maxLength;
            if (eyeButton != null) eyeButton.onClick.AddListener(ToggleEye);
        }

        void OnDisable()
        {
            if (eyeButton != null) eyeButton.onClick.RemoveListener(ToggleEye);
        }

        /// <summary>마스킹 on/off — contentType 전환 + 라벨 갱신 + 눈 스프라이트 동기.</summary>
        public void SetMasked(bool masked)
        {
            Masked = masked;
            if (input != null)
            {
                input.contentType = masked ? TMP_InputField.ContentType.Password
                                           : TMP_InputField.ContentType.Standard;
                input.ForceLabelUpdate();
            }
            if (eyeIcon != null)
            {
                var s = masked ? eyeClosedSprite : eyeOpenSprite;
                if (s != null) eyeIcon.sprite = s;
            }
        }

        public void ToggleEye() => SetMasked(!Masked);

        public void ResetField()
        {
            if (input != null) input.text = "";
        }

        /// <summary>오류 시 빠른 진동 1버스트(감쇠). 종료 시 기준 위치 복원.</summary>
        public void Shake()
        {
            if (input == null || !isActiveAndEnabled) return;
            var rt = input.transform as RectTransform;
            if (rt == null) return;
            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeRoutine(rt));
        }

        IEnumerator ShakeRoutine(RectTransform rt)
        {
            Vector2 basePos = rt.anchoredPosition;
            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.deltaTime;
                float env = Mathf.Clamp01(1f - t / shakeDuration);
                float ox = Mathf.Sin(t * shakeFrequency) * shakeAmplitude * env;
                rt.anchoredPosition = basePos + new Vector2(ox, 0f);
                yield return null;
            }
            rt.anchoredPosition = basePos;
            _shakeCo = null;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `run_tests` mode=PlayMode testFilter=`PasswordField`
Expected: 두 테스트 PASS. `read_console` 0 에러.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/UI/PasswordInputField.cs Assets/_Project/Scripts/UI/PasswordInputField.cs.meta Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs
git commit -m "feat(lockscreen): PasswordInputField 7자/마스킹/눈토글/진동

Why: 비밀번호 입력칸의 7자 제한, 모드별 마스킹 기본값과 눈 토글(★↔평문),
오류 시 빠른 진동을 한 컴포넌트로 캡슐화(스펙 §3). 마스킹은 contentType 전환.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: LoginButton (모드별 라벨 + 스프라이트 토글 + 클릭→Confirm)

**Files:**
- Create: `Assets/_Project/Scripts/UI/LoginButton.cs`
- Test: `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (추가)

**Interfaces:**
- Consumes: `LockScreenView.Confirm()`(기존 public), `TMP_InputField`, `Button`, `Image`, `TMP_Text`.
- Produces:
  - `public void SetLabel(string text)` — 버튼 라벨 교체.
  - `public void Refresh()` — `input.text` 유무로 active/deact 스프라이트 + `button.interactable` 동기.
  - 내부: `input.onValueChanged`→`Refresh`, `button.onClick`→`view.Confirm()`.

- [ ] **Step 1: 실패 테스트 작성** — 테스트 파일에 추가

`Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (추가). 상단 `using`에 다음을 보강:

```csharp
using UnityEngine.UI;
using LoveAlgo.Common;
using LoveAlgo.Events;
```

클래스 내부에 추가:

```csharp
        [UnityTest]
        public IEnumerator LoginButton_Toggles_Sprite_And_Confirms()
        {
            // view + overlay + input 배선(Confirm 경로 확인)
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
            yield return null;

            // LoginButton 배선
            var go = new GameObject("LoginBtn");
            go.SetActive(false);
            var btnGo = new GameObject("Btn");
            btnGo.transform.SetParent(go.transform);
            var image = btnGo.AddComponent<Image>();
            var button = btnGo.AddComponent<Button>();
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var lb = go.AddComponent<LoginButton>();
            lb.Input = vInput;
            lb.Button = button;
            lb.Image = image;
            lb.Label = label;
            lb.View = view;
            var act = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var deact = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            lb.ActiveSprite = act;
            lb.DeactiveSprite = deact;
            go.SetActive(true);
            yield return null; // OnEnable → Refresh("")

            Assert.AreSame(deact, image.sprite, "빈 입력=deact");
            Assert.IsFalse(button.interactable, "빈 입력=비활성");

            lb.SetLabel("입력 완료");
            Assert.AreEqual("입력 완료", label.text);

            // 입력 발생
            vInput.text = "1234";
            lb.Refresh();
            Assert.AreSame(act, image.sprite, "입력 있음=active");
            Assert.IsTrue(button.interactable, "입력 있음=활성");

            // Show로 활성 잠금화면 보장 후 클릭 → Submit 발행
            string published = null;
            var sub = EventBus.Subscribe<SubmitPasswordCommand>(e => published = e.Password);
            EventBus.Publish(new ShowLockScreenCommand(LockMode.FirstSetup, false, null, new CompletionHandle()));
            button.onClick.Invoke();
            Assert.AreEqual("1234", published, "클릭 → view.Confirm → Submit 발행");

            sub.Dispose();
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(viewGo);
        }
```

- [ ] **Step 2: 실패 확인**

Run: `run_tests` mode=PlayMode testFilter=`LoginButton_Toggles_Sprite_And_Confirms`
Expected: 컴파일 에러 — `LoginButton` 없음.

- [ ] **Step 3: 최소 구현**

`Assets/_Project/Scripts/UI/LoginButton.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 잠금화면 확정 버튼(*View: LockScreen). 모드별 라벨("입력 완료"/"LOGIN")을 받고,
    /// 입력 유무로 active/deact 스프라이트와 interactable을 토글한다. 클릭 시 <see cref="LockScreenView.Confirm"/>.
    /// ADR-007: 표시 + 명령 위임만(저장은 Controller). 미바인딩 필드는 null-safe.
    /// </summary>
    public class LoginButton : MonoBehaviour
    {
        [SerializeField] TMP_InputField input;
        [SerializeField] Button button;
        [SerializeField] Image image;
        [SerializeField] TMP_Text label;
        [SerializeField] Sprite activeSprite;   // btn_login_active
        [SerializeField] Sprite deactiveSprite; // btn_login_deact
        [SerializeField] LockScreenView view;

        public TMP_InputField Input { get => input; set => input = value; }
        public Button Button { get => button; set => button = value; }
        public Image Image { get => image; set => image = value; }
        public TMP_Text Label { get => label; set => label = value; }
        public Sprite ActiveSprite { get => activeSprite; set => activeSprite = value; }
        public Sprite DeactiveSprite { get => deactiveSprite; set => deactiveSprite = value; }
        public LockScreenView View { get => view; set => view = value; }

        void OnEnable()
        {
            if (input != null) input.onValueChanged.AddListener(OnValueChanged);
            if (button != null) button.onClick.AddListener(OnClick);
            Refresh();
        }

        void OnDisable()
        {
            if (input != null) input.onValueChanged.RemoveListener(OnValueChanged);
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        void OnValueChanged(string _) => Refresh();

        /// <summary>입력 유무로 스프라이트/interactable 동기.</summary>
        public void Refresh()
        {
            bool has = input != null && !string.IsNullOrEmpty(input.text);
            if (image != null)
            {
                var s = has ? activeSprite : deactiveSprite;
                if (s != null) image.sprite = s;
            }
            if (button != null) button.interactable = has;
        }

        void OnClick()
        {
            if (view != null) view.Confirm();
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `run_tests` mode=PlayMode testFilter=`LoginButton_Toggles_Sprite_And_Confirms`
Expected: PASS. `read_console` 0 에러.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/UI/LoginButton.cs Assets/_Project/Scripts/UI/LoginButton.cs.meta Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs
git commit -m "feat(lockscreen): LoginButton 모드별 라벨 + 스프라이트 토글

Why: 확정 버튼이 입력 유무로 active/deact 스프라이트와 활성 상태를 토글하고,
모드별 라벨(입력 완료/LOGIN)을 받으며, 클릭 시 view.Confirm으로 위임(ADR-007).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: LockScreenIntroDirector (진입 연출)

**Files:**
- Create: `Assets/_Project/Scripts/UI/LockScreenIntroDirector.cs`
- Test: `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (추가)

**Interfaces:**
- Produces:
  - `public bool IsPlaying { get; }`
  - `public void ResetToStart()` — 위젯을 기준 위치로, `dim` alpha=0, `inputGroup` alpha=0.
  - `public void Play(System.Action onInputReady)` — 코루틴: hold → 위젯별 슬라이드아웃 → dim 0→target → inputGroup 0→1 → `onInputReady` 호출.
  - 직렬화 `SlideWidget { RectTransform target; Vector2 exitOffset; }`.

- [ ] **Step 1: 실패 테스트 작성** — 테스트 파일에 추가

```csharp
        [UnityTest]
        public IEnumerator IntroDirector_Play_Slides_Fades_And_Calls_Back()
        {
            var go = new GameObject("Intro");
            // dim
            var dimGo = new GameObject("Dim");
            dimGo.transform.SetParent(go.transform);
            var dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 1f); // 시작값이 1이어도 ResetToStart가 0으로 만든다
            // input group
            var inputGo = new GameObject("InputGroup");
            inputGo.transform.SetParent(go.transform);
            var inputGroup = inputGo.AddComponent<CanvasGroup>();
            // widget
            var wGo = new GameObject("Widget", typeof(RectTransform));
            wGo.transform.SetParent(go.transform);
            var wRt = (RectTransform)wGo.transform;
            wRt.anchoredPosition = new Vector2(10f, 0f);

            var intro = go.AddComponent<LockScreenIntroDirector>();
            intro.Dim = dim;
            intro.InputGroup = inputGroup;
            intro.DimTargetAlpha = 0.58f;
            intro.SetTimings(0.02f, 0.05f, 0.05f, 0.05f); // hold, slide, dim, reveal (빠르게)
            intro.SetWidgets(new[] { (wRt, new Vector2(-200f, 0f)) });
            yield return null;

            intro.ResetToStart();
            Assert.AreEqual(0f, dim.color.a, 0.001f, "Reset 후 dim 0");
            Assert.AreEqual(0f, inputGroup.alpha, 0.001f, "Reset 후 입력 0");

            bool called = false;
            intro.Play(() => called = true);

            float guard = 0f;
            while (intro.IsPlaying && guard < 3f) { guard += Time.deltaTime; yield return null; }

            Assert.IsTrue(called, "onInputReady 콜백 도달");
            Assert.AreEqual(0.58f, dim.color.a, 0.02f, "dim 최종 alpha 도달");
            Assert.AreEqual(1f, inputGroup.alpha, 0.02f, "입력 그룹 노출");
            Assert.That(wRt.anchoredPosition.x, Is.EqualTo(-190f).Within(1f), "위젯 슬라이드아웃(10 + -200)");

            Object.DestroyImmediate(go);
        }
```

- [ ] **Step 2: 실패 확인**

Run: `run_tests` mode=PlayMode testFilter=`IntroDirector_Play_Slides_Fades_And_Calls_Back`
Expected: 컴파일 에러 — `LockScreenIntroDirector` 없음.

- [ ] **Step 3: 최소 구현**

`Assets/_Project/Scripts/UI/LockScreenIntroDirector.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 잠금화면 진입 연출 오케스트레이터(*View: LockScreen). 영상(03_login) 플로우대로
    /// ① 위젯 hold → ② 위젯별 가까운 화면 밖으로 슬라이드아웃(ease-in) → ③ Dim 0→target 페이드
    /// → ④ 입력/버튼 그룹 0→1 reveal → ⑤ onInputReady 콜백. 코루틴 lerp(DOTween 미사용).
    /// LockScreenView가 위임하며, 미바인딩 시 view가 즉시 경로로 폴백한다. 수치는 인스펙터 노출(ADR-012).
    /// </summary>
    public class LockScreenIntroDirector : MonoBehaviour
    {
        [Serializable]
        public struct SlideWidget
        {
            public RectTransform target;
            [Tooltip("기준 위치에서 화면 밖으로의 이동량(px). 가까운 가장자리 방향.")]
            public Vector2 exitOffset;
        }

        [SerializeField] List<SlideWidget> widgets = new();
        [Tooltip("딤 오버레이 Image(검은 반투명). alpha 0→target.")]
        [SerializeField] Image dim;
        [Tooltip("입력+버튼+가이드 묶음 CanvasGroup. alpha 0→1.")]
        [SerializeField] CanvasGroup inputGroup;

        [Header("Timing")]
        [SerializeField] float introHold = 0.6f;
        [SerializeField] float slideDuration = 0.35f;
        [SerializeField] float dimFade = 0.3f;
        [SerializeField] float inputReveal = 0.25f;
        [SerializeField] float dimTargetAlpha = 0.58f;
        [Tooltip("슬라이드아웃 이징(가속해서 빠져나감).")]
        [SerializeField] AnimationCurve slideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public Image Dim { get => dim; set => dim = value; }
        public CanvasGroup InputGroup { get => inputGroup; set => inputGroup = value; }
        public float DimTargetAlpha { get => dimTargetAlpha; set => dimTargetAlpha = value; }
        public bool IsPlaying { get; private set; }

        readonly List<Vector2> _basePos = new();
        Coroutine _co;

        /// <summary>테스트/부팅용 타이밍 일괄 주입.</summary>
        public void SetTimings(float hold, float slide, float dimF, float reveal)
        {
            introHold = hold; slideDuration = slide; dimFade = dimF; inputReveal = reveal;
        }

        /// <summary>테스트/부팅용 위젯 리스트 주입.</summary>
        public void SetWidgets(IEnumerable<(RectTransform rt, Vector2 exit)> items)
        {
            widgets.Clear();
            foreach (var (rt, exit) in items)
                widgets.Add(new SlideWidget { target = rt, exitOffset = exit });
        }

        void CacheBase()
        {
            _basePos.Clear();
            for (int i = 0; i < widgets.Count; i++)
                _basePos.Add(widgets[i].target != null ? widgets[i].target.anchoredPosition : Vector2.zero);
        }

        /// <summary>시작 상태로 복원 — 위젯 기준 위치, dim 0, 입력 그룹 0. Play 전 호출.</summary>
        public void ResetToStart()
        {
            if (_basePos.Count != widgets.Count) CacheBase();
            for (int i = 0; i < widgets.Count; i++)
                if (widgets[i].target != null) widgets[i].target.anchoredPosition = _basePos[i];
            if (dim != null) { var c = dim.color; c.a = 0f; dim.color = c; }
            if (inputGroup != null) inputGroup.alpha = 0f;
        }

        public void Play(Action onInputReady)
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Run(onInputReady));
        }

        IEnumerator Run(Action onInputReady)
        {
            IsPlaying = true;
            CacheBase();

            if (introHold > 0f) yield return new WaitForSeconds(introHold);

            // ② 위젯 슬라이드아웃(동시)
            float t = 0f;
            while (t < slideDuration)
            {
                t += Time.deltaTime;
                float k = slideDuration > 0f ? slideEase.Evaluate(Mathf.Clamp01(t / slideDuration)) : 1f;
                for (int i = 0; i < widgets.Count; i++)
                    if (widgets[i].target != null)
                        widgets[i].target.anchoredPosition = _basePos[i] + widgets[i].exitOffset * k;
                yield return null;
            }
            for (int i = 0; i < widgets.Count; i++)
                if (widgets[i].target != null)
                    widgets[i].target.anchoredPosition = _basePos[i] + widgets[i].exitOffset;

            // ③ Dim 페이드
            yield return Fade(a => { if (dim != null) { var c = dim.color; c.a = a; dim.color = c; } },
                              0f, dimTargetAlpha, dimFade);

            // ④ 입력 그룹 reveal
            yield return Fade(a => { if (inputGroup != null) inputGroup.alpha = a; },
                              0f, 1f, inputReveal);

            IsPlaying = false;
            _co = null;
            onInputReady?.Invoke();
        }

        IEnumerator Fade(Action<float> set, float from, float to, float dur)
        {
            if (dur <= 0f) { set(to); yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                set(Mathf.Lerp(from, to, Mathf.Clamp01(t / dur)));
                yield return null;
            }
            set(to);
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `run_tests` mode=PlayMode testFilter=`IntroDirector_Play_Slides_Fades_And_Calls_Back`
Expected: PASS. `read_console` 0 에러.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/UI/LockScreenIntroDirector.cs Assets/_Project/Scripts/UI/LockScreenIntroDirector.cs.meta Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs
git commit -m "feat(lockscreen): 진입 연출 LockScreenIntroDirector

Why: 영상 플로우(위젯 슬라이드아웃 → 딤 페이드 → 입력 reveal)를 코루틴 타임라인으로
구현. view가 위임하고 미바인딩 시 폴백(기존 테스트 보존). 수치 인스펙터 노출(ADR-012).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: LockScreenView 확장 (모드별 구성 + 연출 위임 + "설정 완료!")

**Files:**
- Modify: `Assets/_Project/Scripts/UI/LockScreenView.cs`
- Test: `Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (추가), `Assets/Tests/PlayMode/LockScreenPlayModeTests.cs`(무변·회귀 확인)

**Interfaces:**
- Consumes: `LockScreenIntroDirector.Play/ResetToStart`, `PasswordInputField.SetMasked/ResetField`, `LockScreenGuideText.SetState`, `LoginButton.SetLabel/Refresh`, `ShowLockScreenCommand.Mode`(기존), `LockMode`.
- Produces (LockScreenView 신규 public/직렬화):
  - 직렬화: `intro`, `passwordField`, `guide`, `loginButton`, `setupButtonLabel="입력 완료"`, `normalButtonLabel="LOGIN"`.
  - 동작 변경: `OnShow`가 `ConfigureForMode(e.Mode)` 호출 + intro 바인딩 시 위임(미바인딩 시 기존 즉시 경로). `Confirm`이 FirstSetup/Reset에서 Hide 전 `guide.SetState(SetupComplete)`.
  - 기존 public API(`Overlay/FadeGroup/Input` 프로퍼티, `OnShow`, `Confirm`)는 시그니처 불변.

- [ ] **Step 1: 회귀 가드 — 기존 테스트 먼저 실행(그린 확인)**

Run: `run_tests` mode=PlayMode testFilter=`LockScreenPlayModeTests`
Expected: 기존 5종 PASS (수정 전 베이스라인).

- [ ] **Step 2: 실패 테스트 작성** — 모드별 구성 검증을 테스트 파일에 추가

`Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs` (추가):

```csharp
        [UnityTest]
        public IEnumerator View_FirstSetup_Configures_Plaintext_And_SetupGuide()
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

            // 하위 위젯
            var pf = viewGo.AddComponent<PasswordInputField>();
            pf.Input = vInput;
            view.PasswordField = pf;
            var guideGo = new GameObject("Guide");
            guideGo.transform.SetParent(viewGo.transform);
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(guideGo.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var guide = guideGo.AddComponent<LockScreenGuideText>();
            guide.Label = label;
            guide.SetupText = "설정문구";
            guide.NormalText = "입력문구";
            view.Guide = guide;
            viewGo.SetActive(true);
            yield return null;

            EventBus.Publish(new ShowLockScreenCommand(LockMode.FirstSetup, false, null, new CompletionHandle()));
            Assert.IsFalse(pf.Masked, "FirstSetup=평문(마스킹 off)");
            Assert.AreEqual("설정문구", label.text, "FirstSetup=설정 가이드");

            // 제출 시 '설정 완료!'로 전환
            guide.SetupCompleteText = "완료문구";
            vInput.text = "1234";
            view.Confirm();
            Assert.AreEqual("완료문구", label.text, "제출 후 설정 완료 텍스트");

            Object.DestroyImmediate(viewGo);
        }
```

- [ ] **Step 3: 실패 확인**

Run: `run_tests` mode=PlayMode testFilter=`View_FirstSetup_Configures_Plaintext_And_SetupGuide`
Expected: 컴파일 에러 — `LockScreenView.PasswordField/Guide` 프로퍼티 없음.

- [ ] **Step 4: LockScreenView 수정**

`Assets/_Project/Scripts/UI/LockScreenView.cs` — 필드 추가(기존 `fadeInDuration` 필드 선언 **다음 줄**에 삽입):

```csharp
        [Header("Custom System (선택 — 미바인딩 시 기존 즉시 경로 폴백)")]
        [Tooltip("진입 연출 오케스트레이터. 바인딩 시 위젯 슬라이드아웃→딤→입력 reveal 후 입력 활성.")]
        [SerializeField] LockScreenIntroDirector intro;
        [Tooltip("입력칸 래퍼(마스킹/눈토글/7자/진동).")]
        [SerializeField] PasswordInputField passwordField;
        [Tooltip("입력칸 위 안내 텍스트(상태별).")]
        [SerializeField] LockScreenGuideText guide;
        [Tooltip("확정 버튼(모드별 라벨).")]
        [SerializeField] LoginButton loginButton;
        [SerializeField] string setupButtonLabel = "입력 완료";
        [SerializeField] string normalButtonLabel = "LOGIN";

        public LockScreenIntroDirector Intro { get => intro; set => intro = value; }
        public PasswordInputField PasswordField { get => passwordField; set => passwordField = value; }
        public LockScreenGuideText Guide { get => guide; set => guide = value; }
        public LoginButton LoginButton { get => loginButton; set => loginButton = value; }
```

기존 필드 `bool _fadeOut;` 아래에 모드 보관 필드 추가:

```csharp
        LockMode _mode;
```

`OnShow` 메서드를 아래로 교체(overlay 동기 활성은 유지, 입력 활성 시점만 분기):

```csharp
        /// <summary>잠금화면 표시 — 오버레이 켜고 모드별 구성. intro 바인딩 시 연출 후 입력 활성, 아니면 즉시.</summary>
        public void OnShow(ShowLockScreenCommand e)
        {
            _fadeOut = e.FadeOut;
            _mode = e.Mode;
            if (overlay == null) return; // 효과 생략 — Controller가 Submit으로 핸들 완료(여기선 막지 않음).
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            overlay.SetActive(true);
            if (input != null) input.text = "";
            if (passwordField != null) passwordField.ResetField();

            ConfigureForMode(_mode);

            if (intro != null)
            {
                // 연출 경로: 즉시 전체 표시(스토리 위 위젯 present) 후 staged 연출.
                if (fadeGroup != null) fadeGroup.alpha = 1f;
                intro.ResetToStart();
                if (isActiveAndEnabled) intro.Play(ActivateInput);
                else { ActivateInput(); }
            }
            else
            {
                // 폴백(기존 동작): 입력 즉시 활성 + 시작 크로스페이드.
                ActivateInput();
                if (fadeGroup != null && isActiveAndEnabled && fadeInDuration > 0f)
                    _fadeRoutine = StartCoroutine(FadeInAndShow());
                else if (fadeGroup != null)
                    fadeGroup.alpha = 1f;
            }
        }

        /// <summary>모드별 위젯 구성 — 마스킹 기본값/버튼 라벨/가이드 상태. 미바인딩 필드는 건너뜀.</summary>
        void ConfigureForMode(LockMode mode)
        {
            bool normal = mode == LockMode.Normal;
            if (passwordField != null) passwordField.SetMasked(normal);
            if (loginButton != null) { loginButton.SetLabel(normal ? normalButtonLabel : setupButtonLabel); loginButton.Refresh(); }
            if (guide != null) guide.SetState(normal ? LockScreenGuideText.LockGuideState.Normal
                                                     : LockScreenGuideText.LockGuideState.Setup);
        }

        /// <summary>입력 활성·포커스. 연출 종료 콜백 또는 폴백 즉시 호출.</summary>
        void ActivateInput()
        {
            if (input != null) input.ActivateInputField();
        }
```

`Confirm` 메서드에서 `EventBus.Publish(new SubmitPasswordCommand(pwd));` **직전**에 설정 완료 전환 삽입:

```csharp
            // FirstSetup/Reset 제출 시 '설정 완료!' 안내로 전환(닫힘 페이드 동안 노출).
            if (guide != null && _mode != LockMode.Normal)
                guide.SetState(LockScreenGuideText.LockGuideState.SetupComplete);
            EventBus.Publish(new SubmitPasswordCommand(pwd)); // 저장은 Controller(ADR-007).
            Hide();
```

> 주의: 기존 `OnShow`의 `FadeInAndShow` 코루틴/`_fadeRoutine`/`HideImmediate` 등은 그대로 둔다. 위 교체는 `OnShow` 본문과 `Confirm` 내부 2줄만 바꾼다.

- [ ] **Step 5: 신규 테스트 통과 확인**

Run: `run_tests` mode=PlayMode testFilter=`View_FirstSetup_Configures_Plaintext_And_SetupGuide`
Expected: PASS. `read_console` 0 에러.

- [ ] **Step 6: 기존 테스트 회귀 확인(핵심)**

Run: `run_tests` mode=PlayMode testFilter=`LockScreenPlayModeTests`
Expected: 기존 5종 모두 PASS (intro/guide 미바인딩 폴백으로 동기 overlay·Confirm·통합 hang0 보존).

- [ ] **Step 7: 커밋**

```bash
git add Assets/_Project/Scripts/UI/LockScreenView.cs Assets/Tests/PlayMode/LockScreenIntroPlayModeTests.cs
git commit -m "feat(lockscreen): View 모드별 구성 + 진입 연출 위임 + 설정완료 전환

Why: OnShow가 모드(Setup/Normal)에 따라 마스킹 기본값/버튼 라벨/가이드를 구성하고,
intro 바인딩 시 연출 후 입력을 활성화(미바인딩 폴백으로 기존 테스트 보존). 제출 시
'비밀번호 설정 완료!'로 전환(스펙 §3,§5).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: LockScreen.prefab 재구성 (위젯 배치 + 컴포넌트 배선)

> **성격:** Unity 에디터/MCP 작업(코드 아님). 프리팹 YAML 직접 편집 대신 Unity MCP(`manage_gameobject`/`manage_ui`/`manage_components`/`manage_prefabs`)로 구성하고 에디터에서 시각 검증. 단위 테스트 없음 — **플레이 모드 관찰 + console 0 에러**가 검증.

**Files:**
- Modify: `Assets/_Project/Prefabs/LockScreen.prefab`
- Import 확인: `Assets/Art/UI/Screen/LockScreen/*.png`

**현재 프리팹 구조(기준):**
```
LockScreen (Image a0, raycast off) + LockScreenView
└─ LockOverlay (Image black a1, CanvasGroup)   ← overlay & fadeGroup
   ├─ Background (Image: _bg)
   ├─ Dim (Image black a0.576)
   └─ PasswordInput (Image: password_box) + TMP_InputField
      └─ PasswordText (TMP)
```

- [ ] **Step 1: 아트 임포트 확인**

Unity MCP `manage_asset`(또는 Project 뷰)로 다음 PNG가 **Sprite (2D and UI)** 로 임포트됐는지 확인. 아니면 TextureImporter `textureType=Sprite`로 변경 후 재임포트:
`btn_login_active.png`, `btn_login_deact.png`, `password_view.png`(뜬눈), `password_hidden.png`(감은눈), `password_key.png`(열쇠), `warn.png`, `audio.png`, `todo.png`, `todo checkbox.png`, `message_box.png`, `header.png`.

확인: `read_console` 0 에러, 각 에셋 type=Sprite.

- [ ] **Step 2: 위젯 레이어 추가 (슬라이드아웃 대상 + 잔존 요소)**

`LockOverlay` 자식으로, `Background` **위**(렌더 순서상 배경 다음)에 위젯들을 배치. 영상 레이아웃 참고:
- 잔존(슬라이드 제외): `Clock`(TMP_Text "23:58", 상단 중앙, 큰 폰트), `Background`.
- 슬라이드아웃 대상(각 Image + 해당 스프라이트):
  - `Widget_Warn` (warn.png, 좌상단)
  - `Widget_Audio` (audio.png, 좌측)
  - `Widget_Todo` (todo.png + todo checkbox.png 자식, 좌하단)
  - `Widget_Message` (message_box.png + header.png 헤더, 하단 중앙)

> `header.png` 정체(상단 바 vs 메시지 헤더)는 이미지 확인 후 결정: 상단 바면 `Clock` 옆 잔존, 메시지 헤더면 `Widget_Message` 자식. (스펙 §9)

확인: Scene/Game 뷰에서 위젯이 영상과 유사 배치, `read_console` 0 에러.

- [ ] **Step 3: 입력 그룹 묶기 (`InputGroup` CanvasGroup)**

`LockOverlay` 자식으로 `InputGroup`(빈 GameObject + `CanvasGroup`) 생성 후, 그 아래로:
- 기존 `PasswordInput`(+`PasswordText`) 이동.
- `EyeButton`(Button + Image, 스프라이트 `password_hidden`) — PasswordInput 우측 끝 자식.
- `GuideLabel`(TMP_Text) — PasswordInput **위쪽**.
- `LoginBtn`(Button + Image `btn_login_deact` + 자식 TMP_Text label "입력 완료") — PasswordInput **아래쪽**.

`InputGroup.alpha`는 런타임에 intro가 0→1 제어(초기값 무관, ResetToStart가 0으로).

확인: 계층 구조가 위 트리와 일치, `read_console` 0 에러.

- [ ] **Step 4: 신규 컴포넌트 부착 + 배선**

`LockScreen`(루트) 또는 `LockOverlay`에 컴포넌트 추가하고 인스펙터 참조 연결:
- `PasswordInputField` (LockScreen 루트): `input`=PasswordInput의 TMP_InputField, `eyeIcon`=EyeButton의 Image, `eyeButton`=EyeButton, `eyeClosedSprite`=password_hidden, `eyeOpenSprite`=password_view, `maxLength`=7.
- `LockScreenGuideText` (LockScreen 루트): `label`=GuideLabel.
- `LoginButton` (LockScreen 루트): `input`=PasswordInput, `button`=LoginBtn, `image`=LoginBtn Image, `label`=LoginBtn 라벨, `activeSprite`=btn_login_active, `deactiveSprite`=btn_login_deact, `view`=LockScreen의 LockScreenView.
- `LockScreenIntroDirector` (LockOverlay): `dim`=Dim Image, `inputGroup`=InputGroup CanvasGroup, `widgets`=[{Widget_Warn, (-화면밖X,0)}, {Widget_Audio, (-X,0)}, {Widget_Todo, (-X,0)}, {Widget_Message, (0,-Y)}] (각 exitOffset은 위젯 위치 기준 가까운 가장자리 밖). 타이밍 기본값 유지(영상 ~1.8s 기반).
- `LockScreenView`(기존): `intro`=IntroDirector, `passwordField`=PasswordInputField, `guide`=GuideText, `loginButton`=LoginButton 연결. `overlay`/`fadeGroup`/`input`은 기존 유지.
- `Dim` Image의 시작 alpha는 무관(ResetToStart가 0으로). 단 인스펙터에서 0으로 둬 에디터 표시 혼동 방지 권장.

확인: 모든 참조 비어있지 않음(누락 시 인스펙터 경고), `read_console` 0 에러.

- [ ] **Step 5: 플레이 모드 시각 검증**

`Game.unity`(또는 LockScreen이 스폰되는 경로)에서 FirstSetup 잠금화면을 띄워(또는 임시 테스트 스폰) 다음을 관찰:
1. 진입 시 위젯+시계+배경이 보이고, hold 후 위젯들이 가까운 화면 밖으로 슬라이드아웃.
2. Dim이 페이드되며 어두워지고, 입력칸+가이드("앞으로 사용할…")+"입력 완료" 버튼이 reveal.
3. 입력 시 평문 표시(FirstSetup), 눈 클릭 시 감은눈→★ 마스킹 토글.
4. "입력 완료"/엔터 → 가이드 "비밀번호 설정 완료!"로 바뀌고 닫힘(FadeOut).
5. `read_console` 런타임 에러 0.

> 검증용 스폰이 마땅치 않으면, 임시로 빈 GameObject에서 `EventBus.Publish(new ShowLockScreenCommand(LockMode.FirstSetup, true, null, new CompletionHandle()))`를 호출하는 1회성 디버그 훅으로 확인 후 제거.

- [ ] **Step 6: 커밋**

```bash
git add Assets/_Project/Prefabs/LockScreen.prefab "Assets/Art/UI/Screen/LockScreen"
git commit -m "feat(lockscreen): 프리팹 위젯 배치 + 입력 시스템 컴포넌트 배선

Why: 영상(03_login) 진입 연출을 위한 위젯(시계/WARNING/오디오/TODO/ROA메시지) 배치와
입력 그룹(눈토글/가이드/입력완료 버튼) 구성, 신규 4컴포넌트 배선. FirstSetup 해피패스
플레이 검증 완료.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review (작성자 점검 결과)

**1. 스펙 커버리지(S1 범위):**
- 진입 연출(위젯→슬라이드→딤→입력) → Task 4 + Task 6. ✓
- 안내 텍스트(설정/설정완료) → Task 1 + Task 5. ✓
- 눈 토글(감은눈/뜬눈) → Task 2 + Task 6. ✓
- 7자 제한, FirstSetup 평문 → Task 2(`characterLimit`/`SetMasked(false)`), Task 5(`ConfigureForMode`). ✓
- "입력 완료" 버튼 + active/deact 토글 → Task 3 + Task 6. ✓
- "비밀번호 설정 완료!" 전환 → Task 5(`Confirm`). ✓
- 기존 테스트 보존 → Task 5 Step 1/6 회귀 가드. ✓
- S2(Normal 검증/진동 실사용)·S3(열쇠/재설정)는 본 계획 비범위(설계 §6). `PasswordInputField.Shake`는 S1에서 구현하되 호출은 S2에서 배선.

**2. 플레이스홀더 스캔:** "적절한 처리/검증 추가" 류 없음. 모든 코드 단계에 실제 코드 포함. 프리팹 Task만 에디터 작업 성격이라 코드 대신 구체적 배선 지시 + 검증 기준 명시. ✓

**3. 타입 일관성:** `SetMasked(bool)`/`ToggleEye()`/`ResetField()`/`Shake()`(Task2) ↔ Task5 `ConfigureForMode` 호출 일치. `SetState(LockGuideState)`(Task1) ↔ Task5 호출 일치. `LoginButton.SetLabel/Refresh`(Task3) ↔ Task5 일치. `Intro.Play(Action)`/`ResetToStart()`/`IsPlaying`(Task4) ↔ Task5 일치. ✓

**미해결(빌드 시 확정, 설계 §9):** `header.png` 정체, IntroDirector exitOffset/타이밍 실측값 — Task 6에서 이미지/플레이 확인 후 확정.
