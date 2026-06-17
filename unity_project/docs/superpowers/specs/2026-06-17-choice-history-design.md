# 선택지 이력(choiceHistory) + 선택 조건 연산자 — 설계 (스펙)

> 작성 2026-06-17 · 위험도 **🔴 Critical (세이브 스키마 가산)** — 감독 정독+승인, 컴파일·테스트 동작 증거 필수.
> 인벤토리 §6/§7의 `choiceHistory`(점프 타깃 목록·SaveData 포함)를 재작성에 이행하되, **소비처를 갖춘 형태**로 설계한다.

---

## 1. 목적

작가가 **과거 선택으로 스토리를 분기**할 수 있게 한다. 현재 조건 미니언어(`ConditionEvaluator`)는
Flag/Love/Stat/EndingCount만 지원하고 선택 이력을 조회할 수단이 없다. (게임플레이상 "과거 선택"의 유일한
용도였던 Event3 재선택 +2 보정은 이미 `eventChoices`+`AffinityFormula.RecordEventChoice`가 처리 — 이 슬라이스와 무관.)

choiceHistory를 "저장만 하는 목록"으로 두면 금지선 #7(과설계 게이트)에 걸리므로, **기록 + 조회 연산자 +
CSV 문법을 한 세트**로 만들어 소비처를 확보한다.

**성공 기준**: 작가가 옵션에 `mark:태그`를 달고 이후 `If:Chose:태그`(또는 옵션 `if:Chose:태그`)로 분기하면,
선택한 적 있을 때만 분기가 성립한다. 세이브/로드를 거쳐도 이력이 유지된다.

## 2. 접근

명시적 마커. 작가가 옵션에 `mark:태그`를 달면 선택 확정 시 그 태그를 `choiceHistory`에 기록하고,
조건 원자 `Chose:태그`가 조회한다.

**기각한 대안**:
- **점프 타깃 LineID 자동 기록**(인벤토리 정본): 점프 없는 옵션 미기록 + 분기 구조 변경에 취약.
- **플래그로 충분**(옵션 효과 `Set:Flag` + `If:Flag`): 이미 가능하나 작가가 옵션마다 플래그를 수동 선언해야 함.
  마커는 "기억할 선택"을 한 토큰으로 명시 — 텍스트/구조 변경에 면역하고 작가 부담이 적다(감독 결정).

## 3. 컴포넌트 / 데이터 흐름

```
[CSV 옵션]  버튼텍스트|점프대상|효과…|if:조건|mark:태그
   │ ParseOption
   ▼
[ChoiceOption.Mark]
   │ PlayChoice 선택 확정
   ▼
[GameStateSO.RecordChoice(tag)] → GameStateData.choiceHistory (세이브 직렬화)
   ▲ HasChosen(tag)
   │
[ConditionEvaluator "Chose:태그"]  ← Flow If: / 옵션 if: 공유
```

## 4. 스키마 가산 (`GameStateData`)

가산적 확장 — 구버전 세이브 로드 시 빈 목록 = 마이그레이션 무해.

```csharp
// 선택 시 기록된 마커 태그(순서 보존). 조건 원자 Chose:태그가 조회 — 작가의 과거-선택 분기.
// (인벤토리 §7 SaveData의 ChoiceHistory 이행. eventChoices=Affinity Event3 보정과 별개.)
public List<string> choiceHistory = new();
```

## 5. 접근자 (`GameStateSO`, GetFlag/SetFlag 형제)

```csharp
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
    if (!HasChosen(tag)) _runtime.choiceHistory.Add(tag); // Set 의미 — 조건은 존재 여부라 중복 방지
}
```

## 6. CSV 문법 / ChoiceParser

`ChoiceOption`에 `public string Mark;` 추가. `ParseOption`의 3번째+ 토큰 분기에 `mark:` 추가:

```
형식: 버튼텍스트|점프대상|효과1|효과2|…|if:조건|mark:태그   (3번째+ 토큰 순서 무관)
  - "if:"   접두 → Condition
  - "mark:" 접두 → Mark
  - 그 외   → Effects
```

`mark:`는 효과 문자열(`Set:`·`Affinity:`·`Love:` 등)·`if:`와 접두사 충돌 없음.

## 7. 기록 (`NarrativeController.PlayChoice`)

선택 확정(인덱스 클램프) 후, 효과 적용과 같은 시점(점프 전)에:

```csharp
if (!string.IsNullOrEmpty(chosen.Mark)) state.RecordChoice(chosen.Mark);
```

**스토리 위치 세이브와의 정합**: 마커 기록은 선택 지점의 상태 변경이다. 스토리 위치 앵커는 *다음* 대기
라인에서 잡히므로 마커 기록은 앵커 이전에 일어나고 — 재개 시 앵커 이전 라인은 재실행되지 않아 **이중
기록이 없다**(옵션 `Set:Flag` 효과와 동일한 의미·동일한 안전성).

## 8. 연산자 (`ConditionEvaluator`)

원자 추가(Flag 원자 미러), `EvaluateAtom`에:

```csharp
if (atom.StartsWith("!Chose:", StringComparison.Ordinal)) return !gs.HasChosen(atom.Substring(7));
if (atom.StartsWith("Chose:",  StringComparison.Ordinal)) return  gs.HasChosen(atom.Substring(6));
```

Flow `If:`와 선택지 `if:`가 `ConditionEvaluator`를 공유하므로 양쪽에서 동작. AND(`&`)/OR(`|`) 우선순위 불변.

## 9. 테스트 (작동 증거)

- **EditMode** `ChoiceParserTests` +: `mark:` 토큰이 `Mark`로 파싱 / `if:`·효과와 순서 무관 공존.
- **EditMode** `ConditionEvaluatorTests` +: `Chose:태그`·`!Chose:태그` 참/거짓 / `&`·`|` 결합.
- **EditMode** (`GameStateSO` 또는 신규): `RecordChoice` 중복 방지 + `HasChosen` / `choiceHistory` JSON 왕복(+구세이브 부재 시 빈 목록).
- **PlayMode** `NarrativeControllerPlayModeTests` +: mark 옵션 선택 → 기록 → 후속 Flow `If:Chose:태그` 점프(엔드투엔드).

## 10. 범위 한정 / 위험

- 마커는 **선택지 옵션 전용**(Flow 라인 마커 X — YAGNI).
- 가산적 스키마 → 구세이브 무해(빈 목록 로드).
- 런타임 상태 영구화 금지(금지선 #6) 준수 — GameStateData 직렬화만.
- 호감도 공식·수치 무관(금지선 #2).
