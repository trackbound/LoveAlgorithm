# 로아(Roa) 오버레이 자동 결합 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 로아가 중앙 슬롯에 등장/표정변경/퇴장할 때, 디바이스(pc/모바일)×감정 카테고리(기본/긍정/부정)에 맞는 Overlay 레이어 이미지를 자동으로 함께 띄우고 전환하고 내린다.

**Architecture:** 새 `RoaOverlayController`(UI 뷰)가 단일 두뇌로 `ShowCharacterCommand`·인라인 `ShowSpeakerEmoteCommand`·신규 `SetRoaDeviceCommand`를 구독하고, 기존 `ShowStageLayerCommand(StageLayerKind.Overlay)`로 `StageLayerView`를 구동한다. 엔진(`NarrativeController`)은 CSV에서 디바이스를 해석해 명령으로만 전달하고 세이브 미러(`storyRoaDevice`)의 단일 작성자로 남는다(ADR-007). 카테고리/오버레이 파일명 규칙은 신규 `RoaOverlaySO`가 데이터로 보유한다.

**Tech Stack:** Unity 6 LTS, C#, 사내 `EventBus`(struct 이벤트), ScriptableObject, NUnit(EditMode 순수 / PlayMode 명령-핸들).

## Global Constraints

- 피처 간 직접 참조 금지 — 교차 통신은 `EventBus` + State SO만 경유. `Services`/`I*` 인터페이스 부활 금지.
- 동기 상태 조회는 State SO 직접 읽기. (단, 본 기능의 컨트롤러는 State를 읽지/쓰지 않는 순수 뷰 — 디바이스는 항상 `SetRoaDeviceCommand`로 받는다.)
- Obsolete API 금지(`FindObjectOfType`→`FindAnyObjectByType` 등).
- 디버그 로그는 `LoveAlgo.Common.Log.Info/Warn`. 사용자 보고용만 `Log.Error`.
- 한 기능 = 한 커밋(Atomic). 커밋 메시지 본문에 **왜(Why)** 명시. 끝에 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- 오버레이 에셋 파일명 규칙은 고정: `{device}_{category}` = `pc_기본`/`pc_긍정`/`pc_부정`/`모바일_기본`/`모바일_긍정`/`모바일_부정` (이미 `Assets/Resources/Overlay/`에 존재).
- 디바이스 CSV 키워드와 SO 파일명 접두어는 일치해야 한다: `pc` / `모바일`.
- 표정은 런타임에 **해석된 코드**(`00`/`41`…)로 흐른다 — SO 카테고리 매칭도 코드 기준.

**참고 설계서:** `docs/superpowers/specs/2026-06-19-roa-overlay-design.md`

> **설계서 대비 1건 정제:** 엔진은 로아 식별자를 추적하지 않으므로 Exit/Clear에서 `storyRoaDevice`를 비우지 않는다(잔여는 무해 — 무대에 로아가 없으면 컨트롤러가 무시). `storyRoaDevice`는 `ClearStoryPosition`(내러티브 종료)에서만 초기화한다. 오버레이의 화면 종료는 컨트롤러가 로아 Exit에서 처리한다.

---

### Task 1: 디바이스 이벤트 + 파서 (Core)

`SetRoaDeviceCommand`, `RoaDevice` enum, 순수 키워드 파서 `RoaDeviceParse`. 엔진(Story)과 부트스트랩(Game), 컨트롤러(UI)가 모두 참조하는 최하위 Core 계층에 둔다.

**Files:**
- Create: `Assets/_Project/Scripts/Core/Events/RoaOverlayEvents.cs`
- Test: `Assets/Tests/EditMode/RoaDeviceParseTests.cs`

**Interfaces:**
- Produces:
  - `enum LoveAlgo.Events.RoaDevice { Pc, Mobile }`
  - `readonly struct LoveAlgo.Events.SetRoaDeviceCommand { RoaDevice Device; ctor(RoaDevice) }`
  - `static class LoveAlgo.Events.RoaDeviceParse`
    - `const string PcToken = "pc"`, `const string MobileToken = "모바일"`
    - `static bool TryParse(string token, out RoaDevice device)`
    - `static string ToToken(RoaDevice device)`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;
using LoveAlgo.Events;

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class RoaDeviceParseTests
    {
        [Test] public void Parse_Pc() { Assert.IsTrue(RoaDeviceParse.TryParse("pc", out var d)); Assert.AreEqual(RoaDevice.Pc, d); }
        [Test] public void Parse_Pc_CaseInsensitive() { Assert.IsTrue(RoaDeviceParse.TryParse("PC", out var d)); Assert.AreEqual(RoaDevice.Pc, d); }
        [Test] public void Parse_Mobile_Korean() { Assert.IsTrue(RoaDeviceParse.TryParse("모바일", out var d)); Assert.AreEqual(RoaDevice.Mobile, d); }
        [Test] public void Parse_Mobile_English() { Assert.IsTrue(RoaDeviceParse.TryParse("Mobile", out var d)); Assert.AreEqual(RoaDevice.Mobile, d); }
        [Test] public void Parse_Unknown_False() { Assert.IsFalse(RoaDeviceParse.TryParse("xyz", out _)); }
        [Test] public void Parse_Empty_False() { Assert.IsFalse(RoaDeviceParse.TryParse("", out _)); }
        [Test] public void ToToken_RoundTrips() { Assert.AreEqual("모바일", RoaDeviceParse.ToToken(RoaDevice.Mobile)); Assert.AreEqual("pc", RoaDeviceParse.ToToken(RoaDevice.Pc)); }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인**

Unity 콘솔에서 `RoaDeviceParse`/`RoaDevice`/`SetRoaDeviceCommand` 미정의 컴파일 에러 확인(타입 없음).

- [ ] **Step 3: 최소 구현**

```csharp
using System;

namespace LoveAlgo.Events
{
    /// <summary>로아가 시각화되는 가상 기기. 오버레이 파일명 접두어(pc_/모바일_)를 결정.</summary>
    public enum RoaDevice { Pc, Mobile }

    /// <summary>
    /// 로아 디바이스 설정/전환 명령. 엔진이 CSV(Enter 디바이스 토큰 / RoaDevice 라인)를 해석해 발행하고,
    /// <c>RoaOverlayController</c>가 구독해 같은 감정 카테고리로 오버레이 디바이스만 교체한다(ADR-007).
    /// </summary>
    public readonly struct SetRoaDeviceCommand
    {
        public readonly RoaDevice Device;
        public SetRoaDeviceCommand(RoaDevice device) { Device = device; }
    }

    /// <summary>로아 디바이스 키워드 순수 파서(EditMode 테스트 가능). 키워드 2종 고정: pc / 모바일(mobile 허용).</summary>
    public static class RoaDeviceParse
    {
        public const string PcToken = "pc";
        public const string MobileToken = "모바일";

        public static bool TryParse(string token, out RoaDevice device)
        {
            device = RoaDevice.Pc;
            if (string.IsNullOrWhiteSpace(token)) return false;
            switch (token.Trim().ToLowerInvariant())
            {
                case "pc": device = RoaDevice.Pc; return true;
                case "모바일":
                case "mobile": device = RoaDevice.Mobile; return true;
                default: return false;
            }
        }

        public static string ToToken(RoaDevice device) => device == RoaDevice.Mobile ? MobileToken : PcToken;
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Unity Test Runner(EditMode)에서 `RoaDeviceParseTests` 7개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Events/RoaOverlayEvents.cs* Assets/Tests/EditMode/RoaDeviceParseTests.cs*
git commit -m "feat(roa-overlay): 디바이스 이벤트+파서 추가

Why: 로아 오버레이가 pc/모바일을 명령으로만 전달받아 컨트롤러를 순수 뷰로
유지하기 위해, Core에 SetRoaDeviceCommand와 순수 키워드 파서를 둔다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: `RoaOverlaySO` — 카테고리/파일명 규칙 (UI)

표정 코드→감정 카테고리 매핑과 `{device}_{category}` 파일명 생성을 데이터로 보유. 컨트롤러(UI)만 소비하므로 UI asmdef에 둔다(`CharacterStageCatalogSO` 선례).

**Files:**
- Create: `Assets/_Project/Scripts/UI/RoaOverlaySO.cs`
- Test: `Assets/Tests/EditMode/RoaOverlaySOTests.cs`

**Interfaces:**
- Consumes: `LoveAlgo.Events.RoaDevice` (Task 1)
- Produces:
  - `class LoveAlgo.UI.RoaOverlaySO : ScriptableObject`
  - `enum RoaOverlaySO.Category { Default, Positive, Negative }`
  - `string RoaCharId { get; }` (기본 "roa")
  - `RoaDevice DefaultDevice { get; }` (기본 Pc)
  - `Category ResolveCategory(string emoteCode)` (인스턴스)
  - `string OverlayName(RoaDevice device, Category category)` → 예 "pc_긍정"
  - `static Category ResolveCategory(IReadOnlyList<string> positive, IReadOnlyList<string> negative, string emoteCode)` (순수)
  - `void Configure(string roaId, string[] positive, string[] negative, RoaDevice defaultDevice)` (테스트/에디터 셋업용)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Events;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class RoaOverlaySOTests
    {
        static readonly string[] Pos = { "41", "42" };
        static readonly string[] Neg = { "51" };

        [Test] public void ResolveCategory_Positive() => Assert.AreEqual(RoaOverlaySO.Category.Positive, RoaOverlaySO.ResolveCategory(Pos, Neg, "41"));
        [Test] public void ResolveCategory_Negative() => Assert.AreEqual(RoaOverlaySO.Category.Negative, RoaOverlaySO.ResolveCategory(Pos, Neg, "51"));
        [Test] public void ResolveCategory_Unlisted_Default() => Assert.AreEqual(RoaOverlaySO.Category.Default, RoaOverlaySO.ResolveCategory(Pos, Neg, "00"));
        [Test] public void ResolveCategory_Empty_Default() => Assert.AreEqual(RoaOverlaySO.Category.Default, RoaOverlaySO.ResolveCategory(Pos, Neg, ""));

        [Test]
        public void OverlayName_BuildsFromDeviceAndCategory()
        {
            var so = ScriptableObject.CreateInstance<RoaOverlaySO>();
            try
            {
                Assert.AreEqual("pc_기본", so.OverlayName(RoaDevice.Pc, RoaOverlaySO.Category.Default));
                Assert.AreEqual("모바일_긍정", so.OverlayName(RoaDevice.Mobile, RoaOverlaySO.Category.Positive));
                Assert.AreEqual("pc_부정", so.OverlayName(RoaDevice.Pc, RoaOverlaySO.Category.Negative));
            }
            finally { Object.DestroyImmediate(so); }
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인**

`RoaOverlaySO` 미정의 컴파일 에러 확인.

- [ ] **Step 3: 최소 구현**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Events; // RoaDevice

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로아 오버레이 규칙 정의 SO. 표정 코드→감정 카테고리(긍정/부정만 나열, 나머지=기본 폴백)와
    /// {device}_{category} 파일명(pc_긍정 등)을 데이터로 보유한다. 컨트롤러(RoaOverlayController)만 소비.
    /// 표정은 런타임에 해석된 코드(00/41…)로 흐르므로 코드 기준으로 매칭한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RoaOverlay", menuName = "LoveAlgo/Roa Overlay")]
    public class RoaOverlaySO : ScriptableObject
    {
        public enum Category { Default, Positive, Negative }

        [Tooltip("이 캐릭터 코드 ID가 등장할 때만 오버레이를 결합한다.")]
        [SerializeField] string roaCharId = "roa";
        [Tooltip("긍정 카테고리 표정 코드(해석된 코드, 예: 41 42). 미나열 표정은 전부 기본.")]
        [SerializeField] string[] positiveEmotes;
        [Tooltip("부정 카테고리 표정 코드.")]
        [SerializeField] string[] negativeEmotes;

        [Header("파일명 규칙 {prefix}_{suffix} — Resources/Overlay/ 파일명과 일치해야 함")]
        [SerializeField] string pcPrefix = "pc";
        [SerializeField] string mobilePrefix = "모바일";
        [SerializeField] string defaultSuffix = "기본";
        [SerializeField] string positiveSuffix = "긍정";
        [SerializeField] string negativeSuffix = "부정";
        [Tooltip("등장 시 디바이스 토큰이 없거나 아직 미설정일 때의 기본 디바이스.")]
        [SerializeField] RoaDevice defaultDevice = RoaDevice.Pc;

        public string RoaCharId => roaCharId;
        public RoaDevice DefaultDevice => defaultDevice;

        public Category ResolveCategory(string emoteCode) => ResolveCategory(positiveEmotes, negativeEmotes, emoteCode);

        public string OverlayName(RoaDevice device, Category category) =>
            $"{(device == RoaDevice.Mobile ? mobilePrefix : pcPrefix)}_{SuffixFor(category)}";

        string SuffixFor(Category c) =>
            c == Category.Positive ? positiveSuffix : c == Category.Negative ? negativeSuffix : defaultSuffix;

        /// <summary>순수 카테고리 룩업: 긍정/부정 코드 일치(대소문자 무시), 미등록/공백=기본.</summary>
        public static Category ResolveCategory(IReadOnlyList<string> positive, IReadOnlyList<string> negative, string emoteCode)
        {
            if (Contains(positive, emoteCode)) return Category.Positive;
            if (Contains(negative, emoteCode)) return Category.Negative;
            return Category.Default;
        }

        static bool Contains(IReadOnlyList<string> list, string code)
        {
            if (list == null || string.IsNullOrWhiteSpace(code)) return false;
            string k = code.Trim();
            for (int i = 0; i < list.Count; i++)
                if (!string.IsNullOrEmpty(list[i]) && string.Equals(list[i].Trim(), k, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>테스트/에디터 셋업용 — 직렬화 필드를 코드로 채운다(런타임 미사용).</summary>
        public void Configure(string roaId, string[] positive, string[] negative, RoaDevice defaultDevice)
        {
            roaCharId = roaId;
            positiveEmotes = positive;
            negativeEmotes = negative;
            this.defaultDevice = defaultDevice;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Test Runner(EditMode) `RoaOverlaySOTests` 5개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/UI/RoaOverlaySO.cs* Assets/Tests/EditMode/RoaOverlaySOTests.cs*
git commit -m "feat(roa-overlay): 카테고리/파일명 규칙 SO 추가

Why: 표정 코드→감정 카테고리와 {device}_{category} 오버레이 파일명을 코드가
아닌 데이터로 두어, 표정 추가/이름 변경을 에셋 편집으로 흡수한다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Char Enter 디바이스 토큰 파싱

`Enter:캐릭터:표정:디바이스` 4번째 토큰을 `CharIntent.Device`로 파싱. 현재 이 토큰은 무시되므로 추가만으로 기존 분기와 충돌 없음.

**Files:**
- Modify: `Assets/_Project/Scripts/Core/Events/StageEvents.cs` (`CharIntent` 구조체)
- Modify: `Assets/_Project/Scripts/Narrative/StageParser.cs` (`ParseCharacter`)
- Test: `Assets/Tests/EditMode/StageParserTests.cs`

**Interfaces:**
- Produces: `CharIntent.Device` (string, 토큰 없으면 ""). 생성자에 `string device = ""` 추가.
- Consumes: 없음 (순수 파서)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;
using LoveAlgo.Events;
using LoveAlgo.Story;

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class StageParserTests
    {
        [Test]
        public void ParseCharacter_Enter_WithDevice()
        {
            var c = StageParser.ParseCharacter("Enter:로아:기본:pc");
            Assert.AreEqual(CharAction.Enter, c.Action);
            Assert.AreEqual("로아", c.Character);
            Assert.AreEqual("기본", c.Emote);
            Assert.AreEqual("pc", c.Device);
        }

        [Test]
        public void ParseCharacter_Enter_NoDevice_EmptyDevice()
        {
            var c = StageParser.ParseCharacter("Enter:로아:기본");
            Assert.AreEqual("기본", c.Emote);
            Assert.AreEqual("", c.Device);
        }

        [Test]
        public void ParseCharacter_Exit_NoDevice()
        {
            var c = StageParser.ParseCharacter("Exit");
            Assert.AreEqual(CharAction.Exit, c.Action);
            Assert.AreEqual("", c.Device);
        }

        [Test]
        public void ParseCharacter_EmoteShortcut_Unaffected()
        {
            var c = StageParser.ParseCharacter("로아:웃음");
            Assert.AreEqual(CharAction.Emote, c.Action);
            Assert.AreEqual("로아", c.Character);
            Assert.AreEqual("웃음", c.Emote);
            Assert.AreEqual("", c.Device);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

`CharIntent.Device` 미정의 컴파일 에러 확인.

- [ ] **Step 3: `CharIntent`에 Device 필드 추가**

`StageEvents.cs`의 `CharIntent` 구조체를 다음으로 교체(필드 + 생성자에 device 추가):

```csharp
    public readonly struct CharIntent
    {
        public readonly CharSlot Slot;
        public readonly CharAction Action;
        public readonly string Character;
        public readonly string Emote;
        public readonly float Duration;
        public readonly EmoteTarget Target;
        /// <summary>로아 등장(Enter)의 디바이스 토큰(pc/모바일). 토큰 없으면 ""(엔진이 기본/직전 디바이스 유지).</summary>
        public readonly string Device;
        public bool IsValid => Action != CharAction.Enter || !string.IsNullOrEmpty(Character);

        public CharIntent(CharSlot slot, CharAction action, string character, string emote, float duration, EmoteTarget target = EmoteTarget.Slot, string device = "")
        {
            Slot = slot;
            Action = action;
            Character = character;
            Emote = emote;
            Duration = duration;
            Target = target;
            Device = device ?? "";
        }
    }
```

- [ ] **Step 4: `StageParser.ParseCharacter`의 Enter 분기에서 디바이스 파싱**

`StageParser.cs`에서 `string emote = "";` 선언 바로 아래에 `string device = "";`를 추가하고, Enter `switch` 분기와 최종 `return`을 다음으로 교체:

```csharp
            string character = null;
            string emote = "";
            string device = "";
            switch (action)
            {
                case CharAction.Enter:
                    if (i < parts.Length) character = parts[i++].Trim();
                    if (i < parts.Length) emote = parts[i++].Trim();
                    if (i < parts.Length) device = parts[i].Trim();
                    break;
                case CharAction.Emote:
                    if (i < parts.Length) emote = parts[i].Trim();
                    break;
                // Exit/Clear: 추가 인자 없음.
            }

            return new CharIntent(slot, action, character, emote, -1f, device: device);
```

> 주의: 기존 코드는 Enter의 emote를 `parts[i]`(i++ 없이)로 읽었다. 위 교체는 `parts[i++]`로 바꿔 4번째(디바이스) 토큰을 읽을 수 있게 한다 — 동작 동일(emote 값은 변하지 않음), 디바이스만 추가.

- [ ] **Step 5: 테스트 통과 확인**

Test Runner(EditMode) `StageParserTests` 4개 PASS. (기존 `StageViewResolutionTests` 등 회귀 없음 확인.)

- [ ] **Step 6: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Events/StageEvents.cs Assets/_Project/Scripts/Narrative/StageParser.cs Assets/Tests/EditMode/StageParserTests.cs*
git commit -m "feat(roa-overlay): Char Enter 디바이스 토큰 파싱

Why: 로아 등장 시점에 pc/모바일을 CSV(Enter:캐릭터:표정:디바이스)로 지정할 수
있도록, 무시되던 4번째 토큰을 CharIntent.Device로 파싱한다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: 엔진 배선 — RoaDevice 라인 + Enter 디바이스 + 세이브 미러

새 `LineType.RoaDevice`, `NarrativeController`가 디바이스를 해석해 `SetRoaDeviceCommand` 발행 + `storyRoaDevice` 기록. 미러의 단일 작성자=엔진.

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/ScriptLine.cs` (`LineType`)
- Modify: `Assets/_Project/Scripts/Core/State/GameStateData.cs` (`storyRoaDevice`)
- Modify: `Assets/_Project/Scripts/Narrative/NarrativeController.cs` (Run switch, `PlayRoaDevice`, `PlayStageChar`, `ClearStoryPosition`)
- Verify: `Assets/_Project/Scripts/Narrative/ScriptValidator.cs` (RoaDevice 라인 미오류)
- Test: `Assets/Tests/PlayMode/NarrativeRoaDevicePlayModeTests.cs`

**Interfaces:**
- Consumes: `RoaDeviceParse`, `SetRoaDeviceCommand`, `RoaDevice` (Task 1); `CharIntent.Device` (Task 3)
- Produces: `GameStateData.storyRoaDevice` (string, 기본 ""); `LineType.RoaDevice`

- [ ] **Step 1: 실패하는 PlayMode 테스트 작성**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Events;
using LoveAlgo.Story.StoryEngine;
using LoveAlgo.Story.StoryEngine.Flow;

namespace LoveAlgo.Tests.PlayMode
{
    public class NarrativeRoaDevicePlayModeTests
    {
        GameStateSO _gs;
        GameObject _playerGo, _routerGo;
        readonly List<IDisposable> _subs = new();

        NarrativeController Make()
        {
            foreach (var p in UnityEngine.Object.FindObjectsByType<NarrativeController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var r in UnityEngine.Object.FindObjectsByType<FlowCommandController>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(r.gameObject);

            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();
            _routerGo = new GameObject("Router");
            _routerGo.AddComponent<FlowCommandController>().State = _gs;
            _playerGo = new GameObject("Player");
            var pc = _playerGo.AddComponent<NarrativeController>();
            pc.State = _gs;
            return pc;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_playerGo != null) UnityEngine.Object.DestroyImmediate(_playerGo);
            if (_routerGo != null) UnityEngine.Object.DestroyImmediate(_routerGo);
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        static IEnumerator WaitDone(NarrativeController p)
        {
            int g = 0;
            while (p.IsRunning && g++ < 600) yield return null;
        }

        [UnityTest]
        public IEnumerator RoaDevice_Line_Publishes_And_Records()
        {
            var p = Make();
            RoaDevice? got = null;
            _subs.Add(EventBus.Subscribe<SetRoaDeviceCommand>(e => got = e.Device));
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => e.Handle.Complete()));
            yield return null;

            string csv = "LineID,Type,Speaker,Value,Next\n,RoaDevice,,모바일,>\n,Text,,끝,click\n";
            EventBus.Publish(new PlayScriptCommand(csv, "t"));
            yield return WaitDone(p);

            Assert.AreEqual(RoaDevice.Mobile, got);
            Assert.AreEqual("모바일", _gs.Data.storyRoaDevice);
        }

        [UnityTest]
        public IEnumerator Enter_DeviceToken_Publishes_Before_Char()
        {
            var p = Make();
            var order = new List<string>();
            _subs.Add(EventBus.Subscribe<SetRoaDeviceCommand>(e => order.Add("device:" + e.Device)));
            _subs.Add(EventBus.Subscribe<ShowCharacterCommand>(e => { order.Add("char:" + e.Action); e.Handle?.Complete(); }));
            _subs.Add(EventBus.Subscribe<ShowDialogueCommand>(e => e.Handle.Complete()));
            yield return null;

            string csv = "LineID,Type,Speaker,Value,Next\n,Char,,Enter:roa:00:pc,await\n,Text,,끝,click\n";
            EventBus.Publish(new PlayScriptCommand(csv, "t"));
            yield return WaitDone(p);

            Assert.AreEqual("device:Pc", order[0]);
            Assert.AreEqual("char:Enter", order[1]);
            Assert.AreEqual("pc", _gs.Data.storyRoaDevice);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

`LineType.RoaDevice`/`storyRoaDevice` 미정의 컴파일 에러 확인.

- [ ] **Step 3: `LineType`에 RoaDevice 추가**

`ScriptLine.cs`의 `LineType` enum에 멤버 추가:

```csharp
        Place,      // 장소/이벤트 표시 (좌상단 배너)
        RoaDevice   // 로아 디바이스(pc/모바일) 전환 — 오버레이만 교체
```

- [ ] **Step 4: `GameStateData`에 storyRoaDevice 추가**

`GameStateData.cs`에서 `storyOverlay` 선언 바로 아래에 추가:

```csharp
        public string storyOverlay = ""; // 현재 Overlay 레이어 이름(해석된 코드ID). 빈=없음
        public string storyRoaDevice = ""; // 로아 디바이스(pc/모바일). 빈=미설정(컨트롤러 기본). 가산 확장
```

- [ ] **Step 5: `NarrativeController` Run 스위치에 RoaDevice 케이스 추가**

`case LineType.Place:` 블록 바로 아래에 추가:

```csharp
                    case LineType.RoaDevice:
                        yield return PlayRoaDevice(line);
                        cursor.MoveNext();
                        break;
```

- [ ] **Step 6: `PlayRoaDevice` 메서드 추가**

`PlayStageChar` 메서드 근처(스테이지 영역)에 추가:

```csharp
        // ── 로아 디바이스 전환(LineType.RoaDevice) ──
        // pc/모바일을 해석해 미러 기록 + SetRoaDeviceCommand 발행. 오버레이 자체는 RoaOverlayController가
        // 현재 카테고리를 유지한 채 디바이스만 교체한다(ADR-007: 엔진은 뷰를 모름). 즉시 라인(대기 없음).
        IEnumerator PlayRoaDevice(ScriptLine line)
        {
            if (RoaDeviceParse.TryParse(line.Value, out var device))
            {
                if (state != null) state.Data.storyRoaDevice = RoaDeviceParse.ToToken(device);
                EventBus.Publish(new SetRoaDeviceCommand(device));
            }
            else
            {
                Log.Warn($"[NarrativeController] 알 수 없는 RoaDevice 값 — 건너뜀: \"{line.Value}\"");
            }
            yield break;
        }
```

- [ ] **Step 7: `PlayStageChar`에서 Enter 디바이스 토큰 처리**

`PlayStageChar`의 단축-표정 early-return 블록(`if (intent.Action == CharAction.Emote && intent.Target != EmoteTarget.Slot)` … `yield break;`) **다음**, `float dur = ResolveCharDuration(intent.Action);` **앞**에 삽입:

```csharp
            // 로아 등장 디바이스 토큰(Enter:캐릭터:표정:디바이스) — SetRoaDeviceCommand를 Char 발행보다 먼저
            // 쏴서 컨트롤러가 올바른 디바이스로 오버레이를 띄우게 한다. 토큰 없으면 컨트롤러가 기본/직전 유지.
            if (intent.Action == CharAction.Enter && !string.IsNullOrEmpty(intent.Device)
                && RoaDeviceParse.TryParse(intent.Device, out var roaDev))
            {
                if (state != null) state.Data.storyRoaDevice = RoaDeviceParse.ToToken(roaDev);
                EventBus.Publish(new SetRoaDeviceCommand(roaDev));
            }

            float dur = ResolveCharDuration(intent.Action);
```

- [ ] **Step 8: `ClearStoryPosition`에서 storyRoaDevice 초기화**

`ClearStoryPosition`의 `d.storyOverlay = "";` 바로 아래에 추가:

```csharp
            d.storyOverlay = "";
            d.storyRoaDevice = "";
```

- [ ] **Step 9: ScriptValidator 확인**

`ScriptValidator.cs`를 열어 `LineType`을 exhaustive하게 분기하는 switch가 있으면 `RoaDevice`를 무해(통과) 처리하도록 케이스를 추가한다. 별도 검증이 없고 default/passthrough면 변경 불필요. (확인만 하고 필요 시 한 줄 추가.)

- [ ] **Step 10: 테스트 통과 확인 + 콘솔 에러 점검**

`read_console`로 컴파일 에러 0 확인 후, Test Runner(PlayMode) `NarrativeRoaDevicePlayModeTests` 2개 PASS.

- [ ] **Step 11: 커밋**

```bash
git add Assets/_Project/Scripts/Narrative/ScriptLine.cs Assets/_Project/Scripts/Core/State/GameStateData.cs Assets/_Project/Scripts/Narrative/NarrativeController.cs Assets/_Project/Scripts/Narrative/ScriptValidator.cs Assets/Tests/PlayMode/NarrativeRoaDevicePlayModeTests.cs*
git commit -m "feat(roa-overlay): 엔진 디바이스 해석+미러(RoaDevice/Enter 토큰)

Why: 디바이스 결정은 엔진이 단일 작성자로 storyRoaDevice에 기록하고
SetRoaDeviceCommand로만 뷰에 전달해야 ADR-007과 세이브 일관성을 지킨다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: `RoaOverlayController` (UI) — 단일 두뇌

로아 Enter/Emote/Exit + 인라인 emote + 디바이스 명령을 받아 Overlay 레이어를 구동.

**Files:**
- Create: `Assets/_Project/Scripts/UI/RoaOverlayController.cs`
- Test: `Assets/Tests/PlayMode/RoaOverlayControllerPlayModeTests.cs`

**Interfaces:**
- Consumes: `ShowCharacterCommand`, `ShowSpeakerEmoteCommand`, `SetRoaDeviceCommand`, `ShowStageLayerCommand`, `StageLayerKind`, `LayerTransition`, `CharAction`, `CharSlot`, `CompletionHandle`, `NarrativeFinishedEvent`, `ResetNarrativeViewsCommand` (모두 `LoveAlgo.Events`); `RoaOverlaySO`/`RoaDevice` (Task 1·2)
- Produces: `class LoveAlgo.UI.RoaOverlayController : MonoBehaviour` (`RoaOverlaySO Config`, `float FadeSeconds` 프로퍼티)

- [ ] **Step 1: 실패하는 PlayMode 테스트 작성**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;
using LoveAlgo.Events;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    public class RoaOverlayControllerPlayModeTests
    {
        GameObject _go;
        RoaOverlaySO _cfg;
        readonly List<IDisposable> _subs = new();
        readonly List<ShowStageLayerCommand> _cmds = new();

        RoaOverlayController Make()
        {
            _cfg = ScriptableObject.CreateInstance<RoaOverlaySO>();
            _cfg.Configure("roa", new[] { "41" }, new[] { "51" }, RoaDevice.Pc);
            _go = new GameObject("RoaOverlayController");
            var c = _go.AddComponent<RoaOverlayController>();
            c.Config = _cfg;
            c.FadeSeconds = 0f;
            _subs.Add(EventBus.Subscribe<ShowStageLayerCommand>(e =>
            {
                if (e.Kind == StageLayerKind.Overlay) _cmds.Add(e);
                e.Handle?.Complete();
            }));
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            _cmds.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_cfg != null) UnityEngine.Object.DestroyImmediate(_cfg);
        }

        static ShowCharacterCommand Char(CharAction a, string id, string emote) =>
            new ShowCharacterCommand(CharSlot.C, a, id, emote, 0f, new CompletionHandle());

        [UnityTest]
        public IEnumerator Enter_Shows_Overlay_DeviceCategory()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "00"));
            yield return null;
            Assert.AreEqual(1, _cmds.Count);
            Assert.IsFalse(_cmds[0].IsClose);
            Assert.AreEqual("pc_기본", _cmds[0].Name);
        }

        [UnityTest]
        public IEnumerator Emote_Positive_Switches_Overlay()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "00"));
            EventBus.Publish(Char(CharAction.Emote, "roa", "41"));
            yield return null;
            Assert.AreEqual("pc_긍정", _cmds[_cmds.Count - 1].Name);
        }

        [UnityTest]
        public IEnumerator InlineEmote_Switches_Overlay()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "00"));
            EventBus.Publish(new ShowSpeakerEmoteCommand("roa", "51"));
            yield return null;
            Assert.AreEqual("pc_부정", _cmds[_cmds.Count - 1].Name);
        }

        [UnityTest]
        public IEnumerator Device_Switch_Keeps_Category()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "41")); // 긍정
            EventBus.Publish(new SetRoaDeviceCommand(RoaDevice.Mobile));
            yield return null;
            Assert.AreEqual("모바일_긍정", _cmds[_cmds.Count - 1].Name);
        }

        [UnityTest]
        public IEnumerator Exit_Closes_Overlay()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "roa", "00"));
            EventBus.Publish(Char(CharAction.Exit, "roa", ""));
            yield return null;
            Assert.IsTrue(_cmds[_cmds.Count - 1].IsClose);
        }

        [UnityTest]
        public IEnumerator NonRoa_Char_Ignored()
        {
            Make();
            yield return null;
            EventBus.Publish(Char(CharAction.Enter, "c01", "00"));
            yield return null;
            Assert.AreEqual(0, _cmds.Count);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

`RoaOverlayController` 미정의 컴파일 에러 확인.

- [ ] **Step 3: 컨트롤러 구현**

```csharp
using System;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Events; // ShowCharacterCommand, ShowSpeakerEmoteCommand, SetRoaDeviceCommand, ShowStageLayerCommand, StageLayerKind, LayerTransition, CharAction, CompletionHandle, NarrativeFinishedEvent, ResetNarrativeViewsCommand, RoaDevice
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로아(Roa) 전용 오버레이 자동 결합 컨트롤러(뷰). 로아가 중앙 슬롯에 등장/표정변경/퇴장할 때 디바이스
    /// (pc/모바일)×감정 카테고리(기본/긍정/부정)에 맞는 Overlay 레이어를 자동으로 띄우고/전환/내린다.
    /// ShowCharacterCommand·인라인 ShowSpeakerEmoteCommand·SetRoaDeviceCommand를 구독해, 기존
    /// ShowStageLayerCommand(Overlay)로 StageLayerView를 구동한다(ADR-007: State 읽기/쓰기 없는 순수 뷰).
    /// 디바이스는 항상 명령으로 받으며, 세이브 복원도 GameBootstrap이 SetRoaDeviceCommand→Char Enter 순으로
    /// 재발행해 자동 재구성된다.
    /// </summary>
    public class RoaOverlayController : MonoBehaviour
    {
        [Tooltip("로아 오버레이 규칙 SO. 미바인딩 시 configResourcePath에서 로드.")]
        [SerializeField] RoaOverlaySO config;
        [Tooltip("config 미바인딩 시 Resources에서 찾을 경로.")]
        [SerializeField] string configResourcePath = "Data/RoaOverlay";
        [Tooltip("오버레이 표시/전환/종료 페이드 시간(초).")]
        [SerializeField] float fadeSeconds = 0.25f;

        public RoaOverlaySO Config { get => config; set => config = value; }
        public float FadeSeconds { get => fadeSeconds; set => fadeSeconds = value; }

        IDisposable _charSub, _emoteSub, _deviceSub, _finishSub, _resetSub;
        RoaDevice _device;
        RoaOverlaySO.Category _category;
        bool _present;

        void OnEnable()
        {
            if (config == null && !string.IsNullOrEmpty(configResourcePath))
                config = Resources.Load<RoaOverlaySO>(configResourcePath);

            _device = config != null ? config.DefaultDevice : RoaDevice.Pc;
            _category = RoaOverlaySO.Category.Default;
            _present = false;

            _charSub = EventBus.Subscribe<ShowCharacterCommand>(OnChar);
            _emoteSub = EventBus.Subscribe<ShowSpeakerEmoteCommand>(OnSpeakerEmote);
            _deviceSub = EventBus.Subscribe<SetRoaDeviceCommand>(OnDevice);
            _finishSub = EventBus.Subscribe<NarrativeFinishedEvent>(_ => ResetState());
            _resetSub = EventBus.Subscribe<ResetNarrativeViewsCommand>(_ => ResetState());
        }

        void OnDisable()
        {
            _charSub?.Dispose(); _emoteSub?.Dispose(); _deviceSub?.Dispose(); _finishSub?.Dispose(); _resetSub?.Dispose();
            _charSub = _emoteSub = _deviceSub = _finishSub = _resetSub = null;
        }

        bool IsRoa(string id) =>
            config != null && !string.IsNullOrEmpty(id)
            && string.Equals(id.Trim(), config.RoaCharId, StringComparison.OrdinalIgnoreCase);

        void OnDevice(SetRoaDeviceCommand e)
        {
            _device = e.Device;
            if (_present) ShowOverlay(); // 같은 카테고리, 새 디바이스
        }

        void OnChar(ShowCharacterCommand e)
        {
            if (config == null || !IsRoa(e.Character)) return;
            switch (e.Action)
            {
                case CharAction.Enter:
                    _category = config.ResolveCategory(e.Emote);
                    _present = true;
                    ShowOverlay();
                    break;
                case CharAction.Emote:
                    ApplyCategory(config.ResolveCategory(e.Emote));
                    break;
                case CharAction.Exit:
                case CharAction.Clear:
                    _present = false;
                    HideOverlay();
                    break;
            }
        }

        void OnSpeakerEmote(ShowSpeakerEmoteCommand e)
        {
            if (config == null || !_present || !IsRoa(e.Speaker) || string.IsNullOrEmpty(e.Emote)) return;
            ApplyCategory(config.ResolveCategory(e.Emote));
        }

        void ApplyCategory(RoaOverlaySO.Category cat)
        {
            if (!_present || cat == _category) return;
            _category = cat;
            ShowOverlay();
        }

        void ShowOverlay()
        {
            string name = config.OverlayName(_device, _category);
            EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.Overlay, false, name, LayerTransition.Fade, fadeSeconds, new CompletionHandle()));
        }

        void HideOverlay()
        {
            EventBus.Publish(new ShowStageLayerCommand(StageLayerKind.Overlay, true, null, LayerTransition.Fade, fadeSeconds, new CompletionHandle()));
        }

        // 내러티브 종료/도구 리셋: StageLayerView가 오버레이 이미지를 ClearAll하므로 여기선 런타임 상태만 비운다.
        // 디바이스는 다음 SetRoaDeviceCommand까지 유지(잔여 무해 — 로아 부재 시 아무 것도 그리지 않음).
        void ResetState()
        {
            _present = false;
            _category = RoaOverlaySO.Category.Default;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

`read_console` 컴파일 에러 0 확인 후, Test Runner(PlayMode) `RoaOverlayControllerPlayModeTests` 6개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/UI/RoaOverlayController.cs* Assets/Tests/PlayMode/RoaOverlayControllerPlayModeTests.cs*
git commit -m "feat(roa-overlay): 오버레이 자동 결합 컨트롤러 추가

Why: 로아 등장/표정(명시+인라인)/디바이스/퇴장을 한 곳에서 받아 Overlay
레이어를 구동해, 얼굴과 오버레이 감정이 항상 일치하도록 한다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: 세이브 복원 배선 + 라운드트립

복원 시 `SetRoaDeviceCommand`를 로아 `Char Enter` 재발행보다 먼저 쏴서 오버레이를 재구성.

**Files:**
- Modify: `Assets/_Project/Scripts/Game/GameBootstrap.cs` (`TryResumeStory`)
- Test: `Assets/Tests/PlayMode/GameBootstrapRoaRestorePlayModeTests.cs`
- Test: `Assets/Tests/EditMode/GameStateRoaDeviceTests.cs`

**Interfaces:**
- Consumes: `RoaDeviceParse`, `SetRoaDeviceCommand` (Task 1); `GameStateData.storyRoaDevice` (Task 4)

- [ ] **Step 1: 실패하는 테스트 작성 (복원 순서)**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Common;
using LoveAlgo.Core;
using LoveAlgo.Events;
using LoveAlgo.Game;

namespace LoveAlgo.Tests.PlayMode
{
    public class GameBootstrapRoaRestorePlayModeTests
    {
        GameObject _go;
        GameStateSO _gs;
        readonly List<IDisposable> _subs = new();
        readonly List<string> _order = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            _order.Clear();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_gs != null) UnityEngine.Object.DestroyImmediate(_gs);
        }

        [UnityTest]
        public IEnumerator Restore_Publishes_Device_Before_RoaEnter()
        {
            _gs = ScriptableObject.CreateInstance<GameStateSO>();
            _gs.ResetRuntime();
            _gs.Data.storyScriptId = "prologue";
            _gs.Data.storyLineIndex = 0;
            _gs.Data.storyRoaDevice = "모바일";
            _gs.Data.storyChars.Add(new GameStateData.StoryCharRecord { slot = (int)CharSlot.C, id = "roa", emote = "41" });

            _go = new GameObject("GameBootstrap");
            var b = _go.AddComponent<GameBootstrap>();
            b.State = _gs;
            b.PrologueCsv = ""; // 복원 후 프롤로그 재생 분기를 무력화(스냅샷 발행만 검증)

            _subs.Add(EventBus.Subscribe<SetRoaDeviceCommand>(e => _order.Add("device:" + e.Device)));
            _subs.Add(EventBus.Subscribe<ShowCharacterCommand>(e => _order.Add("char:" + e.Character)));
            yield return null;

            b.TryResumeStory();
            yield return null;

            int di = _order.IndexOf("device:Mobile");
            int ci = _order.IndexOf("char:roa");
            Assert.GreaterOrEqual(di, 0, "디바이스 명령이 발행되어야 함");
            Assert.GreaterOrEqual(ci, 0, "로아 Enter가 발행되어야 함");
            Assert.Less(di, ci, "디바이스가 로아 Enter보다 먼저여야 함");
        }
    }
}
```

- [ ] **Step 2: 실패하는 테스트 작성 (직렬화 라운드트립)**

```csharp
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class GameStateRoaDeviceTests
    {
        [Test]
        public void StoryRoaDevice_Default_Empty()
        {
            var d = new GameStateData();
            Assert.AreEqual("", d.storyRoaDevice);
        }

        [Test]
        public void StoryRoaDevice_RoundTrips_Json()
        {
            var d = new GameStateData { storyRoaDevice = "모바일" };
            var json = JsonUtility.ToJson(d);
            var d2 = JsonUtility.FromJson<GameStateData>(json);
            Assert.AreEqual("모바일", d2.storyRoaDevice);
        }
    }
}
```

- [ ] **Step 3: 테스트 실패 확인**

`GameBootstrapRoaRestorePlayModeTests`는 디바이스 명령 미발행으로 FAIL(또는 `di < ci` 위반). 라운드트립 테스트는 Task 4가 끝났다면 PASS일 수 있다 — 그래도 같은 커밋으로 묶어 회귀 가드.

- [ ] **Step 4: `TryResumeStory`에 디바이스 선발행 추가**

`GameBootstrap.cs`의 `foreach (var c in d.storyChars)` 루프 **바로 위**에 삽입:

```csharp
            // 로아 디바이스 복원 — 로아 Char Enter 재발행 전에 쏴서 컨트롤러가 올바른 디바이스로 오버레이를
            // 재구성하게 한다(오버레이 이름은 별도 저장하지 않고 디바이스+표정으로 파생).
            if (!string.IsNullOrEmpty(d.storyRoaDevice) && RoaDeviceParse.TryParse(d.storyRoaDevice, out var roaDev))
                EventBus.Publish(new SetRoaDeviceCommand(roaDev));

            foreach (var c in d.storyChars)
```

(`GameBootstrap.cs`는 이미 `using LoveAlgo.Events;` 포함 — `RoaDeviceParse`/`SetRoaDeviceCommand` 사용 가능.)

- [ ] **Step 5: 테스트 통과 확인**

Test Runner: `GameBootstrapRoaRestorePlayModeTests` 1개 + `GameStateRoaDeviceTests` 2개 PASS.

- [ ] **Step 6: 커밋**

```bash
git add Assets/_Project/Scripts/Game/GameBootstrap.cs Assets/Tests/PlayMode/GameBootstrapRoaRestorePlayModeTests.cs* Assets/Tests/EditMode/GameStateRoaDeviceTests.cs*
git commit -m "feat(roa-overlay): 세이브 복원 시 디바이스 선발행

Why: 오버레이는 저장하지 않고 디바이스+표정으로 파생하므로, 복원 때
SetRoaDeviceCommand를 로아 Enter보다 먼저 쏴 오버레이를 정확히 재구성한다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 7: 에셋 생성 + 씬 배선 (🟡 에디터 작업)

코드는 완성. SO 인스턴스 생성 + 컨트롤러를 Game 씬에 배치해 실제 동작을 확인.

**Files:**
- Create: `Assets/Resources/Data/RoaOverlay.asset` (메뉴: `Assets > Create > LoveAlgo > Roa Overlay`)
- Modify: Game 씬(`Assets/_Project/Scenes/Game.unity` 등 실제 게임 씬) — `RoaOverlayController` 컴포넌트 부착

- [ ] **Step 1: SO 에셋 생성/설정**

1. Project 창에서 `Assets/Resources/Data/` 로 이동(없으면 폴더 생성).
2. `Assets > Create > LoveAlgo > Roa Overlay` → 이름 `RoaOverlay`.
3. 인스펙터:
   - `Roa Char Id` = `roa` (별칭 카탈로그의 로아 코드 ID와 일치하는지 확인).
   - `Positive Emotes` = 로아의 긍정 표정 **코드들**(예: `41`, `42` — 실제 로아 표정 코드로 채울 것. 작가/기획 확인).
   - `Negative Emotes` = 로아의 부정 표정 코드들.
   - 접두/접미(`pc`/`모바일`/`기본`/`긍정`/`부정`)는 기본값 유지(파일명과 일치).
   - `Default Device` = `Pc`.

> 미확정 표정 코드는 비워두면 전부 기본으로 안전 폴백된다 — 추후 코드만 추가하면 됨.

- [ ] **Step 2: 컨트롤러 씬 배치**

1. Game 씬을 연다.
2. `StageLayerView`(overlayImage 보유)와 같은 연출 캔버스 계층에 빈 GameObject `RoaOverlayController` 생성.
3. `RoaOverlayController` 컴포넌트 부착 → `Config`에 위 `RoaOverlay.asset` 드래그(또는 `Config Resource Path`=`Data/RoaOverlay` 폴백 사용).
4. `Fade Seconds` 기본(0.25) 유지.

- [ ] **Step 3: 에디터 동작 확인(연출 증거)**

임시 CSV 또는 스토리 도구로 다음을 재생하고 `Resources/Overlay`의 알맞은 이미지가 뜨는지 눈으로 확인:

```
LineID,Type,Speaker,Value,Next
,Char,,Enter:로아:기본:pc,await
,Text,로아,안녕! 만나서 반가워,click
,Char,,로아:긍정표정코드,await
,RoaDevice,,모바일,>
,Text,로아,이제 폰으로 보고 있어,click
,Char,,Exit,await
```

확인 항목: ① Enter 시 `pc_기본` 표시 ② 긍정 표정 시 `pc_긍정` 전환 ③ `RoaDevice,모바일` 시 `모바일_긍정`으로 디바이스만 교체 ④ Exit 시 오버레이 사라짐 ⑤ 스토리 종료(`NarrativeFinishedEvent`) 후 잔여 오버레이 없음.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Resources/Data/RoaOverlay.asset* Assets/_Project/Scenes/
git commit -m "chore(roa-overlay): RoaOverlay SO 에셋+씬 배선

Why: 로아 오버레이 자동 결합을 실제 게임 씬에 연결하고, 표정 카테고리/디바이스
규칙 에셋을 배치해 런타임에서 동작하게 한다.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**1. Spec coverage**

| 설계서 요구 | 담당 Task |
|---|---|
| 로아 등장 시 디바이스×카테고리 오버레이 자동 표시 | Task 5(Enter) + Task 7(에셋) |
| 표정 변경 시 카테고리 전환(명시 Char Emote) | Task 5(Emote) |
| 인라인 `<emote>` 시 전환 | Task 5(OnSpeakerEmote) |
| 퇴장/Clear 시 오버레이 동반 종료 | Task 5(Exit/Clear) |
| 씬 정리(Finished/Reset)에서 정리 | Task 5(ResetState) + StageLayerView 기존 ClearAll |
| 디바이스 등장 시 CSV 지정 | Task 3(파싱) + Task 4(엔진) |
| 디바이스 전환 명령 | Task 4(`LineType.RoaDevice`/`PlayRoaDevice`) |
| 카테고리 SO(긍정/부정 나열, 나머지 기본) | Task 2 |
| 세이브/복원(storyRoaDevice 가산, 파생 복원) | Task 4 + Task 6 |
| 컨트롤러=순수 뷰, 엔진=미러 단일 작성자 | Task 4/5 구조 |

누락 없음.

**2. Placeholder scan**

플레이스홀더 없음. (Task 7의 표정 코드는 의도된 기획 입력값 — 비워도 안전 폴백되며 절차가 명시됨.)

**3. Type consistency**

- `RoaDevice`(Events), `SetRoaDeviceCommand`(Events), `RoaDeviceParse.TryParse/ToToken`(Events) — Task 1 정의, Task 4·5·6 사용 일치.
- `RoaOverlaySO.Category`/`ResolveCategory`/`OverlayName`/`Configure`/`RoaCharId`/`DefaultDevice` — Task 2 정의, Task 5 사용 일치.
- `CharIntent.Device` — Task 3 정의, Task 4 사용 일치.
- `GameStateData.storyRoaDevice` — Task 4 정의, Task 6 사용 일치.
- `RoaOverlayController.Config`/`FadeSeconds` — Task 5 정의, 테스트 사용 일치.
- `LineType.RoaDevice`(멤버)와 `RoaDevice`(타입)는 별개 스코프 — 충돌 없음.

이상 없음.
