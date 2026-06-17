# 선택지 이력(choiceHistory) + 선택 조건 연산자 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 작가가 옵션에 `mark:태그`를 달면 선택 시 `choiceHistory`에 기록되고, 조건 `Chose:태그`로 과거 선택을 분기할 수 있게 한다.

**Architecture:** 명시적 마커. ChoiceParser가 `mark:` 토큰을 `ChoiceOption.Mark`로 파싱 → NarrativeController.PlayChoice가 선택 확정 시 `GameStateSO.RecordChoice`로 기록(세이브 직렬화) → ConditionEvaluator의 새 원자 `Chose:태그`가 `HasChosen`으로 조회. Flow `If:`·선택지 `if:`가 평가기를 공유하므로 양쪽에서 동작.

**Tech Stack:** Unity 6 / C# / EventBus + ScriptableObject 상태 / NUnit. 검증은 Unity MCP `run_tests` + `read_console`.

위험도: **🔴 Critical (세이브 스키마 가산)**. 스펙: `docs/superpowers/specs/2026-06-17-choice-history-design.md`.

---

## File Structure

- `Assets/_Project/Scripts/Core/State/GameStateData.cs` — `choiceHistory` 리스트 가산.
- `Assets/_Project/Scripts/Core/State/GameStateSO.cs` — `HasChosen`/`RecordChoice` 접근자(GetFlag/SetFlag 형제).
- `Assets/_Project/Scripts/Narrative/ChoiceOption.cs` — `ChoiceOption.Mark` 필드 + `ParseOption`의 `mark:` 분기.
- `Assets/_Project/Scripts/Narrative/ConditionEvaluator.cs` — `Chose:`/`!Chose:` 원자.
- `Assets/_Project/Scripts/Narrative/NarrativeController.cs` — `PlayChoice`에서 `RecordChoice` 호출.
- 테스트: `ChoiceParserTests.cs`·`ConditionEvaluatorTests.cs`(EditMode), 신규 `GameStateChoiceHistoryTests.cs`(EditMode), `NarrativeControllerPlayModeTests.cs`(PlayMode).

태스크 1~3은 EditMode 전용(빠름), 태스크 4가 PlayMode 엔드투엔드 + 전체 회귀.

---

## Task 1: 스키마 + 접근자 (GameStateData / GameStateSO)

**Files:**
- Modify: `Assets/_Project/Scripts/Core/State/GameStateData.cs` (`eventChoices` 선언 직후)
- Modify: `Assets/_Project/Scripts/Core/State/GameStateSO.cs` (`SetFlag` 메서드 직후)
- Test: `Assets/Tests/EditMode/GameStateChoiceHistoryTests.cs` (신규)

- [ ] **Step 1: 실패 테스트 작성** — 신규 파일 생성

`Assets/Tests/EditMode/GameStateChoiceHistoryTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core; // GameStateSO, GameStateData

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 선택지 이력(🔴 세이브 스키마): RecordChoice 중복 방지 + HasChosen 조회 + JSON 왕복 + 구세이브 기본값(빈).
    /// </summary>
    [TestFixture]
    public class GameStateChoiceHistoryTests
    {
        [Test]
        public void RecordChoice_DedupsAndHasChosenReads()
        {
            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            gs.ResetRuntime();
            try
            {
                Assert.IsFalse(gs.HasChosen("met_roa"), "초기엔 없음");
                gs.RecordChoice("met_roa");
                gs.RecordChoice("met_roa"); // 중복 — 무시
                gs.RecordChoice("");          // 빈 — 무시
                Assert.IsTrue(gs.HasChosen("met_roa"));
                Assert.AreEqual(1, gs.Data.choiceHistory.Count, "중복/빈 미기록");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void ChoiceHistory_JsonRoundTrip()
        {
            var d = new GameStateData();
            d.choiceHistory.Add("a");
            d.choiceHistory.Add("b");

            var back = JsonUtility.FromJson<GameStateData>(JsonUtility.ToJson(d));

            Assert.AreEqual(2, back.choiceHistory.Count);
            Assert.AreEqual("a", back.choiceHistory[0]);
            Assert.AreEqual("b", back.choiceHistory[1]);
        }

        [Test]
        public void OldSave_WithoutChoiceHistory_LoadsAsEmpty()
        {
            const string oldJson = "{\"playerName\":\"철수\",\"day\":3}";
            var d = JsonUtility.FromJson<GameStateData>(oldJson);
            Assert.IsNotNull(d.choiceHistory, "부재 필드 → 빈 리스트");
            Assert.AreEqual(0, d.choiceHistory.Count);
        }
    }
}
```

- [ ] **Step 2: 컴파일 실패 확인**

`HasChosen`/`RecordChoice`/`choiceHistory` 미정의 → 테스트 어셈블리 컴파일 에러(`read_console`). TDD red는 컴파일 에러로 확정.

- [ ] **Step 3a: 스키마 추가** — `GameStateData.cs`, `eventChoices` 선언 직후

```csharp
        // 선택 시 기록된 마커 태그(순서 보존). 조건 원자 Chose:태그가 조회 — 작가의 과거-선택 분기.
        // (인벤토리 §7 SaveData의 ChoiceHistory 이행. eventChoices=Affinity Event3 보정과 별개.)
        // 가산적 확장이라 구버전 세이브는 빈 목록으로 로드 = 마이그레이션 무해.
        public List<string> choiceHistory = new();
```

(`eventChoices` 선언 = `public List<StringEntry> eventChoices = new();`)

- [ ] **Step 3b: 접근자 추가** — `GameStateSO.cs`, `SetFlag` 메서드 닫는 `}` 직후(GetFlag/SetFlag 형제)

```csharp
        // ── 선택지 이력 동기 접근 (choiceHistory ↔ set 의미) ──
        public bool HasChosen(string tag)
        {
            var list = _runtime.choiceHistory;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == tag) return true;
            return false;
        }

        public void RecordChoice(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            if (!HasChosen(tag)) _runtime.choiceHistory.Add(tag); // set 의미 — 조건은 존재 여부라 중복 방지
        }
```

- [ ] **Step 4: EditMode 테스트 통과 확인**

Run: `run_tests`(EditMode, test_names `LoveAlgo.Tests.Editor.GameStateChoiceHistoryTests`), `read_console` 0에러.
Expected: 3/3 PASS.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/Core/State/GameStateData.cs" "Assets/_Project/Scripts/Core/State/GameStateSO.cs" "Assets/Tests/EditMode/GameStateChoiceHistoryTests.cs"
git commit -m "feat(save): 선택지 이력 스키마+접근자(choiceHistory/HasChosen/RecordChoice)

마커 기반 과거-선택 분기의 토대. set 의미(중복 방지). 가산적이라 구세이브 무해.
파서/연산자/기록 배선은 후속 태스크. EditMode 3/3 그린."
```

---

## Task 2: 파서 마커 (ChoiceParser / ChoiceOption)

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/ChoiceOption.cs`
- Test: `Assets/Tests/EditMode/ChoiceParserTests.cs`

- [ ] **Step 1: 실패 테스트 작성** — `ChoiceParserTests`에 메서드 추가(`Parse_Full_...` 다음)

```csharp
        [Test]
        public void Parse_Mark_Token_Into_Mark_OrderIndependent()
        {
            // mark:는 if:/효과와 순서 무관하게 3번째+ 토큰 어디에 와도 Mark로 분리.
            var o = ChoiceParser.ParseOption("로아 선택|met|mark:met_roa|Love:Roa:3|if:Flag:intro");

            Assert.AreEqual("로아 선택", o.ButtonText);
            Assert.AreEqual("met", o.JumpTarget);
            Assert.AreEqual("met_roa", o.Mark);
            Assert.AreEqual("Flag:intro", o.Condition);
            CollectionAssert.AreEqual(new[] { "Love:Roa:3" }, o.Effects);
        }

        [Test]
        public void Parse_NoMark_LeavesMarkNull()
        {
            var o = ChoiceParser.ParseOption("선택|target|Stat:Int:2");
            Assert.IsNull(o.Mark);
        }
```

- [ ] **Step 2: 컴파일 실패 확인**

`o.Mark` 미정의 → 컴파일 에러(`read_console`).

- [ ] **Step 3a: Mark 필드 추가** — `ChoiceOption.cs`, `public string Condition;` 다음

```csharp
        public string Mark;
```

- [ ] **Step 3b: `ParseOption`에 `mark:` 분기 추가** — 3번째+ 토큰 루프의 `if:` 분기 옆

기존:
```csharp
                if (part.StartsWith("if:", StringComparison.OrdinalIgnoreCase))
                    data.Condition = part.Substring(3);
                else if (part.Length > 0)
                    data.Effects.Add(part);
```
교체:
```csharp
                if (part.StartsWith("if:", StringComparison.OrdinalIgnoreCase))
                    data.Condition = part.Substring(3);
                else if (part.StartsWith("mark:", StringComparison.OrdinalIgnoreCase))
                    data.Mark = part.Substring(5);
                else if (part.Length > 0)
                    data.Effects.Add(part);
```

- [ ] **Step 4: EditMode 테스트 통과 확인**

Run: `run_tests`(EditMode, test_names `LoveAlgo.Tests.Editor.ChoiceParserTests`), `read_console` 0에러.
Expected: 전 테스트(기존 7 + 신규 2) PASS.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/Narrative/ChoiceOption.cs" "Assets/Tests/EditMode/ChoiceParserTests.cs"
git commit -m "feat(narrative): 선택지 mark:태그 파싱(ChoiceOption.Mark)

옵션 3번째+ 토큰에 mark: 분기 추가(if:와 동일 패턴, 순서 무관). 효과 문자열과
접두사 충돌 없음. 기록/조회는 후속. EditMode ChoiceParser 그린."
```

---

## Task 3: 조건 연산자 (ConditionEvaluator)

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/ConditionEvaluator.cs`
- Test: `Assets/Tests/EditMode/ConditionEvaluatorTests.cs`

- [ ] **Step 1: 실패 테스트 작성** — `ConditionEvaluatorTests`에 메서드 추가(`Flag_And_Negation` 다음)

```csharp
        [Test]
        public void Chose_And_Negation()
        {
            var gs = MakeState();
            gs.RecordChoice("met_roa");
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Chose:met_roa"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "!Chose:met_roa"));
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Chose:unknown"), "미선택 = false");
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "!Chose:unknown"));
        }

        [Test]
        public void Chose_Combines_With_And_Or()
        {
            var gs = MakeState();
            gs.RecordChoice("a");
            gs.SetFlag("vip", true);
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Chose:a&Flag:vip"), "둘 다 참");
            Assert.IsFalse(ConditionEvaluator.Evaluate(gs, "Chose:b&Flag:vip"), "AND 일부 거짓");
            Assert.IsTrue(ConditionEvaluator.Evaluate(gs, "Chose:b|Flag:vip"), "OR 하나 참");
        }
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `run_tests`(EditMode, test_names `LoveAlgo.Tests.Editor.ConditionEvaluatorTests`).
Expected: FAIL — `Chose:`는 비교식 아님 → 현재 `Compare`가 false 반환(`Chose:met_roa`도 false). 신규 두 테스트 적색.

- [ ] **Step 3: 원자 추가** — `ConditionEvaluator.cs` `EvaluateAtom`의 `Flag:` 분기 다음(부정 먼저)

기존:
```csharp
            if (atom.StartsWith("!Flag:", StringComparison.Ordinal)) return !gs.GetFlag(atom.Substring(6));
            if (atom.StartsWith("Flag:",  StringComparison.Ordinal)) return  gs.GetFlag(atom.Substring(5));
```
다음 줄에 삽입:
```csharp
            if (atom.StartsWith("!Chose:", StringComparison.Ordinal)) return !gs.HasChosen(atom.Substring(7));
            if (atom.StartsWith("Chose:",  StringComparison.Ordinal)) return  gs.HasChosen(atom.Substring(6));
```

문서 주석(클래스 XML)의 원자 목록에 `Chose:태그` / `!Chose:태그` 한 줄 추가(선택, 권장):
```csharp
    ///   <c>Flag:이름</c> / <c>!Flag:이름</c> · <c>Chose:태그</c> / <c>!Chose:태그</c> · <c>Love:히로인{op}N</c> · ...
```

- [ ] **Step 4: EditMode 테스트 통과 확인**

Run: `run_tests`(EditMode, test_names `LoveAlgo.Tests.Editor.ConditionEvaluatorTests`), `read_console` 0에러.
Expected: 전 테스트(기존 + 신규 2) PASS.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/Narrative/ConditionEvaluator.cs" "Assets/Tests/EditMode/ConditionEvaluatorTests.cs"
git commit -m "feat(narrative): 선택 조건 원자 Chose:/!Chose: (choiceHistory 조회)

Flag 원자 미러. Flow If:·선택지 if: 공유 평가기라 양쪽서 동작, AND/OR 우선순위 불변.
EditMode ConditionEvaluator 그린."
```

---

## Task 4: 기록 배선 (NarrativeController.PlayChoice) + 엔드투엔드

**Files:**
- Modify: `Assets/_Project/Scripts/Narrative/NarrativeController.cs` (`PlayChoice`)
- Test: `Assets/Tests/PlayMode/NarrativeControllerPlayModeTests.cs`

- [ ] **Step 1: 실패 테스트 작성** — `NarrativeControllerPlayModeTests`에 메서드 추가(`Run_PathB_...` 다음)

```csharp
        [UnityTest]
        public IEnumerator Mark_Recorded_On_Choice_Then_If_Chose_Branches()
        {
            var player = SetUp(selectIndex: 0); // 옵션0 = mark:met_roa 선택
            yield return null;

            const string markCsv =
                "LineID,Type,Speaker,Value,Next\n" +
                ",Choice,,,>\n" +
                ",Option,,로아 선택|met|mark:met_roa,>\n" +
                ",Option,,다른 선택|met,>\n" +
                "met,Text,,로아루트,click\n" +
                ",Flow,,If:Chose:met_roa:gotit,>\n" +
                ",Text,,놓침,click\n" +
                ",Flow,,End,>\n" +
                "gotit,Text,,기억함,click\n" +
                ",Flow,,End,>\n";

            EventBus.Publish(new PlayScriptCommand(markCsv, "marktest"));
            yield return WaitUntilDone(player);

            Assert.IsTrue(_gs.HasChosen("met_roa"), "선택 확정 시 마커 기록");
            CollectionAssert.AreEqual(new[] { "로아루트", "기억함" }, _dialogues,
                "If:Chose:met_roa 참 → gotit 점프(놓침 미실행)");
        }
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `run_tests`(PlayMode, test_names `LoveAlgo.Tests.PlayMode.NarrativeControllerPlayModeTests.Mark_Recorded_On_Choice_Then_If_Chose_Branches`, init_timeout 120000).
Expected: FAIL — 기록 미배선이라 `HasChosen` false → `If:Chose` 거짓 → `_dialogues`=["로아루트","놓침"]로 불일치.

- [ ] **Step 3: RecordChoice 호출 추가** — `PlayChoice`의 `ApplyChoiceEffects(chosen.Effects);` 다음

기존:
```csharp
            ApplyChoiceEffects(chosen.Effects);

            if (!string.IsNullOrEmpty(chosen.JumpTarget))
```
교체:
```csharp
            ApplyChoiceEffects(chosen.Effects);

            // 마커 기록(과거-선택 분기용) — 효과와 같은 선택 지점 상태 변경. 스토리 위치 앵커는 다음 대기
            // 라인에서 잡히므로 재개 시 이중 기록 없음(옵션 Flag 효과와 동일 의미).
            if (state != null && !string.IsNullOrEmpty(chosen.Mark)) state.RecordChoice(chosen.Mark);

            if (!string.IsNullOrEmpty(chosen.JumpTarget))
```

- [ ] **Step 4: PlayMode 통과 + 전체 회귀**

Run: `run_tests`(PlayMode, test_names `...Mark_Recorded_On_Choice_Then_If_Chose_Branches`) → PASS.
이어 전체 회귀 — `run_tests`(EditMode 전체) + `run_tests`(PlayMode 전체), `read_console` 0에러.
Expected: 신규 포함 전부 그린, 기존 무회귀.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/_Project/Scripts/Narrative/NarrativeController.cs" "Assets/Tests/PlayMode/NarrativeControllerPlayModeTests.cs"
git commit -m "feat(narrative): 선택 마커 기록 배선 — PlayChoice→RecordChoice

선택 확정 시 chosen.Mark를 choiceHistory에 기록. mark→Chose 엔드투엔드 PlayMode로
검증(기록→If:Chose 분기). 선택지 이력 슬라이스 완료. 전체 회귀 그린."
```

---

## 마무리 (실행 후 별도)

- **HANDOFF.md 갱신**: 완료 기록(마커 기반 choiceHistory + Chose 연산자, 옵션 전용 한정) + 다음 액션. 감독 승인 후 커밋·푸시.
- **STORY_COMMANDS.md / STORY_CSV_GUIDE.md**: `mark:태그` 옵션 토큰 + `Chose:태그`/`!Chose:태그` 조건 원자 문서화(🟢 작가 문서).
- **감독 Play 검증**: mark 옵션이 있는 스토리에서 선택 후 세이브→로드→`If:Chose` 분기 유지 확인.

---

## Self-Review 결과

- **Spec coverage**: §4 스키마→T1(3a), §5 접근자→T1(3b), §6 파서→T2, §7 기록→T4(3), §8 연산자→T3, §9 테스트→T1·T2·T3·T4 매핑. §10 범위(옵션 전용·가산)→T2 토큰·T1 가산 필드. ✅
- **Placeholder scan**: TBD/TODO 없음, 모든 코드 블록 완전. ✅
- **Type consistency**: `HasChosen`/`RecordChoice` T1 정의·T3·T4 호출 일치. `ChoiceOption.Mark` T2 정의·T4 사용 일치. `choiceHistory` 필드명 T1·테스트 일치. `Chose:`(6)/`!Chose:`(7) 부분문자열 인덱스 검증됨. ✅
