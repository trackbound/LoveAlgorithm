# ButtonStateDriver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 버튼 상태 비주얼을 단일 컴포넌트 `ButtonStateDriver`로 수렴하고(배경 child-swap + 공유 라벨 색 + pressed 코드틴트 + UI 사운드), Modal Yes/No를 첫 파일럿으로 이행해 시각·청각 패리티를 증명한다.

**Architecture:** `Button` 옆에 붙는 MonoBehaviour. raw 포인터 이벤트로 상태를 잡고(Selectable 상속 안 함 → 포커스 가림 부재), 상태별 배경은 명시적 직렬화 자식을 정확히 하나 `SetActive`, 라벨은 항상 켜진 단일 TMP의 색만 코드 구동, pressed는 활성 자식 Image에 틴트 곱. 순수 결정층은 정적 함수로 분리해 EditMode 단위테스트.

**Tech Stack:** Unity 6 LTS, C#, uGUI(`UnityEngine.UI`), TextMeshPro, NUnit + Unity Test Framework, EventBus(`LoveAlgo.Common`) + `PlaySfxCommand`(`LoveAlgo.Events`).

## Global Constraints

- 피처 간 직접 참조 금지: 교차통신은 EventBus + State SO만 경유 (ADR-007). 사운드는 `EventBus.Publish(new PlaySfxCommand(name))`로만 발행.
- Obsolete API 금지 (Unity 6 LTS). `FindAnyObjectByType` 등 사용.
- 로깅: 일반은 `Log.Info/Warn`, 사용자 보고용 에러만 `Debug.LogError`.
- 한 기능 = 한 커밋(Atomic). 커밋 메시지 본문에 "왜(Why)" 명시. 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- pressedTint 값 = `(0.7803922, 0.7803922, 0.7803922, 1)` (≈C7C7C7) — 기존 ButtonSpriteSwap과 동일.
- 새 컴포넌트 위치: `Assets/_Project/Scripts/UI/ButtonStateDriver.cs` (StyledButton·ButtonSpriteSwap과 동일 UI 어셈블리).
- 이번 슬라이스에서 `StyledButton`/`ButtonSpriteSwap`/`TitleHighlightSwitcher` 코드는 **수정·삭제하지 않는다**(공존).

---

### Task 1: 순수 결정층 + 타입 정의 (EditMode TDD)

`ButtonStateDriver`의 GameObject 불필요한 정적 결정 함수 4종과 타입(`State`/`TextColorBlock`/`UiSoundRole`)을 먼저 만든다. 이 단계의 파일은 `MonoBehaviour`(인터페이스 없음)로 선언해 컴파일만 통과시키고, 인스턴스 동작은 Task 2에서 채운다.

**Files:**
- Create: `Assets/_Project/Scripts/UI/ButtonStateDriver.cs`
- Test: `Assets/Tests/EditMode/ButtonStateDriverTests.cs`

**Interfaces:**
- Consumes: `LoveAlgo.UI.UiSoundSO`(기존: `.ButtonHover`/`.ButtonClick`/`.ChoiceHover`/`.ChoiceClick`).
- Produces (Task 2·3가 의존):
  - `enum ButtonStateDriver.State { Normal, Hover, On, Disabled }`
  - `enum ButtonStateDriver.UiSoundRole { General, Choice, Silent }`
  - `struct ButtonStateDriver.TextColorBlock { bool drive; Color normal, hover, on, disabled; static TextColorBlock Default; }`
  - `static State ResolveActiveState(bool interactable, bool isOn, bool pointerInside)`
  - `static Color ResolvePressedTint(bool interactable, bool pressed, Color baseColor, Color pressedTint)`
  - `static Color ResolveTextColor(State state, in TextColorBlock c)`
  - `static string ResolveSfx(UiSoundRole role, bool hover, UiSoundSO table)`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/ButtonStateDriverTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// ButtonStateDriver 순수 결정층 단위테스트(GameObject 불필요). 어댑터(포인터/SetOn/SetInteractable→자식 SetActive)는
    /// PlayMode(ButtonStateDriverPlayModeTests)에서 검증.
    /// </summary>
    public class ButtonStateDriverTests
    {
        [Test]
        public void ResolveActiveState_Priority_DisabledOverOnOverHoverOverNormal()
        {
            // 비활성 최우선(ON·호버 무관)
            Assert.AreEqual(ButtonStateDriver.State.Disabled, ButtonStateDriver.ResolveActiveState(false, true, true));
            // ON > 호버
            Assert.AreEqual(ButtonStateDriver.State.On, ButtonStateDriver.ResolveActiveState(true, true, true));
            // 호버
            Assert.AreEqual(ButtonStateDriver.State.Hover, ButtonStateDriver.ResolveActiveState(true, false, true));
            // 기본
            Assert.AreEqual(ButtonStateDriver.State.Normal, ButtonStateDriver.ResolveActiveState(true, false, false));
        }

        [Test]
        public void ResolvePressedTint_MultipliesBase_OnlyWhenInteractableAndPressed()
        {
            var baseColor = Color.white;
            var tint = new Color(0.7803922f, 0.7803922f, 0.7803922f, 1f); // C7C7C7

            Assert.AreEqual(baseColor * tint, ButtonStateDriver.ResolvePressedTint(true, true, baseColor, tint));
            Assert.AreEqual(baseColor, ButtonStateDriver.ResolvePressedTint(true, false, baseColor, tint));
            Assert.AreEqual(baseColor, ButtonStateDriver.ResolvePressedTint(false, true, baseColor, tint)); // 비활성이면 패스
        }

        [Test]
        public void ResolveTextColor_Priority_DisabledOverOnOverHoverOverNormal()
        {
            var c = new ButtonStateDriver.TextColorBlock
            {
                drive = true,
                normal = Color.black,
                hover = Color.white,
                on = Color.red,
                disabled = Color.gray,
            };
            Assert.AreEqual(c.disabled, ButtonStateDriver.ResolveTextColor(ButtonStateDriver.State.Disabled, c));
            Assert.AreEqual(c.on, ButtonStateDriver.ResolveTextColor(ButtonStateDriver.State.On, c));
            Assert.AreEqual(c.hover, ButtonStateDriver.ResolveTextColor(ButtonStateDriver.State.Hover, c));
            Assert.AreEqual(c.normal, ButtonStateDriver.ResolveTextColor(ButtonStateDriver.State.Normal, c));
        }

        [Test]
        public void ResolveSfx_RoleAndHover_NullTableSilent()
        {
            // table null → 항상 null (무음)
            Assert.IsNull(ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.General, true, null));
            // Silent 역할 → null
            var table = ScriptableObject.CreateInstance<UiSoundSO>();
            Assert.IsNull(ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.Silent, true, table));
            // General/Choice는 table 항목을 반환(기본 빈 문자열 — null 아님)
            Assert.AreEqual(table.ButtonHover, ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.General, true, table));
            Assert.AreEqual(table.ButtonClick, ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.General, false, table));
            Assert.AreEqual(table.ChoiceHover, ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.Choice, true, table));
            Assert.AreEqual(table.ChoiceClick, ButtonStateDriver.ResolveSfx(ButtonStateDriver.UiSoundRole.Choice, false, table));
            Object.DestroyImmediate(table);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Unity Test Runner(EditMode)에서 `ButtonStateDriverTests` 실행 (또는 MCP `run_tests` mode=EditMode 필터 `ButtonStateDriverTests`).
Expected: 컴파일 에러 또는 FAIL — `ButtonStateDriver` 타입 미정의.

- [ ] **Step 3: Write minimal implementation**

`Assets/_Project/Scripts/UI/ButtonStateDriver.cs`:

```csharp
using System;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 버튼 상태 비주얼 통합 드라이버(StyledButton·ButtonSpriteSwap·TitleHighlightSwitcher 수렴 대상).
    /// 배경은 상태별 자식을 정확히 하나 SetActive(child-swap), 라벨은 단일 TMP 색 코드 구동,
    /// pressed는 활성 자식 Image에 틴트 곱, UI 사운드도 발행. raw 포인터 이벤트 구동(Selectable 미상속 → 포커스 가림 부재).
    ///
    /// 이 파일의 Task 1 단계는 순수 결정층(정적)만 — 인스턴스 어댑터는 Task 2에서 채운다.
    /// </summary>
    public class ButtonStateDriver : MonoBehaviour
    {
        /// <summary>비주얼 상태. 우선순위 Disabled &gt; On &gt; Hover &gt; Normal.</summary>
        public enum State { Normal, Hover, On, Disabled }

        /// <summary>호버/클릭이 UiSound 테이블의 어느 항목을 쓸지.</summary>
        public enum UiSoundRole { General, Choice, Silent }

        /// <summary>상태별 라벨(TMP) 색. drive=false면 라벨 색 미관여. 상태 4종과 1:1.</summary>
        [Serializable]
        public struct TextColorBlock
        {
            [Tooltip("켜면 상태별로 라벨 색을 구동. 끄면 라벨 색 미관여.")]
            public bool drive;
            public Color normal;   // OFF/기본
            public Color hover;
            public Color on;       // 토글 ON
            public Color disabled;

            public static TextColorBlock Default => new TextColorBlock
            {
                drive = false,
                normal = Color.black,
                hover = Color.white,
                on = Color.white,
                disabled = new Color(0.5f, 0.5f, 0.5f, 1f),
            };
        }

        // ── 순수 결정층 (GameObject 불필요 — EditMode 테스트 대상) ──────────────────────

        /// <summary>활성 상태(우선순위 Disabled &gt; On &gt; Hover &gt; Normal).</summary>
        public static State ResolveActiveState(bool interactable, bool isOn, bool pointerInside)
        {
            if (!interactable) return State.Disabled;
            if (isOn) return State.On;
            if (pointerInside) return State.Hover;
            return State.Normal;
        }

        /// <summary>눌림(interactable && pressed)일 때 baseColor*pressedTint(어두워짐), 아니면 baseColor 유지.</summary>
        public static Color ResolvePressedTint(bool interactable, bool pressed, Color baseColor, Color pressedTint)
            => (interactable && pressed) ? baseColor * pressedTint : baseColor;

        /// <summary>상태별 라벨 색(drive 판단은 호출 측 책임).</summary>
        public static Color ResolveTextColor(State state, in TextColorBlock c)
        {
            switch (state)
            {
                case State.Hover: return c.hover;
                case State.On: return c.on;
                case State.Disabled: return c.disabled;
                default: return c.normal; // Normal
            }
        }

        /// <summary>역할+호버/클릭 → SFX 이름(table/항목 없으면 null). StyledButton.ResolveSfx 이식.</summary>
        public static string ResolveSfx(UiSoundRole role, bool hover, UiSoundSO table)
        {
            if (table == null) return null;
            switch (role)
            {
                case UiSoundRole.Silent: return null;
                case UiSoundRole.Choice: return hover ? table.ChoiceHover : table.ChoiceClick;
                default:                 return hover ? table.ButtonHover : table.ButtonClick;
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Unity Test Runner(EditMode)에서 `ButtonStateDriverTests` 실행.
Expected: 4 테스트 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/ButtonStateDriver.cs Assets/_Project/Scripts/UI/ButtonStateDriver.cs.meta Assets/Tests/EditMode/ButtonStateDriverTests.cs Assets/Tests/EditMode/ButtonStateDriverTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(ui): ButtonStateDriver 순수 결정층(상태/틴트/라벨색/SFX)

왜: 3개 버튼 메커니즘을 단일 컴포넌트로 수렴하기 위한 첫 단계 —
GameObject 불필요한 정적 결정 함수를 먼저 두고 EditMode로 고정해
어댑터(child-swap) 구현 전에 우선순위/폴백/틴트 규칙을 검증한다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: MonoBehaviour 어댑터 — child-swap + 틴트 + 라벨 + 사운드 (PlayMode TDD)

순수 결정층 위에 인스턴스 동작을 얹는다: 직렬화 자식 참조, 포인터/토글/interactable 입력, 활성 자식 정확히 하나 SetActive, 활성 자식 Image에 pressed 틴트(자식별 base 컬러 1회 캡처), 라벨 색, enter/click 사운드 발행.

**Files:**
- Modify: `Assets/_Project/Scripts/UI/ButtonStateDriver.cs` (Task 1이 만든 클래스에 인스턴스 멤버 추가)
- Test: `Assets/Tests/PlayMode/ButtonStateDriverPlayModeTests.cs`

**Interfaces:**
- Consumes: Task 1의 정적 함수·타입 전부. `LoveAlgo.Common.EventBus`, `LoveAlgo.Events.PlaySfxCommand`.
- Produces (Task 3·미래 이행이 의존):
  - `void SetOn(bool on)` / `void SetInteractable(bool value)` / `bool IsOn { get; }`
  - 직렬화 + public 프로퍼티: `GameObject NormalState/HoverState/OnState/DisabledState`, `Color PressedTint`, `TextColorBlock TextColors`, `TMPro.TMP_Text Label`, `UiSoundRole SoundRole`
  - 포인터 핸들러: `OnPointerEnter/Exit/Down/Up/Click`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/PlayMode/ButtonStateDriverPlayModeTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// ButtonStateDriver 어댑터 PlayMode: 포인터/SetOn/SetInteractable가 상태 자식을 정확히 하나 활성으로
    /// 구동하고(child-swap), 활성 자식 Image에 pressed 틴트를 곱하며, 라벨 색을 상태대로 바꾸는지.
    /// </summary>
    public class ButtonStateDriverPlayModeTests
    {
        static Sprite NewSprite() => Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

        static GameObject NewStateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().sprite = NewSprite();
            return go;
        }

        [UnityTest]
        public IEnumerator ChildSwap_Tint_LabelColor_DrivenByState()
        {
            var root = new GameObject("Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.SetActive(false);
            var driver = root.AddComponent<ButtonStateDriver>();

            var normal = NewStateChild(root.transform, "Normal");
            var hover = NewStateChild(root.transform, "Hover");
            var on = NewStateChild(root.transform, "On");
            var disabled = NewStateChild(root.transform, "Disabled");

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            var label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();

            driver.NormalState = normal;
            driver.HoverState = hover;
            driver.OnState = on;
            driver.DisabledState = disabled;
            driver.Label = label;
            driver.TextColors = new ButtonStateDriver.TextColorBlock
            {
                drive = true, normal = Color.black, hover = Color.white, on = Color.red, disabled = Color.gray,
            };

            root.SetActive(true); // OnEnable: base 캡처 + Apply
            yield return null;

            try
            {
                // 기본: Normal만 활성, 라벨 검정
                Assert.IsTrue(normal.activeSelf && !hover.activeSelf && !on.activeSelf && !disabled.activeSelf, "기본=Normal");
                Assert.AreEqual(Color.black, label.color, "기본 라벨 검정");

                // 호버 → Hover만 활성, 라벨 흰
                driver.OnPointerEnter(new PointerEventData(EventSystem.current));
                Assert.IsTrue(hover.activeSelf && !normal.activeSelf, "호버=Hover");
                Assert.AreEqual(Color.white, label.color, "호버 라벨 흰");

                // 눌림 → 활성(Hover) 자식 Image에 C7C7C7 틴트
                driver.OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                var tint = new Color(0.7803922f, 0.7803922f, 0.7803922f, 1f);
                Assert.AreEqual(Color.white * tint, hover.GetComponent<Image>().color, "눌림 틴트");
                driver.OnPointerUp(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                Assert.AreEqual(Color.white, hover.GetComponent<Image>().color, "떼면 복원");

                // 이탈 → Normal
                driver.OnPointerExit(new PointerEventData(EventSystem.current));
                Assert.IsTrue(normal.activeSelf, "이탈=Normal");

                // 토글 ON → On만 활성(호버해도 ON 유지), 라벨 빨강
                driver.SetOn(true);
                Assert.IsTrue(on.activeSelf && !normal.activeSelf, "ON=On");
                Assert.AreEqual(Color.red, label.color, "ON 라벨 빨강");
                driver.OnPointerEnter(new PointerEventData(EventSystem.current));
                Assert.IsTrue(on.activeSelf, "ON 중 호버해도 On 유지");
                driver.OnPointerExit(new PointerEventData(EventSystem.current));
                driver.SetOn(false);

                // 비활성 → Disabled, 라벨 회색
                driver.SetInteractable(false);
                Assert.IsTrue(disabled.activeSelf && !normal.activeSelf, "비활성=Disabled");
                Assert.AreEqual(Color.gray, label.color, "비활성 라벨 회색");
                driver.SetInteractable(true);
                Assert.IsTrue(normal.activeSelf, "재활성=Normal");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator MissingChild_FallsBackToNormal()
        {
            var root = new GameObject("Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.SetActive(false);
            var driver = root.AddComponent<ButtonStateDriver>();
            var normal = NewStateChild(root.transform, "Normal");
            driver.NormalState = normal; // hover/on/disabled 비움
            root.SetActive(true);
            yield return null;

            try
            {
                driver.OnPointerEnter(new PointerEventData(EventSystem.current));
                Assert.IsTrue(normal.activeSelf, "Hover 자식 없으면 Normal 유지");
                driver.SetInteractable(false);
                Assert.IsTrue(normal.activeSelf, "Disabled 자식 없으면 Normal 유지");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Unity Test Runner(PlayMode)에서 `ButtonStateDriverPlayModeTests` 실행.
Expected: 컴파일 에러(프로퍼티/메서드 미정의) 또는 FAIL.

> PlayMode 실행 시 주의(메모리 [[unity-mcp-test-and-execute-gotchas]]): MCP로 PlayMode 테스트를 돌릴 때 Editor 포커스 교착이 날 수 있으니, 막히면 Unity Test Runner 창에서 직접 Run.

- [ ] **Step 3: Write minimal implementation**

`Assets/_Project/Scripts/UI/ButtonStateDriver.cs`를 아래 전체 내용으로 교체(Task 1의 정적 부분 포함, 인스턴스 멤버 추가):

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // PlaySfxCommand

namespace LoveAlgo.UI
{
    /// <summary>
    /// 버튼 상태 비주얼 통합 드라이버(StyledButton·ButtonSpriteSwap·TitleHighlightSwitcher 수렴 대상).
    /// 배경은 상태별 자식을 정확히 하나 SetActive(child-swap), 라벨은 단일 TMP 색 코드 구동,
    /// pressed는 활성 자식 Image에 틴트 곱, UI 사운드도 발행. raw 포인터 이벤트 구동(Selectable 미상속 → 포커스 가림 부재).
    ///
    /// <para>상태 자식은 <b>명시적 직렬화 참조</b>(normalState/hoverState/onState/disabledState). 비운 슬롯은 Normal로 폴백.
    /// 라벨은 항상 켜진 단일 TMP — 텍스트는 외부(예: ChoiceSlot.Bind)가, 색은 이 드라이버가 구동(동적 텍스트 버튼 호환).</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonStateDriver : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public enum State { Normal, Hover, On, Disabled }
        public enum UiSoundRole { General, Choice, Silent }

        [Serializable]
        public struct TextColorBlock
        {
            [Tooltip("켜면 상태별로 라벨 색을 구동. 끄면 라벨 색 미관여.")]
            public bool drive;
            public Color normal;   // OFF/기본
            public Color hover;
            public Color on;       // 토글 ON
            public Color disabled;

            public static TextColorBlock Default => new TextColorBlock
            {
                drive = false,
                normal = Color.black,
                hover = Color.white,
                on = Color.white,
                disabled = new Color(0.5f, 0.5f, 0.5f, 1f),
            };
        }

        [Header("상태 배경 자식 (비우면 Normal로 폴백)")]
        [SerializeField] GameObject normalState;
        [SerializeField] GameObject hoverState;
        [SerializeField] GameObject onState;       // 토글 ON
        [SerializeField] GameObject disabledState;

        [Tooltip("눌림 시 활성 자식 Image의 base 컬러에 곱하는 틴트(≈C7C7C7).")]
        [SerializeField] Color pressedTint = new Color(0.7803922f, 0.7803922f, 0.7803922f, 1f);

        [Header("상태별 라벨 색")]
        [SerializeField] TextColorBlock textColors = TextColorBlock.Default;
        [Tooltip("색을 바꿀 라벨(TMP). 텍스트는 외부가 주입, 색만 구동.")]
        [SerializeField] TMP_Text label;

        [Header("UI 사운드")]
        [Tooltip("호버/클릭 SFX 묶음. General=일반 버튼/모달, Choice=선택지, Silent=무음.")]
        [SerializeField] UiSoundRole soundRole = UiSoundRole.General;

        Button _button;
        bool _pointerInside;
        bool _pressed;
        bool _isOn;

        // 활성 자식에 곱하기 전 base 컬러 1회 캡처(틴트 누적 방지).
        Color _normalBase = Color.white, _hoverBase = Color.white, _onBase = Color.white, _disabledBase = Color.white;
        bool _basesCaptured;

        public GameObject NormalState { get => normalState; set => normalState = value; }
        public GameObject HoverState { get => hoverState; set => hoverState = value; }
        public GameObject OnState { get => onState; set => onState = value; }
        public GameObject DisabledState { get => disabledState; set => disabledState = value; }
        public Color PressedTint { get => pressedTint; set => pressedTint = value; }
        public TextColorBlock TextColors { get => textColors; set => textColors = value; }
        public TMP_Text Label { get => label; set => label = value; }
        public UiSoundRole SoundRole { get => soundRole; set => soundRole = value; }
        public bool IsOn => _isOn;

        // ── 순수 결정층 (GameObject 불필요 — EditMode 테스트 대상) ──────────────────────

        public static State ResolveActiveState(bool interactable, bool isOn, bool pointerInside)
        {
            if (!interactable) return State.Disabled;
            if (isOn) return State.On;
            if (pointerInside) return State.Hover;
            return State.Normal;
        }

        public static Color ResolvePressedTint(bool interactable, bool pressed, Color baseColor, Color pressedTint)
            => (interactable && pressed) ? baseColor * pressedTint : baseColor;

        public static Color ResolveTextColor(State state, in TextColorBlock c)
        {
            switch (state)
            {
                case State.Hover: return c.hover;
                case State.On: return c.on;
                case State.Disabled: return c.disabled;
                default: return c.normal; // Normal
            }
        }

        public static string ResolveSfx(UiSoundRole role, bool hover, UiSoundSO table)
        {
            if (table == null) return null;
            switch (role)
            {
                case UiSoundRole.Silent: return null;
                case UiSoundRole.Choice: return hover ? table.ChoiceHover : table.ChoiceClick;
                default:                 return hover ? table.ButtonHover : table.ButtonClick;
            }
        }

        // ── 얇은 어댑터 ───────────────────────────────────────────────────────────────

        void OnEnable()
        {
            EnsureRefs();
            CaptureBases();
            Apply();
        }

        void OnDisable()
        {
            _pressed = false;       // 눌린 채 비활성 잔류 방지
            _pointerInside = false;
        }

        /// <summary>토글 ON 표시 구동(소유 View가 호출).</summary>
        public void SetOn(bool on)
        {
            if (_isOn == on) return;
            _isOn = on;
            Apply();
        }

        /// <summary>interactable 토글 + Disabled 자식 즉시 반영.</summary>
        public void SetInteractable(bool value)
        {
            EnsureRefs();
            if (_button != null) _button.interactable = value;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true; Apply();
            if (IsInteractable()) PlayUiSfx(hover: true); // 비활성 버튼은 무음
        }

        public void OnPointerExit(PointerEventData eventData) { _pointerInside = false; Apply(); }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pressed = true; Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _pressed = false; Apply();
        }

        // 클릭음: 네이티브 Button이 onClick을 트리거하는 조건과 동일(상호작용 가능 + 좌클릭).
        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsInteractable() && eventData.button == PointerEventData.InputButton.Left)
                PlayUiSfx(hover: false);
        }

        bool IsInteractable() => _button == null || _button.IsInteractable();

        void PlayUiSfx(bool hover)
        {
            string sfx = ResolveSfx(soundRole, hover, UiSoundSO.Shared);
            if (!string.IsNullOrEmpty(sfx)) EventBus.Publish(new PlaySfxCommand(sfx));
        }

        void EnsureRefs()
        {
            if (_button == null) _button = GetComponent<Button>();
        }

        // 각 상태 자식 Image의 원본 컬러를 1회 캡처(틴트 적용 전).
        void CaptureBases()
        {
            if (_basesCaptured) return;
            _normalBase = BaseColorOf(normalState);
            _hoverBase = BaseColorOf(hoverState);
            _onBase = BaseColorOf(onState);
            _disabledBase = BaseColorOf(disabledState);
            _basesCaptured = true;
        }

        static Color BaseColorOf(GameObject go)
        {
            if (go == null) return Color.white;
            var img = go.GetComponent<Image>();
            return img != null ? img.color : Color.white;
        }

        // 상태 → 자식(없으면 Normal 폴백) + 그 base 컬러.
        GameObject StateObject(State state, out Color baseColor)
        {
            switch (state)
            {
                case State.Hover: baseColor = hoverState != null ? _hoverBase : _normalBase; return hoverState != null ? hoverState : normalState;
                case State.On: baseColor = onState != null ? _onBase : _normalBase; return onState != null ? onState : normalState;
                case State.Disabled: baseColor = disabledState != null ? _disabledBase : _normalBase; return disabledState != null ? disabledState : normalState;
                default: baseColor = _normalBase; return normalState;
            }
        }

        void Apply()
        {
            EnsureRefs();
            CaptureBases();
            bool interactable = IsInteractable();
            var state = ResolveActiveState(interactable, _isOn, _pointerInside);
            var active = StateObject(state, out var baseColor);

            // 정확히 하나만 활성.
            SetActiveSafe(normalState, active);
            SetActiveSafe(hoverState, active);
            SetActiveSafe(onState, active);
            SetActiveSafe(disabledState, active);

            // 활성 자식 Image에 pressed 틴트(base에 곱).
            if (active != null)
            {
                var img = active.GetComponent<Image>();
                if (img != null) img.color = ResolvePressedTint(interactable, _pressed, baseColor, pressedTint);
            }

            // 라벨 색.
            if (textColors.drive && label != null)
                label.color = ResolveTextColor(state, textColors);
        }

        static void SetActiveSafe(GameObject go, GameObject active)
        {
            if (go != null) go.SetActive(go == active);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Test Runner(EditMode + PlayMode) 실행.
Expected: `ButtonStateDriverTests`(4) + `ButtonStateDriverPlayModeTests`(2) 모두 PASS. 기존 테스트 회귀 없음.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/ButtonStateDriver.cs Assets/Tests/PlayMode/ButtonStateDriverPlayModeTests.cs Assets/Tests/PlayMode/ButtonStateDriverPlayModeTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(ui): ButtonStateDriver 어댑터 — child-swap+틴트+라벨+사운드

왜: 순수 결정층 위에 인스턴스 동작을 얹어 단일 컴포넌트로 3개 메커니즘을
대체할 수 있게 한다. 배경은 상태 자식 정확히 하나 SetActive, pressed는
활성 자식 Image에 틴트 곱(자식별 base 1회 캡처로 누적 방지), 라벨 색·호버/클릭음 구동.
raw 포인터 이벤트라 포커스가 호버를 가리는 StyledButton 부채가 구조적으로 없다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Modal Yes/No 파일럿 이행 (Unity 에디터 + 회귀)

`YesButton`/`NoButton` 프리팹을 StyledButton → 네이티브 Button + ButtonStateDriver로 교체하고, 배경을 Normal/Hover 자식으로 분리한다. **프리팹 수정은 Unity 에디터에서 수행**(YAML 수기 편집 금지 — fileID/참조 깨짐 위험). `ChoiceSlot`은 유지하고 `ChoiceSlot.button`을 새 Button으로 재배선한다.

**Files:**
- Modify(에디터): `Assets/_Project/Prefabs/UI/YesButton.prefab`, `Assets/_Project/Prefabs/UI/NoButton.prefab`
- 참조(읽기): `Assets/_Project/Scripts/UI/ModalView.cs`(동적 생성·Bind), `Assets/_Project/Scripts/UI/ChoiceSlot.cs`(button/labelText 참조)
- Test(회귀): `Assets/Tests/PlayMode/ModalViewPlayModeTests.cs`(기존)

**Interfaces:**
- Consumes: Task 2의 `ButtonStateDriver`(`Label`/`TextColors`/`NormalState`/`HoverState`/`SoundRole`).
- Produces: 런타임 동작만(코드 산출물 없음). Modal 버튼이 패리티로 동작.

이행 전 현재 `YesButton.prefab` 상태(참고): 루트=Image(흰 배경 sprite `951f499a…`)+StyledButton(highlightedSprite `598d1696…`=핑크, textColors 검정→흰, label=자식 "예")+ChoiceSlot+LayoutElement, 자식 "Label"(TMP).

- [ ] **Step 1: 백업 분기 생성**

```bash
git switch -c feat/button-state-driver-modal-pilot
```
(이미 작업 분기면 생략. main 직접 작업 회피.)

- [ ] **Step 2: YesButton 프리팹 구조 개편 (Unity 에디터)**

Unity에서 `Assets/_Project/Prefabs/UI/YesButton.prefab`을 Prefab Mode로 연다.
1. 루트 `YesButton`의 **StyledButton 컴포넌트 제거** → **Button 컴포넌트 추가**(Transition=None). `targetGraphic`=루트 Image.
2. 루트 `Image`의 `Color` 알파를 0으로(투명 raycast 타겟). `Raycast Target`은 켜둔다.
3. 루트 아래 빈 `States` 오브젝트(RectTransform, stretch anchor) 생성.
4. `States` 아래 `Normal`(Image, sprite=`951f499a…` 흰 배경), `Hover`(Image, sprite=`598d1696…` 핑크) 생성. 둘 다 stretch anchor로 버튼 전체를 덮게.
5. 기존 `Label`(TMP "예")은 루트 자식으로 유지하되 `States`보다 **뒤(아래) 형제**로 둬 위에 렌더되게 한다.
6. 루트에 **ButtonStateDriver 추가** 후 배선:
   - `Normal State`=States/Normal, `Hover State`=States/Hover, `On State`/`Disabled State`=비움.
   - `Label`=Label(TMP).
   - `Text Colors`: `drive`=on, `normal`=검정(0,0,0,1), `hover`=흰(1,1,1,1), `on`/`disabled`는 기본값.
   - `Sound Role`=General.
   - `Pressed Tint`=기본(C7C7C7) 유지.
7. **ChoiceSlot 유지**: `ChoiceSlot.button` 필드를 **새 Button**으로 재지정(StyledButton 제거로 빈 참조가 됨), `ChoiceSlot.labelText`=Label 유지.

- [ ] **Step 3: NoButton 프리팹 동일 개편**

`Assets/_Project/Prefabs/UI/NoButton.prefab`도 Step 2와 동일하게(스프라이트는 NoButton의 흰/핑크 형제, 라벨 "아니오"). textColors·SoundRole 동일.

- [ ] **Step 4: 컴파일/콘솔 확인**

Unity Console에 컴파일 에러·누락 참조(Missing) 경고가 없는지 확인. 프리팹 인스펙터에서 ButtonStateDriver 필드가 전부 채워졌는지 육안 확인.

- [ ] **Step 5: 회귀 테스트 — ModalView PlayMode**

Unity Test Runner(PlayMode)에서 `ModalViewPlayModeTests` 실행.
Expected: 전부 PASS — Modal이 Yes/No 버튼을 생성하고 클릭 시 인덱스 선택/닫힘이 동작(ChoiceSlot.Bind·onClick 정상).

- [ ] **Step 6: 수동 패리티 확인 (Play)**

Play 모드로 모달을 띄우는 경로(예: 타이틀에서 종료 확인 등 Yes/No 모달)를 실행하고 확인:
- [ ] 평상: 흰 배경 + 검정 글씨.
- [ ] 호버: 핑크 배경 + 흰 글씨.
- [ ] 누르는 중: 살짝 어두워짐(C7C7C7).
- [ ] 호버/클릭 시 효과음(UiSound General 항목이 설정된 경우).
- [ ] 클릭: 모달이 의도대로 닫히고 선택이 반영.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Prefabs/UI/YesButton.prefab Assets/_Project/Prefabs/UI/NoButton.prefab
git commit -m "$(cat <<'EOF'
feat(ui): Modal Yes/No를 ButtonStateDriver로 이행(파일럿)

왜: 통합 드라이버의 첫 실전 적용으로 시각·청각 패리티를 증명한다.
배경을 Normal/Hover 자식으로 분리(child-swap), 루트는 투명 raycast 타겟,
라벨 색·사운드는 드라이버가 구동. ChoiceSlot은 ModalView의 Bind 경로상 유지하고
button 참조만 새 Button으로 재배선. StyledButton/레거시 3종은 이번엔 공존(미삭제).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

## 부록: 후속 슬라이스(이 계획 밖)

- Title 메뉴(StyledButton+TitleHighlightSwitcher 이중)·Close·Settings/SaveLoad 화살표·토글 이행.
- 3개 레거시 컴포넌트 삭제 + `UiSoundRole` 공용 위치 이동(StyledButton 참조 정리) + 에디터 와이어툴/에셋 네이밍 규약 갱신.
