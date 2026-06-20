# 사용자 입력 검증 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** username 입력에 NameValidator(금칙어·완성형만·길이)를 배선해 무효 입력을 거부·표시·흔들고, 비밀번호 입력은 두벌식 QWERTY 역매핑으로 한글을 실시간 영문화하며 표준 ASCII 문자셋만 허용한다.

**Architecture:** 순수 `HangulQwerty.ToQwerty`(한글→QWERTY 역매핑 + ASCII 통과 + strip)를 핵심으로, `PasswordInputField`가 onValueChanged에서 실시간 적용. `UsernameScreenView.Submit`이 기존 `NameValidator`를 호출해 거부 시 에러 라벨+흔들림. 흔들림은 공용 `UiNudge`로 추출.

**Tech Stack:** Unity 6 LTS, C#, TextMeshPro(`TMP_InputField`), NUnit + Unity Test Framework, `LoveAlgo.Core`(NameValidator, GameStateSO), `LoveAlgo.UI`.

## Global Constraints

- `NameValidator` 규칙·`BannedWords.txt`·LockScreen 정답/저장 로직은 **변경 금지**(입력 텍스트만 영문화/검증).
- 허용 password 문자: 영문 대소문자, 숫자, 표준 ASCII 특수문자 = 코드포인트 `0x21–0x7E`(공백 0x20 제외). 그 외(한글 변환 후 잔여·제어·이모지)는 제거.
- 두벌식 매핑은 §Task1 테이블 그대로(대표값: `가`→`rk`, `안`→`dks`, `값`→`rkqt`, `ㅂㅈㄷ`→`qwe`).
- Obsolete API 금지(Unity 6). 로깅 `Log.Info/Warn`, 사용자 에러만 `Debug.LogError`.
- 한 기능 = 한 커밋(Atomic). 커밋 본문에 "왜". 끝에 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- 신규 .cs는 Unity 임포트 위해 `refresh_unity(scope=all)` 필요(아니면 run_tests가 total:0).

---

### Task 1: HangulQwerty 두벌식 역매핑 순수 로직 (EditMode TDD)

한글(완성형·호환자모)을 두벌식 QWERTY 키로 역매핑하고 ASCII는 통과, 그 외는 제거하는 정적 함수.

**Files:**
- Create: `Assets/_Project/Scripts/Core/HangulQwerty.cs`
- Test: `Assets/Tests/EditMode/HangulQwertyTests.cs`

**Interfaces:**
- Produces: `static string LoveAlgo.Core.HangulQwerty.ToQwerty(string text)` — 입력의 각 문자를 변환/통과/제거한 문자열. null→"".

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/HangulQwertyTests.cs`:

```csharp
using NUnit.Framework;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 한글→두벌식 QWERTY 역매핑(GameObject 불필요). 완성형 음절 분해·호환자모·복합자모·ASCII 통과·strip.
    /// </summary>
    public class HangulQwertyTests
    {
        [Test] public void Syllable_Basic()
        {
            Assert.AreEqual("rk", HangulQwerty.ToQwerty("가"));   // ㄱ+ㅏ
            Assert.AreEqual("dks", HangulQwerty.ToQwerty("안"));  // ㅇ+ㅏ+ㄴ
            Assert.AreEqual("dkssud", HangulQwerty.ToQwerty("안녕")); // 안 + ㄴ+ㅕ+ㅇ
        }

        [Test] public void Syllable_CompoundJong_AndVowel()
        {
            Assert.AreEqual("rkqt", HangulQwerty.ToQwerty("값")); // ㄱ+ㅏ+ㅄ
            Assert.AreEqual("dhk", HangulQwerty.ToQwerty("와"));  // ㅇ+ㅘ
        }

        [Test] public void CompatibilityJamo()
        {
            Assert.AreEqual("qwe", HangulQwerty.ToQwerty("ㅂㅈㄷ"));
            Assert.AreEqual("r", HangulQwerty.ToQwerty("ㄱ"));
        }

        [Test] public void AsciiPassthrough()
        {
            Assert.AreEqual("Pass1!@#", HangulQwerty.ToQwerty("Pass1!@#")); // 영/숫/특수 통과
            Assert.AreEqual("aZ09~`", HangulQwerty.ToQwerty("aZ09~`"));     // 경계 ASCII 통과
        }

        [Test] public void Strip_SpaceAndNonMappable()
        {
            Assert.AreEqual("abc", HangulQwerty.ToQwerty("a b c")); // 공백 제거
            Assert.AreEqual("hi", HangulQwerty.ToQwerty("hi😀"));   // 이모지(서로게이트) 제거
        }

        [Test] public void Mixed_KoreanAscii()
        {
            // 비밀번호 의도: 한글 IME로 친 "ㅔ뮤소" 같은 잔여 + ASCII
            Assert.AreEqual("rkPass1", HangulQwerty.ToQwerty("가Pass1"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

`refresh_unity(scope=all)` → `run_tests` EditMode `HangulQwertyTests`. Expected: 컴파일 에러(`HangulQwerty` 미정의).

- [ ] **Step 3: Write minimal implementation**

`Assets/_Project/Scripts/Core/HangulQwerty.cs`:

```csharp
using System.Text;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 한글 입력을 두벌식 키보드 기준 QWERTY 키로 역매핑한다(예: 가→rk, ㅂㅈㄷ→qwe).
    /// 완성형 음절(가-힣)은 초/중/종성으로 분해해 각 자모를 매핑하고, 호환 자모(ㄱ-ㅣ)는 직접 매핑한다.
    /// 영문/숫자/표준 ASCII 특수문자(0x21–0x7E)는 그대로 통과, 그 외(공백·제어·기타 유니코드)는 제거.
    /// 비밀번호 입력의 한글→영문 통일·문자셋 제한에 사용(순수 — EditMode 테스트 대상).
    /// </summary>
    public static class HangulQwerty
    {
        // 초성 19
        static readonly string[] Cho = { "r","R","s","e","E","f","a","q","Q","t","T","d","w","W","c","z","x","v","g" };
        // 중성 21
        static readonly string[] Jung = { "k","o","i","O","j","p","u","P","h","hk","ho","hl","y","n","nj","np","nl","b","m","ml","l" };
        // 종성 28 (index 0 = 받침 없음)
        static readonly string[] Jong = { "","r","R","rt","s","sw","sg","e","f","fr","fa","fq","ft","fx","fv","fg","a","q","qt","t","T","d","w","c","z","x","v","g" };
        // 호환 자모 U+3131–U+3163 (ㄱ..ㅣ) 51개
        static readonly string[] Compat = {
            "r","R","rt","s","sw","sg","e","E","f","fr","fa","fq","ft","fx","fv","fg","a","q","Q","qt","t","T","d","w","W","c","z","x","v","g",
            "k","o","i","O","j","p","u","P","h","hk","ho","hl","y","n","nj","np","nl","b","m","ml","l"
        };

        public static string ToQwerty(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var sb = new StringBuilder(text.Length * 2);
            foreach (char c in text)
            {
                if (c >= 0xAC00 && c <= 0xD7A3) // 완성형 음절
                {
                    int s = c - 0xAC00;
                    sb.Append(Cho[s / (21 * 28)]);
                    sb.Append(Jung[(s % (21 * 28)) / 28]);
                    sb.Append(Jong[s % 28]);
                }
                else if (c >= 0x3131 && c <= 0x3163) // 호환 자모
                {
                    sb.Append(Compat[c - 0x3131]);
                }
                else if (c >= 0x21 && c <= 0x7E) // 영문/숫자/표준 ASCII 특수문자
                {
                    sb.Append(c);
                }
                // else: 제거(공백·제어·기타)
            }
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

`refresh_unity(scope=all)` → `run_tests` EditMode `HangulQwertyTests`. Expected: 6 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Core/HangulQwerty.cs Assets/_Project/Scripts/Core/HangulQwerty.cs.meta Assets/Tests/EditMode/HangulQwertyTests.cs Assets/Tests/EditMode/HangulQwertyTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(core): HangulQwerty 두벌식 QWERTY 역매핑 순수 로직

왜: 비밀번호 입력에서 한글 IME로 쳐도 의도한 영문이 나오도록(가→rk, ㅂㅈㄷ→qwe)
완성형 음절 분해 + 호환자모 + ASCII 통과 + 비허용 strip을 순수 함수로 두고 EditMode로 고정.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: UiNudge 공용 흔들림 + PasswordInputField.Shake 정리 (PlayMode)

오류 피드백 흔들림을 공용 정적 `UiNudge`로 추출하고, `PasswordInputField.Shake`가 이를 쓰도록 정리(동작·수치 유지). host가 코루틴 핸들을 보유해 중복 호출 시 이전 흔들림을 멈춰 기준 위치 드리프트를 막는다.

**Files:**
- Create: `Assets/_Project/Scripts/UI/UiNudge.cs`
- Modify: `Assets/_Project/Scripts/UI/PasswordInputField.cs`
- Test: `Assets/Tests/PlayMode/UiNudgePlayModeTests.cs`

**Interfaces:**
- Produces: `static void LoveAlgo.UI.UiNudge.Shake(MonoBehaviour host, RectTransform rt, ref Coroutine handle, float amplitude=12f, float frequency=60f, float duration=0.25f)` — 이전 handle 중단 후 새 흔들림 시작, handle 갱신.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/PlayMode/UiNudgePlayModeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>UiNudge.Shake: rt를 잠시 흔든 뒤 기준 위치로 복원하는지(host 코루틴 구동).</summary>
    public class UiNudgePlayModeTests
    {
        class Host : MonoBehaviour { }

        [UnityTest]
        public IEnumerator Shake_Perturbs_ThenRestores()
        {
            var hostGo = new GameObject("Host"); var host = hostGo.AddComponent<Host>();
            var rtGo = new GameObject("W", typeof(RectTransform));
            var rt = rtGo.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(100f, 50f);
            var basePos = rt.anchoredPosition;
            Coroutine co = null;
            try
            {
                UiNudge.Shake(host, rt, ref co, 12f, 60f, 0.2f);
                yield return null; yield return null; // 흔들림 중
                Assert.AreNotEqual(basePos.x, rt.anchoredPosition.x, "흔들림 중엔 위치가 변함");
                yield return new WaitForSeconds(0.3f); // 종료 대기
                Assert.AreEqual(basePos, rt.anchoredPosition, "종료 후 기준 위치 복원");
            }
            finally { Object.DestroyImmediate(hostGo); Object.DestroyImmediate(rtGo); }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

`run_tests` PlayMode `UiNudgePlayModeTests`. Expected: 컴파일 에러(`UiNudge` 미정의). (신규 파일 → `refresh_unity(scope=all)` 먼저.)

- [ ] **Step 3: Write UiNudge + refactor PasswordInputField**

`Assets/_Project/Scripts/UI/UiNudge.cs`:

```csharp
using System.Collections;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 위젯 오류 피드백 흔들림(공용). host가 코루틴을 구동하고, rt를 수평 1버스트 감쇠 진동 후 기준 위치로 복원한다.
    /// 중복 호출 시 호출 측이 <paramref name="handle"/>로 이전 흔들림을 멈춰 기준 위치 드리프트를 막는다.
    /// </summary>
    public static class UiNudge
    {
        public static void Shake(MonoBehaviour host, RectTransform rt, ref Coroutine handle,
            float amplitude = 12f, float frequency = 60f, float duration = 0.25f)
        {
            if (host == null || rt == null || !host.isActiveAndEnabled) return;
            if (handle != null) host.StopCoroutine(handle);
            handle = host.StartCoroutine(ShakeRoutine(rt, amplitude, frequency, duration));
        }

        static IEnumerator ShakeRoutine(RectTransform rt, float amplitude, float frequency, float duration)
        {
            Vector2 basePos = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float env = Mathf.Clamp01(1f - t / duration);
                float ox = Mathf.Sin(t * frequency) * amplitude * env;
                rt.anchoredPosition = basePos + new Vector2(ox, 0f);
                yield return null;
            }
            rt.anchoredPosition = basePos;
        }
    }
}
```

`PasswordInputField.cs` — `Shake()` 본문과 `ShakeRoutine` 제거 후 UiNudge 호출로 교체(필드 `_shakeCo` 유지):

```csharp
        /// <summary>오류 시 빠른 진동 1버스트(감쇠). 종료 시 기준 위치 복원.</summary>
        public void Shake()
        {
            var rt = input != null ? input.transform as RectTransform : null;
            if (rt == null) return;
            UiNudge.Shake(this, rt, ref _shakeCo, shakeAmplitude, shakeFrequency, shakeDuration);
        }
```

(기존 `IEnumerator ShakeRoutine(...)` 메서드는 삭제. `Coroutine _shakeCo;` 필드는 유지.)

- [ ] **Step 4: Run tests to verify they pass**

`run_tests` PlayMode `UiNudgePlayModeTests` + 기존 `LockScreenPlayModeTests`(PasswordInputField.Shake 회귀). Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/UiNudge.cs Assets/_Project/Scripts/UI/UiNudge.cs.meta Assets/_Project/Scripts/UI/PasswordInputField.cs Assets/Tests/PlayMode/UiNudgePlayModeTests.cs Assets/Tests/PlayMode/UiNudgePlayModeTests.cs.meta
git commit -m "$(cat <<'EOF'
refactor(ui): 흔들림을 공용 UiNudge로 추출 + PasswordInputField 정리

왜: username 에러 피드백에서도 같은 흔들림이 필요해 PasswordInputField.Shake 로직을
공용 UiNudge.Shake(ref Coroutine로 중복 중단)로 빼고 PasswordInputField는 이를 호출.
동작·수치 동일. PlayMode 흔들림/복원 검증.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: PasswordInputField 한글→영문 실시간 변환 (PlayMode)

`onValueChanged`에서 `HangulQwerty.ToQwerty`로 텍스트를 영문화(재진입 가드). 한글 IME 입력도 즉시 영문으로 보이고, 마스킹/대조 모두 영문형 사용.

**Files:**
- Modify: `Assets/_Project/Scripts/UI/PasswordInputField.cs`
- Test: `Assets/Tests/PlayMode/PasswordInputFieldPlayModeTests.cs`

**Interfaces:**
- Consumes: `LoveAlgo.Core.HangulQwerty.ToQwerty(string)`.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/PlayMode/PasswordInputFieldPlayModeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>PasswordInputField: 한글 입력이 onValueChanged에서 두벌식 QWERTY 영문으로 변환되는지.</summary>
    public class PasswordInputFieldPlayModeTests
    {
        [UnityTest]
        public IEnumerator KoreanInput_ConvertedToQwerty()
        {
            var go = new GameObject("Pw", typeof(RectTransform));
            go.SetActive(false);
            var input = go.AddComponent<TMP_InputField>();
            var textArea = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            textArea.transform.SetParent(go.transform, false);
            input.textComponent = textArea;
            var pw = go.AddComponent<PasswordInputField>();
            pw.Input = input;
            go.SetActive(true); // OnEnable → onValueChanged 등록
            yield return null;
            try
            {
                input.text = "ㅂㅈㄷ"; // IME 잔여 자모
                Assert.AreEqual("qwe", input.text, "한글 자모 → QWERTY");
                input.text = "가Pass1!";
                Assert.AreEqual("rkPass1!", input.text, "혼합: 한글 변환 + ASCII 통과");
                input.text = "Plain9#";
                Assert.AreEqual("Plain9#", input.text, "ASCII는 그대로");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

`run_tests` PlayMode `PasswordInputFieldPlayModeTests`. Expected: FAIL(`input.text`가 "ㅂㅈㄷ" 그대로 — 변환 미적용).

- [ ] **Step 3: Implement conversion**

`PasswordInputField.cs` — `using LoveAlgo.Core;` 추가. 필드 + 핸들러 추가, `OnEnable`/`OnDisable`에 등록/해제:

```csharp
        bool _suppress; // onValueChanged 재진입 가드(text 재설정 → 콜백 재호출 방지)
```
`OnEnable` 끝에:
```csharp
            if (input != null) input.onValueChanged.AddListener(OnValueChangedConvert);
```
`OnDisable` 안에:
```csharp
            if (input != null) input.onValueChanged.RemoveListener(OnValueChangedConvert);
```
메서드 추가:
```csharp
        // 한글 입력을 두벌식 QWERTY 영문으로 실시간 통일 + 표준 ASCII 외 제거.
        void OnValueChangedConvert(string value)
        {
            if (_suppress) return;
            string converted = HangulQwerty.ToQwerty(value);
            if (converted == value) return;
            _suppress = true;
            input.text = converted;
            input.caretPosition = converted.Length;
            _suppress = false;
        }
```

- [ ] **Step 4: Run test to verify it passes**

`run_tests` PlayMode `PasswordInputFieldPlayModeTests` + 기존 `LockScreenPlayModeTests` 회귀. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/PasswordInputField.cs Assets/Tests/PlayMode/PasswordInputFieldPlayModeTests.cs Assets/Tests/PlayMode/PasswordInputFieldPlayModeTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(ui): 비밀번호 한글→영문 실시간 변환(두벌식 QWERTY)

왜: 한글 IME 켜진 채로 쳐도 의도한 영문 비밀번호가 되도록 PasswordInputField가
onValueChanged에서 HangulQwerty.ToQwerty로 통일(재진입 가드, 캐럿 끝). 표준 ASCII 외 제거.
마스킹/LockScreen 대조 모두 영문형 사용. PlayMode 변환 검증.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: UsernameScreenView NameValidator 배선 + 에러 라벨/흔들림 (PlayMode + 에디터)

`Submit`이 `NameValidator.Validate`로 검사해 무효면 저장 안 하고 에러 라벨+흔들림. 에러 라벨 직렬화 필드 추가 + 게임 username 화면 프리팹에 라벨 배치.

**Files:**
- Modify: `Assets/_Project/Scripts/UI/UsernameScreenView.cs`
- Modify(에디터): username 화면 프리팹/씬(에러 TMP 라벨 추가 + `ErrorLabel` 배선)
- Test: `Assets/Tests/PlayMode/UsernameScreenPlayModeTests.cs`(보강), `Assets/Tests/EditMode/NameValidatorTests.cs`(신규 회귀)

**Interfaces:**
- Consumes: `LoveAlgo.Core.NameValidator.Validate/GetErrorMessage`, `LoveAlgo.UI.UiNudge.Shake`.
- Produces: `UsernameScreenView.ErrorLabel { get; set; }`(TMP_Text) — 테스트·프리팹 배선용.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/NameValidatorTests.cs`(우리가 의존하는 규칙 고정):
```csharp
using NUnit.Framework;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>UsernameScreen이 의존하는 NameValidator 규칙 회귀(완성형만·금칙어·길이).</summary>
    public class NameValidatorTests
    {
        [Test] public void Valid_Korean() => Assert.AreEqual(NameValidator.Result.Valid, NameValidator.Validate("가나"));
        [Test] public void IncompleteJamo_Rejected() => Assert.AreEqual(NameValidator.Result.InvalidCharacter, NameValidator.Validate("ㄱㄴ"));
        [Test] public void TooShort() => Assert.AreEqual(NameValidator.Result.TooShort, NameValidator.Validate("가"));
        [Test] public void BannedWord() => Assert.AreEqual(NameValidator.Result.BannedWord, NameValidator.Validate("admin"));
    }
}
```

`UsernameScreenPlayModeTests.cs`에 추가(기존 `CreateView`는 `view.ErrorLabel` 배선이 필요 — 아래 Step 3에서 프로퍼티 추가 후 테스트의 CreateView에 errorLabel 생성·배선 한 줄 추가):
```csharp
        [UnityTest]
        public IEnumerator InvalidName_NotSaved_AndErrorShown()
        {
            var view = CreateView(); // CreateView가 ErrorLabel까지 배선(아래 참조)
            var handle = new LoveAlgo.Events.CompletionHandle();
            EventBus.Publish(new LoveAlgo.Events.ShowUsernameCommand(handle));
            yield return null;
            try
            {
                view.Input.text = "ㄱㄴ"; // 비문(미완성 자모)
                view.Submit();
                Assert.IsTrue(string.IsNullOrEmpty(_state.Data.playerName), "무효 이름은 저장 안 됨");
                Assert.IsTrue(view.IsShown, "무효면 화면 유지");
                Assert.IsFalse(string.IsNullOrEmpty(view.ErrorLabel.text), "에러 메시지 표시");
            }
            finally { }
        }
```
그리고 `CreateView` 끝부분(`view.ConfirmButton = ...` 다음)에 에러 라벨 배선 추가:
```csharp
            var errGo = new GameObject("Error", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            errGo.transform.SetParent(overlay.transform, false);
            view.ErrorLabel = errGo;
```
(`ShowUsernameCommand`/`CompletionHandle` 시그니처는 기존 테스트의 사용 형태를 따른다 — 기존 파일에서 동일 호출을 복사.)

- [ ] **Step 2: Run tests to verify they fail**

`refresh_unity(scope=all)` → EditMode `NameValidatorTests`(PASS 예상 — NameValidator 기존) + PlayMode `UsernameScreenPlayModeTests`(컴파일 에러: `ErrorLabel` 미정의).

- [ ] **Step 3: Implement UsernameScreenView wiring**

`UsernameScreenView.cs` — `using LoveAlgo.Core;`(NameValidator) 확인. 필드/프로퍼티 추가:
```csharp
        [Tooltip("무효 입력 시 사유 표시(TMP). 미바인딩 시 메시지 생략.")]
        [SerializeField] TMPro.TMP_Text errorLabel;
        public TMPro.TMP_Text ErrorLabel { get => errorLabel; set => errorLabel = value; }
```
`Coroutine _shakeCo;` 필드 추가. `Submit()` 교체:
```csharp
        public void Submit()
        {
            if (_pending == null && !IsShown) return;
            string name = input != null ? input.text.Trim() : "";
            if (string.IsNullOrEmpty(name)) return; // 입력 강제 — {{Player}} 치환 전제

            var result = NameValidator.Validate(name);
            if (result != NameValidator.Result.Valid)
            {
                if (errorLabel != null) errorLabel.text = NameValidator.GetErrorMessage(result);
                var rt = input != null ? input.transform as RectTransform : null;
                if (rt != null) UiNudge.Shake(this, rt, ref _shakeCo);
                return; // 무효 → 저장/숨김 안 함
            }

            if (errorLabel != null) errorLabel.text = "";
            if (state != null) state.Data.playerName = name;
            else Debug.LogError("[UsernameScreenView] state(GameStateSO) 미바인딩 — 이름 저장 불가.");
            HideImmediate();
        }
```

- [ ] **Step 4: Run tests to verify they pass**

`run_tests` EditMode `NameValidatorTests` + PlayMode `UsernameScreenPlayModeTests`. Expected: 모두 PASS(무효 미저장·에러표시·유효 저장 회귀 포함).

- [ ] **Step 5: 게임 프리팹에 에러 라벨 배치 (Unity 에디터/MCP)**

실제 username 화면(`UsernameScreenView`가 붙은 프리팹/씬)을 열어 `Box`(overlay) 아래에 TMP 에러 라벨 1개를 입력칸 근처에 배치하고 `UsernameScreenView.ErrorLabel`에 배선. 저장 후 Console 에러 0 확인. (테스트는 합성 뷰라 이 단계와 독립이지만, 실게임 표시를 위해 필요.)

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/UsernameScreenView.cs Assets/Tests/PlayMode/UsernameScreenPlayModeTests.cs Assets/Tests/EditMode/NameValidatorTests.cs Assets/Tests/EditMode/NameValidatorTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(ui): username 확인 시 NameValidator 배선 + 에러표시·흔들림

왜: 검증기(금칙어·완성형만→비문거부·길이)가 있으나 Submit에 미배선이라 무효 이름이
그대로 저장됐다. Submit이 NameValidator.Validate로 검사해 무효면 저장 안 하고 사유를
errorLabel에 표시 + UiNudge 흔들림. 유효 시에만 저장. NameValidator 규칙 회귀 테스트 추가.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

## 부록: 위험 대응

- **실시간 IME 변환 간섭**(Task 3): onValueChanged의 text 교체가 한글 조합을 끊어 IME와 충돌할 수 있다. 플레이모드(실 LockScreen)에서 한글 입력을 직접 쳐 검증하고, 불안정하면 `onEndEdit`/조합 확정 시 변환으로 폴백(가드 동일). 테스트의 프로그램적 `input.text` 설정은 조합과 무관하므로 별도 실기기 확인 권장.
