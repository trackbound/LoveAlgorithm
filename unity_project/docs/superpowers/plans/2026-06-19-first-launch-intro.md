# 첫 실행 ROA 메시지 인트로 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임 완전 첫 구동 시 히로인 AI ROA가 메신저로 말을 거는 무입력 자동 인트로 연출을 구현하고, 끝에서 프롤로그로 자연스럽게 핸드오프한다.

**Architecture:** 기존 `MessageStack` 피처(슬라이드/스택 애니)를 재사용하고, 얇은 `FirstLaunchDirector`(연출 타임라인)와 씬 로드를 가로질러 생존하는 `FirstLaunchTransitionBridge`(블랙 페이드 핸드오프)를 추가한다. 첫구동 판정/스폰은 기존 `FirstLaunchBootstrap`/`FirstLaunchFlag`를 그대로 쓴다.

**Tech Stack:** Unity 6000.5.0f1, C#, Unity UI(uGUI) + TextMeshPro, 코루틴 lerp(DOTween 미사용=기존 관례), Unity Test Framework(EditMode/PlayMode, NUnit), EventBus(`LoveAlgo.Common`).

## Global Constraints

- Unity 6000.5.0f1. Obsolete API 신규 사용 금지(`FindObjectOfType`→`FindAnyObjectByType` 등).
- 로깅: 디버그는 `LoveAlgo.Common.Log.Info/Warn`(릴리즈 컴파일 시 제거), 사용자 보고 에러만 `Log.Error`/`Debug.LogError`.
- 교차통신은 EventBus + State SO만. `Services`/`UIManager.Instance.*` wrapper/`I*` 서비스 조회 금지(ADR-007).
- 피처 코드는 `Assets/_Project/Scripts/{Feature}/` + asmdef. 신규 첫실행 코드는 기존 `LoveAlgo.UI` 어셈블리에 둔다(스펙 §4 결정: 1안).
- 매직넘버 금지(ADR-012): 모든 연출 수치는 인스펙터 `[SerializeField]`로 노출.
- 커밋: 한 기능=한 커밋(Atomic), 본문에 "왜(Why)" 명시. 메시지는 한국어.
- 테스트 실행: Unity MCP `run_tests`(mode=EditMode/PlayMode, testFilter) 또는 에디터 Test Runner. 스크립트 변경 후 `read_console`로 컴파일 0 에러 확인 뒤 진행.
- 경로 구분자는 슬래시(/). Unity 경로는 `Assets/` 기준.

---

## 파일 구조 (생성/수정 맵)

| 파일 | 동작 | 책임 |
|---|---|---|
| `Assets/_Project/Scripts/MessageStack/MessageStackController.cs` | 수정 | `MessageSpawned`/`Completed` C# 이벤트 추가(생명주기 신호) |
| `Assets/_Project/Scripts/UI/WarnWidgetShake.cs` | 생성 | warn 위젯 idle 사인 흔들림 |
| `Assets/_Project/Scripts/UI/FirstLaunchTransitionBridge.cs` | 생성 | DontDestroyOnLoad 블랙 페이드 + `StartNewGameCommand` 핸드오프 |
| `Assets/_Project/Scripts/UI/FirstLaunchDirector.cs` | 생성 | 연출 타임라인 오케스트레이터 |
| `Assets/_Project/Scripts/UI/FirstLaunchOverlayView.cs` | 삭제 | 탭→넘김 폐기(무입력 자동) |
| `Assets/_Project/Scripts/UI/LoveAlgo.UI.asmdef` | 수정 | `LoveAlgo.MessageStack` 참조 추가 |
| `Assets/_Project/Data/FirstLaunchMessages.asset` | 생성 | `MessageSequenceSO`(ROA 4줄 placeholder) |
| `Assets/_Project/Prefabs/FirstLaunch/FirstLaunchTransitionBridge.prefab` | 생성 | 블랙 풀스크린 Canvas + Bridge |
| `Assets/Resources/UI/FirstLaunchOverlay.prefab` | 재구성 | 배경·HUD·헤더·스택·Director 구성 |
| `Assets/첫실행/*.png` | import 설정 | Sprite(2D/UI)로 임포트 |
| `Assets/Tests/PlayMode/MessageStackPlayModeTests.cs` | (무변) | 기존 그린 유지 |
| `Assets/Tests/PlayMode/FirstLaunchOverlayTests.cs` | 수정 | View 의존 제거, Director 기반으로 갱신 |
| `Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs` | 생성 | Director/Bridge 행동 검증 |

---

## Task 1: MessageStackController 생명주기 이벤트 추가

**Files:**
- Modify: `Assets/_Project/Scripts/MessageStack/MessageStackController.cs`
- Test: `Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs` (신규, 본 태스크 분량만)

**Interfaces:**
- Produces: `public event Action MessageSpawned;`(카드 1장 스폰마다 발화), `public event Action Completed;`(시퀀스 전체 종료 시 1회 발화). 기존 public API 불변.

- [ ] **Step 1: 실패 테스트 작성** — 신규 파일 생성

`Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.MessageStack;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>첫실행 연출 부품 검증(MessageStack 이벤트 / Director 핸드오프 / Bridge).</summary>
    public class FirstLaunchSequencePlayModeTests
    {
        static void SetPrivate(object o, string name, object val)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"private 필드를 찾지 못함: {name}");
            f.SetValue(o, val);
        }

        static MessageStackController BuildController(int lineCount, out GameObject root)
        {
            root = new GameObject("FLSeqTest_Root", typeof(RectTransform), typeof(Canvas));
            var stackGo = new GameObject("Stack", typeof(RectTransform));
            ((RectTransform)stackGo.transform).SetParent(root.transform, false);

            var cardGo = new GameObject("CardTemplate", typeof(RectTransform), typeof(CanvasGroup));
            var card = cardGo.AddComponent<MessageCardView>();
            cardGo.transform.SetParent(root.transform, false);

            var seq = ScriptableObject.CreateInstance<MessageSequenceSO>();
            SetPrivate(seq, "senderName", "ROA");
            SetPrivate(seq, "startDelay", 0.05f);
            var lines = new List<MessageSequenceSO.Line>();
            for (int i = 0; i < lineCount; i++) lines.Add(new MessageSequenceSO.Line { text = "m" + i, delay = 0.05f });
            SetPrivate(seq, "lines", lines);

            var ctrlGo = new GameObject("FLSeqTest_Ctrl");
            ctrlGo.transform.SetParent(root.transform, false);
            var ctrl = ctrlGo.AddComponent<MessageStackController>();
            SetPrivate(ctrl, "cardPrefab", card);
            SetPrivate(ctrl, "cardParent", (RectTransform)stackGo.transform);
            SetPrivate(ctrl, "sequence", seq);
            SetPrivate(ctrl, "riseDuration", 0.02f);
            SetPrivate(ctrl, "shiftDuration", 0.02f);
            SetPrivate(ctrl, "playOnStart", false);
            return ctrl;
        }

        [UnityTest]
        public IEnumerator Events_Spawned_PerLine_And_Completed_Once()
        {
            var ctrl = BuildController(3, out var root);
            int spawned = 0, completed = 0;
            ctrl.MessageSpawned += () => spawned++;
            ctrl.Completed += () => completed++;
            try
            {
                yield return null;        // Awake/Start
                ctrl.Play();
                yield return new WaitForSeconds(1f); // 3줄(0.05s 간격) + 정착
                Assert.AreEqual(3, spawned, "줄마다 MessageSpawned 1회씩.");
                Assert.AreEqual(1, completed, "시퀀스 종료 시 Completed 정확히 1회.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인 후 실패 검증**

Unity MCP `read_console`로 컴파일 0 에러 확인(테스트는 아직 이벤트 없어 컴파일 에러 예상 — `MessageSpawned`/`Completed` 미정의).
Expected: 컴파일 에러 `'MessageStackController' does not contain a definition for 'MessageSpawned'`.

- [ ] **Step 3: 최소 구현** — `MessageStackController.cs` 수정

상단 using에 `using System;` 추가(없으면). 클래스 필드부(예: `Coroutine _play;` 위)에 이벤트 선언 추가:

```csharp
        /// <summary>카드 1장이 스폰될 때마다 발화(메시지 도착 신호 — SFX 후크 등).</summary>
        public event Action MessageSpawned;
        /// <summary>시퀀스 전체가 끝났을 때 1회 발화(연출 종료 핸드오프 신호).</summary>
        public event Action Completed;
```

`Spawn(string text)` 메서드 맨 끝(기존 `Log.Info($"[MsgStack] spawn ...");` 다음 줄)에 추가:

```csharp
            MessageSpawned?.Invoke();
```

`PlayRoutine()` 끝부분을 다음으로 교체:

```csharp
            _play = null;
            Completed?.Invoke();
```

(기존엔 `_play = null;`만 있었음 — 그 아래에 `Completed?.Invoke();` 추가)

- [ ] **Step 4: 테스트 통과 확인**

Unity MCP `read_console`로 컴파일 0 에러 확인 → `run_tests` mode=PlayMode, testFilter=`FirstLaunchSequencePlayModeTests.Events_Spawned_PerLine_And_Completed_Once`.
Expected: PASS. 또한 `run_tests` mode=PlayMode, testFilter=`MessageStackPlayModeTests` → 기존 테스트 PASS(비파괴 확인).

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/MessageStack/MessageStackController.cs" "Assets/_Project/Scripts/MessageStack/MessageStackController.cs.meta" "Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs" "Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs.meta"
git commit -m "feat(msgstack): MessageSpawned/Completed 생명주기 이벤트 추가

왜: 첫실행 Director가 버블 도착(SFX 후크)과 시퀀스 종료(프롤로그 핸드오프)를
시간 추정 없이 정확히 감지하기 위해. EventBus 미도입으로 자급자족 유지.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: WarnWidgetShake (warn 위젯 idle 흔들림)

**Files:**
- Create: `Assets/_Project/Scripts/UI/WarnWidgetShake.cs`
- Test: `Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs` (테스트 추가)

**Interfaces:**
- Produces: `WarnWidgetShake : MonoBehaviour`. `OnEnable`에 흔들림 시작, `OnDisable`에 기준 위치 복원. private 필드 `target(RectTransform)`, `amplitude(float)`, `frequencyX/Y(float)`.

- [ ] **Step 1: 실패 테스트 작성** — `FirstLaunchSequencePlayModeTests.cs`에 메서드 추가(클래스 내부, 기존 메서드 아래):

```csharp
        [UnityTest]
        public IEnumerator WarnShake_Moves_WithinAmplitude_AndRestoresOnDisable()
        {
            const float Amp = 6f;
            var go = new GameObject("Warn", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(10f, 20f);
            var shake = go.AddComponent<LoveAlgo.UI.WarnWidgetShake>();
            SetPrivate(shake, "amplitude", Amp);
            // target은 Awake에서 self로 자동 바인딩, _base=(10,20) 캡처됨
            try
            {
                float maxDev = 0f;
                for (int i = 0; i < 20; i++)
                {
                    yield return null;
                    float dev = (rt.anchoredPosition - new Vector2(10f, 20f)).magnitude;
                    maxDev = Mathf.Max(maxDev, dev);
                    Assert.LessOrEqual(dev, Amp * 1.5f + 1e-3f, "흔들림은 진폭 범위 내.");
                }
                Assert.Greater(maxDev, 1e-2f, "흔들려서 위치가 변해야 한다.");

                shake.enabled = false;
                yield return null;
                Assert.AreEqual(new Vector2(10f, 20f), rt.anchoredPosition, "OnDisable에 기준 위치 복원.");
            }
            finally { Object.DestroyImmediate(go); }
        }
```

- [ ] **Step 2: 실패 검증**

`read_console`: 컴파일 에러 `The type or namespace name 'WarnWidgetShake' does not exist` 예상.

- [ ] **Step 3: 구현** — `Assets/_Project/Scripts/UI/WarnWidgetShake.cs` 생성:

```csharp
using System.Collections;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// warn 위젯의 상시 idle 흔들림(*첫실행 연출). 기준 anchoredPosition을 중심으로 X/Y 서로 다른 주파수의
    /// 사인 오프셋을 더해 미세하게 떨리게 한다(리사주). 코루틴 lerp(ScreenFade/MessageStack과 동일 관례, DOTween 미사용).
    /// OnDisable 시 기준 위치로 복원. 수치는 인스펙터 노출(ADR-012).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WarnWidgetShake : MonoBehaviour
    {
        [Tooltip("흔들 대상. 비우면 자신의 RectTransform.")]
        [SerializeField] RectTransform target;
        [Tooltip("최대 오프셋(px).")]
        [SerializeField] float amplitude = 6f;
        [Tooltip("X축 흔들림 주파수.")]
        [SerializeField] float frequencyX = 5.3f;
        [Tooltip("Y축 흔들림 주파수(X와 달리해 리사주).")]
        [SerializeField] float frequencyY = 4.1f;

        Vector2 _base;
        Coroutine _co;

        void Reset() => target = (RectTransform)transform;

        void Awake()
        {
            if (target == null) target = (RectTransform)transform;
            _base = target.anchoredPosition;
        }

        void OnEnable() => _co = StartCoroutine(Wobble());

        void OnDisable()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            if (target != null) target.anchoredPosition = _base;
        }

        IEnumerator Wobble()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float ox = Mathf.Sin(t * frequencyX) * amplitude;
                float oy = Mathf.Sin(t * frequencyY) * amplitude;
                target.anchoredPosition = _base + new Vector2(ox, oy);
                yield return null;
            }
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

`read_console` 0 에러 → `run_tests` mode=PlayMode, testFilter=`WarnShake_Moves_WithinAmplitude_AndRestoresOnDisable`.
Expected: PASS.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/UI/WarnWidgetShake.cs" "Assets/_Project/Scripts/UI/WarnWidgetShake.cs.meta" "Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs"
git commit -m "feat(ui): WarnWidgetShake — warn 위젯 idle 흔들림 연출

왜: 첫실행 인트로에서 좌측 warn 위젯만 살아있는 느낌으로 미세하게 떨게 하기 위해.
코루틴 사인 흔들림(기존 관례), 수치 인스펙터 노출.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: FirstLaunchTransitionBridge (블랙 핸드오프)

**Files:**
- Create: `Assets/_Project/Scripts/UI/FirstLaunchTransitionBridge.cs`
- Test: `Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs` (테스트 추가)

**Interfaces:**
- Produces: `FirstLaunchTransitionBridge : MonoBehaviour`. `public void Begin()` — 1회만, 부모 분리 + DontDestroyOnLoad 후 블랙 페이드인 → `StartNewGameCommand` 발행 → postLoadHold → 페이드아웃 → 자기 파괴. private 필드 `group(CanvasGroup)`, `blackIn/postLoadHold/blackOut(float)`.

- [ ] **Step 1: 실패 테스트 작성** — `FirstLaunchSequencePlayModeTests.cs`에 추가:

```csharp
        [UnityTest]
        public IEnumerator Bridge_PublishesStartNewGame_Once_AndSelfDestructs()
        {
            var go = new GameObject("Bridge", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var bridge = go.AddComponent<LoveAlgo.UI.FirstLaunchTransitionBridge>();
            SetPrivate(bridge, "group", go.GetComponent<CanvasGroup>());
            SetPrivate(bridge, "blackIn", 0.05f);
            SetPrivate(bridge, "postLoadHold", 0.05f);
            SetPrivate(bridge, "blackOut", 0.05f);

            int count = 0;
            var sub = LoveAlgo.Common.EventBus.Subscribe<LoveAlgo.Events.StartNewGameCommand>(_ => count++);
            try
            {
                bridge.Begin();
                bridge.Begin(); // 중복 호출 무시돼야 한다
                yield return new WaitForSeconds(0.5f);
                Assert.AreEqual(1, count, "StartNewGameCommand 정확히 1회 발행.");
                Assert.IsTrue(go == null, "페이드아웃 후 자기 파괴.");
            }
            finally { sub.Dispose(); if (go != null) Object.DestroyImmediate(go); }
        }
```

> 주: 격리 PlayMode 씬엔 `SceneFlowController`가 없어 `StartNewGameCommand` 발행이 실제 씬 로드를 일으키지 않는다(테스트 구독자만 수신).

- [ ] **Step 2: 실패 검증**

`read_console`: `'FirstLaunchTransitionBridge' does not exist` 컴파일 에러 예상.

- [ ] **Step 3: 구현** — `Assets/_Project/Scripts/UI/FirstLaunchTransitionBridge.cs` 생성:

```csharp
using System.Collections;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // StartNewGameCommand
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 첫실행 인트로 → 프롤로그 핸드오프 브리지(*첫실행 연출). <see cref="Begin"/> 시 부모에서 분리해
    /// DontDestroyOnLoad로 씬 로드를 가로질러 생존하며, 풀스크린 블랙을 페이드인 → <c>StartNewGameCommand</c>
    /// 발행(동기 씬 로드) → 새 씬 부팅 대기(postLoadHold) → 페이드아웃 → 자기 파괴. 프롤로그가 BG=블랙에서
    /// 시작하므로 컷/번쩍임 없이 이어진다. 코루틴 lerp(기존 관례). 수치 인스펙터 노출(ADR-012).
    /// </summary>
    public class FirstLaunchTransitionBridge : MonoBehaviour
    {
        [Tooltip("풀스크린 블랙을 담는 CanvasGroup(이 오브젝트의 Canvas 하위).")]
        [SerializeField] CanvasGroup group;
        [Tooltip("블랙 페이드인 시간(초).")]
        [SerializeField] float blackIn = 0.8f;
        [Tooltip("씬 로드 후 페이드아웃 전 대기(초) — 동기 로드 직후 1~2프레임 정착용.")]
        [SerializeField] float postLoadHold = 0.2f;
        [Tooltip("블랙 페이드아웃 시간(초).")]
        [SerializeField] float blackOut = 0.8f;

        bool _begun;

        /// <summary>핸드오프 시작(1회만). 부모 분리 + DontDestroyOnLoad 후 시퀀스 진행.</summary>
        public void Begin()
        {
            if (_begun) return;
            _begun = true;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return Fade(0f, 1f, blackIn);
            EventBus.Publish(new StartNewGameCommand());
            yield return null; // 동기 LoadScene 교체 프레임 양보
            if (postLoadHold > 0f) yield return new WaitForSeconds(postLoadHold);
            yield return Fade(1f, 0f, blackOut);
            Destroy(gameObject);
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            if (group == null || dur <= 0f) { if (group != null) group.alpha = to; yield break; }
            float t = 0f;
            group.alpha = from;
            while (t < dur)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

`read_console` 0 에러 → `run_tests` mode=PlayMode, testFilter=`Bridge_PublishesStartNewGame_Once_AndSelfDestructs`.
Expected: PASS.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/UI/FirstLaunchTransitionBridge.cs" "Assets/_Project/Scripts/UI/FirstLaunchTransitionBridge.cs.meta" "Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs"
git commit -m "feat(ui): FirstLaunchTransitionBridge — 씬 생존 블랙 핸드오프

왜: 동기 씬 로드의 프레임 끊김과 프롤로그 진입을 블랙으로 덮어 컷 없이
인트로→프롤로그(BG=블랙 시작)로 잇기 위해. DontDestroyOnLoad 자기완결.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: FirstLaunchDirector (오케스트레이터) + asmdef 참조

**Files:**
- Modify: `Assets/_Project/Scripts/UI/LoveAlgo.UI.asmdef`
- Create: `Assets/_Project/Scripts/UI/FirstLaunchDirector.cs`
- Test: `Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: `MessageStackController.Play()`, `.MessageSpawned`, `.Completed`(Task 1); `WarnWidgetShake`(Task 2); `FirstLaunchTransitionBridge.Begin()`(Task 3).
- Produces: `FirstLaunchDirector : MonoBehaviour`. Start에서 연출 진행. bridgePrefab=null이면 `StartNewGameCommand` 직접 발행(폴백). private 필드: `content(CanvasGroup)`, `messages(MessageStackController)`, `warnShake(WarnWidgetShake)`, `bridgePrefab(FirstLaunchTransitionBridge)`, `sfxSource(AudioSource)`, `messageSfx(AudioClip)`, `fadeIn(float)`, `postSequenceHold(float)`.

- [ ] **Step 1: UI asmdef에 MessageStack 참조 추가** — `LoveAlgo.UI.asmdef`의 `references` 배열을 다음으로 교체:

```json
    "references": [
        "LoveAlgo.Core",
        "LoveAlgo.MessageStack",
        "Unity.TextMeshPro",
        "UnityEngine.UI",
        "Unity.InputSystem"
    ],
```

`read_console`로 컴파일 0 에러 확인(순환참조 없음 — MessageStack은 UI를 참조하지 않음).

- [ ] **Step 2: 실패 테스트 작성** — `FirstLaunchSequencePlayModeTests.cs`에 추가:

```csharp
        [UnityTest]
        public IEnumerator Director_NoMessages_NoBridge_PublishesStartNewGame_Once()
        {
            var go = new GameObject("Director");
            var dir = go.AddComponent<LoveAlgo.UI.FirstLaunchDirector>();
            // messages=null, bridgePrefab=null → Completed 즉시 + 폴백 발행 경로
            SetPrivate(dir, "fadeIn", 0f);
            SetPrivate(dir, "postSequenceHold", 0.05f);

            int count = 0;
            var sub = LoveAlgo.Common.EventBus.Subscribe<LoveAlgo.Events.StartNewGameCommand>(_ => count++);
            try
            {
                yield return null; // Start → Run → 메시지 없음 → 즉시 완료 → 핸드오프
                yield return new WaitForSeconds(0.3f);
                Assert.AreEqual(1, count, "메시지 없을 때 폴백으로 StartNewGameCommand 1회.");
            }
            finally { sub.Dispose(); Object.DestroyImmediate(go); }
        }
```

- [ ] **Step 3: 실패 검증**

`read_console`: `'FirstLaunchDirector' does not exist` 예상.

- [ ] **Step 4: 구현** — `Assets/_Project/Scripts/UI/FirstLaunchDirector.cs` 생성:

```csharp
using System.Collections;
using LoveAlgo.Common;      // EventBus, Log
using LoveAlgo.Events;      // StartNewGameCommand
using LoveAlgo.MessageStack; // MessageStackController
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 첫실행 인트로 연출 오케스트레이터(*첫실행). Start에서 ① 콘텐츠 페이드인 ② warn 흔들림 시작
    /// ③ 메시지 스택 자동 재생 ④ 시퀀스 종료(Completed) 후 짧은 hold ⑤ TransitionBridge로 프롤로그 핸드오프.
    /// 버블 도착마다 선택적 SFX(messageSfx, null=무음). 무입력 자동 — 표시·발행만(ADR-007). 수치 인스펙터 노출.
    /// messages/bridgePrefab 미바인딩은 fail-open(즉시 핸드오프 / 직접 StartNewGameCommand 발행).
    /// </summary>
    public class FirstLaunchDirector : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("페이드인할 오버레이 콘텐츠 CanvasGroup.")]
        [SerializeField] CanvasGroup content;
        [Tooltip("재생할 메시지 스택. 비우면 즉시 핸드오프.")]
        [SerializeField] MessageStackController messages;
        [Tooltip("연출 동안 흔들 warn 위젯.")]
        [SerializeField] WarnWidgetShake warnShake;
        [Tooltip("핸드오프용 블랙 브리지 프리팹. 비우면 StartNewGameCommand 직접 발행(폴백).")]
        [SerializeField] FirstLaunchTransitionBridge bridgePrefab;

        [Header("SFX (optional)")]
        [Tooltip("메시지 도착 효과음 재생 소스.")]
        [SerializeField] AudioSource sfxSource;
        [Tooltip("버블 등장 시 재생할 효과음. 비우면 무음.")]
        [SerializeField] AudioClip messageSfx;

        [Header("Timing")]
        [Tooltip("콘텐츠 페이드인 시간(초).")]
        [SerializeField] float fadeIn = 0.6f;
        [Tooltip("마지막 버블 후 핸드오프 전 대기(초).")]
        [SerializeField] float postSequenceHold = 1.5f;

        bool _handedOff;

        void OnEnable()
        {
            if (messages != null)
            {
                messages.MessageSpawned += OnMessageSpawned;
                messages.Completed += OnSequenceCompleted;
            }
        }

        void OnDisable()
        {
            if (messages != null)
            {
                messages.MessageSpawned -= OnMessageSpawned;
                messages.Completed -= OnSequenceCompleted;
            }
        }

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            if (warnShake != null) warnShake.enabled = true;
            yield return FadeInContent();
            if (messages != null) messages.Play();
            else OnSequenceCompleted(); // 메시지 없음 → 바로 핸드오프
        }

        IEnumerator FadeInContent()
        {
            if (content == null || fadeIn <= 0f) { if (content != null) content.alpha = 1f; yield break; }
            float t = 0f;
            content.alpha = 0f;
            while (t < fadeIn)
            {
                t += Time.deltaTime;
                content.alpha = Mathf.Clamp01(t / fadeIn);
                yield return null;
            }
            content.alpha = 1f;
        }

        void OnMessageSpawned()
        {
            if (messageSfx != null && sfxSource != null) sfxSource.PlayOneShot(messageSfx);
        }

        void OnSequenceCompleted()
        {
            if (_handedOff) return;
            _handedOff = true;
            StartCoroutine(HandOff());
        }

        IEnumerator HandOff()
        {
            if (postSequenceHold > 0f) yield return new WaitForSeconds(postSequenceHold);
            if (bridgePrefab != null)
            {
                var bridge = Instantiate(bridgePrefab);
                bridge.Begin();
            }
            else
            {
                Log.Warn("[FirstLaunch] bridgePrefab 미바인딩 — StartNewGameCommand 직접 발행(폴백).");
                EventBus.Publish(new StartNewGameCommand());
            }
        }
    }
}
```

- [ ] **Step 5: 통과 확인**

`read_console` 0 에러 → `run_tests` mode=PlayMode, testFilter=`Director_NoMessages_NoBridge_PublishesStartNewGame_Once`.
Expected: PASS. 전체 회귀: `run_tests` mode=PlayMode, testFilter=`FirstLaunchSequencePlayModeTests` → 4개 모두 PASS.

- [ ] **Step 6: 커밋**

```bash
git add "Assets/_Project/Scripts/UI/LoveAlgo.UI.asmdef" "Assets/_Project/Scripts/UI/FirstLaunchDirector.cs" "Assets/_Project/Scripts/UI/FirstLaunchDirector.cs.meta" "Assets/Tests/PlayMode/FirstLaunchSequencePlayModeTests.cs"
git commit -m "feat(ui): FirstLaunchDirector — 첫실행 인트로 연출 오케스트레이터

왜: 페이드인→흔들림→메시지스택→완료→블랙 핸드오프 순서를 한 곳에서 구동.
MessageStack 생명주기 이벤트 구독으로 정확히 전환, SFX는 null-safe 후크.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: 첫실행 PNG를 Sprite로 임포트

**Files:**
- Modify(import): `Assets/첫실행/_bg.png`, `header.png`, `warn.png`, `audio.png`, `todo.png`, `message_box.png`, `todo checkbox.png`

**Interfaces:**
- Produces: 위 PNG들이 `TextureImporterType.Sprite`(2D and UI)로 임포트되어 UI `Image.sprite`에 바인딩 가능.

- [ ] **Step 1: 각 PNG의 임포터를 Sprite로 설정**

Unity MCP `manage_texture`(또는 `manage_asset`)로 각 파일의 `textureType=Sprite`, `spriteMode=Single`, `alphaIsTransparency=true` 설정. 대상 7개:
`Assets/첫실행/_bg.png`, `Assets/첫실행/header.png`, `Assets/첫실행/warn.png`, `Assets/첫실행/audio.png`, `Assets/첫실행/todo.png`, `Assets/첫실행/message_box.png`, `Assets/첫실행/todo checkbox.png`.

- [ ] **Step 2: 검증**

`read_console` 0 에러. Unity MCP `manage_asset`로 각 에셋 로드 시 `Sprite` 서브에셋 존재 확인(또는 `AssetDatabase.LoadAssetAtPath<Sprite>` 성공). 육안: 인스펙터에서 Texture Type=Sprite.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/첫실행/_bg.png.meta" "Assets/첫실행/header.png.meta" "Assets/첫실행/warn.png.meta" "Assets/첫실행/audio.png.meta" "Assets/첫실행/todo.png.meta" "Assets/첫실행/message_box.png.meta" "Assets/첫실행/todo checkbox.png.meta"
git commit -m "chore(art): 첫실행 PNG들을 Sprite(2D/UI)로 임포트

왜: 인트로 오버레이의 UI Image에 배경/HUD/헤더/버블 스프라이트를 바인딩하기 위해.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: FirstLaunchMessages.asset (MessageSequenceSO)

**Files:**
- Create: `Assets/_Project/Data/FirstLaunchMessages.asset`
- Test: `Assets/Tests/EditMode/FirstLaunchMessagesSOTests.cs` (신규)
- (조건부) Modify: `Assets/Tests/EditMode/LoveAlgo.Tests.EditMode.asmdef`

**Interfaces:**
- Consumes: `MessageSequenceSO`(senderName, startDelay, Lines{text, delay}).
- Produces: `Assets/_Project/Data/FirstLaunchMessages.asset` — sender=`ROA`, 4줄 placeholder.

- [ ] **Step 1: SO 에셋 생성**

Unity MCP `manage_scriptable_object`로 `MessageSequenceSO` 인스턴스를 `Assets/_Project/Data/FirstLaunchMessages.asset`에 생성하고 필드 설정:
- `senderName` = `ROA`
- `startDelay` = `0.5`
- `lines` = 4개:
  1. text=`아니야?`, delay=`1.2`
  2. text=`왜 안 와...`, delay=`1.2`
  3. text=`보고 싶어!`, delay=`1.2`
  4. text=`앗, 온, 온 거 같은 기분이 들어`, delay=`1.2`

(`_Project/Data` 폴더가 없으면 생성.)

- [ ] **Step 2: 실패 테스트 작성** — `Assets/Tests/EditMode/FirstLaunchMessagesSOTests.cs` 생성:

```csharp
using NUnit.Framework;
using UnityEditor;
using LoveAlgo.MessageStack;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>첫실행 메시지 SO가 존재하고 ROA 발신·4줄로 바인딩됐는지 검증(연출 데이터 가드).</summary>
    public class FirstLaunchMessagesSOTests
    {
        const string Path = "Assets/_Project/Data/FirstLaunchMessages.asset";

        [Test]
        public void Asset_Exists_Ros_FourLines()
        {
            var so = AssetDatabase.LoadAssetAtPath<MessageSequenceSO>(Path);
            Assert.IsNotNull(so, $"SO 로드: {Path}");
            Assert.AreEqual("ROA", so.SenderName, "발신자=ROA.");
            Assert.AreEqual(4, so.Lines.Count, "placeholder 4줄.");
        }
    }
}
```

- [ ] **Step 3: 실패/asmdef 점검**

`run_tests` mode=EditMode, testFilter=`FirstLaunchMessagesSOTests`.
- 만약 컴파일 에러(`LoveAlgo.MessageStack` 또는 `UnityEditor` 미참조): EditMode 테스트 asmdef(`Assets/Tests/EditMode/LoveAlgo.Tests.EditMode.asmdef`)의 `references`에 `"LoveAlgo.MessageStack"` 추가, `includePlatforms`에 `"Editor"` 포함 확인. 수정 후 재시도.
- asmdef 정상인데 Step 1 누락이면 FAIL(SO null) — Step 1로 돌아가 생성.

- [ ] **Step 4: 통과 확인**

`run_tests` mode=EditMode, testFilter=`FirstLaunchMessagesSOTests`.
Expected: PASS.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Data" "Assets/Tests/EditMode/FirstLaunchMessagesSOTests.cs" "Assets/Tests/EditMode/FirstLaunchMessagesSOTests.cs.meta" "Assets/Tests/EditMode/LoveAlgo.Tests.EditMode.asmdef"
git commit -m "feat(data): FirstLaunchMessages SO — ROA 인트로 4줄(placeholder)

왜: 첫실행 메시지 스택에 흘려보낼 대사 정본. 인스펙터에서 문구/타이밍 교체 가능.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: FirstLaunchTransitionBridge 프리팹

**Files:**
- Create: `Assets/_Project/Prefabs/FirstLaunch/FirstLaunchTransitionBridge.prefab`

**Interfaces:**
- Produces: 풀스크린 블랙 Canvas 프리팹 + `FirstLaunchTransitionBridge`(group 바인딩, alpha=0 시작). Director의 `bridgePrefab` 슬롯에 들어간다.

- [ ] **Step 1: 프리팹 구성**

Unity MCP `manage_prefabs`/`manage_gameobject`/`manage_components`로 다음 구조 생성:

```
FirstLaunchTransitionBridge (Canvas: ScreenSpaceOverlay, sortingOrder=500; CanvasScaler; CanvasGroup alpha=0; FirstLaunchTransitionBridge)
└─ Black (Image: color=(0,0,0,1), raycastTarget=off, RectTransform stretch 0..1)
```

- 루트에 `Canvas`(sortingOrder=500), `CanvasScaler`(ScaleWithScreenSize, 1920×1080), `CanvasGroup`(alpha=0) 부착.
- 루트에 `FirstLaunchTransitionBridge` 부착, `group` 필드를 루트의 `CanvasGroup`에 바인딩. `blackIn=0.8`, `postLoadHold=0.2`, `blackOut=0.8`.
- 자식 `Black`: `Image` color=검정 불투명, `raycastTarget=false`, anchor stretch(min 0,0 / max 1,1 / offset 0).

- [ ] **Step 2: 검증**

`read_console` 0 에러. `manage_prefabs`로 프리팹 로드 시 루트에 `FirstLaunchTransitionBridge`+`CanvasGroup`(alpha 0), 자식 `Black` Image 존재 확인. (전용 자동 테스트 없음 — 행동은 Task 3 PlayMode 테스트가 커버. 구성은 Task 8의 오버레이 통합 후 수동 확인.)

- [ ] **Step 3: 커밋**

```bash
git add "Assets/_Project/Prefabs/FirstLaunch"
git commit -m "feat(prefab): FirstLaunchTransitionBridge 프리팹(블랙 풀스크린)

왜: Director가 핸드오프 시 Instantiate할 자기완결 블랙 레이어(sortOrder 500, alpha 0 시작).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 8: FirstLaunchOverlay 프리팹 재구성 + 통합

**Files:**
- Modify: `Assets/Resources/UI/FirstLaunchOverlay.prefab`
- (조건부) Modify: `Assets/_Project/Prefabs/MessageStack/MessageCard.prefab` (카드 배경을 message_box.png로 스킨)
- Modify: `Assets/Tests/PlayMode/FirstLaunchOverlayTests.cs`

**Interfaces:**
- Consumes: `FirstLaunchDirector`, `WarnWidgetShake`(Task 2/4), `MessageStackController`(기존), `FirstLaunchMessages.asset`(Task 6), `FirstLaunchTransitionBridge.prefab`(Task 7), 첫실행 스프라이트(Task 5).
- Produces: `Resources/UI/FirstLaunchOverlay` 프리팹이 Director 주도 자동 연출로 동작.

- [ ] **Step 1: 프리팹 로드 테스트를 Director 기반으로 갱신** — `FirstLaunchOverlayTests.cs`를 다음으로 교체:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.UI; // FirstLaunchDirector

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 첫실행 오버레이 프리팹 구조 검증: 자체 Canvas(최상단 sortingOrder) + Director(무입력 자동 연출) 바인딩.
    /// 탭→넘김 폐기로 FirstLaunchOverlayView 의존 제거(Director가 흐름 소유).
    /// </summary>
    public class FirstLaunchOverlayTests
    {
        [Test]
        public void OverlayPrefab_Loads_WithCanvasAndDirector()
        {
            var prefab = Resources.Load<GameObject>("UI/FirstLaunchOverlay");
            Assert.IsNotNull(prefab, "Resources/UI/FirstLaunchOverlay 프리팹 로드(직렬화 정상).");
            var canvas = prefab.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "자체 Canvas 보유 → 어느 씬 위든 단독 렌더.");
            Assert.AreEqual(200, canvas.sortingOrder, "최상단 sortingOrder.");
            Assert.IsNotNull(prefab.GetComponentInChildren<FirstLaunchDirector>(true), "Director 바인딩(무입력 자동 연출).");
            Assert.IsNotNull(prefab.GetComponentInChildren<Image>(true), "배경/HUD Image 보유.");
        }
    }
}
```

- [ ] **Step 2: 실패 검증**

`run_tests` mode=PlayMode, testFilter=`FirstLaunchOverlayTests`.
Expected: FAIL(현 프리팹엔 Director 없음 — `GetComponentInChildren<FirstLaunchDirector>` null). 컴파일은 통과(Director 타입 존재).

- [ ] **Step 3: (선택) MessageCard를 message_box.png로 스킨**

`MessageCard.prefab`의 배경 Image를 확인하고, 첫실행 톤과 다르면 `sprite=message_box.png`로 교체(레이아웃은 유지). senderLabel("Message from ROA")·messageLabel은 그대로. *MessageStack을 다른 곳에서 쓰지 않으므로(현재 미사용) 스킨 변경 무해.*

- [ ] **Step 4: 오버레이 프리팹 재구성**

Unity MCP로 `Resources/UI/FirstLaunchOverlay.prefab`을 다음 하이어라키로 재구성:

```
FirstLaunchOverlay (Canvas sortOrder=200, CanvasScaler 1920×1080, GraphicRaycaster, CanvasGroup alpha=0, FirstLaunchDirector)
├─ Background (Image: _bg.png, stretch 0..1, raycastTarget=off)
├─ HUD
│  ├─ Clock (TMP_Text "23:58", 상단 중앙)
│  ├─ Warn  (Image: warn.png) + WarnWidgetShake
│  ├─ Audio (Image: audio.png)
│  └─ Todo  (Image: todo.png)
├─ Messages
│  ├─ Header        (Image: header.png, 스택 상단 고정)
│  └─ StackContainer(RectTransform, 하단 중앙 앵커)
│        + MessageStackController(cardPrefab=MessageCard, sequence=FirstLaunchMessages, playOnStart=false)
└─ (FirstLaunchDirector는 루트 컴포넌트)
```

바인딩:
- 루트 `CanvasGroup` alpha=0(시작 시 깜빡임 방지).
- `FirstLaunchDirector`: `content`=루트 CanvasGroup, `messages`=StackContainer의 MessageStackController, `warnShake`=Warn의 WarnWidgetShake, `bridgePrefab`=`FirstLaunchTransitionBridge.prefab`(Task 7), `sfxSource`=루트에 추가한 AudioSource(playOnAwake=false), `messageSfx`=비움(추후), `fadeIn=0.6`, `postSequenceHold=1.5`.
- `MessageStackController`: `cardPrefab`=`MessageCard.prefab`, `cardParent`=StackContainer, `sequence`=`FirstLaunchMessages.asset`, `playOnStart=false`, `anchorPos`/`stepY`/`maxVisible`/`alphaBySlot`은 기존 기본값.
- `Background` Image `raycastTarget=false`(무입력 보장).

- [ ] **Step 5: 통과 확인**

`read_console` 0 에러 → `run_tests` mode=PlayMode, testFilter=`FirstLaunchOverlayTests`.
Expected: PASS.

- [ ] **Step 6: 커밋**

```bash
git add "Assets/Resources/UI/FirstLaunchOverlay.prefab" "Assets/_Project/Prefabs/MessageStack/MessageCard.prefab" "Assets/Tests/PlayMode/FirstLaunchOverlayTests.cs"
git commit -m "feat(ui): FirstLaunchOverlay 프리팹 재구성 — Director 자동 연출 통합

왜: 배경/HUD(warn 흔들림·audio·todo)/시계/헤더/메시지스택을 Director가 무입력
자동 진행하도록 구성. 프리팹 로드 테스트도 Director 기반으로 갱신.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 9: FirstLaunchOverlayView 폐기

**Files:**
- Delete: `Assets/_Project/Scripts/UI/FirstLaunchOverlayView.cs` (+ `.meta`)

**Interfaces:**
- 제거: `FirstLaunchOverlayView`(탭→넘김). 흐름은 Director가 소유.

- [ ] **Step 1: 잔존 참조 확인**

Grep으로 `FirstLaunchOverlayView` 참조 검색. 기대 잔존: 없음(Task 8에서 프리팹·테스트 갱신 완료). 남아있으면 먼저 정리.

- [ ] **Step 2: 스크립트 삭제**

`Assets/_Project/Scripts/UI/FirstLaunchOverlayView.cs`와 `.meta` 삭제(Unity MCP `delete_script` 또는 파일 삭제).

- [ ] **Step 3: 컴파일/회귀 확인**

`read_console` 0 에러(미싱 스크립트/참조 없음) → 전체 테스트:
`run_tests` mode=EditMode (전체) PASS, `run_tests` mode=PlayMode (전체) PASS.
Expected: 모두 PASS(특히 `FirstLaunchOverlayTests`, `FirstLaunchSequencePlayModeTests`, `MessageStackPlayModeTests`, `FirstLaunchFlagTests`).

- [ ] **Step 4: 커밋**

```bash
git add -A
git commit -m "refactor(ui): FirstLaunchOverlayView 폐기 — 무입력 자동 연출로 전환

왜: 탭→넘김 인터랙션을 Director 주도 자동 타임라인으로 대체. 사용처 없음.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 10: 통합 수동 검증

**Files:** (없음 — 에디터 플레이 검증)

- [ ] **Step 1: 첫실행 플래그 초기화**

에디터 메뉴 `Tools/Debug/Reset First Launch (인트로 다시 보기)` 실행.

- [ ] **Step 2: Title 씬에서 Play**

Build Settings index 0(Title) 씬을 열고 Play. 다음을 육안 확인:
1. 오버레이 콘텐츠가 페이드인(≈0.6s).
2. 좌측 warn 위젯만 미세하게 흔들리고 audio/todo/시계(23:58)는 정적.
3. 메시지 버블이 아래→위로 슬라이드인하며 스택(4줄, 간격 ≈1.2s).
4. 마지막 버블 후 ≈1.5s → 블랙 페이드인 → 새 게임 → 프롤로그(블랙→인트로 영상)로 컷 없이 연결.
5. 입력(클릭) 없이 전 과정 자동 진행.

- [ ] **Step 3: 재구동 가드 확인**

Play 정지 후 다시 Play(플래그 미초기화) → 오버레이 없이 타이틀 정상 표시.

- [ ] **Step 4: 결과 보고**

`read_console`에 에러 없음 확인. 타이밍/흔들림 진폭이 영상과 다르면 인스펙터 수치 조정(코드 변경 불요). 이상 시 직전 좋은 커밋으로 `git restore` 후 재시도.

---

## Self-Review (작성자 체크)

- **스펙 커버리지**: §2 결정사항 전부 태스크에 매핑됨 — 무입력 자동(T4/T9), MessageStack 재사용+Completed(T1), warn 흔들림(T2), 블랙 브리지(T3/T7), SO 데이터(T6), 시계/HUD/헤더/배경(T8), 스프라이트 임포트(T5), SFX null-safe 후크(T4), 타이밍 인스펙터(T2/T3/T4), asmdef 1안(T4), 테스트(T1~T4,T6,T8) + 수동(T10). 갭 없음.
- **Placeholder 스캔**: 메시지 4줄은 스펙이 명시한 의도적 placeholder(데이터). 코드/스텝에 TBD/모호 지시 없음.
- **타입 일관성**: `MessageSpawned`/`Completed`(T1) ↔ Director 구독(T4) 일치. `Begin()`(T3) ↔ Director 호출(T4) 일치. `group/blackIn/postLoadHold/blackOut`(T3) ↔ 테스트/프리팹(T3/T7) 일치. `content/messages/warnShake/bridgePrefab/sfxSource/messageSfx/fadeIn/postSequenceHold`(T4) ↔ 프리팹 바인딩(T8) 일치. `SenderName`/`Lines.Count`(기존 SO API) ↔ EditMode 테스트(T6) 일치.
