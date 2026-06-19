# 캐릭터 폴더 구조 + 표정-만-명시 문법 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Resources/Characters`를 캐릭터별 폴더 + 한글 표정 파일 구조로 전환해 로딩을 복구하고, 스토리 CSV에서 표정만으로 화자 캐릭터의 표정을 바꾸는 단축 문법(`Roa:웃음`, `웃음`, 인라인 `<웃음>`)을 추가한다.

**Architecture:** 런타임 식별자를 폴더/파일명(`Roa`/`기본`)으로 통일하고 구 코드(`c01`/`00`)는 카탈로그 별칭으로 보존한다(기존 세이브 호환). 해석은 기존대로 엔진(NarrativeController)이 발행 직전에 수행하고 뷰(StageView)는 `Characters/{char}/{emote}` 컨벤션으로 로드한다. 표정-by-식별자/직전화자 라우팅은 순수 파서(StageParser)가 인텐트로 분해하고 엔진이 `storyChars` 상태로 슬롯을 찾아 기존 `ShowCharacterCommand(Emote)` / `ShowSpeakerEmoteCommand` 경로를 재사용한다.

**Tech Stack:** Unity 6000.5 LTS, C#, NUnit (EditMode/PlayMode), EventBus(구조체 이벤트) + State SO.

## Global Constraints

- 피처 간 직접 참조 금지 — 교차통신은 EventBus + State SO만 경유 (ADR-007).
- Obsolete API 금지 (Unity 6 LTS 기준).
- 로깅: 디버그는 `LoveAlgo.Common.Log.Info/Warn`, 사용자 보고용만 `Log.Error`/`Debug.LogError`.
- **최우선: 기존 기능 전부 이전처럼 작동** — 인라인 `<emote=표정/>`, `C:Enter:로아:기본:Mob`, `C:Emote:찌릿:PC`, Setup 매크로, 구 세이브(c01/00) 복원, 로그 초상, 히로인 배치.
- 한 기능 = 한 커밋. 커밋 본문에 Why 명시. 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- 스크립트 수정 후 `read_console`로 컴파일 에러 확인 후 진행(UnityMCP).

---

## File Structure

| 파일 | 책임 | 변경 |
|---|---|---|
| `Assets/Resources/Data/ResourceAliasCatalog.asset` | 별칭→id 데이터 | 캐릭터/표정 id를 폴더·파일명으로, 구 코드를 별칭으로; defaultEmote=기본 |
| `Assets/_Project/Scripts/UI/StageView.cs` | 캐릭터 스프라이트 로드 | 경로 `{char}_{emote}`→`{char}/{emote}`; 순수 키 헬퍼 추출 |
| `Assets/_Project/Scripts/Core/Events/StageEvents.cs` | Char 인텐트/enum | `EmoteTarget` enum + `CharIntent.Target` 필드 |
| `Assets/_Project/Scripts/Narrative/StageParser.cs` | Char Value 파싱 | 액션 키워드 없을 때 단축 문법(`캐릭터:표정`, `표정`) |
| `Assets/_Project/Scripts/Narrative/NarrativeController.cs` | 해석/발행 | 직전화자 추적 + 식별자/직전화자 Emote 라우팅 |
| `Assets/_Project/Scripts/Narrative/InlineTagParser.cs` | 대사 인라인 태그 | 단일토큰 `<웃음>`→emote |
| `Assets/Resources/Data/CharacterStageCatalog.asset` | 히로인 배치 데이터 | characterId 키 c01→Roa 등 |
| `Assets/_Project/Scripts/UI/DialogueLogView.cs`(프리팹 직렬화) | 로그 초상 맵 | portraits speakerId 키 c01→Roa 등 |
| `Assets/StreamingAssets/Story/Prologue.csv` | 프롤로그 대본 | `활짝웃음`→`활짝` |
| `Assets/Tests/...` | 테스트 | 파서/카탈로그/인라인/뷰 테스트 갱신·신규 |

---

## Task 1: 카탈로그 데이터 — 캐릭터/표정 id를 폴더·파일명으로, 구 코드는 별칭 보존

**Files:**
- Modify: `Assets/Resources/Data/ResourceAliasCatalog.asset` (characters 34~54, emotes 55~128, defaultEmote 128)
- Test: `Assets/Tests/EditMode/ResourceAliasCatalogTests.cs`, `Assets/Tests/EditMode/ResourceAliasCatalogIntegrityTests.cs`, `Assets/Tests/PlayMode/AliasResolutionPlayModeTests.cs`

**Interfaces:**
- Consumes: `ResourceAliasCatalogSO.ResolveCharacter(string)`, `.ResolveEmote(string)`, `.TryResolveCharacter(string,out string)`, `.DefaultEmote` (변경 없음 — 순수 룩업).
- Produces: 해석 결과가 폴더/파일명. `ResolveCharacter("로아")=="Roa"`, `ResolveCharacter("c01")=="Roa"`, `ResolveEmote("기본")=="기본"`, `ResolveEmote("00")=="기본"`, `ResolveEmote("활짝웃음")=="활짝"`, `DefaultEmote=="기본"`.

- [ ] **Step 1: 실패 테스트 작성** — `ResourceAliasCatalogTests.cs`에 추가(클래스 내부). 카탈로그를 코드로 구성해 순수 Resolve 검증:

```csharp
[Test]
public void Character_Alias_And_LegacyCode_Resolve_To_Folder()
{
    var entries = new System.Collections.Generic.List<ResourceAliasCatalogSO.Entry>
    {
        new ResourceAliasCatalogSO.Entry { id = "Roa", aliases = new[] { "로아", "Roa", "c01" } },
    };
    Assert.AreEqual("Roa", ResourceAliasCatalogSO.Resolve(entries, "로아"));
    Assert.AreEqual("Roa", ResourceAliasCatalogSO.Resolve(entries, "c01")); // 구 세이브 코드
    Assert.AreEqual("Roa", ResourceAliasCatalogSO.Resolve(entries, "Roa"));
}

[Test]
public void Emote_Alias_And_LegacyCode_Resolve_To_Filename()
{
    var entries = new System.Collections.Generic.List<ResourceAliasCatalogSO.Entry>
    {
        new ResourceAliasCatalogSO.Entry { id = "기본", aliases = new[] { "Default", "00" } },
        new ResourceAliasCatalogSO.Entry { id = "활짝", aliases = new[] { "활짝웃음", "13" } },
    };
    Assert.AreEqual("기본", ResourceAliasCatalogSO.Resolve(entries, "00"));   // 구 코드
    Assert.AreEqual("활짝", ResourceAliasCatalogSO.Resolve(entries, "활짝웃음")); // 콘텐츠 별칭
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run (UnityMCP): `run_tests` mode=EditMode, filter=`ResourceAliasCatalogTests`
Expected: 위 2개 테스트는 통과(순수 Resolve는 이미 동작) — 이 단계는 **회귀 가드**다. 만약 `Resolve` 시그니처가 달라 컴파일 실패면 즉시 멈춤.

> 주: 위 테스트는 in-memory 엔트리라 항상 통과. **실 데이터(.asset) 검증은 Step 3 이후 통합/PlayMode 테스트가 담당.**

- [ ] **Step 3: ResourceAliasCatalog.asset 편집 — characters 섹션** (라인 34~54). 각 `id`를 폴더명으로 바꾸고 구 코드를 aliases에 추가:

```yaml
  characters:
  - id: Roa
    aliases:
    - "로아"   # 로아
    - Roa
    - c01
  - id: Daeun
    aliases:
    - "서다은" # 서다은
    - SeoDaEun
    - Daeun
    - c02
  - id: Yeeun
    aliases:
    - "하예은" # 하예은
    - HaYeEun
    - Yeeun
    - c03
  - id: Heewon
    aliases:
    - "도희원" # 도희원
    - DoHeewon
    - Heewon
    - c04
  - id: Bom
    aliases:
    - "이봄"   # 이봄
    - LeeBom
    - Bom
    - c05
```

- [ ] **Step 4: ResourceAliasCatalog.asset 편집 — emotes 섹션** (라인 55~128). 각 `id`를 한글 파일명으로, 구 코드 숫자를 aliases에 추가. 파일이 실제 존재하는 표정 위주로 정확히, 나머지는 한글명+코드 유지. 매핑:

| 신 id | aliases(구 코드 포함) |
|---|---|
| 기본 | Default, 00 |
| 눈웃음 | 11 |
| 밝게웃음 | BrightSmile, 12 |
| 활짝 | 활짝웃음, 13 |
| 행복 | Happy, 14 |
| 찌릿 | 21 |
| 쟀짐 | 22 |
| 머쓱 | 23 |
| 어질어질 | 24 |
| 울먹 | 31 |
| 주르륵 | 32 |
| 와아앙 | 33 |
| 부끄 | 34 |
| 졸려 | 35 |
| 깜짝 | 41 |
| 반짝빈짝 | 42 |
| 궁금 | 43 |
| 윙크 | 44 |
| 자신만만 | 45 |
| 음주 | 55 |
| 만취 | 56 |
| 집중 | 57 |
| 고민 | 58 |

> 표의 한글명은 기존 asset의 첫 별칭과 동일(유니코드 이스케이프는 기존 값을 그대로 이동). `활짝`의 별칭에 `활짝웃음` 추가가 콘텐츠 갭 보정.

그리고 `defaultEmote`를 변경:

```yaml
  defaultEmote: "기본"   # 기본 (구: 00)
```

- [ ] **Step 5: 기존 해석 테스트 기대값 갱신** — `AliasResolutionPlayModeTests.cs`에서 `로아→c01`/`기본→00` 등 구 코드 기대값을 신 값(`로아→Roa`, `기본→기본`, `c01→Roa`)으로 수정. `ResourceAliasCatalogIntegrityTests.cs`가 특정 id(`c01`)를 단언하면 신 id로 수정.

```
# 파일 내 해당 Assert를 찾아 기대 문자열 교체 (예시)
Assert.AreEqual("Roa", catalog.ResolveCharacter("로아"));
Assert.AreEqual("Roa", catalog.ResolveCharacter("c01")); // 구 세이브 호환
Assert.AreEqual("기본", catalog.ResolveEmote("기본"));
Assert.AreEqual("기본", catalog.ResolveEmote("00"));
```

- [ ] **Step 6: 테스트 실행 + 컴파일 확인**

Run (UnityMCP): `run_tests` mode=EditMode filter=`ResourceAliasCatalog`; 이어서 `run_tests` mode=PlayMode filter=`AliasResolution`. 그 전 `read_console`로 컴파일 클린 확인.
Expected: PASS.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Resources/Data/ResourceAliasCatalog.asset Assets/Tests/EditMode/ResourceAliasCatalogTests.cs Assets/Tests/EditMode/ResourceAliasCatalogIntegrityTests.cs Assets/Tests/PlayMode/AliasResolutionPlayModeTests.cs
git commit -m "feat(narrative): 카탈로그 id를 폴더/파일명으로 통일, 구 코드는 별칭 보존

왜: Characters 폴더 재편(Roa/기본.png)에 맞춰 해석 결과가 폴더/파일명이
되도록. 구 코드(c01/00)는 별칭으로 남겨 기존 세이브가 그대로 복원되게 함.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: StageView 로딩 경로 `{char}/{emote}`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/StageView.cs:415-420` (`LoadCharSprite`)
- Test: `Assets/Tests/EditMode/StageViewKeyTests.cs` (신규)

**Interfaces:**
- Produces: `public static string CharSpriteKey(string character, string emote)` — `"{character}/{emote}"`, character 비면 null, emote 비면 character만.

- [ ] **Step 1: 실패 테스트 작성** — 신규 `Assets/Tests/EditMode/StageViewKeyTests.cs`:

```csharp
using NUnit.Framework;
using LoveAlgo.UI; // StageView

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class StageViewKeyTests
    {
        [Test]
        public void Key_Joins_Char_And_Emote_With_Slash()
        {
            Assert.AreEqual("Roa/기본", StageView.CharSpriteKey("Roa", "기본"));
        }

        [Test]
        public void Key_Null_When_Character_Empty()
        {
            Assert.IsNull(StageView.CharSpriteKey("", "기본"));
            Assert.IsNull(StageView.CharSpriteKey(null, "기본"));
        }

        [Test]
        public void Key_CharOnly_When_Emote_Empty()
        {
            Assert.AreEqual("Roa", StageView.CharSpriteKey("Roa", ""));
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run (UnityMCP): `run_tests` mode=EditMode filter=`StageViewKeyTests`
Expected: FAIL — `CharSpriteKey` 미정의(컴파일 에러).

- [ ] **Step 3: 최소 구현** — `StageView.cs`의 `LoadCharSprite`를 순수 키 헬퍼 사용으로 교체:

```csharp
/// <summary>캐릭터/표정 → Resources 하위 경로 키(순수). 폴더 구조 Characters/{char}/{emote}.</summary>
public static string CharSpriteKey(string character, string emote)
{
    if (string.IsNullOrEmpty(character)) return null;
    return string.IsNullOrEmpty(emote) ? character : $"{character}/{emote}";
}

Sprite LoadCharSprite(string character, string emote)
{
    string key = CharSpriteKey(character, emote);
    return key == null ? null : LoadSprite($"{charRoot}/{key}");
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run (UnityMCP): `run_tests` mode=EditMode filter=`StageViewKeyTests`
Expected: PASS. `read_console` 컴파일 클린.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/UI/StageView.cs Assets/Tests/EditMode/StageViewKeyTests.cs
git commit -m "feat(ui): StageView 캐릭터 로딩 경로 {char}/{emote}로 전환

왜: Characters 폴더 재편(Roa/기본.png)에 맞춰 슬래시 경로로 로드. 순수 키
헬퍼로 추출해 EditMode 검증 가능하게 함.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: StageParser — Char Value 단축 문법(`캐릭터:표정`, `표정`)

**Files:**
- Modify: `Assets/_Project/Scripts/Core/Events/StageEvents.cs:24-71` (`CharIntent` + 신규 enum)
- Modify: `Assets/_Project/Scripts/Narrative/StageParser.cs:37-73` (`ParseCharacter`)
- Test: `Assets/Tests/EditMode/StageParserTests.cs`

**Interfaces:**
- Produces:
  - `enum EmoteTarget { Slot, Character, LastSpeaker }` (StageEvents.cs, namespace `LoveAlgo.Events`).
  - `CharIntent.Target` (readonly `EmoteTarget`). 기존 생성자에 인자 추가: `CharIntent(slot, action, character, emote, duration, target)`. 비-Emote/기존 Emote는 `EmoteTarget.Slot`.
  - `ParseCharacter("Roa:웃음")` → Action=Emote, Character="Roa", Emote="웃음", Target=Character.
  - `ParseCharacter("웃음")` → Action=Emote, Character=null, Emote="웃음", Target=LastSpeaker.
  - `ParseCharacter("C:Emote:21")` → Target=Slot (회귀).

- [ ] **Step 1: 실패 테스트 작성** — `StageParserTests.cs`에 추가:

```csharp
[Test]
public void Char_Shorthand_CharColonEmote_Is_Identity_Emote()
{
    var c = StageParser.ParseCharacter("Roa:웃음");
    Assert.IsTrue(c.IsValid);
    Assert.AreEqual(CharAction.Emote, c.Action);
    Assert.AreEqual("Roa", c.Character);
    Assert.AreEqual("웃음", c.Emote);
    Assert.AreEqual(EmoteTarget.Character, c.Target);
}

[Test]
public void Char_Shorthand_BareEmote_Targets_LastSpeaker()
{
    var c = StageParser.ParseCharacter("웃음");
    Assert.IsTrue(c.IsValid);
    Assert.AreEqual(CharAction.Emote, c.Action);
    Assert.IsNull(c.Character);
    Assert.AreEqual("웃음", c.Emote);
    Assert.AreEqual(EmoteTarget.LastSpeaker, c.Target);
}

[Test]
public void Char_Explicit_Emote_Keeps_Slot_Target()
{
    var c = StageParser.ParseCharacter("C:Emote:21");
    Assert.AreEqual(CharAction.Emote, c.Action);
    Assert.AreEqual(EmoteTarget.Slot, c.Target);
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run (UnityMCP): `run_tests` mode=EditMode filter=`StageParserTests`
Expected: FAIL — `EmoteTarget`/`.Target` 미정의(컴파일 에러).

- [ ] **Step 3: StageEvents.cs 편집** — enum 추가 + CharIntent 확장:

```csharp
/// <summary>Emote 액션의 대상 결정 방식(슬라이스: 단축 문법). Slot=명시/기존, Character=식별자, LastSpeaker=직전 화자.</summary>
public enum EmoteTarget { Slot, Character, LastSpeaker }
```

`CharIntent`에 필드/생성자 인자 추가(기존 4 필드 아래):

```csharp
public readonly EmoteTarget Target;
// ...
public CharIntent(CharSlot slot, CharAction action, string character, string emote, float duration, EmoteTarget target = EmoteTarget.Slot)
{
    Slot = slot;
    Action = action;
    Character = character;
    Emote = emote;
    Duration = duration;
    Target = target;
}
```

> `IsValid`는 그대로(Emote는 Action!=Enter라 true). LastSpeaker도 emote 토큰이 있으므로 유효.

- [ ] **Step 4: StageParser.cs 편집** — 액션 미인식 분기를 단축 문법으로 교체. 현재 라인 53~55(`if (i >= parts.Length || !TryParseAction(...)) return invalid`)를:

```csharp
            // 액션 토큰이 아니면 → 단축 문법 분기.
            if (i >= parts.Length || !TryParseAction(parts[i], out CharAction action))
            {
                // 슬롯만 있고 더 없음 → 무효(기존 "L" 케이스 보존).
                int remaining = parts.Length - i;
                if (remaining <= 0) return new CharIntent(slot, CharAction.Enter, null, "", -1f);
                if (remaining >= 2)
                {
                    // 캐릭터:표정 → 식별자 Emote.
                    string ch = parts[i].Trim();
                    string em = parts[i + 1].Trim();
                    return new CharIntent(slot, CharAction.Emote, ch, em, -1f, EmoteTarget.Character);
                }
                // 단일 토큰 표정 → 직전 화자 Emote.
                return new CharIntent(slot, CharAction.Emote, null, parts[i].Trim(), -1f, EmoteTarget.LastSpeaker);
            }
            i++;
```

> 주의: 기존 `ParseCharacter("Enter")`(캐릭터 없는 Enter)는 액션 인식되어 아래 switch로 가고 Character=null→IsValid=false 유지. `"L"`(슬롯만)은 remaining<=0 → 무효 유지. 기존 invalid 케이스 회귀 없음.

기존 switch의 정상 경로(Enter/Emote/Exit/Clear)는 그대로 두되, 마지막 `return`에 Target 기본값(Slot) 적용 — 생성자 기본 인자라 변경 불필요. 즉 라인 72 `return new CharIntent(slot, action, character, emote, -1f);`는 그대로(Target=Slot).

- [ ] **Step 5: 테스트 통과 확인**

Run (UnityMCP): `run_tests` mode=EditMode filter=`StageParserTests`
Expected: PASS(신규 3 + 기존 전부). `read_console` 클린.

- [ ] **Step 6: 커밋**

```bash
git add Assets/_Project/Scripts/Core/Events/StageEvents.cs Assets/_Project/Scripts/Narrative/StageParser.cs Assets/Tests/EditMode/StageParserTests.cs
git commit -m "feat(narrative): Char 단축 문법 — 캐릭터:표정 / 표정만(직전화자)

왜: 작가가 Emote: 키워드 없이 표정만 명시해 화자 표정을 바꾸도록. EmoteTarget로
슬롯(기존)·식별자·직전화자 라우팅을 구분해 기존 C:Emote:x 회귀 없이 확장.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: NarrativeController — 직전 화자 추적 + 식별자/직전화자 Emote 라우팅

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/NarrativeController.cs` (PlayText 323~350, PlayStageChar 516~531, 신규 헬퍼)
- Test: `Assets/Tests/EditMode/StoryCharSlotLookupTests.cs` (신규, 순수 헬퍼)

**Interfaces:**
- Consumes: `StageParser.ParseCharacter` (Task 3), `state.Data.storyChars` (`List<StoryCharRecord>{ int slot; string id; string emote; }`), `ResolveSpeakerId`, `ResolveCharEmote`, `aliasCatalog`.
- Produces:
  - `static int FindSlotForCharId(IReadOnlyList<GameStateData.StoryCharRecord> chars, string id)` — id(대소문자 무시) 일치 레코드의 slot, 없으면 -1.
  - `_lastSpeakerId` 필드(직전 Text 라인의 화자 코드 id, 미등록이면 null).
  - PlayStageChar가 `EmoteTarget.Character`/`LastSpeaker`일 때 대상 슬롯을 찾아 `ShowCharacterCommand(slot, Emote, id, emoteFile)` 발행(+RecordChar). 무대에 없으면 `Log.Info` 후 스킵.

- [ ] **Step 1: 실패 테스트 작성** — 신규 `Assets/Tests/EditMode/StoryCharSlotLookupTests.cs`:

```csharp
using NUnit.Framework;
using System.Collections.Generic;
using LoveAlgo.Narrative;       // NarrativeController
using LoveAlgo.Core.State;      // GameStateData

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class StoryCharSlotLookupTests
    {
        static List<GameStateData.StoryCharRecord> Sample() => new()
        {
            new GameStateData.StoryCharRecord { slot = 1, id = "Roa", emote = "기본" },
            new GameStateData.StoryCharRecord { slot = 0, id = "Yeeun", emote = "기본" },
        };

        [Test]
        public void Finds_Slot_By_Id_CaseInsensitive()
        {
            Assert.AreEqual(1, NarrativeController.FindSlotForCharId(Sample(), "roa"));
            Assert.AreEqual(0, NarrativeController.FindSlotForCharId(Sample(), "Yeeun"));
        }

        [Test]
        public void Returns_Minus1_When_Not_On_Stage()
        {
            Assert.AreEqual(-1, NarrativeController.FindSlotForCharId(Sample(), "Bom"));
            Assert.AreEqual(-1, NarrativeController.FindSlotForCharId(Sample(), null));
        }
    }
}
```

> 네임스페이스/타입 경로는 실제 파일 상단 `namespace`와 `StoryCharRecord` 접근성으로 맞출 것(`GameStateData.StoryCharRecord`가 public인지 확인; 아니면 테스트는 `[assembly: InternalsVisibleTo]` 대신 public 헬퍼만 검증하도록 단순화).

- [ ] **Step 2: 테스트 실패 확인**

Run (UnityMCP): `run_tests` mode=EditMode filter=`StoryCharSlotLookupTests`
Expected: FAIL — `FindSlotForCharId` 미정의.

- [ ] **Step 3: 순수 헬퍼 추가** — `NarrativeController.cs`에:

```csharp
/// <summary>storyChars에서 id(대소문자 무시) 일치 레코드의 슬롯 인덱스. 없으면 -1(순수).</summary>
public static int FindSlotForCharId(IReadOnlyList<GameStateData.StoryCharRecord> chars, string id)
{
    if (chars == null || string.IsNullOrEmpty(id)) return -1;
    for (int i = 0; i < chars.Count; i++)
        if (chars[i] != null && string.Equals(chars[i].id, id, System.StringComparison.OrdinalIgnoreCase))
            return chars[i].slot;
    return -1;
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run (UnityMCP): `run_tests` mode=EditMode filter=`StoryCharSlotLookupTests`
Expected: PASS.

- [ ] **Step 5: 직전 화자 추적** — `PlayText`(라인 332~340 부근)에서 화자 코드 id를 보관. 필드 선언(클래스 상단, `_lastChoiceJumped` 근처):

```csharp
string _lastSpeakerId; // 직전 Text 라인 화자의 코드 id(미등록 화자/내레이션이면 null)
```

`PlayText`에서 `speaker` 계산 직후(라인 334 이후) 추가:

```csharp
_lastSpeakerId = isPlayer ? null : ResolveSpeakerId(line.Speaker);
```

- [ ] **Step 6: PlayStageChar 라우팅** — 라인 516~531을 Emote 단축 분기 포함으로 교체:

```csharp
IEnumerator PlayStageChar(ScriptLine line)
{
    var intent = StageParser.ParseCharacter(line.Value);
    if (!intent.IsValid)
    {
        Log.Warn($"[NarrativeController] 잘못된 Char 라인 — 건너뜀: \"{line.Value}\"");
        yield break;
    }

    // 표정 단축(식별자/직전화자): 대상 캐릭터의 현재 슬롯을 찾아 Emote 발행.
    if (intent.Action == CharAction.Emote && intent.Target != EmoteTarget.Slot)
    {
        string targetId = intent.Target == EmoteTarget.Character
            ? (aliasCatalog != null ? aliasCatalog.ResolveCharacter(intent.Character) : intent.Character)
            : _lastSpeakerId;
        int slotIdx = state != null ? FindSlotForCharId(state.Data.storyChars, targetId) : -1;
        if (slotIdx < 0)
        {
            Log.Info($"[NarrativeController] 표정 대상 캐릭터가 무대에 없음: target='{intent.Character ?? "(직전화자)"}'");
            yield break;
        }
        var slotE = (CharSlot)slotIdx;
        string emFile = string.IsNullOrEmpty(intent.Emote) ? "" : (aliasCatalog != null ? aliasCatalog.ResolveEmote(intent.Emote) : intent.Emote);
        RecordChar(slotE, CharAction.Emote, targetId, emFile);
        var reqE = new CompletionHandle();
        EventBus.Publish(new ShowCharacterCommand(slotE, CharAction.Emote, targetId, emFile, ResolveCharDuration(CharAction.Emote), reqE));
        yield return WaitNext(line, () => reqE.IsComplete);
        yield break;
    }

    float dur = ResolveCharDuration(intent.Action);
    var (ch, em) = ResolveCharEmote(intent.Character, intent.Emote, intent.Action);
    RecordChar(intent.Slot, intent.Action, ch, em);
    var req = new CompletionHandle();
    EventBus.Publish(new ShowCharacterCommand(intent.Slot, intent.Action, ch, em, dur, req));
    yield return WaitNext(line, () => req.IsComplete);
}
```

- [ ] **Step 7: 컴파일 + 전체 EditMode + 회귀 PlayMode**

Run (UnityMCP): `read_console`(클린 확인) → `run_tests` mode=EditMode → `run_tests` mode=PlayMode filter=`Emote`(인라인 회귀).
Expected: PASS.

- [ ] **Step 8: 커밋**

```bash
git add Assets/_Project/Scripts/Narrative/NarrativeController.cs Assets/Tests/EditMode/StoryCharSlotLookupTests.cs
git commit -m "feat(narrative): Char 표정 단축 라우팅 — 식별자/직전화자 슬롯 조회

왜: Roa:웃음 / 웃음(직전화자) 단축이 대상 캐릭터의 현재 슬롯을 storyChars에서
찾아 기존 Emote 명령으로 발행하도록. 무대에 없으면 경고 후 스킵.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: InlineTagParser — 단일토큰 `<웃음>` → emote

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/InlineTagParser.cs:67-91` (`HandleTag`)
- Test: `Assets/Tests/EditMode/InlineTagParserTests.cs` (신규)

**Interfaces:**
- Consumes: `InlineTagParser.Parse(string)` → `ParsedDialogue{ Text, Pauses, Emotes }`.
- Produces: `Parse("<웃음>안녕")` → Text="안녕", Emotes=[(0,"웃음")]. 기존 `<emote=x/>`, `<wait:s>`는 불변. `=`나 `:` 포함 미지원 태그는 기존대로 제거.

- [ ] **Step 1: 실패 테스트 작성** — 신규 `Assets/Tests/EditMode/InlineTagParserTests.cs`:

```csharp
using NUnit.Framework;
using LoveAlgo.Story; // InlineTagParser

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class InlineTagParserTests
    {
        [Test]
        public void Bare_Token_Is_Emote()
        {
            var p = InlineTagParser.Parse("<웃음>안녕");
            Assert.AreEqual("안녕", p.Text);
            Assert.IsNotNull(p.Emotes);
            Assert.AreEqual(1, p.Emotes.Count);
            Assert.AreEqual(0, p.Emotes[0].CharIndex);
            Assert.AreEqual("웃음", p.Emotes[0].Emote);
        }

        [Test]
        public void Emote_Equals_Form_Still_Works()
        {
            var p = InlineTagParser.Parse("<emote=활짝/>야");
            Assert.AreEqual("야", p.Text);
            Assert.AreEqual("활짝", p.Emotes[0].Emote);
        }

        [Test]
        public void Wait_Tag_Not_Treated_As_Emote()
        {
            var p = InlineTagParser.Parse("<wait:1>음");
            Assert.AreEqual("음", p.Text);
            Assert.IsNull(p.Emotes);     // wait는 emote 아님
            Assert.IsNotNull(p.Pauses);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run (UnityMCP): `run_tests` mode=EditMode filter=`InlineTagParserTests`
Expected: FAIL — `Bare_Token_Is_Emote`에서 Emotes null(현재 미지원 태그라 제거됨).

- [ ] **Step 3: HandleTag 확장** — `InlineTagParser.cs`의 `HandleTag` 끝(라인 90 주석 위치)에 단일토큰 분기 추가. `wait` 처리 블록 뒤, 메서드 말미:

```csharp
            // 단일토큰 표정 태그 <웃음>: '=' 도 ':' 도 없는 비어있지 않은 토큰 → 화자 표정.
            if (t.Length > 0 && t.IndexOf('=') < 0 && t.IndexOf(':') < 0)
            {
                (emotes ??= new List<InlineEmote>()).Add(new InlineEmote(charIndex, t));
            }
            // 그 외(= 포함 미지원, : 포함 비정규) = 제거(무시).
```

> 주: `wait`은 위에서 이미 처리·return되므로 여기 도달 안 함. `parts`/`name` 계산은 wait 전용이라 그대로 둠.

- [ ] **Step 4: 테스트 통과 확인**

Run (UnityMCP): `run_tests` mode=EditMode filter=`InlineTagParserTests`
Expected: PASS. `read_console` 클린.

- [ ] **Step 5: 커밋**

```bash
git add Assets/_Project/Scripts/Narrative/InlineTagParser.cs Assets/Tests/EditMode/InlineTagParserTests.cs
git commit -m "feat(narrative): 인라인 단일토큰 표정 태그 <웃음> 지원

왜: 작가가 <emote=x/> 대신 <웃음>으로 화자 표정을 바꾸도록. = / : 없는
단일 토큰만 emote로 처리해 wait·emote= 기존 태그와 충돌 없음.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: 배치·로그 초상 데이터 키 갱신(c01→Roa 등)

**Files:**
- Modify: `Assets/Resources/Data/CharacterStageCatalog.asset` (`characterId` 항목)
- Modify: `DialogueLogView`의 `portraits`가 직렬화된 프리팹/씬(아래 Step 1에서 위치 확정)
- Test: `Assets/Tests/EditMode/CharacterStageCatalogTests.cs`

**Interfaces:**
- Consumes: `CharacterStageCatalogSO.Resolve(string characterId)`, `DialogueLogView.PortraitFor(speakerId)`.
- Produces: 배치/초상 키가 폴더명(`Roa`…)으로 일치 → 발행되는 Character/SpeakerId(폴더명)와 매칭.

- [ ] **Step 1: 직렬화 위치 확정** — `portraits`의 `speakerId: c01` 항목이 들어있는 파일 검색:

```bash
grep -rl "speakerId: c01" Assets --include=*.prefab --include=*.unity
grep -rl "characterId: c01" Assets --include=*.asset
```

- [ ] **Step 2: CharacterStageCatalog 키 테스트 갱신** — `CharacterStageCatalogTests.cs`에서 `c01` 기대 키를 `Roa`로 수정(있는 경우). 없으면 신규:

```csharp
[Test]
public void Resolve_By_FolderId()
{
    var entries = new System.Collections.Generic.List<CharacterStageCatalogSO.Entry>
    {
        new CharacterStageCatalogSO.Entry { characterId = "Roa", scale = 0.5f },
    };
    Assert.AreEqual(0.5f, CharacterStageCatalogSO.Resolve(entries, "Roa").Scale, 1e-4f);
    Assert.AreEqual(1f, CharacterStageCatalogSO.Resolve(entries, "c01").Scale, 1e-4f); // 구 키는 미등록=항등
}
```

- [ ] **Step 3: CharacterStageCatalog.asset 편집** — 각 `characterId: c01..c05`를 `Roa/Daeun/Yeeun/Heewon/Bom`으로 치환(scale/offset 값 보존).

- [ ] **Step 4: portraits 키 편집** — Step 1에서 찾은 prefab/scene의 `speakerId: c01..c05`를 `Roa..Bom`으로 치환(sprite 참조 보존). 직렬화 YAML을 Edit로 직접 수정.

- [ ] **Step 5: 테스트 + 컴파일**

Run (UnityMCP): `read_console` → `run_tests` mode=EditMode filter=`CharacterStageCatalog`.
Expected: PASS.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Resources/Data/CharacterStageCatalog.asset Assets/Tests/EditMode/CharacterStageCatalogTests.cs
# + Step1에서 찾은 prefab/scene
git commit -m "feat(ui): 배치·로그 초상 키를 폴더명(Roa 등)으로 갱신

왜: 발행 Character/SpeakerId가 폴더명이 되므로 히로인 배치·로그 초상 맵 키도
일치시켜 기존 기능(스케일/오프셋·초상)을 보존.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: Prologue.csv 콘텐츠 정합(`활짝웃음`→`활짝`)

**Files:**
- Modify: `Assets/StreamingAssets/Story/Prologue.csv`

**Interfaces:** 없음(데이터). 표정 토큰이 실제 파일명과 일치하게.

- [ ] **Step 1: 치환 대상 확인**

```bash
grep -nE 'emote=활짝웃음|<활짝웃음>' Assets/StreamingAssets/Story/Prologue.csv
```

- [ ] **Step 2: 치환** — 매칭된 행의 `활짝웃음`을 `활짝`으로 Edit(예: `<emote=활짝웃음/>`→`<emote=활짝/>`). `활짝` 단독 표기는 손대지 않음.

- [ ] **Step 3: 검증** — 다시 grep해 `활짝웃음` 0건 확인:

```bash
grep -nE '활짝웃음' Assets/StreamingAssets/Story/Prologue.csv   # 결과 없어야 함
```

- [ ] **Step 4: 커밋**

```bash
git add Assets/StreamingAssets/Story/Prologue.csv
git commit -m "fix(story): Prologue 표정 토큰 활짝웃음→활짝 (파일명 정합)

왜: Roa 폴더에 활짝웃음.png가 없고 활짝.png만 존재. 카탈로그 별칭으로도
해석되지만 CSV를 파일명에 맞춰 정합성 확보.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 8: 통합 검증(수동 통주행)

**Files:** 없음(검증).

- [ ] **Step 1: 전체 테스트**

Run (UnityMCP): `run_tests` mode=EditMode → `run_tests` mode=PlayMode. Expected: 전부 PASS.

- [ ] **Step 2: 컴파일 클린** — `read_console` types=[error,warning]로 strikethrough/obsolete/누락 경고 없음 확인.

- [ ] **Step 3: 프롤로그 통주행(에디터)** — Game 씬 Play, 프롤로그 진행하며 육안 확인:
  - 로아 등장 스프라이트 표시(`Characters/Roa/기본` 로드 성공).
  - `<emote=…/>`·표정 전환이 그림에 반영.
  - 신규 단축(`,Char,,웃음` 형태로 임시 1줄 삽입 시 직전 화자 표정 변경) 동작 — 확인 후 임시 줄 제거.
  - 대화 로그 열어 로아 초상 표시.
- [ ] **Step 4: 결과 보고** — 콘솔 경고/누락 스프라이트 목록과 함께 통주행 결과를 감독에게 요약. 누락 표정(파일 없는 코드 사용처) 있으면 보고만(아트는 범위 밖).

---

## Self-Review (작성자 점검 결과)

1. **Spec coverage:** §3.1 카탈로그→Task1; §3.2 StageView→Task2; §3.3 배치/§3.4 초상→Task6; §3.5 컨트롤러→Task4; §3.6 StageParser→Task3; §3.7 InlineTagParser→Task5; §3.8 CSV→Task7; §4 회귀→각 Task의 기존 테스트 유지 + Task8 통주행; §5 테스트→각 Task. 전 항목 매핑됨.
2. **Placeholder scan:** 모든 코드 스텝에 실제 코드/명령/기대 결과 명시. "적절히 처리" 류 없음. Task6 Step1만 위치 탐색(파일 미상)이라 grep 명령으로 구체화.
3. **Type consistency:** `EmoteTarget`(Task3) → Task4에서 동일 사용. `CharSpriteKey`(Task2) 단일 정의. `FindSlotForCharId`(Task4) 단일 정의. `StoryCharRecord{slot,id,emote}` 필드명 Task1/4 일관. 카탈로그 `Resolve`/`ResolveCharacter`/`ResolveEmote` 시그니처 기존과 일치.

> 미해결 가정: Task4 테스트의 `GameStateData.StoryCharRecord` 접근성(public 여부)·`NarrativeController` 네임스페이스는 구현 시 실제 파일로 확인해 맞춘다(접근 불가 시 헬퍼만 public 검증으로 단순화).
